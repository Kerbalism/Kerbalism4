using KERBALISM.SteppedSim.Jobs;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
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
			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.AllocateTimeOrbitIndex");
			var timeOrbitIndex = new NativeArray<TimeOrbitIndex>(sz, Allocator.TempJob);
			Profiler.EndSample();
			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.AllocateRotationsOutput");
			rotations = new NativeArray<RotationCondition>(sz, Allocator.TempJob);
			Profiler.EndSample();
			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.AllocateRelPositions");
			var relativePositions = new NativeArray<double3>(sz, Allocator.TempJob);
			Profiler.EndSample();
			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.AllocateWorldPositions");
			worldPositions = new NativeArray<double3>(sz, Allocator.TempJob);
			Profiler.EndSample();

			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.MakeAndScheduleJobs");
			var buildIndexesJob = new BuildIndexes
			{
				stats = frameStats,
				timeOrbitIndex = timeOrbitIndex,
			}.Schedule(stepGeneratorJob);

			var computeRotationsJob = new RotationsComputeJob
			{
				timeOrbitIndex = timeOrbitIndex,
				PlanetariumInverseRotAngle = Planetarium.InverseRotAngle,
				times = timestepsSource,
				rotationsIn = rotationsSource,
				rotationsOut = rotations
			}.Schedule(sz, 16, buildIndexesJob);

			var computeRelPositionsJob = new NaiveOrbitComputeJob
			{
				stats = frameStats,
				timeOrbitIndex = timeOrbitIndex,
				times = timestepsSource,
				vesselTemplates = vesselTemplates,
				orbitsSource = stepOrbitsSource,
				flagsSource = flagsSource,
				rotations = rotations,
				defaultPos = new double3(defPos.x, defPos.y, defPos.z).xzy,
				relPositions = relativePositions,
			}.Schedule(sz, 8, computeRotationsJob);

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
