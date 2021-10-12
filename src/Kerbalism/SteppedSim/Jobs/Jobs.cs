using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace KERBALISM.SteppedSim
{
	[BurstCompile]
	struct StepGeneratorJob : IJob
	{
		[ReadOnly] public double startUT;
		[ReadOnly] public double interval;
		[ReadOnly] public double numSteps;
		[WriteOnly] public NativeArray<double> times;

		public StepGeneratorJob(double startUT, double duration, double maxSubstepTime) : this()
		{
			this.startUT = startUT;
			numSteps = math.ceil(duration / maxSubstepTime);
			interval = duration / numSteps;
		}

		public void Execute()
		{
			var ut = startUT;
			for (int i=0; i<numSteps; i++)
			{
				ut += interval;
				times[i] = ut;
			}
		}
	}


}
