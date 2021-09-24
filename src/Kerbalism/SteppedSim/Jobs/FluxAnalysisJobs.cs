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
		[ReadOnly] public FrameStats stats;
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<bool> occlusionRelevance;
		[WriteOnly] public NativeArray<bool> occluded;

		public void Execute(int index)
		{
			TimeBodyStarIndex i = timeBodyStarIndex[index];
			var body = bodies[i.directBody];
			var star = bodies[i.directStar];
			// Occlusion Relevance is a time-body-body unrolled array
			int occlusionRelevanceIndex = (i.time * (stats.numBodies + stats.numBodies)) + (i.origBody * stats.numBodies);
			int occluderIndex = i.time * stats.numBodies;    // Bodies is a time-body unrolled array
			bool occludedTemp = false;
			for (int ind = 0; ind < stats.numBodies; ind++)
			{
				var occluder = bodies[occluderIndex];
				if (Unity.Burst.CompilerServices.Hint.Unlikely(occlusionRelevance[occlusionRelevanceIndex] && ind != i.origStar))
					occludedTemp |= FluxAnalysisFactory.OcclusionTest(body.position, star.position, occluder.position, occluder.radius);
				occlusionRelevanceIndex++;
				occluderIndex++;
			}
			occluded[index] = occludedTemp;
		}
	}

	[BurstCompile]
	public struct VesselBodyOcclusionJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<TimeVesselBodyIndex> timeVesselBodyIndex;
		[ReadOnly] public FrameStats stats;
		[ReadOnly] public NativeArray<SubstepVessel> vessels;
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<bool> occlusionRelevance;
		[WriteOnly] public NativeArray<bool> occluded;

		public void Execute(int index)
		{
			TimeVesselBodyIndex i = timeVesselBodyIndex[index];
			var vessel = vessels[i.directVessel];
			var body = bodies[i.directBody];
			// Occlusion Relevance is a time-vessel-body unrolled array
			int occlusionRelevanceIndex = (i.time * (stats.numVessels + stats.numBodies)) + (i.origVessel * stats.numBodies);
			int occluderIndex = i.time * stats.numBodies;    // Bodies is a time-body unrolled array
			bool occludedTemp = false;
			for (int ind = 0; ind < stats.numBodies; ind++)
			{
				var occluder = bodies[occluderIndex];
				// TODO/FIXME : We should handle the case where a vessel has a negative altitude.
				// The current occlusion code will return "always occluded" if the vessel is inside the sphere of its main body.
				// Ideally, the vessel FoV shoud be reduced according to it's "depth", but a fallback where we consider it to be 
				// at an altitude of 0 is acceptable.
				if (Unity.Burst.CompilerServices.Hint.Unlikely(occlusionRelevance[occlusionRelevanceIndex] && ind != i.origBody))
					occludedTemp |= FluxAnalysisFactory.OcclusionTest(vessel.position, body.position, occluder.position, occluder.radius);
				occlusionRelevanceIndex++;
				occluderIndex++;
			}
			occluded[index] = occludedTemp;
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
				var bodyToSun = math.normalize(star.position - body.position);
				var bodyToVessel = math.normalize(vessel.position - body.position);
				double angleFactor = (math.dot(bodyToSun, bodyToVessel) + 1) * 0.5;    // [-1,1] => [0,1]
				int luminIndex = (tuple.directBody * stats.numStars) + tuple.origStar;
				luminosity[index] = isotropicAlbedoLuminosityPerStar[luminIndex] * FluxAnalysisFactory.GeometricAlbedoFactor(angleFactor);
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

	[BurstCompile]
	public struct SolarAtmosphericEffectsJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<TimeVesselBodyIndex> tuples;
		[ReadOnly] public FrameStats stats;
		[ReadOnly] public NativeArray<SubstepVessel> vessels;
		[ReadOnly] public NativeArray<SubstepBody> bodies;
		[ReadOnly] public double homeAtmDensityASL;
		[ReadOnly] public double solarInsolationAtHome;
		[WriteOnly] public NativeArray<double> fluxMultipliers;

		public void Execute(int index)
		{
			TimeVesselBodyIndex i = tuples[index];
			var vessel = vessels[i.directVessel];
			double density = vessel.atmosphericDensity;
			double solarFluxMultiplier = 1;
			if (Unity.Burst.CompilerServices.Hint.Unlikely(density > 0))
			{
				int directMainBody = i.time * stats.numBodies + vessel.mainBodyIndex;
				var mainBody = bodies[directMainBody];
				var body = bodies[i.directBody];
				if (Unity.Burst.CompilerServices.Hint.Unlikely(vessel.mainBodyIndex == i.origBody))
				{
					GetSolarAtmosphericEffects(1, density, body.radiusAtmoFactor, homeAtmDensityASL, solarInsolationAtHome, out _, out solarFluxMultiplier);
					// When looking at the main body, the atmosphere -below- the vessel is the important part.
					solarFluxMultiplier = 1 - solarFluxMultiplier;
				} else
				{
					double3 up = math.normalize(vessel.position - mainBody.position);
					double3 source_dir = math.normalize(body.position - vessel.position);
					GetSolarAtmosphericEffects(math.dot(up, source_dir), density, body.radiusAtmoFactor, homeAtmDensityASL, solarInsolationAtHome, out _, out solarFluxMultiplier);
				}
			}
			fluxMultipliers[index] = solarFluxMultiplier;
		}

		// Re-implementation of stock body.GetSolarAtmosphericEffects(Vector3d.Dot(up, source_dir), density, out _, out double stockFluxFactor);
		private  void GetSolarAtmosphericEffects(
		  double sunDot,
		  double density,
		  double radiusAtmoFactor,
		  double homeAtmDensityASL,
		  double solarInsolationAtHome,
		  out double solarAirMass,
		  out double solarFluxMultiplier)
		{
			// When sunDot == 1, num = radiusAtmoFactor.  Sqrt parameter becomes (x^2 + 2x + 1) = (x+1)^2 where x = radiusAtmoFactor
			// so compute result = 1.  When sunDot <= 0, parameter is 2x+1, result ~= 1.4 * sqrt(radiusAtmoFactor)
			double num = math.max(radiusAtmoFactor * sunDot, 0);
			solarAirMass = math.sqrt(num * num + 2.0 * radiusAtmoFactor + 1.0) - num;
			solarFluxMultiplier = GetSolarPowerFactor(density * solarAirMass, homeAtmDensityASL, solarInsolationAtHome);
		}

		private double GetSolarPowerFactor(double density, double homeAtmDensityASL, double solarInsolationAtHome)
		{
			double num2 = (1.0 - solarInsolationAtHome) * homeAtmDensityASL;
			return num2 / (num2 + density * solarInsolationAtHome);
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
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<double> atmosphericEffects;

		[WriteOnly] public NativeArray<VesselBodyIrradiance> irradiance;

		public void Execute(int index)
		{
			TimeVesselBodyIndex i = tuples[index];
			var vessel = vessels[i.directVessel];
			var body = bodies[i.directBody];
			var distSq = math.distancesq(vessel.position, body.position);
			bool valid = distSq > 0;
			bool unoccluded = !vesselOccludedFromBody[index];
			double areaRecipNoOcclusion = Unity.Burst.CompilerServices.Hint.Likely(valid) ? 1.0 / (4.0 * math.PI_DBL * distSq) : 0;
			double areaRecip = Unity.Burst.CompilerServices.Hint.Likely(unoccluded && valid) ? areaRecipNoOcclusion : 0;
			float visibility = Unity.Burst.CompilerServices.Hint.Likely(unoccluded && valid) ? 1 : 0;
			double atmosphereEffect = atmosphericEffects[index];
			irradiance[index] = new VesselBodyIrradiance
			{
				visibility = visibility,
				solar = body.solarLuminosity * atmosphereEffect * areaRecip,
				solarRaw = body.solarLuminosity * areaRecipNoOcclusion,
				core = body.bodyCoreThermalFlux * atmosphereEffect * areaRecip,
				emissive = bodyEmissiveLuminosity[i.directBody] * atmosphereEffect * areaRecip,
				albedo = bodyAlbedoLuminosity[index] * atmosphereEffect * areaRecip,
			};
		}
	}

	[BurstCompile]
	public struct SumVesselBodyIrradiancesJob : IJobParallelFor
	{
		[ReadOnly] public FrameStats stats;
		[ReadOnly] public NativeArray<float> weights;
		[ReadOnly] public NativeArray<VesselBodyIrradiance> irradiances;
		[WriteOnly] public NativeArray<VesselBodyIrradiance> output;

		// for each vessel,body: sum its irradiances over all times.
		// index = vessel,body.  This isn't unrolled per vessel,body so do it manually.
		public void Execute(int index)
		{
			int vesselIndex = index / stats.numBodies;
			int bodyIndex = index - (vesselIndex * stats.numBodies);
			int frameSize = stats.numBodies * stats.numVessels;
			VesselBodyIrradiance result = default;
			for (int frameIndex = 0; frameIndex < weights.Length; frameIndex++)
			{
				VesselBodyIrradiance vbi = irradiances[frameIndex * frameSize + (vesselIndex * stats.numBodies) + bodyIndex];
				float weight = weights[frameIndex];
				result.albedo += vbi.albedo * weight;
				result.emissive += vbi.emissive * weight;
				result.core += vbi.core * weight;
				result.solar += vbi.solar * weight;
				result.solarRaw += vbi.solarRaw * weight;
				result.visibility += vbi.visibility * weight;
			}
			output[index] = result;
		}
	}
}
