using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static KERBALISM.SteppedSim.PositionComputeFactory;

namespace KERBALISM.SteppedSim.Jobs
{
	[BurstCompile]
	public struct BuildIndexes : IJob
	{
		[ReadOnly] internal PositionComputeFactory.FrameStats stats;
		[WriteOnly] internal NativeArray<PositionComputeFactory.TimeOrbitIndex> timeOrbitIndex;

		public void Execute()
		{
			for (int time = 0; time < stats.numSteps; time++)
				for (int i = 0; i < stats.numOrbits; i++)
					timeOrbitIndex[time * stats.numOrbits + i] = new TimeOrbitIndex
					{
						time = time,
						directOrbit = time * stats.numOrbits + i,
						origOrbit = i,
						isVessel = i >= stats.numBodies,
					};
		}
	}

	[BurstCompile]
	public struct RotationsComputeJob : IJobParallelFor
	{
		[ReadOnly] public double PlanetariumInverseRotAngle;
		[ReadOnly] internal NativeArray<TimeOrbitIndex> timeOrbitIndex;
		[ReadOnly] public NativeArray<double> times;
		[ReadOnly] public NativeArray<RotationCondition> rotationsIn;
		[WriteOnly] public NativeArray<RotationCondition> rotationsOut;

		public void Execute(int index)
		{
			var i = timeOrbitIndex[index];
			var srcRotation = rotationsIn[i.origOrbit];

			var dt = times[i.time] - srcRotation.frameTime;
			var ang = 360 + srcRotation.angle + srcRotation.velocity * dt;
			var directRotAngle = (ang - PlanetariumInverseRotAngle);
			directRotAngle = QuickMod(directRotAngle, 360);
			ang = QuickMod(ang, 360);
			Planetarium.CelestialFrame frame = default;
			Planetarium.CelestialFrame.PlanetaryFrame(0.0, 90.0, directRotAngle, ref frame);
			rotationsOut[index] = new RotationCondition
			{
				frameTime = times[i.time],
				axis = srcRotation.axis,
				velocity = srcRotation.velocity,
				celestialFrame = frame,
				angle = ang,
			};
		}
		private double QuickMod(double num, double mod)
		{
			while (num > mod)
				num -= mod;
			while (num < 0)
				num += mod;
			return num;
		}
	}

	[BurstCompile]
	public struct NaiveOrbitComputeJob : IJobParallelFor
	{
		[ReadOnly] internal PositionComputeFactory.FrameStats stats;
		[ReadOnly] internal NativeArray<TimeOrbitIndex> timeOrbitIndex;
		[ReadOnly] public NativeArray<double> times;
		[ReadOnly] public NativeArray<SubstepVessel> vesselTemplates;
		[ReadOnly] public NativeArray<SubStepOrbit> orbitsSource;
		[ReadOnly] public NativeArray<SubstepComputeFlags> flagsSource;
		[ReadOnly] public NativeArray<RotationCondition> rotations;
		[ReadOnly] public double3 defaultPos;
		[WriteOnly] public NativeArray<double3> relPositions;

		public void Execute(int index)
		{
			TimeOrbitIndex i = timeOrbitIndex[index];
			var flags = flagsSource[i.origOrbit];
			var orbit = orbitsSource[i.origOrbit];

			if (Unity.Burst.CompilerServices.Hint.Likely(flags.isValidOrbit))
				relPositions[index] = orbit.getRelativePositionAtUT(times[i.time]);
			else if (Unity.Burst.CompilerServices.Hint.Unlikely(flags.isLandedVessel))
			{
				// relPositions is in worldspace.  Convert the CB-localspace from GetRelSurfacePosition to worldspace, too.
				//relPositions[index] = GetRelSurfacePosition(sparseDataCompute[index].latLonAlt);

				int blockStart = i.time * stats.numOrbits;
				int parentBodyIndex = blockStart + orbit.refBodyIndex;
				int vesselIndex = i.origOrbit - stats.numBodies;

				var relPos = GetRelSurfacePosition(vesselTemplates[vesselIndex].LLA);
				var rp = new Vector3d(relPos.x, relPos.y, relPos.z);
				var rpw = rotations[parentBodyIndex].celestialFrame.LocalToWorld(rp.xzy);
				relPositions[index] = new double3(rpw.x, rpw.y, rpw.z);
			}
			else
				relPositions[index] = defaultPos;
		}

