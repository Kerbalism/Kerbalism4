using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace KERBALISM.SteppedSim.Jobs.VesselDataJobs
{
	[BurstCompile]
	public struct ComputeFrameWeights : IJob
	{
		[ReadOnly] public double start;
		[ReadOnly] public NativeArray<double> times;
		[WriteOnly] public NativeArray<float> weights;

		public void Execute()
		{
			double totalTime = times[times.Length - 1] - start;
			double totalTimeRecip = Unity.Burst.CompilerServices.Hint.Likely(totalTime > 0) ? 1 / totalTime : 1;
			double prev = start;
			for (int i=0; i<times.Length; i++)
			{
				weights[i] = (float) ((times[i] - prev) * totalTimeRecip);
				prev = times[i];
			}
		}
	}

	[BurstCompile]
	public struct SumIrradiancesJob : IJobParallelFor
	{
		[ReadOnly] public int numBodies;
		[ReadOnly] public NativeArray<float> weights;
		[ReadOnly] public NativeArray<VesselBodyIrradiance> irradiances;
		[WriteOnly] public NativeArray<VesselBodyIrradiance> output;

		// for each body, sum its irradiances over all times.
		// index == which body
		public void Execute(int index)
		{
			VesselBodyIrradiance result = default;
			for (int frameIndex=0; frameIndex < weights.Length; frameIndex++)
			{
				VesselBodyIrradiance vbi = irradiances[frameIndex * numBodies + index];
				float weight = weights[frameIndex];
				result.albedo += vbi.albedo * weight;
				result.emissive += vbi.emissive * weight;
				result.core += vbi.core * weight;
				result.solar += vbi.solar * weight;
				result.solarRaw += vbi.solarRaw * weight;
			}
			output[index] = result;
		}
	}

	[BurstCompile]
	public struct SumIrradiancesJobFinal : IJob
	{
		[ReadOnly] public int numBodies;
		[ReadOnly] public NativeArray<VesselBodyIrradiance> irradiances;
		[WriteOnly] public NativeArray<VesselBodyIrradiance> output;

		// irradiances is the time-combined source, for each body.  Sum.
		public void Execute()
		{
			VesselBodyIrradiance result = default;
			for (int body = 0; body < numBodies; body++)
			{
				VesselBodyIrradiance vbi = irradiances[body];
				result.albedo += vbi.albedo;
				result.emissive += vbi.emissive;
				result.core += vbi.core;
				result.solar += vbi.solar;
				result.solarRaw += vbi.solarRaw;
			}
			output[0] = result;
		}
	}
}
