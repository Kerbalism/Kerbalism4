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
			out NativeArray<double3> relativePositions,
			out NativeArray<double3> worldPositions,
			out NativeArray<SubstepBody> bodyData,
			out NativeArray<SubstepVessel> vesselData)
		{
			int numSteps = timestepsSource.Length;
			int numBodies = bodyTemplates.Length;
			int numVessels = vesselTemplates.Length;
			int numOrbits = numBodies + numVessels;

			int sz = numSteps * numOrbits;
			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.AllocateIndicesSource");
			var indicesSource = new NativeArray<int2>(numOrbits, Allocator.TempJob);
			Profiler.EndSample();
			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.AllocateIndices");
			var indices = new NativeArray<int2>(sz, Allocator.TempJob);
			Profiler.EndSample();
			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.AllocateSteps");
			var timestepsUnrolled = new NativeArray<double>(sz, Allocator.TempJob);
			Profiler.EndSample();
			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.AllocateFlags");
			var flagsUnrolled = new NativeArray<SubstepComputeFlags>(sz, Allocator.TempJob);
			Profiler.EndSample();
			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.AllocateRotationsOutput");
			rotations = new NativeArray<RotationCondition>(sz, Allocator.TempJob);
			Profiler.EndSample();
			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.AllocateRelPositions");
			relativePositions = new NativeArray<double3>(sz, Allocator.TempJob);
			Profiler.EndSample();
			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.AllocateWorldPositions");
			worldPositions = new NativeArray<double3>(sz, Allocator.TempJob);
			Profiler.EndSample();

			stepGeneratorJob.Complete();
			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.MakeAndScheduleJobs");
			var buildIndexesJob = new BuildIndexes
			{
				numBodies = numBodies,
				numVessels = numVessels,
				indices = indicesSource
			}.Schedule();
			var unrollIndicesJob = new Unroll<int2>(numOrbits)
			{
				input = indicesSource,
				output = indices
			}.Schedule(numSteps, 1, buildIndexesJob);
			var unrollIndicesDeallocatedJob = indicesSource.Dispose(unrollIndicesJob);
			var unrollFlagsJob = new Unroll<SubstepComputeFlags>(numOrbits)
			{
				input = flagsSource,
				output = flagsUnrolled
			}.Schedule(numSteps, 1);
			var unrollTimestepsJob = new UnrollSingle<double>(numOrbits)
			{
				input = timestepsSource,
				output = timestepsUnrolled
			}.Schedule(numSteps, 1);
			JobHandle.ScheduleBatchedJobs();

			var computeRotationsJob = new RotationsComputeJob
			{
				frameSize = numOrbits,
				PlanetariumInverseRotAngle = Planetarium.InverseRotAngle,
				times = timestepsUnrolled,
				rotationsIn = rotationsSource,
				rotationsOut = rotations
			}.Schedule(sz, 16, JobHandle.CombineDependencies(unrollIndicesDeallocatedJob, unrollTimestepsJob, unrollFlagsJob));

			//var defPos = Bodies[0].position;
			var computeRelPositionsJob = new NaiveOrbitComputeJob
			{
				numTimesteps = numSteps,
				numOrbitsPerTimestep = numOrbits,
				indices = indices,
				vesselTemplates = vesselTemplates,
				orbitsSource = stepOrbitsSource,
				timesCompute = timestepsUnrolled,
				flagsArr = flagsUnrolled,
				rotationsCompute = rotations,
				defaultPos = new double3(defPos.x, defPos.y, defPos.z).xzy,
				relPositions = relativePositions,
			}.Schedule(sz, 1, computeRotationsJob);

			var computeWorldPositionJob = new ComputeWorldspacePositionsJob
			{
				numOrbits = numOrbits,
				timesCompute = timestepsUnrolled,
				orbitsSource = stepOrbitsSource,
				flagsData = flagsUnrolled,
				relPositions = relativePositions,
				worldPositions = worldPositions,
			}.Schedule(numSteps, 4, computeRelPositionsJob);
			JobHandle.ScheduleBatchedJobs();

			Profiler.EndSample();

			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.AllocateFinalVesselAndBodyData");
			bodyData = new NativeArray<SubstepBody>(sz, Allocator.TempJob);
			vesselData = new NativeArray<SubstepVessel>(sz, Allocator.TempJob);
			Profiler.EndSample();

			finalJob = new BuildBodyAndVesselHolders
			{
				bodySourceData = bodyTemplates,
				vesselSourceData = vesselTemplates,
				indices = indices,
				rotations = rotations,
				relPositions = relativePositions,
				worldPositions = worldPositions,
				bodyData = bodyData,
				vesselData = vesselData,
			}.Schedule(sz, 16, computeWorldPositionJob);

			//computeWorldPositionJob.Complete();

		}
	}
}
