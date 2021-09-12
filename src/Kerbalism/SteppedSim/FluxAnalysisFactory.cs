﻿using KERBALISM.SteppedSim.Jobs;
using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Profiling;

namespace KERBALISM.SteppedSim
{
	#region IndexStructs
	public struct TimeBodyStarIndex
	{
		public int time;
		public int origBody;
		public int directBody;
		public int origStar;
		public int directStar;
	}
	public struct TimeBodyOccluderIndex
	{
		public int time;
		public int origBody;
		public int directBody;
		public int origOccluder;
		public int directOccluder;
	}
	public struct TimeVesselBodyIndex
	{
		public int time;
		public int origVessel;
		public int directVessel;
		public int origBody;
		public int directBody;
	}
	public struct TimeVesselBodyStarIndex
	{
		public int time;
		public int origVessel;
		public int directVessel;
		public int origBody;
		public int directBody;
		public int origStar;
		public int directStar;
	}
	#endregion

	public struct FrameStats
	{
		public int numSteps;
		public int numVessels;
		public int numBodies;
		public int numStars;
	}

	public struct VesselBodyIrradiance
	{
		public double solar;
		public double core;
		public double emissive;
		public double albedo;
	}

	public class FluxAnalysisFactory : IDisposable
	{
		private NativeArray<TimeBodyOccluderIndex> timeBodyOccluderIndex;
		private NativeArray<TimeVesselBodyIndex> timeVesselBodyIndex;
		private NativeArray<TimeVesselBodyStarIndex> timeVesselBodyStarIndex;

