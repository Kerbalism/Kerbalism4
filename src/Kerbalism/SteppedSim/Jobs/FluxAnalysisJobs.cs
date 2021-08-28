using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace KERBALISM.SteppedSim.Jobs
{
	[BurstCompile]
	public struct BuildPairsJob : IJob
	{
		[ReadOnly] public int numBodies;
		[ReadOnly] public int numVessels;
		[WriteOnly] public NativeArray<int2> pairs;
		public void Execute()
		{
			int sz = numBodies * numVessels;
			for (int i = 0; i < sz; i++)
			{
				// Under this scenario, .x is VesselIndex an .y is BodyIndex
				pairs[i] = new int2(i / numBodies, i % numBodies);
			}
		}
	}

	[BurstCompile]
	public struct OcclusionRelevanceJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[ReadOnly] public NativeArray<SubstepVessel> vessels;
		[WriteOnly] public NativeArray<byte> interestMap;

		public void Execute(int index)
		{
			// Compute the interesting body for the vessel at vessels[index]
			// if apparent diameter < ~10 arcmin (~0.003 radians), don't consider the body for occlusion checks
			// real apparent diameters at earth : sun/moon ~ 30 arcmin, Venus ~ 1 arcmin max
			const double minRequiredHalfAngleRadians = 0.002909 / 2;    // 10 arcminutes
			var vesselPos = vessels[index].position;
			var baseIndex = index * bodies.Length;
			for (int i=0; i<bodies.Length; i++)
			{
				var body = bodies[i];
				var d1 = math.distance(vesselPos, body.position);
				//var halfAngle = math.atan2(d1, body.radius);
				//interestMap[baseIndex++] = (byte)(halfAngle > minRequiredHalfAngleRadians ? 1 : 0);

				// Take advantage of fact that sin(x) == x, cos(x) == 1, tan(x) == x for small x
				// Simplify atan(x/y) > min ==> (x/y) > min ==> x > min * y
				interestMap[baseIndex++] = (byte) (body.radius > minRequiredHalfAngleRadians * d1 ? 1 : 0);
			}
		}
	}


	[BurstCompile]
	public struct BodySunOcclusionJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[WriteOnly] public NativeArray<bool> occluded;

		public void Execute(int index)
		{
			if (Unity.Burst.CompilerServices.Hint.Unlikely(index == 0))
				occluded[index] = false;
			else
			{
				var sun = bodies[0];
				var body = bodies[index];
				// avoid self-occluding, so adjust the body and sun positions...
				var dir = math.normalize(sun.position - body.position);
				var start = body.position + (body.radius + 10) * dir;
				var end = sun.position - (sun.radius + 10) * dir;
				occluded[index] = SubstepBody.Occluded(start, end, bodies);
			}
		}
	}

	[BurstCompile]
	public struct BodyVesselOcclusionJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<SubstepVessel> vessels;
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[ReadOnly] public NativeArray<int2> pairs;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<byte> interestMap;
		[WriteOnly] public NativeArray<bool> occluded;

		public void Execute(int index)
		{
			var pair = pairs[index];
			var vessel = vessels[pair.x];
			var body = bodies[pair.y];

			// avoid self-occluding, so adjust the body position
			var dir = math.normalize(body.position - vessel.position);
			var end = body.position - (body.radius + 10) * dir;

			var a = vessel.position;
			var b = end;

			double3 ab = b - a;
			var abLen2 = math.lengthsq(ab);
			if (Unity.Burst.CompilerServices.Hint.Unlikely(abLen2 < 1))
				occluded[index] = false;
			else
			{
				bool occludedLocal = false;
				int interestIndex = pair.x * bodies.Length;
				for (int i = 0; i < bodies.Length; i++)
				{
					if (interestMap[interestIndex++] > 0)
					{
						double3 v = bodies[i].position;
						var radiusSq = bodies[i].radius * bodies[i].radius;
						double3 av = v - a;
						double3 bv = v - b;
						if (math.dot(av, ab) < 0)
							occludedLocal |= math.lengthsq(av) <= radiusSq;
						else if (math.dot(bv, ab) > 0)
							occludedLocal |= math.lengthsq(bv) <= radiusSq;
						else
							occludedLocal |= math.lengthsq(math.cross(ab, av)) <= radiusSq * abLen2;
					}
					occluded[index] = occludedLocal;
				}

				//occluded[index] = SubstepBody.Occluded(vessel.position, end, bodies);
			}
		}
	}

	[BurstCompile]
	public struct SunFluxAtBodyJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[WriteOnly] public NativeArray<double> flux;

		public void Execute(int index)
		{
			if (Unity.Burst.CompilerServices.Hint.Unlikely(index == 0))
				flux[index] = double.PositiveInfinity;
			else
			{
				var body = bodies[index];
				var sun = bodies[0];
				var d2 = math.distancesq(sun.position, body.position);
				flux[index] = sun.solarLuminosity / (4 * math.PI_DBL * d2);
				//var dist = math.distance(sun.position, body.position);
				//flux[index] = FluxAnalysisFactory.SolarFlux(sun.solarLuminosity, dist);
			}
		}
	}

	[BurstCompile]
	public struct SunFluxAtVesselJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[ReadOnly] public NativeArray<SubstepVessel> vessels;
		[ReadOnly] public NativeArray<bool> occluded;
		[WriteOnly] public NativeArray<double> flux;

		public void Execute(int index)
		{
			var vessel = vessels[index];
			var sun = bodies[0];
			var d2 = math.distancesq(sun.position, vessel.position);
			if (Unity.Burst.CompilerServices.Hint.Unlikely(occluded[index * bodies.Length]))
				flux[index] = 0;
			else
				flux[index] = sun.solarLuminosity / (4 * math.PI_DBL * d2);
			//var dist = math.distance(sun.position, vessel.position);
			//flux[index] = FluxAnalysisFactory.SolarFlux(sun.solarLuminosity, dist);
		}
	}

	// Divide by distance squared to target to compute actual irradiance
	[BurstCompile]
	public struct BodyHemisphericReradiatedIrradianceFactor : IJobParallelFor
	{
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[ReadOnly] public NativeArray<double> sunFluxAtBody;
		[ReadOnly] public NativeArray<bool> occluded;
		[WriteOnly] public NativeArray<double> factor;

		public void Execute(int index)
		{
			if (Unity.Burst.CompilerServices.Hint.Unlikely(index == 0 || occluded[index]))
				factor[index] = 0;
			else
			{
				var body = bodies[index];
				factor[index] = sunFluxAtBody[index] * body.radius * body.radius * 0.5 * body.albedo;
			}
		}
	}

	[BurstCompile]
	public struct AlbedoIrradianceAtVesselJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[ReadOnly] public NativeArray<SubstepVessel> vessels;
		[ReadOnly] public NativeArray<int2> pairs;
		[ReadOnly] public NativeArray<bool> vesselOccludedFromBody;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> hemisphericReradiatedIrradianceFactor;
		[WriteOnly] public NativeArray<double> irradiance;

		public void Execute(int index)
		{
			var pair = pairs[index];
			var vessel = vessels[pair.x];
			var body = bodies[pair.y];
			var notReallyTheSun = bodies[0];

			if (Unity.Burst.CompilerServices.Hint.Unlikely(math.distancesq(body.position, notReallyTheSun.position) < 1 || vesselOccludedFromBody[index]))
				irradiance[index] = 0;
			else
			{
				var distanceSq = math.distancesq(vessel.position, body.position);
				var rawIrradiance = hemisphericReradiatedIrradianceFactor[pair.y] / distanceSq;

				// ALDEBO COSINE FACTOR
				// the full albedo flux is received only when the vessel is positioned along the sun-body axis, and goes
				// down to zero on the night side.
				var bodyToSun = math.normalize(notReallyTheSun.position - body.position);
				var bodyToVessel = math.normalize(vessel.position - body.position);
				double angleFactor = (math.dot(bodyToSun, bodyToVessel) + 1) * 0.5;    // [-1,1] => [0,1]
				rawIrradiance *= FluxAnalysisFactory.GeometricAlbedoFactor(angleFactor);
				irradiance[index] = rawIrradiance;
			}
		}
	}

	[BurstCompile]
	public struct BodyEmissiveIrradianceJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[ReadOnly] public NativeArray<SubstepVessel> vessels;
		[ReadOnly] public NativeArray<int2> pairs;
		[ReadOnly] public NativeArray<double> sunFluxAtBodies;
		[WriteOnly] public NativeArray<double> irradiance;

		public void Execute(int index)
		{
			// THERMAL RE-EMISSION
			// We account for this even if the body is currently occluded from the sun
			// We use the same formula, excepted re-emitted power is spread over the full
			// body sphere, that is a solid angle of 4 * π steradians
			// The end formula becomes :
			// (sunFluxAtBody * r²) / (4 * (r + a)²)
			var pair = pairs[index];
			var vessel = vessels[pair.x];
			var body = bodies[pair.y];
			var d2 = math.distancesq(body.position, vessel.position);
			if (Unity.Burst.CompilerServices.Hint.Unlikely(d2 < double.Epsilon))
				irradiance[index] = 0;
			else
				irradiance[index] = sunFluxAtBodies[pair.y] * (1.0 - body.albedo) * body.radius * body.radius / (4.0 * d2);
		}
	}

	[BurstCompile]
	public struct SumForVessel : IJobParallelFor
	{
		[ReadOnly] public int count;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> input;
		[ReadOnly] public NativeArray<double> output;

		public void Execute(int index)
		{
			int inputIndex = index * count;
			double val = 0;
			for (int i=0; i < count; i++)
			{
				val += input[inputIndex++];
			}
			output[index] = val;
		}
	}
}
