using KERBALISM.SteppedSim.Jobs;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Profiling;

namespace KERBALISM.SteppedSim
{
	public class FluxAnalysisFactory
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="dependencyHandle">Pre-requisite job that computes the body and vessel positions</param>
		/// <param name="timesteps">Timesteps to compute</param>
		/// <param name="bodies">1-m array of SubstepBody, for each timestep</param>
		/// <param name="vessels">1-n array of SubstepVessel, for each timestep</param>
		/// <param name="outputJob">Output JobHandle for follow-on processing to depend upon or Complete()</param>
		/// <param name="vesselBodyOcclusionMap">Unrolled array of body occlusion from perspective of vessel.  For each timestep, for each vessel, for each body, is it occluded?</param>
		/*
		 * Theory of operation, or What Does This Do?
		 * Called as:
			FluxAnalysisFactory.Process(ref computeWorldPosJob,
				timestepsSource,
				bodyDataGlobalArray,
				vesselDataGlobalArray,
				out JobHandle fluxJob,
				out NativeArray<bool> vesselBodyOcclusionMap);
		 *
		 * Create and launch the processing chain to compute the irradiance triplet: (timestep, vessel, source body)
		 * 
		 * BuildTripletsJob: Unroll the timesteps, bodies, and vessels lists into triplets of <timestep, vessel, body> indices for parallel computation
		 * GatherStarsJob: Collect all the stars (body.solarLuminosity > 0)
		 * FluxFacts: Compute baseline facts for every triplet: body distance and direction from vessel, and body relevance for occlusion
		 * SolarIrradianceAtBodyJob: Compute the solar irradiance at each body for each timestep
		 * BodySunOcclusionJob: Compute the body's occlusion from (all stars collectively) for each timestep
		 * BodyVesselOcclusionJob: Compute each vessel's occlusion from each body for each timestep
		 * BodyIncidentFluxJob: Compute the total flux into each body for each timestep
		 * BodyEmissiveLuminositiesJob: Compute the (thermal re-emission & median albedo) luminosities from each body given BodyIncidentFluxJob for each timestep
		 * AlbedoLuminosityForVesselJob: Skew the median albedo by the direction factor for each vessel, body, mainstar orientation for each timestep
		 * BodyIrradiancesJob: Compute the (occlusion-accounted) direct (solarLuminosity), body-core, body-emissive and albedo irradiance at each vessel from each body at each timestep
		 * Various summing jobs to consolidate the per-(vessel,body) data to just per-vessel
		 * 
		 * Likely bug: BodySunOcclusion is true only if all stars are occluded.  But SolarIrradiance doesn't check per-star for occlusion
		 */
		public static void Process(ref JobHandle dependencyHandle,
			in NativeArray<double> timesteps,
			in NativeArray<SubstepBody> bodies,
			in NativeArray<SubstepVessel> vessels,
			out JobHandle outputJob,
			out NativeArray<bool> vesselBodyOcclusionMap)
		{
			int numSteps = timesteps.Length;
			int numBodies = bodies.Length / numSteps;
			int numVessels = vessels.Length / numSteps;
			int fullSize = numSteps * numBodies * numVessels;

			var triplets = new NativeArray<int3>(fullSize, Allocator.TempJob);
			var buildTripletsJob = new BuildTripletsJob(numSteps, numBodies, numVessels)
			{
				triplets = triplets,
			}.Schedule(dependencyHandle);

			var starIndex = new NativeList<int>(numBodies, Allocator.TempJob);
			var gatherStarsJob = new GatherStarsJob
			{
				numBodies = numBodies,
				bodySlice = bodies.Slice(0, numBodies),
				stars = starIndex,
			}.Schedule(dependencyHandle);

			var occlusionRelevance = new NativeArray<bool>(fullSize, Allocator.TempJob);
			var vesselBodyDistance = new NativeArray<double>(fullSize, Allocator.TempJob);
			var vesselBodyDirection = new NativeArray<double3>(fullSize, Allocator.TempJob);
			var fluxFactsJob = new FluxFacts
			{
				// if apparent diameter < ~10 arcmin (~0.003 radians), don't consider the body for occlusion checks
				// real apparent diameters at earth : sun/moon ~ 30 arcmin, Venus ~ 1 arcmin max
				minRequiredHalfAngleRadians = 0.002909 / 2,     // ~10 arcmin = 0.003 radians
				bodies = bodies,
				vessels = vessels,
				triplets = triplets,
				distance = vesselBodyDistance,
				direction = vesselBodyDirection,
				occlusionRelevance = occlusionRelevance,
			}.Schedule(fullSize, 256, buildTripletsJob);

			var solarIrradianceAtBody = new NativeArray<double>(numSteps * numBodies, Allocator.TempJob);
			var solarIrradianceAtBodyJob = new SolarIrradianceAtBodyJob
			{
				triplets = triplets,
				bodies = bodies,
				bodiesPerStep = numBodies,
				starIndexes = starIndex.AsDeferredJobArray(),
				flux = solarIrradianceAtBody,
			}.Schedule(numSteps * numBodies, 64, JobHandle.CombineDependencies(buildTripletsJob, gatherStarsJob));

			var bodyOccludedFromSun = new NativeArray<bool>(numSteps * numBodies, Allocator.TempJob);
			var bodySunOcclusionJob = new BodySunOcclusionJob
			{
				triplets = triplets,
				bodies = bodies,
				bodiesPerStep = numBodies,
				starIndexes = starIndex.AsDeferredJobArray(),
				occluded = bodyOccludedFromSun,
			}.Schedule(numSteps * numBodies, 16, JobHandle.CombineDependencies(buildTripletsJob, gatherStarsJob));

			vesselBodyOcclusionMap  = new NativeArray<bool>(fullSize, Allocator.TempJob);
			var vesselBodyOcclusionJob = new BodyVesselOcclusionJob
			{
				triplets = triplets,
				numBodiesPerStep = numBodies,
				bodies = bodies,
				vessels = vessels,
				occlusionRelevance = occlusionRelevance,
				occluded = vesselBodyOcclusionMap,
			}.Schedule(fullSize, 16, fluxFactsJob);

			var bodyIncidentFlux = new NativeArray<double>(fullSize, Allocator.TempJob);
			var bodyIncidentFluxJob = new BodyIncidentFluxJob
			{
				triplets = triplets,
				bodies = bodies,
				solarIrradianceAtBodies = solarIrradianceAtBody,
				incidentFlux = bodyIncidentFlux,
			}.Schedule(fullSize, 256, solarIrradianceAtBodyJob);

			var bodyEmissiveLuminosity = new NativeArray<double>(fullSize, Allocator.TempJob);
			var bodyMedianAlbedoLuminosity = new NativeArray<double>(fullSize, Allocator.TempJob);
			var bodyEmissiveLuminositiesJob = new BodyEmissiveLuminositiesJob
			{
				triplets = triplets,
				bodies = bodies,
				bodyOccludedFromSun = bodyOccludedFromSun,
				bodyIncidentFlux = bodyIncidentFlux,
				emissiveLuminosity = bodyEmissiveLuminosity,
				medianAlbedoLuminosity = bodyMedianAlbedoLuminosity,
			}.Schedule(fullSize, 256, JobHandle.CombineDependencies(bodyIncidentFluxJob, bodySunOcclusionJob));

			var albedoLuminosity = new NativeArray<double>(fullSize, Allocator.TempJob);
			var albedoLuminosityForVesselJob = new AlbedoLuminosityForVesselJob
			{
				triplets = triplets,
				numBodiesPerStep = numBodies,
				bodies = bodies,
				vessels = vessels,
				starIndexes = starIndex.AsDeferredJobArray(),
				directions = vesselBodyDirection,
				vesselOccludedFromBody = vesselBodyOcclusionMap,
				medianAlbedoLuminosity = bodyMedianAlbedoLuminosity,
				luminosity = albedoLuminosity,
			}.Schedule(fullSize, 256, JobHandle.CombineDependencies(bodyEmissiveLuminositiesJob, vesselBodyOcclusionJob));

			var directIrradiance = new NativeArray<double>(fullSize, Allocator.TempJob);
			var coreIrradiance = new NativeArray<double>(fullSize, Allocator.TempJob);
			var emissiveIrradiance = new NativeArray<double>(fullSize, Allocator.TempJob);
			var albedoIrradiance = new NativeArray<double>(fullSize, Allocator.TempJob);
			var bodyIrradiancesJob = new BodyIrradiancesJob
			{
				triplets = triplets,
				bodies = bodies,
				distances = vesselBodyDistance,
				bodyEmissiveLuminosity = bodyEmissiveLuminosity,
				bodyAlbedoLuminosity = albedoLuminosity,
				occluded = vesselBodyOcclusionMap,
				solarIrradiance = directIrradiance,
				bodyCoreIrradiance = coreIrradiance,
				bodyEmissiveIrradiance = emissiveIrradiance,
				bodyAlbedoIrradiance = albedoIrradiance,
			}.Schedule(fullSize, 256, JobHandle.CombineDependencies(vesselBodyOcclusionJob, bodyEmissiveLuminositiesJob, fluxFactsJob));


			//var directRawFlux = sunFluxAtVessels[i];
			//var directFlux = directRawFlux;
			//if (MainBody.hasAtmosphere && altitude < MainBody.atmosphereDepth)
			//directFlux *= MainBody.LightTransparencyFactor(mainBodyPosition, starFlux.direction, vesselPosition, altitude);


			// 1 standard unit Solar luminosity = 3.828e26 W
			// Solar Irradiance (energy / unit area, ie W/m^2) relates to solar luminosity:
			// Luminosity = 4 * pi * Irradiance * (d * d) where d = distance where irradiance was measured


			// if we are inside the atmosphere, scale down both fluxes by the atmosphere absorbtion at the current altitude
			// rationale : the atmosphere itself play a role in refracting the solar flux toward space, and the proportion of
			// the emissive flux released by the atmosphere itself is really only valid when you're in space. The real effects
			// are quite complex, this is a first approximation.
			//
			/*
			if (body.hasAtmosphere && altitude < body.atmosphereDepth)
			{
				double atmoFactor = body.LightTransparencyFactor(altitude);
				albedoFlux *= atmoFactor;
				emissiveFlux *= atmoFactor;
			}
			*/
			var directIrradianceSum = new NativeArray<double>(numSteps * numVessels, Allocator.TempJob);
			var sum0 = new SumForVessel
			{
				numVesselsPerStep = numVessels,
				numBodiesPerStep = numBodies,
				input = directIrradiance,
				output = directIrradianceSum,
			}.Schedule(numSteps * numVessels, numVessels, bodyIrradiancesJob);

			var bodyCoreIrradianceSum = new NativeArray<double>(numSteps * numVessels, Allocator.TempJob);
			var sum1 = new SumForVessel
			{
				numVesselsPerStep = numVessels,
				numBodiesPerStep = numBodies,
				input = coreIrradiance,
				output = bodyCoreIrradianceSum,
			}.Schedule(numSteps * numVessels, numVessels, bodyIrradiancesJob);

			var bodyEmissiveIrradianceSum = new NativeArray<double>(numSteps * numVessels, Allocator.TempJob);
			var sum2 = new SumForVessel
			{
				numVesselsPerStep = numVessels,
				numBodiesPerStep = numBodies,
				input = emissiveIrradiance,
				output = bodyEmissiveIrradianceSum,
			}.Schedule(numSteps * numVessels, numVessels, bodyIrradiancesJob);

			var albedoIrradianceSum = new NativeArray<double>(numSteps * numVessels, Allocator.TempJob);
			var sum3 = new SumForVessel
			{
				numVesselsPerStep = numVessels,
				numBodiesPerStep = numBodies,
				input = albedoLuminosity,
				output = albedoIrradianceSum,
			}.Schedule(numSteps * numVessels, numVessels, albedoLuminosityForVesselJob);

			var sumTemp = JobHandle.CombineDependencies(sum0, sum1, sum2);
			var copyJob = new RecordIrradiances
			{
				vessels = vessels,
				directIrradiance = directIrradianceSum,
				albedoIrradiance = albedoIrradianceSum,
				emissiveIrradiance = bodyEmissiveIrradianceSum,
				coreIrradiance = bodyCoreIrradianceSum,
			}.Schedule(numSteps * numVessels, numVessels, JobHandle.CombineDependencies(sumTemp, sum3));

			var dispose1 = triplets.Dispose(copyJob);
			var dispose2 = bodyOccludedFromSun.Dispose(dispose1);
			var dispose3 = starIndex.Dispose(dispose2);
			var dispose4 = vesselBodyDistance.Dispose(dispose3);
			var dispose5 = vesselBodyDirection.Dispose(dispose4);

			outputJob = solarIrradianceAtBody.Dispose(dispose5);
			JobHandle.ScheduleBatchedJobs();
		}
		public static double SolarFlux(double luminosity, double distance)
		{
			return distance < double.Epsilon ? double.PositiveInfinity : luminosity / (4 * math.PI_DBL * distance * distance);
		}
		public static double GeometricAlbedoFactor(double sunBodyObserverAngleFactor)
		{
			//if (hasAtmosphere)
				//return Math.Pow(sunBodyObserverAngleFactor * 1.113, 1.3);
			//else
				return math.pow(sunBodyObserverAngleFactor * 1.225, 2);
		}

	}
}
