using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace KERBALISM.SteppedSim.Jobs
{
	[BurstCompile]
	public struct BuildTripletsJob : IJob
	{
		[ReadOnly] public int numSteps;
		[ReadOnly] public int numBodies;
		[ReadOnly] public int numVessels;
		[WriteOnly] public NativeArray<int3> triplets;

		public BuildTripletsJob(int numSteps, int numBodies, int numVessels) : this()
		{
			this.numSteps = numSteps;
			this.numBodies = numBodies;
			this.numVessels = numVessels;
		}

		public void Execute()
		{
			int index = 0;
			for (int step=0; step < numSteps; step++)
				for (int vessel = 0; vessel < numVessels; vessel++)
					for (int body = 0; body < numBodies; body++)
						triplets[index++] = new int3(step, vessel + (step * numVessels), body + (step * numBodies));
		}
	}

	[BurstCompile]
	public struct FluxFacts : IJobParallelFor
	{
		[ReadOnly] public NativeArray<int3> triplets;
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[ReadOnly] public NativeArray<SubstepVessel> vessels;
//		const double minRequiredHalfAngleRadians = 0.002909 / 2;    // 10 arcminutes ~= 0.003 radians.  sun/moon ~30 arcmin, Venus ~1 arcmin max
		[ReadOnly] public double minRequiredHalfAngleRadians;
		[WriteOnly] public NativeArray<double> distance;
		[WriteOnly] public NativeArray<double3> direction;
		[WriteOnly] public NativeArray<bool> occlusionRelevance;

		public void Execute(int index)
		{
			var vessel = vessels[triplets[index].y];
			var body = bodies[triplets[index].z];
			var toBody = body.position - vessel.position;
			var dist = math.length(toBody);
			distance[index] = dist;
			direction[index] = toBody / dist;

			// Take advantage of fact that sin(x) == x, cos(x) == 1, tan(x) == x for small x
			// Simplify atan(x/y) > min ==> (x/y) > min ==> x > min * y
			occlusionRelevance[index] = body.radius > minRequiredHalfAngleRadians * dist;
		}
	}

	[BurstCompile]
	public struct GatherStarsJob : IJob
	{
		[ReadOnly] public int numBodies;
		[ReadOnly] public NativeSlice<SubstepBody> bodySlice;
		[WriteOnly] public NativeList<int> stars;

		public void Execute()
		{
			for (int i=0; i<numBodies; i++)
				if (bodySlice[i].solarLuminosity > 0)
					stars.Add(i);
		}
	}

	[BurstCompile]
	public struct BodySunOcclusionJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<int3> triplets;
		[ReadOnly] public int bodiesPerStep;
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[ReadOnly] public NativeArray<int> starIndexes;
		[WriteOnly] public NativeArray<bool> occluded;

		public void Execute(int index)
		{
			var body = bodies[index];
			if (Unity.Burst.CompilerServices.Hint.Unlikely(body.solarLuminosity > 0))
				occluded[index] = false;
			else
			{
				int step = triplets[index].x;
				int firstBodyIndex = step * bodiesPerStep;
				var frameSlice = bodies.Slice(firstBodyIndex, bodiesPerStep);
				bool occludedTemp = true;
				for (int i = 0; i < starIndexes.Length && !occludedTemp; i++)
				{
					var star = bodies[firstBodyIndex + starIndexes[i]];
					// avoid self-occluding, so adjust the body and sun positions...
					var dir = math.normalize(star.position - body.position);
					var start = body.position + (body.radius + 10) * dir;
					var end = star.position - (star.radius + 10) * dir;
					occludedTemp &= SubstepBody.Occluded(start, end, frameSlice);
				}
				occluded[index] = occludedTemp;
			}
		}
	}

	[BurstCompile]
	public struct BodyVesselOcclusionJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<SubstepVessel> vessels;
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[ReadOnly] public int numBodiesPerStep;
		[ReadOnly] public NativeArray<int3> triplets;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<bool> occlusionRelevance;
		[WriteOnly] public NativeArray<bool> occluded;

		public void Execute(int index)
		{
			var triplet = triplets[index];
			var step = triplet.x;
			var vessel = vessels[triplet.y];
			var body = bodies[triplet.z];
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
				int firstBodyInStepIndex = step * numBodiesPerStep;
				for (int testBodyIndex = firstBodyInStepIndex;
					testBodyIndex < firstBodyInStepIndex + numBodiesPerStep;
					testBodyIndex++)
				{
					if (occlusionRelevance[testBodyIndex])
					{
						var testBody = bodies[testBodyIndex];
						double3 v = testBody.position;
						var radiusSq = testBody.radius * testBody.radius;
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
		[ReadOnly] public NativeArray<int3> triplets;
		[ReadOnly] public int bodiesPerStep;
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[ReadOnly] public NativeArray<int> starIndexes;
		[WriteOnly] public NativeArray<double> flux;

		public void Execute(int index)
		{
			var body = bodies[index];
			double tempFlux = 0;
			if (Unity.Burst.CompilerServices.Hint.Unlikely(body.solarLuminosity > 0))
				tempFlux = double.PositiveInfinity;
			else
			{
				int step = triplets[index].x;
				int firstBodyIndex = step * bodiesPerStep;
				for (int i=0; i<starIndexes.Length; i++)
				{
					var star = bodies[firstBodyIndex + starIndexes[i]];
					var d2 = math.distancesq(star.position, body.position);
					tempFlux += star.solarLuminosity / (4 * math.PI_DBL * d2);
					//var dist = math.distance(sun.position, body.position);
					//flux[index] = FluxAnalysisFactory.SolarFlux(sun.solarLuminosity, dist);
				}
			}
			flux[index] = tempFlux;
		}
	}

	[BurstCompile]
	public struct BodyEmissiveFlux : IJobParallelFor
	{
		[ReadOnly] public NativeArray<int3> triplets;
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[ReadOnly] public NativeArray<double> sunFluxAtBodies;
		[WriteOnly] public NativeArray<double> flux;

		public void Execute(int index)
		{
			// THERMAL RE-EMISSION: total non-reflected flux abosorbed by the body from the sun
			var triplet = triplets[index];
			var body = bodies[triplet.z];
			flux[index] = sunFluxAtBodies[triplet.z] * (1.0 - body.albedo) * math.PI_DBL * body.radius * body.radius;
		}
	}

	/// <summary>
	/// Compute irradiances from:
	///     Stars (body.solarIrradiance)
	///     Internal body processes (body.bodyCoreThermalFlux)
	///     Re-emitted absorption from star fluxes (bodyEmissiveFlux inpt)
	/// TODO: Why these are two separate computations?  To simplify that only body.solarIrradiance from stars gets albedo / reflection effects?
	/// Some bodies emit an internal thermal flux due to various tidal, geothermal or accretional phenomenons
	/// This is given by CelestialBody.coreTemperatureOffset
	/// From that value we derive thermal flux in W/m² using the blackbody equation
	/// We assume that the atmosphere has no effect on that value.
	/// </summary>
	/// <returns>Flux in W/m2 at distance</returns>
	[BurstCompile]
	public struct BodyDirectIrradiances : IJobParallelFor
	{
		[ReadOnly] public NativeArray<int3> triplets;
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[ReadOnly] public NativeArray<double> distances;
		[ReadOnly] public NativeArray<bool> occluded;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> bodyEmissiveFlux;
		[WriteOnly] public NativeArray<double> solarIrradiance;
		[WriteOnly] public NativeArray<double> bodyCoreIrradiance;
		[WriteOnly] public NativeArray<double> bodyEmissiveIrradiance;

		public void Execute(int index)
		{
			var triplet = triplets[index];
			var body = bodies[triplet.z];
			var d2 = distances[index] * distances[index];
			bool valid = occluded[index] == false && d2 > 0;
			var denomRecip = Unity.Burst.CompilerServices.Hint.Likely(valid) ? 1 / (4 * math.PI_DBL * d2) : 0;
			solarIrradiance[index] = body.solarLuminosity * denomRecip;
			bodyCoreIrradiance[index] = body.bodyCoreThermamFlux * denomRecip;
			bodyEmissiveIrradiance[index] = bodyEmissiveIrradiance[index] * denomRecip;
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
			var body = bodies[index];
			factor[index] = Unity.Burst.CompilerServices.Hint.Unlikely(occluded[index]) ?
							0 : sunFluxAtBody[index] * body.radius * body.radius * 0.5 * body.albedo;
		}
	}

	[BurstCompile]
	public struct AlbedoIrradianceAtVesselJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<int3> triplets;
		[ReadOnly] public int numBodiesPerStep;
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[ReadOnly] public NativeArray<SubstepVessel> vessels;
		[ReadOnly] public NativeArray<int> starIndexes;
		[ReadOnly] public NativeArray<bool> vesselOccludedFromBody;
		[ReadOnly] public NativeArray<double> distances;
		[ReadOnly] public NativeArray<double3> directions;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> hemisphericReradiatedIrradianceFactor;
		[WriteOnly] public NativeArray<double> irradiance;

		public void Execute(int index)
		{
			var triplet = triplets[index];
			var step = triplet.x;
			var vessel = vessels[triplet.y];
			var body = bodies[triplet.z];
			int firstStarIndex = step * numBodiesPerStep + starIndexes[0];
			var firstStar = bodies[firstStarIndex];

			if (Unity.Burst.CompilerServices.Hint.Unlikely(index == firstStarIndex || vesselOccludedFromBody[index]))
				irradiance[index] = 0;
			else
			{
				var distanceSq = distances[index] * distances[index];
				var rawIrradiance = hemisphericReradiatedIrradianceFactor[triplet.z] / distanceSq;

				// ALDEBO COSINE FACTOR
				// the full albedo flux is received only when the vessel is positioned along the sun-body axis, and goes
				// down to zero on the night side.
				var bodyToSun = math.normalize(firstStar.position - body.position);
				var bodyToVessel = directions[index];
				double angleFactor = (math.dot(bodyToSun, bodyToVessel) + 1) * 0.5;    // [-1,1] => [0,1]
				rawIrradiance *= FluxAnalysisFactory.GeometricAlbedoFactor(angleFactor);
				irradiance[index] = rawIrradiance;
			}
		}
	}

	[BurstCompile]
	public struct BodyEmissiveIrradianceJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<int3> triplets;
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[ReadOnly] public NativeArray<double> distances;
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
			var triplet = triplets[index];
			var body = bodies[triplet.z];
			var d2 = distances[index] * distances[index];
			if (Unity.Burst.CompilerServices.Hint.Unlikely(d2 < double.Epsilon))
				irradiance[index] = 0;
			else
				irradiance[index] = sunFluxAtBodies[triplet.z] * (1.0 - body.albedo) * body.radius * body.radius / (4.0 * d2);
		}
	}

	[BurstCompile]
	public struct SumForVessel : IJobParallelFor
	{
		[ReadOnly] public int numBodiesPerStep;
		[ReadOnly] public int numVesselsPerStep;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> input;
		[WriteOnly] public NativeArray<double> output;

		public void Execute(int index)
		{
			int step = index / numVesselsPerStep;
			int vesselNum = index % numVesselsPerStep;
			int startIndex = step * (numVesselsPerStep + numBodiesPerStep) + (vesselNum * numBodiesPerStep);
			double val = 0;
			for (int i = startIndex; i < startIndex + numBodiesPerStep; i++)
			{
				val += input[i];
			}
			output[index] = val;
		}
	}

	[BurstCompile]
	public struct RecordIrradiances : IJobParallelFor
	{
		public NativeArray<SubstepVessel> vessels;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> directIrradiance;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> albedoIrradiance;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> emissiveIrradiance;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> coreIrradiance;

		public void Execute(int index)
		{
			var v = vessels[index];
			v.directIrradiance = directIrradiance[index];
			v.bodyAlbedoIrradiance = albedoIrradiance[index];
			v.bodyEmissiveIrradiance = emissiveIrradiance[index];
			v.bodyCoreIrradiance = coreIrradiance[index];
			vessels[index] = v;
		}
	}
}
