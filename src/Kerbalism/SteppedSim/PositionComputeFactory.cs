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
			in NativeArray<RotationCondition> rotationsSource,
			in NativeArray<SparseSimData> sparseDataSource,
			in NativeArray<SubstepBody> bodyTemplates,
			in NativeArray<SubstepVessel> vesselTemplates,
			in List<(Orbit, CelestialBody)> Orbits,
			in Dictionary<CelestialBody, int> BodyIndex,
			in Vector3d defPos,
			ref JobHandle stepGeneratorJob,
			out JobHandle finalJob,
			out NativeArray<RotationCondition> rotations,
			out NativeArray<double3> relativePositions,
			out NativeArray<double3> worldPositions,
			out NativeArray<SubstepBody> bodyData,
			out NativeArray<SubstepVessel> vesselData)
		{
			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.SetupOrbitsSource");

			int numSteps = timestepsSource.Length;
			int numBodies = bodyTemplates.Length;

			var stepOrbitsSource = new NativeArray<SubStepOrbit>(Orbits.Count, Allocator.TempJob);
			int numOrbits = 0;
			foreach (var (stockOrbit, refBody) in Orbits)
			{
				stepOrbitsSource[numOrbits++] = new SubStepOrbit(stockOrbit, refBody, BodyIndex);
			}
			Profiler.EndSample();

			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.AllocateFirst6");
			int sz = numSteps * numOrbits;
			var timestepsUnrolled = new NativeArray<double>(sz, Allocator.TempJob);
			var stepOrbitsUnrolled = new NativeArray<SubStepOrbit>(sz, Allocator.TempJob);
			var rotationsUnrolled = new NativeArray<RotationCondition>(sz, Allocator.TempJob);
			var sparseDataUnrolled = new NativeArray<SparseSimData>(sz, Allocator.TempJob);
			var bodyTemplatesUnrolled = new NativeArray<SubstepBody>(sz, Allocator.TempJob);
			var vesselTemplatesUnrolled = new NativeArray<SubstepVessel>(sz, Allocator.TempJob);
			Profiler.EndSample();

			stepGeneratorJob.Complete();
			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions.MakeAndScheduleJobs");

			var computeSetJob = new UnrollPositionComputationsJob(numOrbits, numBodies)
			{
				times = timestepsSource,
				bodyTemplates = bodyTemplates,
				vesselTemplates = vesselTemplates,
				orbits = stepOrbitsSource,
				rotations = rotationsSource,
				sparseData = sparseDataSource,
				timesCompute = timestepsUnrolled,
				orbitsCompute = stepOrbitsUnrolled,
				rotationsCompute = rotationsUnrolled,
				sparseDataCompute = sparseDataUnrolled,
				bodyTemplatesCompute = bodyTemplatesUnrolled,
				vesselTemplatesCompute = vesselTemplatesUnrolled,
			}.Schedule(numSteps, 1);
			JobHandle.ScheduleBatchedJobs();

			rotations = new NativeArray<RotationCondition>(sz, Allocator.TempJob);
			var computeRotationsJob = new RotationsComputeJob
			{
				PlanetariumInverseRotAngle = Planetarium.InverseRotAngle,
				times = timestepsUnrolled,
				rotationsIn = rotationsUnrolled,
				rotationsOut = rotations
			}.Schedule(sz, 16, computeSetJob);
			
			relativePositions = new NativeArray<double3>(sz, Allocator.TempJob);
			//var defPos = Bodies[0].position;
			var computeRelPositionsJob = new NaiveOrbitComputeJob
			{
				numTimesteps = numSteps,
				numOrbitsPerTimestep = numOrbits,
				timesCompute = timestepsUnrolled,
				orbitsCompute = stepOrbitsUnrolled,
				sparseDataCompute = sparseDataUnrolled,
				vesselTemplatesCompute = vesselTemplatesUnrolled,
				rotationsCompute = rotations,
				relPositions = relativePositions,
				defaultPos = new double3(defPos.x, defPos.y, defPos.z).xzy
			}.Schedule(sz, 1, computeRotationsJob);

			worldPositions = new NativeArray<double3>(sz, Allocator.TempJob);
			var computeWorldPositionJob = new ComputeWorldspacePositionsJob
			{
				numOrbits = numOrbits,
				timesCompute = timestepsUnrolled,
				orbitsCompute = stepOrbitsUnrolled,
				sparseData = sparseDataUnrolled,
				rotations = rotations,
				relPositions = relativePositions,
				worldPositions = worldPositions,
			}.Schedule(numSteps, 4, computeRelPositionsJob);
			JobHandle.ScheduleBatchedJobs();

			bodyData = new NativeArray<SubstepBody>(sz, Allocator.TempJob);
			vesselData = new NativeArray<SubstepVessel>(sz, Allocator.TempJob);
			finalJob = new BuildBodyAndVesselHolders
			{
				bodyTemplatesUnrolled = bodyTemplatesUnrolled,
				vesselTemplatesUnrolled = vesselTemplatesUnrolled,
				rotations = rotations,
				relPositions = relativePositions,
				worldPositions = worldPositions,
				bodyData = bodyData,
				vesselData = vesselData,
			}.Schedule(sz, 16, computeWorldPositionJob);

			Profiler.EndSample();
			//computeWorldPositionJob.Complete();

		}
	}
}
