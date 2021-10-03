using KERBALISM.SteppedSim.Jobs;
using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Profiling;

namespace KERBALISM.SteppedSim
{
	#region IndexStructs
	public struct TimeVesselIndex
	{
		public int time;
		public int origVessel;
	}

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
		public float visibility;
		public double solarRaw;
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
		private readonly double homeAtmDensityASL = 1.225;
		private readonly double solarInsolationAtHome;

		public FluxAnalysisFactory()
		{
			timeBodyOccluderIndex = new NativeArray<TimeBodyOccluderIndex>(20, Allocator.Persistent);
			timeVesselBodyIndex = new NativeArray<TimeVesselBodyIndex>(20, Allocator.Persistent);
			timeVesselBodyStarIndex = new NativeArray<TimeVesselBodyStarIndex>(20, Allocator.Persistent);
			homeAtmDensityASL = (Planetarium.fetch?.Home.atmosphereDepth > 0.0) ? Planetarium.fetch.Home.atmDensityASL : 1.225;
			solarInsolationAtHome = PhysicsGlobals.SolarInsolationAtHome;
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
		 * SolarAtmosphericEffectsJob: For each (vessel, body) pair, evaluate the atmospheric effect between the vessel and the body
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
			in double prevUT,
			out JobHandle outputJob,
			out NativeArray<bool> vesselBodyOcclusionMap,
			out NativeArray<VesselBodyIrradiance> vesselBodyIrradiances,
			out NativeArray<VesselBodyIrradiance> vesselBodyIrradiancesSummary)
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
						out NativeArray<TimeVesselIndex> timeVesselIndices,
						out JobHandle buildTimeVesselIndexJob,
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
			var vesselBodyOcclusionRelevanceLength = new NativeArray<int>(numSteps * numVessels, Allocator.TempJob);
			var vesselBodyOcclusionRelevance = new NativeArray<int>(numStepsVesselsBodies, Allocator.TempJob);
			var vesselBodyOcclusionRelevanceJob = new VesselBodyOcclusionRelevanceJob
			{
				indices = timeVesselIndices,
				stats = frameStats,
				minRequiredHalfAngleRadians = 0.002909 / 2,
				bodies = bodies,
				vessels = vessels,
				relevance = vesselBodyOcclusionRelevance,
				relevanceLengths = vesselBodyOcclusionRelevanceLength,
			}.Schedule(numSteps * numVessels, 512, buildTimeVesselIndexJob);
			Profiler.EndSample();

			Profiler.BeginSample("BodyStarOcclusion");
			// Compute occlusion between each (body, star) pair
			var bodyStarOcclusion = new NativeArray<bool>(numStepsBodiesStars, Allocator.TempJob);
			var bodyStarOcclusionJob = new BodyStarOcclusionJob()
			{
				bodies = bodies,
				stats = frameStats,
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
				stats = frameStats,
				vessels = vessels,
				bodies = bodies,
				occlusionRelevance = vesselBodyOcclusionRelevance,
				occlusionRelevanceLengths = vesselBodyOcclusionRelevanceLength,
				occluded = vesselBodyOcclusionMap,
			}.Schedule(numStepsVesselsBodies, 16, JobHandle.CombineDependencies(buildTimeVesselBodyIndexJob, vesselBodyOcclusionRelevanceJob));
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
			var solarAtmosphericEffects = new NativeArray<double>(numStepsVesselsBodies, Allocator.TempJob);
			var solarAtmosphericEffectsJob = new SolarAtmosphericEffectsJob
			{
				tuples = timeVesselBodyIndex,
				stats = frameStats,
				vessels = vessels,
				bodies = bodies,
				homeAtmDensityASL = homeAtmDensityASL,
				solarInsolationAtHome = solarInsolationAtHome,
				fluxMultipliers = solarAtmosphericEffects,
			}.Schedule(numStepsVesselsBodies, 16, buildTimeVesselBodyIndexJob);

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
				isotropicAlbedoLuminosityPerStar = bodyIsotropicAlbedoLuminosityPerStar,
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
			var combineLuminositiesJob = JobHandle.CombineDependencies(combineAlbedoLuminositySourcesJob, combineEmissiveLuminositySourcesJob);
			vesselBodyIrradiances = new NativeArray<VesselBodyIrradiance>(numStepsVesselsBodies, Allocator.TempJob);
			var bodyIrradiancesJob = new BodyIrradiancesJob
			{
				tuples = timeVesselBodyIndex,
				vessels = vessels,
				bodies = bodies,
				bodyEmissiveLuminosity = combinedEmissiveLuminosity,
				bodyAlbedoLuminosity = combinedAlbedoLuminosity,
				vesselOccludedFromBody = vesselBodyOcclusionMap,
				atmosphericEffects = solarAtmosphericEffects,
				irradiance = vesselBodyIrradiances,
			}.Schedule(numStepsVesselsBodies, 256, JobHandle.CombineDependencies(vesselBodyOcclusionJob, combineLuminositiesJob, solarAtmosphericEffectsJob));
			Profiler.EndSample();

