using KERBALISM.SteppedSim.Jobs;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace KERBALISM.SteppedSim
{
	public static class PositionComputeFactory
	{
		internal static void ComputePositions(
			in NativeArray<double> timestepsSource,
			in NativeArray<RotationCondition> rotationsSource,
			in NativeArray<SparseSimData> sparseDataSource,
			in List<(Orbit, CelestialBody)> Orbits,
			in Dictionary<CelestialBody, int> BodyIndex,
			in Vector3d defPos,
			ref JobHandle stepGeneratorJob,
			out JobHandle computeWorldPositionJob,
			out NativeArray<RotationCondition> rotations,
			out NativeArray<double3> relativePositions,
			out NativeArray<double3> worldPositions)
		{
			int numSteps = timestepsSource.Length;

			var stepOrbitsSource = new NativeArray<SubStepOrbit>(Orbits.Count, Allocator.TempJob);
			int numOrbits = 0;
			foreach (var (stockOrbit, refBody) in Orbits)
			{
				stepOrbitsSource[numOrbits++] = new SubStepOrbit(stockOrbit, refBody, BodyIndex);
			}

			int sz = numSteps * numOrbits;
			var timestepsUnrolled = new NativeArray<double>(sz, Allocator.TempJob);
			var stepOrbitsUnrolled = new NativeArray<SubStepOrbit>(sz, Allocator.TempJob);
			var rotationsUnrolled = new NativeArray<RotationCondition>(sz, Allocator.TempJob);
			var sparseDataUnrolled = new NativeArray<SparseSimData>(sz, Allocator.TempJob);

			stepGeneratorJob.Complete();
			var computeSetJob = new UnrollPositionComputationsJob(numOrbits)
			{
				times = timestepsSource,
				orbits = stepOrbitsSource,
				rotations = rotationsSource,
				sparseData = sparseDataSource,
				timesCompute = timestepsUnrolled,
				orbitsCompute = stepOrbitsUnrolled,
				rotationsCompute = rotationsUnrolled,
				sparseDataCompute = sparseDataUnrolled,
			}.Schedule(numSteps, numOrbits);

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
				rotationsCompute = rotations,
				relPositions = relativePositions,
				defaultPos = new double3(defPos.x, defPos.y, defPos.z).xzy
			}.Schedule(sz, 8, computeRotationsJob);

			worldPositions = new NativeArray<double3>(sz, Allocator.TempJob);
			computeWorldPositionJob = new ComputeWorldspacePositionsJob
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

			//computeWorldPositionJob.Complete();

		}
	}
}
