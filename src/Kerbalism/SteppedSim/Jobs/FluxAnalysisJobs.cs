using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace KERBALISM.SteppedSim.Jobs
{
	[BurstCompile]
	public struct BuildTimeBodyStarsIndexJob : IJob
	{
		[ReadOnly] public FrameStats stats;
		[ReadOnly] public NativeArray<int> starsIndex;
		[WriteOnly] public NativeArray<TimeBodyStarIndex> triplets;

		public void Execute()
		{
			int index = 0;
			for (int step = 0; step < stats.numSteps; step++)
				for (int body = 0; body < stats.numBodies; body++)
					for (int star = 0; star < stats.numStars; star++)
						triplets[index++] = new TimeBodyStarIndex
						{
							time = step,
							origBody = body,
							origStar = star,
							directBody = body + (step * stats.numBodies),
							directStar = starsIndex[star] + (step * stats.numBodies),
						};
		}
	}

	[BurstCompile]
	public struct BuildTimeBodyOccludersIndexJob : IJob
	{
		[ReadOnly] public FrameStats stats;
		[WriteOnly] public NativeArray<TimeBodyOccluderIndex> triplets;

		public void Execute()
		{
			int index = 0;
			for (int step = 0; step < stats.numSteps; step++)
				for (int body = 0; body < stats.numBodies; body++)
					for (int occluder = 0; occluder < stats.numBodies; occluder++)
						triplets[index++] = new TimeBodyOccluderIndex
						{
							time = step,
							origBody = body,
							origOccluder = occluder,
							directBody = body + (step * stats.numBodies),
							directOccluder = occluder + (step * stats.numBodies),
						};
		}
	}

	[BurstCompile]
	public struct BuildTimeVesselBodyIndicesJob : IJob
	{
		[ReadOnly] public FrameStats stats;
		[WriteOnly] public NativeArray<TimeVesselBodyIndex> triplets;

		public void Execute()
		{
			int index = 0;
			for (int step = 0; step < stats.numSteps; step++)
				for (int vessel = 0; vessel < stats.numVessels; vessel++)
					for (int body = 0; body < stats.numBodies; body++)
						triplets[index++] = new TimeVesselBodyIndex()
						{
							time = step,
							origVessel = vessel,
							origBody = body,
							directVessel = vessel + (step * stats.numVessels),
							directBody = body + (step * stats.numBodies)
						};
		}
	}

	[BurstCompile]
	public struct BuildTimeVesselBodyStarIndicesJob : IJob
	{
		[ReadOnly] public FrameStats stats;
		[ReadOnly] public NativeArray<int> starsIndex;
		[WriteOnly] public NativeArray<TimeVesselBodyStarIndex> tuple;

		public void Execute()
		{
			int index = 0;
			for (int step = 0; step < stats.numSteps; step++)
				for (int vessel = 0; vessel < stats.numVessels; vessel++)
					for (int body = 0; body < stats.numBodies; body++)
						for (int star = 0; star < stats.numStars; star++)
							tuple[index++] = new TimeVesselBodyStarIndex()
							{
								time = step,
								origVessel = vessel,
								origBody = body,
								origStar = star,
								directVessel = vessel + (step * stats.numVessels),
								directBody = body + (step * stats.numBodies),
								directStar = starsIndex[star] + (step * stats.numBodies),
							};
		}
	}

	[BurstCompile]
	public struct BodyBodyOcclusionRelevanceJob : IJobParallelFor
	{
		[ReadOnly] public double minRequiredHalfAngleRadians;
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[ReadOnly] public NativeArray<TimeBodyOccluderIndex> indices;
		[WriteOnly] public NativeArray<bool> relevance;

		public void Execute(int index)
		{
			TimeBodyOccluderIndex i = indices[index];
			var body = bodies[i.directBody];
			var occluder = bodies[i.directOccluder];
			double dist = math.distance(body.position, occluder.position);
			// Take advantage of fact that sin(x) == x, cos(x) == 1, tan(x) == x for small x
			// Simplify atan(x/y) > min ==> (x/y) > min ==> x > min * y
			relevance[index] = i.directBody != i.directOccluder && occluder.radius > minRequiredHalfAngleRadians * dist;
		}
	}

	[BurstCompile]
	public struct VesselBodyOcclusionRelevanceJob : IJobParallelFor
	{
		[ReadOnly] public double minRequiredHalfAngleRadians;
		[ReadOnly] public NativeArray<SubstepVessel> vessels;
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[ReadOnly] public NativeArray<TimeVesselBodyIndex> indices;
		[WriteOnly] public NativeArray<bool> relevance;

		public void Execute(int index)
		{
			TimeVesselBodyIndex i = indices[index];
			var vessel = vessels[i.directVessel];
			var occluder = bodies[i.directBody];
			double dist = math.distance(vessel.position, occluder.position);
			// Take advantage of fact that sin(x) == x, cos(x) == 1, tan(x) == x for small x
			// Simplify atan(x/y) > min ==> (x/y) > min ==> x > min * y
			relevance[index] = occluder.radius > minRequiredHalfAngleRadians * dist;
		}
	}


	[BurstCompile]
	public struct BodyStarOcclusionJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<TimeBodyStarIndex> timeBodyStarIndex;
		[ReadOnly] public int numBodiesPerStep;
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<bool> occlusionRelevance;
		[WriteOnly] public NativeArray<bool> occluded;

		public void Execute(int index)
		{
			TimeBodyStarIndex i = timeBodyStarIndex[index];
			var body = bodies[i.directBody];
			var star = bodies[i.directStar];
			int occluderIndex = i.time * numBodiesPerStep;
			bool occludedTemp = false;
			for (int ind = 0; ind < numBodiesPerStep && !occludedTemp; ind++)
			{
				var occluder = bodies[occluderIndex];
				if (Unity.Burst.CompilerServices.Hint.Unlikely(occlusionRelevance[occluderIndex] && occluderIndex != i.directStar))
					occludedTemp |= FluxAnalysisFactory.OcclusionTest(body.position, star.position, occluder.position, occluder.radius);
				occluderIndex++;
			}
			occluded[index] = occludedTemp;
		}
	}

	[BurstCompile]
	public struct VesselBodyOcclusionJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<TimeVesselBodyIndex> timeVesselBodyIndex;
		[ReadOnly] public int numBodiesPerStep;
		[ReadOnly] public NativeArray<SubstepVessel> vessels;
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<bool> occlusionRelevance;
		[WriteOnly] public NativeArray<bool> occluded;

		public void Execute(int index)
		{
			TimeVesselBodyIndex i = timeVesselBodyIndex[index];
			var vessel = vessels[i.directVessel];
			var body = bodies[i.directBody];
			int occluderIndex = i.time * numBodiesPerStep;
			bool occludedTemp = false;
			for (int ind = 0; ind < numBodiesPerStep && !occludedTemp; ind++)
			{
				var occluder = bodies[occluderIndex];
				if (Unity.Burst.CompilerServices.Hint.Unlikely(occlusionRelevance[occluderIndex] && occluderIndex != i.directBody))
					//occludedTemp |= FluxAnalysisFactory.OcclusionTest(vessel.position, body.position, occluder.position, occluder.radius);
					occludedTemp |= LocalOcclusionTest(vessel.position, body.position, occluder.position, occluder.radius);
				occluderIndex++;
			}
			occluded[index] = occludedTemp;
		}
		private bool LocalOcclusionTest(double3 a, double3 b, double3 v, double dist)
		{
			double3 ab = b - a;
			var abLenSq = math.lengthsq(ab);
			if (Unity.Burst.CompilerServices.Hint.Likely(abLenSq > 1))
			{
				var distSq = dist * dist;
				double3 av = v - a;
				double3 bv = v - b;
				if (math.dot(av, ab) < 0)
					return math.lengthsq(av) <= distSq;
				else if (math.dot(bv, ab) > 0)
					return math.lengthsq(bv) <= distSq;
				else
					return math.lengthsq(math.cross(ab, av)) <= distSq * abLenSq;
			}
			return false;
		}
	}

	[BurstCompile]
	public struct BodySolarIncidentFluxJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<TimeBodyStarIndex> triplets;
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[WriteOnly] public NativeArray<double> bodyIncidentFlux;

		public void Execute(int index)
		{
			var i = triplets[index];
			var body = bodies[i.directBody];
			var star = bodies[i.directStar];
			// Can optimize away the two math.PI_DBL mults...
			double irradiance = Unity.Burst.CompilerServices.Hint.Likely(i.directBody != i.directStar) ?
				star.solarLuminosity / (4 * math.PI_DBL * math.distancesq(star.position, body.position)) : 0;
			bodyIncidentFlux[index] = irradiance * math.PI_DBL * body.radius * body.radius;
		}
	}

	[BurstCompile]
	public struct BodyEmissiveLuminositiesJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<TimeBodyStarIndex> timeBodyStarIndex;
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[ReadOnly] public NativeArray<bool> bodyStarOcclusion;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> bodyIncidentFlux;
		[WriteOnly] public NativeArray<double> emissiveLuminosity;
		[WriteOnly] public NativeArray<double> isotropicAlbedoLuminosity;

		public void Execute(int index)
		{
			// THERMAL RE-EMISSION: total non-reflected flux abosorbed by the body from a star
			// Long-term process, so account even if the body is currently occluded from the star
			// ALBEDO Isotropic Reflection: Total reflected flux by the body from a star
			// Short-term process, accounts for occlusion.
			// Geometric effects (hemispherical reflection, vessel only sees partial area) will be applied later.
			TimeBodyStarIndex i = timeBodyStarIndex[index];
			var body = bodies[i.directBody];
			double incidentFlux = bodyIncidentFlux[index];
			double isoAlbedoLumin = Unity.Burst.CompilerServices.Hint.Likely(!bodyStarOcclusion[index]) ? incidentFlux * body.albedo : 0;
			emissiveLuminosity[index] = incidentFlux * (1.0 - body.albedo);
			isotropicAlbedoLuminosity[index] = isoAlbedoLumin;
		}
	}

	[BurstCompile]
	public struct AlbedoLuminosityForVesselJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<TimeVesselBodyStarIndex> tuples;
		[ReadOnly] public FrameStats stats;
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[ReadOnly] public NativeArray<SubstepVessel> vessels;
		[ReadOnly] public NativeArray<bool> vesselOccludedFromBody;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> isotropicAlbedoLuminosityPerStar;
		[WriteOnly] public NativeArray<double> luminosity;

		public void Execute(int index)
		{
			TimeVesselBodyStarIndex tuple = tuples[index];
			var vessel = vessels[tuple.directVessel];
			var body = bodies[tuple.directBody];
			var star = bodies[tuple.directStar];
			int occlusionIndex = tuple.directVessel * stats.numBodies + tuple.origBody;
			// Vessel-body occlusion stored in time * numVessel * numBody array, at (time*numVessel*numBody + body) = (directVessel*numBody + body)
			if (Unity.Burst.CompilerServices.Hint.Unlikely(vesselOccludedFromBody[occlusionIndex] || body.solarLuminosity > 0))
				luminosity[index] = 0;
			else
			{
				// ALDEBO COSINE FACTOR
				// the full albedo flux is received only when the vessel is positioned along the sun-body axis, and goes
				// down to zero on the night side.
				// Since the total flux is the same, but is re-emitted only over 1 hemisphere, the effective luminosity in a direction is *2 * the geometric albedo factor
				var bodyToSun = math.normalize(star.position - body.position);
				var bodyToVessel = math.normalize(vessel.position - body.position);
				double angleFactor = (math.dot(bodyToSun, bodyToVessel) + 1) * 0.5;    // [-1,1] => [0,1]
				int luminIndex = (tuple.directBody * stats.numStars) + tuple.origStar;
				luminosity[index] = isotropicAlbedoLuminosityPerStar[luminIndex] * 2 * FluxAnalysisFactory.GeometricAlbedoFactor(angleFactor);
			}
		}
	}

	[BurstCompile]
	// Sum into the destination array from an unrolled source array (unrolled by # of stars per source entry)
	// ie TimeBodyStar -> TimeBody, or TimeVesselBodyStar->TimeVesselBody
	public struct CombinePerStarLuminosity : IJobParallelFor
	{
		[ReadOnly] public FrameStats stats;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> perStarLuminosity;
		[WriteOnly] public NativeArray<double> combinedLuminosity;

		public void Execute(int index)
		{
			double total = 0;
			int sourceIndex = index * stats.numStars;
			for (int star = 0; star < stats.numStars; star++)
				total += perStarLuminosity[sourceIndex + star];
			combinedLuminosity[index] = total;
		}
	}

	/// <summary>
	/// Compute final matrix of irradiances per (vessel, body):
	///     Stars (body.solarIrradiance)
	///     Internal body processes (body.bodyCoreThermalFlux)
	///     Re-emitted absorption from star fluxes (bodyEmissiveFlux input)
	///     Albedo from star fluxes
	/// Some bodies emit an internal thermal flux due to various tidal, geothermal or accretional phenomenons, given by CelestialBody.coreTemperatureOffset
	/// From that value we derive thermal flux in W/m² using the blackbody equation
	/// We assume that the atmosphere has no effect on that value
	/// </summary>
	/// <returns>Flux in W/m2 at distance</returns>
	[BurstCompile]
	public struct BodyIrradiancesJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<TimeVesselBodyIndex> tuples;
		[ReadOnly] public NativeArray<SubstepVessel> vessels;
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[ReadOnly] public NativeArray<bool> vesselOccludedFromBody;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> bodyEmissiveLuminosity;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> bodyAlbedoLuminosity;
		[WriteOnly] public NativeArray<VesselBodyIrradiance> irradiance;

		// TODO : ATMOSPHERIC FACTOR
		public void Execute(int index)
		{
			TimeVesselBodyIndex i = tuples[index];
			var vessel = vessels[i.directVessel];
			var body = bodies[i.directBody];
			var distSq = math.distancesq(vessel.position, body.position);
			bool valid = vesselOccludedFromBody[index] == false && distSq > 0;
			double denomRecipNoOcclusion = 1.0 / (4.0 * math.PI_DBL * distSq);
			double denomRecipOcclusion = Unity.Burst.CompilerServices.Hint.Likely(valid) ? denomRecipNoOcclusion : 0;
			irradiance[index] = new VesselBodyIrradiance
			{
				visibility = valid,
				solar = body.solarLuminosity * denomRecipOcclusion,
				solarRaw = body.solarLuminosity * denomRecipNoOcclusion,
				core = body.bodyCoreThermalFlux * denomRecipOcclusion,
				emissive = bodyEmissiveLuminosity[i.directBody] * denomRecipOcclusion,
				albedo = bodyAlbedoLuminosity[index] * denomRecipOcclusion,
			};
		}
	}
}
