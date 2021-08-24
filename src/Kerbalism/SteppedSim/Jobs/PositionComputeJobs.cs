using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace KERBALISM.SteppedSim.Jobs
{
	[BurstCompile]
	struct UnrollPositionComputationsJob : IJobParallelFor
	{
		[ReadOnly] public int numOrbits;
		[ReadOnly] public int numBodies;

		public UnrollPositionComputationsJob(int numOrbits, int numBodies) : this()
		{
			this.numOrbits = numOrbits;
			this.numBodies = numBodies;
		}

		[ReadOnly] public NativeArray<double> times;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<SubStepOrbit> orbits;
		[ReadOnly] public NativeArray<RotationCondition> rotations;
		[ReadOnly] public NativeArray<SparseSimData> sparseData;
		[ReadOnly] public NativeArray<SubstepBody> bodyTemplates;
		[ReadOnly] public NativeArray<SubstepVessel> vesselTemplates;
		[WriteOnly] public NativeArray<double> timesCompute;
		[WriteOnly] public NativeArray<SubStepOrbit> orbitsCompute;
		[WriteOnly] public NativeArray<RotationCondition> rotationsCompute;
		[WriteOnly] public NativeArray<SparseSimData> sparseDataCompute;
		[WriteOnly] public NativeArray<SubstepBody> bodyTemplatesCompute;
		[WriteOnly] public NativeArray<SubstepVessel> vesselTemplatesCompute;

		// index = timestep we are on, each timestep has numOrbits indices.
		public void Execute(int index)
		{
			int offset = numOrbits * index;
			for (int i = 0; i < numOrbits; i++)
			{
				timesCompute[offset] = times[index];
				orbitsCompute[offset] = orbits[i];
				rotationsCompute[offset] = rotations[i];
				sparseDataCompute[offset] = sparseData[i];
				bodyTemplatesCompute[offset] = i < numBodies ? bodyTemplates[i] : default;
				vesselTemplatesCompute[offset] = i < numBodies ? default : vesselTemplates[i - numBodies];
				offset++;
			}
		}
	}

	[BurstCompile]
	struct RotationsComputeJob : IJobParallelFor
	{
		[ReadOnly] public double PlanetariumInverseRotAngle;
		[ReadOnly] public NativeArray<double> times;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<RotationCondition> rotationsIn;
		[WriteOnly] public NativeArray<RotationCondition> rotationsOut;

		public void Execute(int index)
		{
			var dt = times[index] - rotationsIn[index].frameTime;
			var vel = rotationsIn[index].velocity;
			var ang = 360 + rotationsIn[index].angle + vel * dt;
			var directRotAngle = (ang - PlanetariumInverseRotAngle) % 360.0;
			Planetarium.CelestialFrame frame = default;
			Planetarium.CelestialFrame.PlanetaryFrame(0.0, 90.0, directRotAngle, ref frame);
			rotationsOut[index] = new RotationCondition
			{
				frameTime = times[index],
				axis = rotationsIn[index].axis,
				velocity = vel,
				celestialFrame = frame,
				angle = ang % 360,
			};
		}
	}

	[BurstCompile]
	struct NaiveOrbitComputeJob : IJobParallelFor
	{
		[ReadOnly] public int numTimesteps;
		[ReadOnly] public int numOrbitsPerTimestep;
		[ReadOnly] public NativeArray<double> timesCompute;
		[ReadOnly] public NativeArray<SubStepOrbit> orbitsCompute;
		[ReadOnly] public NativeArray<RotationCondition> rotationsCompute;
		[ReadOnly] public NativeArray<SparseSimData> sparseDataCompute;
		[ReadOnly] public NativeArray<SubstepVessel> vesselTemplatesCompute;
		[ReadOnly] public double3 defaultPos;
		[WriteOnly] public NativeArray<double3> relPositions;

		public void Execute(int index)
		{
			var sparseData = sparseDataCompute[index];
			if (Unity.Burst.CompilerServices.Hint.Likely(sparseData.isValidOrbit))
				relPositions[index] = orbitsCompute[index].getRelativePositionAtUT(timesCompute[index]);
			else if (Unity.Burst.CompilerServices.Hint.Unlikely(sparseData.isLandedVessel))
			{
				// relPositions is in worldspace.  Convert the CB-localspace from GetRelSurfacePosition to worldspace, too.
				//relPositions[index] = GetRelSurfacePosition(sparseDataCompute[index].latLonAlt);

				int blockNum = index / numOrbitsPerTimestep;
				int blockStart = blockNum * numOrbitsPerTimestep;
				int parentBodyIndex = blockStart + orbitsCompute[index].refBodyIndex;

				var relPos = GetRelSurfacePosition(vesselTemplatesCompute[index].LLA);
				var rp = new Vector3d(relPos.x, relPos.y, relPos.z);
				var rpw = rotationsCompute[parentBodyIndex].celestialFrame.LocalToWorld(rp.xzy);
				relPositions[index] = new double3(rpw.x, rpw.y, rpw.z);
			}
			else
				relPositions[index] = defaultPos;
		}

		// Ported from CelestialBody, modified for alt measured from body center [not surface] and using double3
		private double3 GetRelSurfacePosition(double3 LLA) => GetRelSurfaceNVector(LLA.x, LLA.y) * LLA.z;
		private double3 GetRelSurfaceNVector(double lat, double lon)
		{
			lat *= Math.PI / 180.0;
			lon *= Math.PI / 180.0;
			return SphericalVector(lat, lon).xzy;
		}

		// Ported from Planetarium.SphericalVector
		private double3 SphericalVector(double lat, double lon)
		{
			math.sincos(lat, out double sLat, out double cLat);
			math.sincos(lon, out double sLon, out double cLon);
			return new double3(cLat * cLon, cLat * sLon, sLat);
		}
	}

	[BurstCompile]
	struct ComputeWorldspacePositionsJob : IJobParallelFor
	{
		[ReadOnly] public int numOrbits;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> timesCompute;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<SubStepOrbit> orbitsCompute;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<SparseSimData> sparseData;
		[ReadOnly] public NativeArray<RotationCondition> rotations;
		[ReadOnly] public NativeArray<double3> relPositions;
		public NativeArray<double3> worldPositions;

		// index = timestep we are on, each timestep has numOrbits indices.
		public void Execute(int index)
		{
			int baseOffset = index * numOrbits;
			for (int i = baseOffset; i < baseOffset + numOrbits; i++)
			{
				var sparse = sparseData[i];
				var obt = orbitsCompute[i];
				var parentIndex = obt.refBodyIndex;

				bool getWorldOffset = (sparse.isLandedVessel || sparse.isValidOrbit) && parentIndex >= 0;
				var parentWorldPos = Unity.Burst.CompilerServices.Hint.Likely(getWorldOffset) ? worldPositions[parentIndex + baseOffset] : double3.zero;
				worldPositions[i] = relPositions[i].xzy + parentWorldPos;

				//				var offset = (!obt.valid || parentIndex < 0) ? double3.zero : worldPositions[parentIndex + baseOffset];
				//				worldPositions[i] = relPositions[i].xzy + offset;

			}
		}
		// Copied from CelestialBody
		//private Vector3d GetWorldSurfacePosition(double lat, double lon, double alt) => this.BodyFrame.LocalToWorld(this.GetRelSurfacePosition(lat, lon, alt).xzy).xzy + this.position;
	}

	[BurstCompile]
	struct BuildBodyAndVesselHolders : IJobParallelFor
	{
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<SubstepBody> bodyTemplatesUnrolled;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<SubstepVessel> vesselTemplatesUnrolled;
		[ReadOnly] public NativeArray<RotationCondition> rotations;
		[ReadOnly] public NativeArray<double3> relPositions;
		[ReadOnly] public NativeArray<double3> worldPositions;
		[WriteOnly] public NativeArray<SubstepBody> bodyData;
		[WriteOnly] public NativeArray<SubstepVessel> vesselData;

		public void Execute(int index)
		{
			// This data could be nonsense but sort it out later when pulling from the array, rather than here.
			bodyData[index] = new SubstepBody
			{
				bodyFrame = rotations[index].celestialFrame,
				position = worldPositions[index],
				radius = bodyTemplatesUnrolled[index].radius
			};
			vesselData[index] = new SubstepVessel
			{
				isLanded = vesselTemplatesUnrolled[index].isLanded,
				position = worldPositions[index],
				relPosition = relPositions[index],
				rotation = rotations[index].angle,
				LLA = vesselTemplatesUnrolled[index].LLA,
				mainBodyIndex = vesselTemplatesUnrolled[index].mainBodyIndex
			};
		}
	}

}
