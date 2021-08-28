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
		public static void Process(in SubstepFrame frame, out JobHandle outputJob)
		{
			var ut = frame.timestamp;
			var bodies = frame.bodies;
			var vessels = frame.vessels;

			int numBodies = bodies.Length;
			int numVessels = vessels.Length;

			// Each vessel will be computed against each body
			int sz = numBodies * numVessels;
			var unrolledPairs = new NativeArray<int2>(sz, Allocator.TempJob);
			var buildPairsJob = new BuildPairsJob
			{
				numBodies = numBodies,
				numVessels = numVessels,
				pairs = unrolledPairs,
			}.Schedule();

			var sunFluxAtBodies = new NativeArray<double>(numBodies, Allocator.TempJob);
			var sunFluxAtBodyJob = new SunFluxAtBodyJob
			{
				bodies = bodies,
				flux = sunFluxAtBodies,
			}.Schedule(numBodies, 64);

			var bodyOccludedFromSun = new NativeArray<bool>(numBodies, Allocator.TempJob);
			var bodySunOcclusionJob = new BodySunOcclusionJob
			{
				bodies = bodies,
				occluded = bodyOccludedFromSun,
			}.Schedule(numBodies, 1);

			var occlusionRelevanceMap = new NativeArray<byte>(sz, Allocator.TempJob);
			var occlusionRelevanceJob = new OcclusionRelevanceJob
			{
				bodies = bodies,
				vessels = vessels,
				interestMap = occlusionRelevanceMap,
			}.Schedule(numVessels, 8);

			var vesselOccludedFromBody = new NativeArray<bool>(sz, Allocator.TempJob);
			var vesselBodyOcclusionJob = new BodyVesselOcclusionJob
			{
				bodies = bodies,
				vessels = vessels,
				pairs = unrolledPairs,
				interestMap = occlusionRelevanceMap,
				occluded = vesselOccludedFromBody,
			}.Schedule(sz, 16, JobHandle.CombineDependencies(buildPairsJob, occlusionRelevanceJob));

			//var sunFluxAtVessels = new NativeArray<double>(numVessels, Allocator.TempJob);
			var sunFluxAtVesselsJob = new SunFluxAtVesselJob
			{
				bodies = bodies,
				vessels = vessels,
				occluded = vesselOccludedFromBody,
				flux = frame.directIrradiance,
			}.Schedule(numVessels, 64, vesselBodyOcclusionJob);

			var bodyHemisphericReradiatedIrradianceFactor = new NativeArray<double>(numBodies, Allocator.TempJob);
			var bodyHemisphericReradiatedIrradianceFactorJob = new BodyHemisphericReradiatedIrradianceFactor
			{
				bodies = bodies,
				occluded = bodyOccludedFromSun,
				sunFluxAtBody = sunFluxAtBodies,
				factor = bodyHemisphericReradiatedIrradianceFactor
			}.Schedule(numBodies, 64, JobHandle.CombineDependencies(sunFluxAtBodyJob, bodySunOcclusionJob));

			var albedoIrradiance = new NativeArray<double>(sz, Allocator.TempJob);
			var albedoIrradianceAtVesselJob = new AlbedoIrradianceAtVesselJob
			{
				bodies = bodies,
				vessels = vessels,
				pairs = unrolledPairs,
				vesselOccludedFromBody = vesselOccludedFromBody,
				hemisphericReradiatedIrradianceFactor = bodyHemisphericReradiatedIrradianceFactor,
				irradiance = albedoIrradiance,
			}.Schedule(numVessels, 32, JobHandle.CombineDependencies(bodyHemisphericReradiatedIrradianceFactorJob, vesselBodyOcclusionJob, buildPairsJob));

			var emissiveIrradiance = new NativeArray<double>(sz, Allocator.TempJob);
			var bodyEmissiveIrradianceJob = new BodyEmissiveIrradianceJob
			{
				bodies = bodies,
				vessels = vessels,
				pairs = unrolledPairs,
				sunFluxAtBodies = sunFluxAtBodies,
				irradiance = emissiveIrradiance,
			}.Schedule(sz, 128, JobHandle.CombineDependencies(sunFluxAtBodyJob, buildPairsJob));

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

			var sum1 = new SumForVessel
			{
				count = numBodies,
				input = albedoIrradiance,
				output = frame.albedoIrradiance,
			}.Schedule(numVessels, numVessels, albedoIrradianceAtVesselJob);
			var sum2 = new SumForVessel
			{
				count = numBodies,
				input = emissiveIrradiance,
				output = frame.bodyEmissiveIrradiance,
			}.Schedule(numVessels, numVessels, bodyEmissiveIrradianceJob);
			var prepJob = JobHandle.CombineDependencies(sunFluxAtVesselsJob, sum1, sum2);

			var dispose1 = unrolledPairs.Dispose(prepJob);
			var dispose2 = bodyOccludedFromSun.Dispose(dispose1);
			var dispose3 = sunFluxAtBodies.Dispose(dispose2);

			outputJob = vesselOccludedFromBody.Dispose(dispose3);
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
