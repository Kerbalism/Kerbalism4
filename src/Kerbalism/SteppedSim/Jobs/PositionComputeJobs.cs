using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace KERBALISM.SteppedSim.Jobs
{
	// input = array<int> timesteps (ts0, ts1, ts2, ts3) for 4 frames
	// index = frame #
	// output = fully unrolled
	// framesize = size of frames
	[BurstCompile]
	public struct UnrollSingle<T> : IJobParallelFor where T : struct
	{
		[ReadOnly] public int framesize;
		[ReadOnly] public NativeArray<T> input;
		[WriteOnly] public NativeArray<T> output;
		public UnrollSingle(int framesize) : this()
		{
			this.framesize = framesize;
		}
		public void Execute(int index)
		{
			int offset = framesize * index;
			var x = input[index];
			for (int i = 0; i < framesize; i++, offset++)
				output[offset] = x;
		}
	}

	// Copy reference frame (input) into unrolled list (output) at each frame (index)
	[BurstCompile]
	public struct Unroll<T> : IJobParallelFor where T : struct
	{
		[ReadOnly] public int count;
		[ReadOnly] public NativeArray<T> input;
		[WriteOnly] public NativeArray<T> output;
		public Unroll(int count) : this()
		{
			this.count = count;
		}
		public void Execute(int index)
		{
			int offset = count * index;
			output.Slice(offset, count).CopyFrom(input.Slice(0, count));
			//for (int i = 0; i < count; i++, offset++)
//				output[offset] = input[i];
		}
	}

	[BurstCompile]
	public struct BuildIndexes : IJob
	{
		[ReadOnly] public int numBodies;
		[ReadOnly] public int numVessels;
		[WriteOnly] public NativeArray<int2> indices;

		public void Execute()
		{
			for (int i=0; i < numBodies; i++)
				indices[i] = new int2(i, -1);
			for (int i=0; i < numVessels; i++)
				indices[numBodies + i] = new int2(-1, i);
		}
	}

	[BurstCompile]
	public struct RotationsComputeJob : IJobParallelFor
	{
		[ReadOnly] public double PlanetariumInverseRotAngle;
		[ReadOnly] public int frameSize;
		[ReadOnly] public NativeArray<double> times;
		[ReadOnly] public NativeArray<RotationCondition> rotationsIn;
		[WriteOnly] public NativeArray<RotationCondition> rotationsOut;

		public void Execute(int index)
		{
			var srcIndex = index % frameSize;
			var srcRotation = rotationsIn[srcIndex];

			var dt = times[index] - srcRotation.frameTime;
			var vel = srcRotation.velocity;
			var ang = 360 + srcRotation.angle + vel * dt;
			var directRotAngle = (ang - PlanetariumInverseRotAngle) % 360.0;
			Planetarium.CelestialFrame frame = default;
			Planetarium.CelestialFrame.PlanetaryFrame(0.0, 90.0, directRotAngle, ref frame);
			rotationsOut[index] = new RotationCondition
			{
				frameTime = times[index],
				axis = srcRotation.axis,
				velocity = vel,
				celestialFrame = frame,
				angle = ang % 360,
			};
		}
	}

	[BurstCompile]
	public struct NaiveOrbitComputeJob : IJobParallelFor
	{
		[ReadOnly] public int numTimesteps;
		[ReadOnly] public int numOrbitsPerTimestep;
		[ReadOnly] public NativeArray<int2> indices;
		[ReadOnly] public NativeArray<SubstepVessel> vesselTemplates;
		[ReadOnly] public NativeArray<SubStepOrbit> orbitsSource;
		[ReadOnly] public NativeArray<double> timesCompute;
		[ReadOnly] public NativeArray<SubstepComputeFlags> flagsArr;
		[ReadOnly] public NativeArray<RotationCondition> rotationsCompute;
		[ReadOnly] public double3 defaultPos;
		[WriteOnly] public NativeArray<double3> relPositions;

		public void Execute(int index)
		{
			var flags = flagsArr[index];
			var srcIndex = index % numOrbitsPerTimestep;
			var orbit = orbitsSource[srcIndex];

			if (Unity.Burst.CompilerServices.Hint.Likely(flags.isValidOrbit))
				relPositions[index] = orbit.getRelativePositionAtUT(timesCompute[index]);
			else if (Unity.Burst.CompilerServices.Hint.Unlikely(flags.isLandedVessel))
			{
				// relPositions is in worldspace.  Convert the CB-localspace from GetRelSurfacePosition to worldspace, too.
				//relPositions[index] = GetRelSurfacePosition(sparseDataCompute[index].latLonAlt);

				int blockNum = index / numOrbitsPerTimestep;
				int blockStart = blockNum * numOrbitsPerTimestep;
				int parentBodyIndex = blockStart + orbit.refBodyIndex;
				int vesselIndex = indices[index].y;

				var relPos = GetRelSurfacePosition(vesselTemplates[vesselIndex].LLA);
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
		[ReadOnly] public NativeArray<SubStepOrbit> orbitsSource;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<SubstepComputeFlags> flagsData;
		[ReadOnly] public NativeArray<double3> relPositions;
		public NativeArray<double3> worldPositions;

		// index = timestep we are on, each timestep has numOrbits indices.
		public void Execute(int index)
		{
			int baseOffset = index * numOrbits;
			for (int j=0; j < numOrbits; j++)
			{
				var obt = orbitsSource[j];
				var parentIndex = obt.refBodyIndex;

				int i = j + baseOffset;
				var sparse = flagsData[i];

				bool getWorldOffset = (sparse.isLandedVessel || sparse.isValidOrbit) && parentIndex >= 0;
				var parentWorldPos = Unity.Burst.CompilerServices.Hint.Likely(getWorldOffset) ? worldPositions[parentIndex + baseOffset] : double3.zero;
				worldPositions[i] = relPositions[i].xzy + parentWorldPos;
			}
		}
		// Copied from CelestialBody
		//private Vector3d GetWorldSurfacePosition(double lat, double lon, double alt) => this.BodyFrame.LocalToWorld(this.GetRelSurfacePosition(lat, lon, alt).xzy).xzy + this.position;
	}

	[BurstCompile]
	struct BuildBodyAndVesselHolders : IJobParallelFor
	{
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<SubstepBody> bodySourceData;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<SubstepVessel> vesselSourceData;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int2> indices;
		[ReadOnly] public NativeArray<RotationCondition> rotations;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double3> relPositions;
		[ReadOnly] public NativeArray<double3> worldPositions;
		[WriteOnly] public NativeArray<SubstepBody> bodyData;
		[WriteOnly] public NativeArray<SubstepVessel> vesselData;

		public void Execute(int index)
		{
			var srcIndex = indices[index];
			if (srcIndex.x >= 0)
			{
				var body = bodySourceData[srcIndex.x];
				body.bodyFrame = rotations[index].celestialFrame;
				body.position = worldPositions[index];
				bodyData[index] = body;
			}
			if (srcIndex.y >= 0)
			{
				var vessel = vesselSourceData[srcIndex.y];
				vessel.position = worldPositions[index];
				vessel.rotation = rotations[index].angle;
				vesselData[index] = vessel;
			}
		}
	}

	[BurstCompile]
	public struct RealignBodyAndVesselArrays : IJob
	{
		// Source/Holder data is as built in BuildBodyAndVesselHolders
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<SubstepBody> bodyHolderData;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<SubstepVessel> vesselHolderData;
		[ReadOnly] public int numFrames;
		[ReadOnly] public int numBodies;
		[ReadOnly] public int numVessels;
		[WriteOnly] public NativeArray<SubstepBody> bodyData;
		[WriteOnly] public NativeArray<SubstepVessel> vesselData;

		public RealignBodyAndVesselArrays(int numFrames, int numBodies, int numVessels) : this()
		{
			this.numFrames = numFrames;
			this.numBodies = numBodies;
			this.numVessels = numVessels;
		}

		public void Execute()
		{
			int bodyIndex = 0, vesselIndex = 0;
			int frameSize = numBodies + numVessels;

			for (int i=0; i<numFrames; i++)
			{
				bodyData.Slice(bodyIndex, numBodies).CopyFrom(bodyHolderData.Slice(i * frameSize, numBodies));
				vesselData.Slice(vesselIndex, numVessels).CopyFrom(vesselHolderData.Slice(i * frameSize + numBodies, numVessels));
				bodyIndex += numBodies;
				vesselIndex += numVessels;
			}
		}
	}
}
