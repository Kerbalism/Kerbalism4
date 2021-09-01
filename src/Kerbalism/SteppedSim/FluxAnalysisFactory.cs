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
				minRequiredHalfAngleRadians = 0.002909 / 2,     // ~10 arcmin = 0.003 radians
				bodies = bodies,
				vessels = vessels,
				triplets = triplets,
				distance = vesselBodyDistance,
				direction = vesselBodyDirection,
				occlusionRelevance = occlusionRelevance,
			}.Schedule(fullSize, 256, buildTripletsJob);

			var sunFluxAtBodies = new NativeArray<double>(numSteps * numBodies, Allocator.TempJob);
			var sunFluxAtBodyJob = new SunFluxAtBodyJob
			{
				triplets = triplets,
				bodies = bodies,
				bodiesPerStep = numBodies,
				starIndexes = starIndex.AsDeferredJobArray(),
				flux = sunFluxAtBodies,
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

			var directIrradiance = new NativeArray<double>(fullSize, Allocator.TempJob);
			var sunFluxAtVesselsJob = new SunFluxAtVesselJob
			{
				triplets = triplets,
				bodies = bodies,
				distances = vesselBodyDistance,
				occluded = vesselBodyOcclusionMap,
				flux = directIrradiance,
			}.Schedule(fullSize, 256, vesselBodyOcclusionJob);

			var bodyHemisphericReradiatedIrradianceFactor = new NativeArray<double>(numSteps * numBodies, Allocator.TempJob);
			var bodyHemisphericReradiatedIrradianceFactorJob = new BodyHemisphericReradiatedIrradianceFactor
			{
				bodies = bodies,
				occluded = bodyOccludedFromSun,
				sunFluxAtBody = sunFluxAtBodies,
				factor = bodyHemisphericReradiatedIrradianceFactor
			}.Schedule(numSteps * numBodies, 64, JobHandle.CombineDependencies(sunFluxAtBodyJob, bodySunOcclusionJob));

			var albedoIrradiance = new NativeArray<double>(fullSize, Allocator.TempJob);
			var albedoIrradianceAtVesselJob = new AlbedoIrradianceAtVesselJob
			{
				triplets = triplets,
				numBodiesPerStep = numBodies,
				bodies = bodies,
				vessels = vessels,
				starIndexes = starIndex.AsDeferredJobArray(),
				directions = vesselBodyDirection,
				distances = vesselBodyDistance,
				vesselOccludedFromBody = vesselBodyOcclusionMap,
				hemisphericReradiatedIrradianceFactor = bodyHemisphericReradiatedIrradianceFactor,
				irradiance = albedoIrradiance,
			}.Schedule(fullSize, 256, JobHandle.CombineDependencies(bodyHemisphericReradiatedIrradianceFactorJob, vesselBodyOcclusionJob));

			var emissiveIrradiance = new NativeArray<double>(fullSize, Allocator.TempJob);
			var bodyEmissiveIrradianceJob = new BodyEmissiveIrradianceJob
			{
				triplets = triplets,
				bodies = bodies,
				distances = vesselBodyDistance,
				sunFluxAtBodies = sunFluxAtBodies,
				irradiance = emissiveIrradiance,
			}.Schedule(fullSize, 256, JobHandle.CombineDependencies(sunFluxAtBodyJob, fluxFactsJob));

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
			}.Schedule(numSteps * numVessels, numVessels, sunFluxAtVesselsJob);

			var albedoIrradianceSum = new NativeArray<double>(numSteps * numVessels, Allocator.TempJob);
			var sum1 = new SumForVessel
			{
				numVesselsPerStep = numVessels,
				numBodiesPerStep = numBodies,
				input = albedoIrradiance,
				output = albedoIrradianceSum,
			}.Schedule(numSteps * numVessels, numVessels, albedoIrradianceAtVesselJob);

			var bodyEmissiveIrradianceSum = new NativeArray<double>(numSteps * numVessels, Allocator.TempJob);
			var sum2 = new SumForVessel
			{
				numVesselsPerStep = numVessels,
				numBodiesPerStep = numBodies,
				input = emissiveIrradiance,
				output = bodyEmissiveIrradianceSum,
			}.Schedule(numSteps * numVessels, numVessels, bodyEmissiveIrradianceJob);

			var copyJob = new RecordIrradiances
			{
				vessels = vessels,
				directIrradiance = directIrradianceSum,
				albedoIrradiance = albedoIrradianceSum,
				emissiveIrradiance = bodyEmissiveIrradianceSum,
			}.Schedule(numSteps * numVessels, numVessels, JobHandle.CombineDependencies(sum0, sum1, sum2));

			var dispose1 = triplets.Dispose(copyJob);
			var dispose2 = bodyOccludedFromSun.Dispose(dispose1);
			var dispose3 = starIndex.Dispose(dispose2);
			var dispose4 = vesselBodyDistance.Dispose(dispose3);
			var dispose5 = vesselBodyDirection.Dispose(dispose4);

			outputJob = sunFluxAtBodies.Dispose(dispose5);
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
