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
	public struct UnrollUsefulData : IJobParallelFor
	{
		[ReadOnly] internal NativeArray<TimeOrbitIndex> timeOrbitIndex;
		[ReadOnly] public NativeArray<double> timestepSource;
		[ReadOnly] public NativeArray<SubstepComputeFlags> flagsSource;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> sqrt_EccMinus1sSource;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> sqrt_EccPlus1sSource;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> eccentricitiesSource;
		[WriteOnly] public NativeArray<double> times;
		[WriteOnly] public NativeArray<SubstepComputeFlags> flags;
		[WriteOnly] public NativeArray<double> sqrt_EccMinus1s;
		[WriteOnly] public NativeArray<double> sqrt_EccPlus1s;
		[WriteOnly] public NativeArray<double> eccentricities;

		public void Execute(int index)
		{
			TimeOrbitIndex i = timeOrbitIndex[index];
			times[index] = timestepSource[i.time];
			flags[index] = flagsSource[i.origOrbit];
			sqrt_EccMinus1s[index] = sqrt_EccMinus1sSource[i.origOrbit];
			sqrt_EccPlus1s[index] = sqrt_EccPlus1sSource[i.origOrbit];
			eccentricities[index] = eccentricitiesSource[i.origOrbit];
		}
	}

	[BurstCompile]
	public struct EccentricityPrecalcJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<SubStepOrbit> orbits;
		[WriteOnly] public NativeArray<double> sqrt_eccPlus1;
		[WriteOnly] public NativeArray<double> sqrt_eccMinus1;
		[WriteOnly] public NativeArray<double> eccentricity;

		public void Execute(int index)
		{
			double ecc = orbits[index].eccentricity;
			eccentricity[index] = ecc;
			sqrt_eccPlus1[index] = math.sqrt(ecc + 1);
			sqrt_eccMinus1[index] = math.sqrt(math.abs(ecc - 1));
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
			const double recip360 = 1.0 / 360;
			TimeOrbitIndex i = timeOrbitIndex[index];
			double time = times[index];
			var srcRotation = rotationsIn[i.origOrbit];

			var dt = time - srcRotation.frameTime;
			var ang = 360 + srcRotation.angle + srcRotation.velocity * dt;
			var directRotAngle = ang - PlanetariumInverseRotAngle;
			ang -= ((int)(ang * recip360)) * 360;
			//PlanetaryFrame(0.0, 90.0, directRotAngle, ref frame);
			FastPlanetaryFrame(directRotAngle, out Planetarium.CelestialFrame frame);
			rotationsOut[index] = new RotationCondition
			{
				frameTime = time,
				axis = srcRotation.axis,
				velocity = srcRotation.velocity,
				celestialFrame = frame,
				angle = ang,
			};
		}

		private void SetFrame(double A, double B, double C, ref Planetarium.CelestialFrame cf)
		{
			math.sincos(A, out double sinA, out double cosA);
			math.sincos(B, out double sinB, out double cosB);
			math.sincos(C, out double sinC, out double cosC);
			cf.X = new Vector3d(cosA * cosC - sinA * cosB * sinC, sinA * cosC + cosA * cosB * sinC, sinB * sinC);
			cf.Y = new Vector3d(-cosA * sinC - sinA * cosB * cosC, -sinA * sinC + cosA * cosB * cosC, sinB * cosC);
			cf.Z = new Vector3d(sinA * sinB, -cosA * sinB, cosB);
		}

		private void PlanetaryFrame(
		  double ra,
		  double dec,
		  double rot,
		  ref Planetarium.CelestialFrame cf)
		{
			const double degToRad = math.PI_DBL / 180;
			ra = (ra - 90.0) * degToRad;
			dec = (dec - 90.0) * degToRad;
			rot = (rot + 90.0) * degToRad;
			SetFrame(ra, dec, rot, ref cf);
		}

		// Dedicated form of PlanetaryFrame(0, 90, rot, cf)
		private void FastPlanetaryFrame(double rot, out Planetarium.CelestialFrame cf)
		{
			const double degToRad = math.PI_DBL / 180;
			cf = default;
			//const double ra = (0-90) * degToRad;
			//const double dec = (90 - 90) * degToRad;
			rot = (rot + 90) * degToRad;
			//			SetFrame(ra, dec, rot, ref cf);
			//double sinA = -1;   // sin ra = sin -90 = -1
			//double cosA = 0;    // cos ra = cos -90 = 0
			//double sinB = 0;        // sin dec = sin 0
			//double cosB = 1;        // cos dec = cos 0
			math.sincos(rot, out double sinC, out double cosC);
			cf.X = new Vector3d(sinC, -cosC, 0);
			cf.Y = new Vector3d(cosC, sinC, 0);
			cf.Z = new Vector3d(0, 0, 1);
		}
	}

	[BurstCompile]
	public struct ComputeObTJob : IJobParallelFor
	{
		[ReadOnly] internal NativeArray<TimeOrbitIndex> timeOrbitIndex;
		[ReadOnly] public NativeArray<double> times;
		[ReadOnly] public NativeArray<SubStepOrbit> orbitsSource;
		[ReadOnly] public NativeArray<SubstepComputeFlags> flags;
		[WriteOnly] public NativeArray<double> obtAtUTs;

		public void Execute(int index)
		{
			TimeOrbitIndex i = timeOrbitIndex[index];
			double UT = times[index];
			var flag = flags[index];
			if (Unity.Burst.CompilerServices.Hint.Likely(flag.isValidOrbit))
			{
				var orbit = orbitsSource[i.origOrbit];
				double period = orbit.period;
				double periodRecip = 1 / period;
				double obt = orbit.ObTAtEpoch + UT - orbit.epoch;
				if (Unity.Burst.CompilerServices.Hint.Likely(orbit.eccentricity < 1))
				{
					// Optimize mod operation with remainder = x - (x/y)*y pattern
					// Optimize away if by rotating before and after the mod operation.
					double halfPeriod = period * 0.5;
					obt += halfPeriod;
					int div = (int)(obt * periodRecip);
					obt -= div * period;
					obt -= halfPeriod;
				}
				obtAtUTs[index] = obt;
			}
		}
	}

	[BurstCompile]
	public struct SolveEccentricAnomalyJob : IJobParallelFor
	{
		[ReadOnly] internal NativeArray<TimeOrbitIndex> timeOrbitIndex;
		[ReadOnly] public NativeArray<SubStepOrbit> orbitsSource;
		[ReadOnly] public NativeArray<SubstepComputeFlags> flags;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> obtAtUTs;
		[WriteOnly] public NativeArray<double> eccAnomalies;
		public void Execute(int index)
		{
			TimeOrbitIndex i = timeOrbitIndex[index];
			var flag = flags[index];
			if (Unity.Burst.CompilerServices.Hint.Likely(flag.isValidOrbit))
			{
				var orbit = orbitsSource[i.origOrbit];
				double obt = obtAtUTs[index];
				eccAnomalies[index] = orbit.solveEccentricAnomaly(obt * orbit.meanMotion, orbit.eccentricity);
			}
		}
	}

	[BurstCompile]
	public struct GetEccAnomalyRefData : IJobParallelFor
	{
		[ReadOnly] public NativeArray<double> eccAnomalies;
		[ReadOnly] public NativeArray<double> eccentricities;
		[WriteOnly] public NativeArray<double4> eccAnomalyData;

		public void Execute(int index)
		{
			double halfE = eccAnomalies[index] * 0.5;
			double ecc = eccentricities[index];
			double sinE = 0, cosE = 0, sinhE = 0, coshE = 0;
			if (Unity.Burst.CompilerServices.Hint.Likely(ecc < 1))
				math.sincos(halfE, out sinE, out cosE);
			else
			{
				sinhE = math.sinh(halfE);
				coshE = math.cosh(halfE);
			}
			eccAnomalyData[index] = new double4(sinE, cosE, sinhE, coshE);
		}
	}

	[BurstCompile]
	public struct GetTrueAnomalyJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<SubstepComputeFlags> flags;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> eccentricities;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> eccAnomalies;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> sqrt_eccPlus1s;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> sqrt_eccMinus1s;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double4> eccAnomalyData;
		[WriteOnly] public NativeArray<double> trueAnomalies;
		public void Execute(int index)
		{
			bool orbitValid = flags[index].isValidOrbit;
			double eccentricity = eccentricities[index];
			double E = eccAnomalies[index];
			bool finite = !double.IsInfinity(E);
			double4 anomalyData = eccAnomalyData[index];
			double sqrt_eccP1 = sqrt_eccPlus1s[index];
			double sqrt_eccM1 = sqrt_eccMinus1s[index];
			bool validClosedOrbit = orbitValid && eccentricity < 1 && finite;
			bool validHyperbolicOrbit = orbitValid && eccentricity >= 1 && finite;
			if (Unity.Burst.CompilerServices.Hint.Likely(validClosedOrbit))
			{
				double param1 = sqrt_eccP1 * anomalyData.x;
				double param2 = sqrt_eccM1 * anomalyData.y;
				trueAnomalies[index] = 2 * math.atan2(param1, param2);
			} else if (Unity.Burst.CompilerServices.Hint.Likely(validHyperbolicOrbit))
			{
				double param1 = sqrt_eccP1 * anomalyData.z;
				double param2 = sqrt_eccM1 * anomalyData.w;
				trueAnomalies[index] = 2 * math.atan2(param1, param2);
			} else if (orbitValid && !finite)
				trueAnomalies[index] = math.sign(E) * math.acos(-1 / eccentricity);
		}
	}

	[BurstCompile]
	public struct OrbitComputeJob : IJobParallelFor
	{
		[ReadOnly] internal PositionComputeFactory.FrameStats stats;
		[ReadOnly] internal NativeArray<TimeOrbitIndex> timeOrbitIndex;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> times;
		[ReadOnly] public NativeArray<SubstepVessel> vesselTemplates;
		[ReadOnly] public NativeArray<SubStepOrbit> orbitsSource;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<SubstepComputeFlags> flags;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> trueAnomalies;
		[ReadOnly] public NativeArray<RotationCondition> rotations;
		[ReadOnly] public double3 defaultPos;
		[WriteOnly] public NativeArray<double3> relPositions;

		public void Execute(int index)
		{
			TimeOrbitIndex i = timeOrbitIndex[index];
			var flag = flags[index];
			var orbit = orbitsSource[i.origOrbit];
			double UT = times[index];

			if (Unity.Burst.CompilerServices.Hint.Likely(flag.isValidOrbit))
			{
				if (Unity.Burst.CompilerServices.Hint.Unlikely(double.IsInfinity(UT)))
					relPositions[index] = orbit.eccentricity < 1 ? double.NaN : UT;
				else
					relPositions[index] = orbit.getPositionFromTrueAnomaly(trueAnomalies[index], true);
			}
			else if (Unity.Burst.CompilerServices.Hint.Likely(flag.isLandedVessel))
			{
				// relPositions is in worldspace.  Convert the CB-localspace from GetRelSurfacePosition to worldspace, too.

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
			const double convert = math.PI_DBL / 180;
			lat *= convert;
			lon *= convert;
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