		// Ported from CelestialBody, modified for alt measured from body center [not surface] and using double3
		private double3 GetRelSurfacePosition(double3 LLA) => GetRelSurfaceNVector(LLA.x, LLA.y) * LLA.z;
		private double3 GetRelSurfaceNVector(double lat, double lon)
		{
			lat *= math.PI_DBL / 180.0;
			lon *= math.PI_DBL / 180.0;
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
		[ReadOnly] internal PositionComputeFactory.FrameStats stats;
		[ReadOnly] internal NativeArray<TimeOrbitIndex> timeOrbitIndex;
		[ReadOnly] public NativeArray<double> times;
		[ReadOnly] public NativeArray<SubStepOrbit> orbitsSource;
		[ReadOnly] public NativeArray<SubstepComputeFlags> flagsSource;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double3> relPositions;
		public NativeArray<double3> worldPositions;

		// Ordering matters because of orbit parent/child hierarchy, so we compute each timestep block start to finish.
		public void Execute(int index)
		{
			int baseOffset = index * stats.numOrbits;
			for (int orbitIndex = 0; orbitIndex < stats.numOrbits; orbitIndex++)
			{
				int curOrbitIndex = baseOffset + orbitIndex;
				TimeOrbitIndex i = timeOrbitIndex[curOrbitIndex];
				SubStepOrbit obt = orbitsSource[i.origOrbit];
				var flags = flagsSource[i.origOrbit];
				bool getWorldOffset = (flags.isLandedVessel || flags.isValidOrbit) && obt.refBodyIndex >= 0;
				var parentWorldPos = Unity.Burst.CompilerServices.Hint.Likely(getWorldOffset) ? worldPositions[baseOffset + obt.refBodyIndex] : double3.zero;
				worldPositions[curOrbitIndex] = relPositions[curOrbitIndex].xzy + parentWorldPos;
			}
		}
		// Copied from CelestialBody
		//private Vector3d GetWorldSurfacePosition(double lat, double lon, double alt) => this.BodyFrame.LocalToWorld(this.GetRelSurfacePosition(lat, lon, alt).xzy).xzy + this.position;
	}

	[BurstCompile]
	struct BuildBodyAndVesselHolders : IJobParallelFor
	{
		[ReadOnly] internal PositionComputeFactory.FrameStats stats;
		[DeallocateOnJobCompletion] [ReadOnly] internal NativeArray<TimeOrbitIndex> timeOrbitIndex;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<SubstepBody> bodySourceData;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<SubstepVessel> vesselSourceData;
		[ReadOnly] public NativeArray<RotationCondition> rotations;
		[ReadOnly] public NativeArray<double3> worldPositions;
		[WriteOnly] public NativeArray<SubstepBody> bodyData;
		[WriteOnly] public NativeArray<SubstepVessel> vesselData;

		public void Execute(int index)
		{
			TimeOrbitIndex i = timeOrbitIndex[index];
			if (!i.isVessel)
			{
				var body = bodySourceData[i.origOrbit];
				body.bodyFrame = rotations[index].celestialFrame;
				body.position = worldPositions[index];
				bodyData[index] = body;
			}
			else
			{
				var vessel = vesselSourceData[i.origOrbit - stats.numBodies];
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
		[ReadOnly] internal PositionComputeFactory.FrameStats stats;
		[WriteOnly] public NativeArray<SubstepBody> bodyData;
		[WriteOnly] public NativeArray<SubstepVessel> vesselData;

		public void Execute()
		{
			int bodyIndex = 0, vesselIndex = 0;
			for (int i=0; i<stats.numSteps; i++)
			{
				bodyData.Slice(bodyIndex, stats.numBodies).CopyFrom(bodyHolderData.Slice(i * stats.numOrbits, stats.numBodies));
				vesselData.Slice(vesselIndex, stats.numVessels).CopyFrom(vesselHolderData.Slice(i * stats.numOrbits + stats.numBodies, stats.numVessels));
				bodyIndex += stats.numBodies;
				vesselIndex += stats.numVessels;
			}
		}
	}
}
