using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace KERBALISM.SteppedSim.Jobs.VesselDataJobs
{
	// TODO: Make timestep-aware
    [BurstCompile]
	public struct SumIrradiancesJob : IJobParallelFor
	{
		[ReadOnly] public int numBodies;
		[ReadOnly] public NativeArray<double> times;
		[ReadOnly] public NativeArray<VesselBodyIrradiance> irradiances;
		[WriteOnly] public NativeArray<VesselBodyIrradiance> output;

		// for each body, sum its irradiances over all times.
		// index == which body
		public void Execute(int index)
		{
			VesselBodyIrradiance result = default;
			for (int time=0; time < times.Length; time++)
			{
				VesselBodyIrradiance vbi = irradiances[time * numBodies + index];
				result.albedo += vbi.albedo;
				result.emissive += vbi.emissive;
				result.core += vbi.core;
				result.solar += vbi.solar;
				result.solarRaw += vbi.solarRaw;
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