		public FluxAnalysisFactory()
		{
			timeBodyOccluderIndex = new NativeArray<TimeBodyOccluderIndex>(20, Allocator.Persistent);
			timeVesselBodyIndex = new NativeArray<TimeVesselBodyIndex>(20, Allocator.Persistent);
			timeVesselBodyStarIndex = new NativeArray<TimeVesselBodyStarIndex>(20, Allocator.Persistent);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="dependencyHandle">Pre-requisite job that computes the body and vessel positions</param>
		/// <param name="timesteps">Timesteps to compute</param>
		/// <param name="bodies">1-m array of SubstepBody, for each timestep</param>
		/// <param name="vessels">1-n array of SubstepVessel, for each timestep</param>
		/// <param name="starIndexes">1-k array of the body indexes for each star</param>
		/// <param name="outputJob">Output JobHandle for follow-on processing to depend upon or Complete()</param>
		/// <param name="vesselBodyOcclusionMap">Unrolled array of body occlusion from perspective of vessel.  For each timestep, for each vessel, for each body, is it occluded?</param>
		/*
		 * Theory of operation, or What Does This Do?
		 * Called as:
			fluxAnalysisFactory.Process(ref computeWorldPosJob,
				timestepsSource,
				bodyDataGlobalArray,
				vesselDataGlobalArray,
				starsIndex,
				out JobHandle fluxJob,
				out NativeArray<bool> vesselBodyOcclusionMap,
				out NativeArray<VesselBodyIrradiance> vesselBodyIrradiance);
		 *
		 * Create and launch the processing chain to compute the irradiance matrix: (timestep, vessel, source body) for each irradiance type
		 * 
		 * ResizeArrays: Resize persistent arrays to support current frame request (memory allocation optimization)
		 * BuildAndLaunchIndexJobs: Build all relevant tuples for calculations:
		 *   (body,star) for body re-emissive and albedo luminosities, per-star
		 *   (vessel,body,star) for 3-body geometry effects on albedo luminosity
		 *   (body,body) for occlusion between bodies
		 *   (vessel,body) for occlusion and computing various luminosities on each vessel by each body
		 * Compute occlusion:
		 *   BodyBodyOcclusionRelevanceJob: For each (body, occluder) pair, compute if occluder is relevant.  (Occlusion Optimization)
		 *   VesselBodyOcclusionRelevanceJob: For each (vessel, occluder) pair, compute if occluder is relevant.  (Occlusion Optimization)
		 *   BodyStarOcclusionJob: For each (body, star) pair, evaluate occlusion from all [relevant] (body, occluder) pairs.
		 *   VesselBodyOcclusionJob: For each (vessel, body) pair, evaluate occlusion from all [relevant] (vessel, occluder) pairs.
		 * BodySolarIncidentFluxJob: For each (body, star) pair, evaluate the solar flux through the body cross-sectional area
		 * BodyEmissiveLuminositiesJob: Compute Re-emissive and isotropic albedo luminosities for each body-star relationship
		 * AlbedoLuminosityForVesselJob: Apply 3-body (vessel, body, star) geometry effects to albedo
		 * CombinePerStarLuminosity: Roll per-star emissive and albedo luminosities together into the body
		 * BodyIrradiancesJob: Compute the matrix of solar, core, re-emissive and albedo irradiances for each (timestep, vessel, body)
		 */
		public void Process(ref JobHandle dependencyHandle,
			in NativeArray<double> timesteps,
			in NativeArray<SubstepBody> bodies,
			in NativeArray<SubstepVessel> vessels,
			in NativeArray<int> starIndexes,
			out JobHandle outputJob,
			out NativeArray<bool> vesselBodyOcclusionMap,
			out NativeArray<VesselBodyIrradiance> vesselBodyIrradiances)
		{
			int numSteps = timesteps.Length;
			int numBodies = bodies.Length / numSteps;
			int numVessels = vessels.Length / numSteps;
			int numStars = starIndexes.Length;
			int numStepsVesselsBodies = numSteps * numVessels * numBodies;
			int numStepsBodiesStars = numSteps * numBodies * numStars;
			FrameStats frameStats = new FrameStats
			{
				numSteps = numSteps,
				numVessels = numVessels,
				numBodies = numBodies,
				numStars = numStars
			};

			Profiler.BeginSample("ResizeArrays");
			ResizeArrays(frameStats);
			Profiler.EndSample();

			Profiler.BeginSample("BuildIndexJobs");
			BuildAndLaunchIndexJobs(ref dependencyHandle,
						in frameStats,
						in starIndexes,
						out NativeArray<TimeBodyStarIndex> timeBodyStarIndex,
						out JobHandle buildTimeBodyStarsJob,
						out JobHandle buildTimeBodyOccludersJob,
						out JobHandle buildTimeVesselBodyIndexJob,
						out JobHandle buildTimeVesselBodyStarIndexJob);
			Profiler.EndSample();


			Profiler.BeginSample("BodyOcclusionRelevance");
			// Optimize occlusion checks: determine how relevant every [other] body is from perspective of every source body/vessel
			// If apparent diameter < ~10 arcmin (~0.003 radians), don't consider the body for occlusion checks
			// Real apparent diameters at earth : sun/moon ~ 30 arcmin, Venus ~ 1 arcmin max
			var bodyBodyOcclusionRelevance = new NativeArray<bool>(numSteps * numBodies * numBodies, Allocator.TempJob);
			var bodyBodyOcclusionRelevanceJob = new BodyBodyOcclusionRelevanceJob
			{
				minRequiredHalfAngleRadians = 0.002909 / 2,
				bodies = bodies,
				indices = timeBodyOccluderIndex,
				relevance = bodyBodyOcclusionRelevance,
			}.Schedule(numSteps * numBodies * numBodies, 512, buildTimeBodyOccludersJob);
			Profiler.EndSample();

			Profiler.BeginSample("VesselBodyOcclusionRelevance");
			var vesselBodyOcclusionRelevance = new NativeArray<bool>(numStepsVesselsBodies, Allocator.TempJob);
			var vesselBodyOcclusionRelevanceJob = new VesselBodyOcclusionRelevanceJob
			{
				minRequiredHalfAngleRadians = 0.002909 / 2,
				bodies = bodies,
				vessels = vessels,
				indices = timeVesselBodyIndex,
				relevance = vesselBodyOcclusionRelevance,
			}.Schedule(numStepsVesselsBodies, 512, buildTimeVesselBodyIndexJob);
			Profiler.EndSample();

			Profiler.BeginSample("BodyStarOcclusion");
			// Compute occlusion between each (body, star) pair
			var bodyStarOcclusion = new NativeArray<bool>(numStepsBodiesStars, Allocator.TempJob);
			var bodyStarOcclusionJob = new BodyStarOcclusionJob()
			{
				bodies = bodies,
				numBodiesPerStep = numBodies,
				occlusionRelevance = bodyBodyOcclusionRelevance,
				timeBodyStarIndex = timeBodyStarIndex,
				occluded = bodyStarOcclusion,
			}.Schedule(numStepsBodiesStars, 32, JobHandle.CombineDependencies(buildTimeBodyStarsJob, bodyBodyOcclusionRelevanceJob));
			Profiler.EndSample();

			Profiler.BeginSample("VesselBodyOcclusion");
			vesselBodyOcclusionMap = new NativeArray<bool>(numStepsVesselsBodies, Allocator.TempJob);
			var vesselBodyOcclusionJob = new VesselBodyOcclusionJob
			{
				timeVesselBodyIndex = timeVesselBodyIndex,
				numBodiesPerStep = numBodies,
				vessels = vessels,
				bodies = bodies,
				occlusionRelevance = vesselBodyOcclusionRelevance,
				occluded = vesselBodyOcclusionMap,
			}.Schedule(numStepsVesselsBodies, 16, vesselBodyOcclusionRelevanceJob);
			Profiler.EndSample();

			Profiler.BeginSample("SolarFlux");
			// Compute the individual contribution of each star to each body's incident flux, without regard to occlusion
			var bodyIncidentFluxPerStar = new NativeArray<double>(numStepsBodiesStars, Allocator.TempJob);
			var bodySolarIncidentFluxJob = new BodySolarIncidentFluxJob
			{
				triplets = timeBodyStarIndex,
				bodies = bodies,
				bodyIncidentFlux = bodyIncidentFluxPerStar,
			}.Schedule(numStepsBodiesStars, 256, buildTimeBodyStarsJob);
			Profiler.EndSample();

			Profiler.BeginSample("Luminosities");
			// 1 standard unit Solar luminosity = 3.828e26 W
			// Solar Irradiance (energy / unit area, ie W/m^2) relates to solar luminosity:
			// Irradiance = Luminosity / 4 * pi * (d * d) where d = distance where irradiance was measured

			// Compute the thermal re-emission luminosity (without regard to occlusion)
			// Compute the isotropic (non-directed) albedo luminosity (with occlusion considered)
			var bodyEmissiveLuminosityPerStar = new NativeArray<double>(numStepsBodiesStars, Allocator.TempJob);
			var bodyIsotropicAlbedoLuminosityPerStar = new NativeArray<double>(numStepsBodiesStars, Allocator.TempJob);
			var bodyEmissiveLuminositiesJob = new BodyEmissiveLuminositiesJob
			{
				timeBodyStarIndex = timeBodyStarIndex,
				bodies = bodies,
				bodyIncidentFlux = bodyIncidentFluxPerStar,
				emissiveLuminosity = bodyEmissiveLuminosityPerStar,
				isotropicAlbedoLuminosity = bodyIsotropicAlbedoLuminosityPerStar,
				bodyStarOcclusion = bodyStarOcclusion,
			}.Schedule(numStepsBodiesStars, 256, JobHandle.CombineDependencies(bodyStarOcclusionJob, bodySolarIncidentFluxJob));
			Profiler.EndSample();

			Profiler.BeginSample("AlbedoLuminosity");
			var bodyAlbedoLuminosityPerStar = new NativeArray<double>(numStepsVesselsBodies * numStars, Allocator.TempJob);
			var albedoLuminosityForVesselJob = new AlbedoLuminosityForVesselJob
			{
				tuples = timeVesselBodyStarIndex,
				stats = frameStats,
				bodies = bodies,
				vessels = vessels,
				vesselOccludedFromBody = vesselBodyOcclusionMap,
				isotropicAlbedoLuminosity = bodyIsotropicAlbedoLuminosityPerStar,
				luminosity = bodyAlbedoLuminosityPerStar,
			}.Schedule(numStepsVesselsBodies * numStars, 256, JobHandle.CombineDependencies(buildTimeVesselBodyStarIndexJob, bodyEmissiveLuminositiesJob, vesselBodyOcclusionJob));
			Profiler.EndSample();

			Profiler.BeginSample("CombineEmissiveLuminosity");
			var combinedEmissiveLuminosity = new NativeArray<double>(numSteps * numBodies, Allocator.TempJob);
			var combineEmissiveLuminositySourcesJob = new CombinePerStarLuminosity
			{
				stats = frameStats,
				perStarLuminosity = bodyEmissiveLuminosityPerStar,
				combinedLuminosity = combinedEmissiveLuminosity,
			}.Schedule(numSteps * numBodies, 512, bodyEmissiveLuminositiesJob);
			Profiler.EndSample();

			Profiler.BeginSample("CombineAlbedoLuminosity");
			var combinedAlbedoLuminosity = new NativeArray<double>(numStepsVesselsBodies, Allocator.TempJob);
			var combineAlbedoLuminositySourcesJob = new CombinePerStarLuminosity
			{
				stats = frameStats,
				perStarLuminosity = bodyAlbedoLuminosityPerStar,
				combinedLuminosity = combinedAlbedoLuminosity,
			}.Schedule(numStepsVesselsBodies, 512, albedoLuminosityForVesselJob);
			Profiler.EndSample();

			Profiler.BeginSample("BodyIrradiances");
			/*
			var directIrradiance = new NativeArray<double>(numStepsVesselsBodies, Allocator.TempJob);
			var coreIrradiance = new NativeArray<double>(numStepsVesselsBodies, Allocator.TempJob);
			var emissiveIrradiance = new NativeArray<double>(numStepsVesselsBodies, Allocator.TempJob);
			var albedoIrradiance = new NativeArray<double>(numStepsVesselsBodies, Allocator.TempJob);
			*/
			vesselBodyIrradiances = new NativeArray<VesselBodyIrradiance>(numStepsVesselsBodies, Allocator.TempJob);
			var bodyIrradiancesJob = new BodyIrradiancesJob
			{
				tuples = timeVesselBodyIndex,
				vessels = vessels,
				bodies = bodies,
				bodyEmissiveLuminosity = combinedEmissiveLuminosity,
				bodyAlbedoLuminosity = combinedAlbedoLuminosity,
				vesselOccludedFromBody = vesselBodyOcclusionMap,
				irradiance = vesselBodyIrradiances,
			}.Schedule(numStepsVesselsBodies, 256, JobHandle.CombineDependencies(vesselBodyOcclusionJob, combineAlbedoLuminositySourcesJob, combineEmissiveLuminositySourcesJob));
			Profiler.EndSample();



			var d0 = timeBodyStarIndex.Dispose(bodyIrradiancesJob);
			outputJob = bodyStarOcclusion.Dispose(d0);
			JobHandle.ScheduleBatchedJobs();

			/*
			//var directRawFlux = sunFluxAtVessels[i];
			//var directFlux = directRawFlux;
			//if (MainBody.hasAtmosphere && altitude < MainBody.atmosphereDepth)
			//directFlux *= MainBody.LightTransparencyFactor(mainBodyPosition, starFlux.direction, vesselPosition, altitude);

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
		public static bool OcclusionTest(double3 a, double3 b, double3 v, double dist)
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

		public void Dispose()
		{
			timeBodyOccluderIndex.Dispose();
			timeVesselBodyIndex.Dispose();
			timeVesselBodyStarIndex.Dispose();
		}

		private void ResizeArrays(in FrameStats stats)
		{
			int stepsBodiesBodies = stats.numSteps * stats.numBodies * stats.numBodies;
			if (timeBodyOccluderIndex.Length < stepsBodiesBodies)
			{
				timeBodyOccluderIndex.Dispose();
				timeBodyOccluderIndex = new NativeArray<TimeBodyOccluderIndex>(stepsBodiesBodies, Allocator.Persistent);
			}
			int stepsVesselsBodies = stats.numSteps * stats.numVessels * stats.numBodies;
			if (timeVesselBodyIndex.Length < stepsVesselsBodies)
			{
				timeVesselBodyIndex.Dispose();
				timeVesselBodyIndex = new NativeArray<TimeVesselBodyIndex>(stepsVesselsBodies, Allocator.Persistent);
			}
			int stepsVesselsBodiesStars = stepsVesselsBodies * stats.numStars;
			if (timeVesselBodyStarIndex.Length < stepsVesselsBodiesStars)
			{
				timeVesselBodyStarIndex.Dispose();
				timeVesselBodyStarIndex = new NativeArray<TimeVesselBodyStarIndex>(stepsVesselsBodiesStars, Allocator.Persistent);
			}
		}

		private void BuildAndLaunchIndexJobs(ref JobHandle dependencyHandle,
			in FrameStats frameStats,
			in NativeArray<int> starIndexes,
			out NativeArray<TimeBodyStarIndex> timeBodyStarIndex,
			out JobHandle buildTimeBodyStarsJob,
			out JobHandle buildTimeBodyOccludersJob,
			out JobHandle buildTimeVesselBodyIndexJob,
			out JobHandle buildTimeVesselBodyStarIndexJob)
		{
			// Build index of all body-star pairings
			timeBodyStarIndex = new NativeArray<TimeBodyStarIndex>(frameStats.numSteps * frameStats.numBodies * frameStats.numStars, Allocator.TempJob);
			buildTimeBodyStarsJob = new BuildTimeBodyStarsIndexJob
			{
				stats = frameStats,
				starsIndex = starIndexes,
				triplets = timeBodyStarIndex,
			}.Schedule(dependencyHandle);

			// Build index of all body-body pairings
			buildTimeBodyOccludersJob = new BuildTimeBodyOccludersIndexJob
			{
				stats = frameStats,
				triplets = timeBodyOccluderIndex,
			}.Schedule(dependencyHandle);

			buildTimeVesselBodyIndexJob = new BuildTimeVesselBodyIndicesJob
			{
				stats = frameStats,
				triplets = timeVesselBodyIndex,
			}.Schedule(dependencyHandle);

			buildTimeVesselBodyStarIndexJob = new BuildTimeVesselBodyStarIndicesJob
			{
				stats = frameStats,
				starsIndex = starIndexes,
				tuple = timeVesselBodyStarIndex,
			}.Schedule(dependencyHandle);
		}
	}
}