			Profiler.BeginSample("AllStepsSummation");
			var frameWeights = new NativeArray<float>(numSteps, Allocator.TempJob);
			vesselBodyIrradiancesSummary = new NativeArray<VesselBodyIrradiance>(numVessels * numBodies, Allocator.TempJob);
			var frameWeightsJob = new Jobs.VesselDataJobs.ComputeFrameWeights
			{
				start = prevUT,
				times = timesteps,
				weights = frameWeights,
			}.Schedule(dependencyHandle);

			// Sum each (vessel,body)'s irradiances separately across all timesteps
			var vesselBodyIrradianceSummaryJob = new SumVesselBodyIrradiancesJob
			{
				stats = frameStats,
				weights = frameWeights,
				irradiances = vesselBodyIrradiances,
				output = vesselBodyIrradiancesSummary,
			}.Schedule(numVessels * numBodies, 64, JobHandle.CombineDependencies(frameWeightsJob, bodyIrradiancesJob));
			Profiler.EndSample();

			var d0 = timeBodyStarIndex.Dispose(vesselBodyIrradianceSummaryJob);
			var d1 = frameWeights.Dispose(d0);
			outputJob = bodyStarOcclusion.Dispose(d1);
			JobHandle.ScheduleBatchedJobs();
		}
		public static double SolarFlux(double luminosity, double distance)
		{
			return distance < double.Epsilon ? double.PositiveInfinity : luminosity / (4 * math.PI_DBL * distance * distance);
		}
		public static double GeometricAlbedoFactor(double sunBodyObserverAngleFactor)
		{
			return 4.0 * sunBodyObserverAngleFactor * sunBodyObserverAngleFactor * sunBodyObserverAngleFactor;
		}

		/// <summary>
		/// Calculate if point v is within dist of line segment AB.
		/// Compute if occluder centered at V with radius = dist occludes line AB.
		/// </summary>
		/// <param name="a">First point of line segment</param>
		/// <param name="b">Second point of line segment</param>
		/// <param name="v">Vertex to measure</param>
		/// <param name="dist">Maximum distance to vertex V</param>
		/// <returns>True if <paramref name="v"/> is within <paramref name="dist"/> of line segment AB</returns>
		public static bool OcclusionTest(double3 a, double3 b, double3 v, double dist)
		{
			double3 ab = b - a;
			var abLenSq = math.lengthsq(ab);
			if (Unity.Burst.CompilerServices.Hint.Likely(abLenSq > 0))
			{
				var distSq = dist * dist;
				double3 av = v - a;
				double3 bv = v - b;

				bool vBehindA = math.dot(av, ab) < 0;
				bool vPastB = math.dot(bv, ab) > 0;
				bool vBetweenAB = !(vBehindA || vPastB);
				bool aCloseEnough = math.lengthsq(av) <= distSq;
				bool bCloseEnough = math.lengthsq(bv) <= distSq;
				bool crossClose = math.lengthsq(math.cross(ab, av)) <= distSq * abLenSq;

				//	return (vBehindA && aCloseEnough) || (vPastB & bCloseEnough) || (vBetweenAB && crossClose);
				return aCloseEnough || bCloseEnough || (vBetweenAB && crossClose);
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
			out NativeArray<TimeVesselIndex> timeVesselIndices,
			out JobHandle buildTimeVesselIndexJob,
			out JobHandle buildTimeBodyStarsJob,
			out JobHandle buildTimeBodyOccludersJob,
			out JobHandle buildTimeVesselBodyIndexJob,
			out JobHandle buildTimeVesselBodyStarIndexJob)
		{
			timeVesselIndices = new NativeArray<TimeVesselIndex>(frameStats.numSteps * frameStats.numVessels, Allocator.TempJob);
			buildTimeVesselIndexJob = new BuildTimeVesselIndexJob
			{
				stats = frameStats,
				tuples = timeVesselIndices,
			}.Schedule(dependencyHandle);

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
