using KERBALISM.SteppedSim.Jobs;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine.Profiling;

namespace KERBALISM.SteppedSim
{
	public static class PositionComputeFactory
	{
		internal struct TimeOrbitIndex
		{
			public int time;
			public int origOrbit;
			public int directOrbit;
			public bool isVessel;
		}
		internal struct FrameStats
		{
			public int numSteps;
			public int numBodies;
			public int numVessels;
			public int numOrbits;
		}
		internal static void ComputePositions(
			in NativeArray<double> timestepsSource,
			in NativeArray<SubStepOrbit> stepOrbitsSource,
			in NativeArray<RotationCondition> rotationsSource,
			in NativeArray<SubstepComputeFlags> flagsSource,
			in NativeArray<SubstepBody> bodyTemplates,
			in NativeArray<SubstepVessel> vesselTemplates,
			in Vector3d defPos,
			ref JobHandle stepGeneratorJob,
			out JobHandle finalJob,
			out NativeArray<RotationCondition> rotations,
			out NativeArray<double3> worldPositions,
			out NativeArray<SubstepBody> bodyData,
			out NativeArray<SubstepVessel> vesselData)
		{
			int numSteps = timestepsSource.Length;
			int numBodies = bodyTemplates.Length;
			int numVessels = vesselTemplates.Length;
			int numOrbits = numBodies + numVessels;

			var frameStats = new FrameStats
			{
				numSteps = numSteps,
				numBodies = numBodies,
				numVessels = numVessels,
				numOrbits = numOrbits,
			};

			int sz = numSteps * numOrbits;
			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.FastestAllocations");
			var flags = new NativeArray<SubstepComputeFlags>(sz, Allocator.TempJob);
			var times = new NativeArray<double>(sz, Allocator.TempJob);
			var obtAtUTs = new NativeArray<double>(sz, Allocator.TempJob);
			var eccAnomalies = new NativeArray<double>(sz, Allocator.TempJob);
			var trueAnomalies = new NativeArray<double>(sz, Allocator.TempJob);
			var eccentricitiesSource = new NativeArray<double>(numOrbits, Allocator.TempJob);
			var sqrt_EccPlus1sSource = new NativeArray<double>(numOrbits, Allocator.TempJob);
			var sqrt_EccMinus1sSource = new NativeArray<double>(numOrbits, Allocator.TempJob);
			var eccentricities = new NativeArray<double>(sz, Allocator.TempJob);
			var sqrt_EccPlus1s = new NativeArray<double>(sz, Allocator.TempJob);
			var sqrt_EccMinus1s = new NativeArray<double>(sz, Allocator.TempJob);
			Profiler.EndSample();
			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.MediumAllocations");
			var timeOrbitIndex = new NativeArray<TimeOrbitIndex>(sz, Allocator.TempJob);
			var eccAnomaliesData = new NativeArray<double4>(sz, Allocator.TempJob);
			var relativePositions = new NativeArray<double3>(sz, Allocator.TempJob);
			worldPositions = new NativeArray<double3>(sz, Allocator.TempJob);
			Profiler.EndSample();
			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.AllocateRotationsOutput");
			rotations = new NativeArray<RotationCondition>(sz, Allocator.TempJob);
			Profiler.EndSample();

			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.MakeAndScheduleJobs");
			var eccSourceJob = new EccentricityPrecalcJob
			{
				orbits = stepOrbitsSource,
				eccentricity = eccentricitiesSource,
				sqrt_eccMinus1 = sqrt_EccMinus1sSource,
				sqrt_eccPlus1 = sqrt_EccPlus1sSource,
			}.Schedule(numOrbits, 8);
			var buildIndexesJob = new BuildIndexes
			{
				stats = frameStats,
				timeOrbitIndex = timeOrbitIndex,
			}.Schedule(stepGeneratorJob);
			var unrollJob = new UnrollUsefulData
			{
				timeOrbitIndex = timeOrbitIndex,
				timestepSource = timestepsSource,
				flagsSource = flagsSource,
				sqrt_EccMinus1sSource = sqrt_EccMinus1sSource,
				sqrt_EccPlus1sSource = sqrt_EccPlus1sSource,
				eccentricitiesSource = eccentricitiesSource,
				times = times,
				flags = flags,
				sqrt_EccMinus1s = sqrt_EccMinus1s,
				sqrt_EccPlus1s = sqrt_EccPlus1s,
				eccentricities = eccentricities,
			}.Schedule(sz, 256, JobHandle.CombineDependencies(eccSourceJob, buildIndexesJob));

			var computeRotationsJob = new RotationsComputeJob
			{
				timeOrbitIndex = timeOrbitIndex,
				PlanetariumInverseRotAngle = Planetarium.InverseRotAngle,
				times = times,
				rotationsIn = rotationsSource,
				rotationsOut = rotations
			}.Schedule(sz, 128, unrollJob);

			var computeObTsJob = new ComputeObTJob
			{
				timeOrbitIndex = timeOrbitIndex,
				times = times,
				flags = flags,
				orbitsSource = stepOrbitsSource,
				obtAtUTs = obtAtUTs,
			}.Schedule(sz, 256, unrollJob);
			JobHandle.ScheduleBatchedJobs();

			var solveEccAnomaliesJob = new SolveEccentricAnomalyJob
			{
				timeOrbitIndex = timeOrbitIndex,
				flags = flags,
				orbitsSource = stepOrbitsSource,
				obtAtUTs = obtAtUTs,
				eccAnomalies = eccAnomalies,
			}.Schedule(sz, 32, computeObTsJob);

			var getAnomalyDataJob = new GetEccAnomalyRefData
			{
				eccAnomalies = eccAnomalies,
				eccentricities = eccentricities,
				eccAnomalyData = eccAnomaliesData,
			}.Schedule(sz, 256, solveEccAnomaliesJob);

			var getTrueAnomaliesJob = new GetTrueAnomalyJob
			{
				flags = flags,
				eccentricities = eccentricities,
				sqrt_eccMinus1s = sqrt_EccMinus1s,
				sqrt_eccPlus1s = sqrt_EccPlus1s,
				eccAnomalies = eccAnomalies,
				eccAnomalyData = eccAnomaliesData,
				trueAnomalies = trueAnomalies,
			}.Schedule(sz, 128, getAnomalyDataJob);

			var computeRelPositionsJob = new OrbitComputeJob
			{
				stats = frameStats,
				timeOrbitIndex = timeOrbitIndex,
				times = times,
				trueAnomalies = trueAnomalies,
				vesselTemplates = vesselTemplates,
				orbitsSource = stepOrbitsSource,
				flags = flags,
				rotations = rotations,
				defaultPos = new double3(defPos.x, defPos.y, defPos.z).xzy,
				relPositions = relativePositions,
			}.Schedule(sz, 32, JobHandle.CombineDependencies(getTrueAnomaliesJob, computeRotationsJob));

			var computeWorldPositionJob = new ComputeWorldspacePositionsJob
			{
				stats = frameStats,
				timeOrbitIndex = timeOrbitIndex,
				times = timestepsSource,
				orbitsSource = stepOrbitsSource,
				flagsSource = flagsSource,
				relPositions = relativePositions,
				worldPositions = worldPositions,
			}.Schedule(numSteps, 1, computeRelPositionsJob);
			JobHandle.ScheduleBatchedJobs();

			Profiler.EndSample();

			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.AllocateTempBodyVesselData");
			var bodyDataTemp = new NativeArray<SubstepBody>(sz, Allocator.TempJob);
			var vesselDataTemp = new NativeArray<SubstepVessel>(sz, Allocator.TempJob);
			Profiler.EndSample();

			var buildHoldersJob = new BuildBodyAndVesselHolders
			{
				stats = frameStats,
				timeOrbitIndex = timeOrbitIndex,
				bodySourceData = bodyTemplates,
				vesselSourceData = vesselTemplates,
				rotations = rotations,
				worldPositions = worldPositions,
				bodyData = bodyDataTemp,
				vesselData = vesselDataTemp,
			}.Schedule(sz, 128, computeWorldPositionJob);

			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.AllocateFinalBodyVesselData");
			bodyData = new NativeArray<SubstepBody>(numBodies * numSteps, Allocator.TempJob);
			vesselData = new NativeArray<SubstepVessel>(numVessels * numSteps, Allocator.TempJob);
			Profiler.EndSample();
			finalJob = new RealignBodyAndVesselArrays
			{
				stats = frameStats,
				bodyHolderData = bodyDataTemp,
				vesselHolderData = vesselDataTemp,
				bodyData = bodyData,
				vesselData = vesselData,
			}.Schedule(buildHoldersJob);
			JobHandle.ScheduleBatchedJobs();
		}
	}
}
