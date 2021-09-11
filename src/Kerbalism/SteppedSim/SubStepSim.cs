using KERBALISM.Collections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Profiling;

namespace KERBALISM.SteppedSim
{
	public struct SubstepComputeFlags
	{
		internal bool isVessel;
		internal bool isLandedVessel;
		internal bool isValidOrbit;
	}

	public struct RotationCondition
	{
		public double frameTime;
		public double3 axis;
		public double angle;
		public double velocity;
		public Planetarium.CelestialFrame celestialFrame;
	}

	public class SubStepSim
	{
		public float maxSubstepTime = 30;
		private const float SimErrorThreshold = 10; // Detect positional errors > 10m
		private bool runStockCalcs = false;

		private Planetarium.CelestialFrame currentZup;
		private double currentInverseRotAngle;

		private double lastUT = 0;
		private double startUT = 0;
		private double duration;

		private string errorMessage;

		private readonly List<(Orbit, CelestialBody)> Orbits = new List<(Orbit, CelestialBody)>();
		private readonly List<CelestialBody> Bodies = new List<CelestialBody>();
		private readonly List<Vessel> Vessels = new List<Vessel>();
		private readonly Dictionary<CelestialBody, int> BodyIndex = new Dictionary<CelestialBody, int>();
		private readonly CelestialBody placeholderBody;
		private Dictionary<Guid, int> vesselIndexMap;

		public readonly FrameManager frameManager = new FrameManager();
		private readonly FluxAnalysisFactory fluxAnalysisFactory = new FluxAnalysisFactory();

		// Source lists: timesteps to compute, orbit data per Body/Vessel
		private NativeArray<double> timestepsSource;
		private NativeArray<RotationCondition> rotationsSource;
		private NativeArray<SubstepComputeFlags> flagDataSource;
		private NativeArray<SubStepOrbit> stepOrbitsSource;
		private NativeList<int> starsIndex;

		private NativeArray<SubstepBody> bodyTemplates;
		private NativeArray<SubstepVessel> vesselTemplates;

		private JobHandle fluxJob;
		public JobHandle FluxJob => fluxJob;
		private NativeArray<bool> vesselBodyOcclusionMap;
		private NativeArray<VesselBodyIrradiance> vesselBodyIrradiance;
		private NativeArray<RotationCondition> rotations;
		//private NativeArray<double3> relativePositions;
		private NativeArray<double3> worldPositions;
		private NativeArray<SubstepBody> bodyDataGlobalArray;
		private NativeArray<SubstepVessel> vesselDataGlobalArray;

		public SubStepSim(float maxSubstepTime = 30)
		{
			this.maxSubstepTime = maxSubstepTime;
			placeholderBody = FlightGlobals.Bodies.FirstOrDefault(x => x.orbit != null);
			if (!(placeholderBody is CelestialBody))
				UnityEngine.Debug.LogError("SubStepSim initialization didn't find any bodies with orbits!");
		}

		public void Init()
		{
			lastUT = startUT = Planetarium.GetUniversalTime();
			starsIndex = new NativeList<int>(Bodies.Count, Allocator.Persistent);
			Bodies.Clear();
			Bodies.AddRange(FlightGlobals.Bodies.Where(x => x.orbit == null || x.referenceBody == x));
			while (Bodies.Count < FlightGlobals.Bodies.Count)
			{
				Bodies.AddUniqueRange(FlightGlobals.Bodies.Where(x => Bodies.Contains(x.referenceBody)));
			}
			BodyIndex.Clear();
			int i = 0;
			foreach (var b in Bodies)
			{
				if (b.isStar)
					starsIndex.Add(i);
				BodyIndex.Add(b, i++);
			}
		}

		public void ClearExpiredFrames(double ts) => frameManager.ClearExpiredFrames(ts);

		public void OnFixedUpdate()
		{
			if (!Lib.IsGameRunning)
				return;

			simSetupWatch.Restart();

			if (errorMessage != null)
			{
				Lib.Log(errorMessage, Lib.LogLevel.Warning);
				errorMessage = null;
			}

			Profiler.BeginSample("Kerbalism.SubStepSimJobified.Update");
			Synchronize();
			if (duration > 0)
				RunSubStepSim();
			lastUT = startUT + duration;
			simSetupWatch.Stop();
			MiniProfiler.lastFuTicks = simSetupWatch.ElapsedTicks;
			Profiler.EndSample();
		}

		private void Synchronize()
		{
			if (lastUT == 0) lastUT = Planetarium.GetUniversalTime();
			startUT = lastUT;
			duration = Planetarium.GetUniversalTime() - startUT;

			// copy things from Planetarium
			currentZup = Planetarium.Zup;
			currentInverseRotAngle = Planetarium.InverseRotAngle;

			Vessels.Clear();
			// DB.VesselDatas may be out of sync and referencing destroyed vessels
			foreach (var vd in DB.VesselDatas)
				if (vd.IsSimulated && vd.Vessel != null)
					Vessels.Add(vd.Vessel);
		}

		private readonly Stopwatch simSetupWatch = new Stopwatch();

		private void RunSubStepSim()
		{
			Profiler.BeginSample("Kerbalism.RunSubstepSimSetup");

			// Generate steps
			int numSteps = (int)math.ceil(duration / maxSubstepTime);
			timestepsSource = new NativeArray<double>(numSteps, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			var stepGeneratorJob = new StepGeneratorJob(startUT, duration, maxSubstepTime)
			{
				times = timestepsSource
			}.Schedule();
			JobHandle.ScheduleBatchedJobs();

			// This is effectively what SubStepBody.Update() and SubStepVessel.Synchronize() do.
			Profiler.BeginSample("Kerbalism.RunSubstepSim.GenerateBodyVesselOrbitData");
			GenerateBodyVesselOrbitData(
				Bodies,
				Vessels,
				Orbits,
				placeholderBody,
				out stepOrbitsSource,
				out rotationsSource,
				out flagDataSource,
				out bodyTemplates,
				out vesselTemplates,
				out vesselIndexMap);
			Profiler.EndSample();

			Profiler.BeginSample("Kerbalism.RunSubstepSim.ComputePositions");
			PositionComputeFactory.ComputePositions(timestepsSource,
				stepOrbitsSource,
				rotationsSource,
				flagDataSource,
				bodyTemplates,
				vesselTemplates,
				Bodies[0].position,
				ref stepGeneratorJob,
				out var computeWorldPosJob,
				out rotations,
				out worldPositions,
				out bodyDataGlobalArray,
				out vesselDataGlobalArray);
			Profiler.EndSample();

			Profiler.BeginSample("Kerbalism.RunSubstepSim.FluxAnalysis.BuildAndLaunch");
			fluxAnalysisFactory.Process(ref computeWorldPosJob,
				timestepsSource,
				bodyDataGlobalArray,
				vesselDataGlobalArray,
				starsIndex,
				out fluxJob,
				out vesselBodyOcclusionMap,
				out vesselBodyIrradiance);
			Profiler.EndSample();

			Profiler.EndSample();
		}

		public void Complete()
		{
			Profiler.BeginSample("Kerbalism.RunSubstepSim.Complete");

			Profiler.BeginSample("Kerbalism.RunSubstepSim.Complete.Actual");
			fluxJob.Complete();
			Profiler.EndSample();

			// Do things
			Profiler.BeginSample("Kerbalism.RunSubstepSim.RecordFrames");
			int numVessels = Vessels.Count;
			int numBodies = Bodies.Count;
			int numSteps = (int)math.ceil(duration / maxSubstepTime);
			for (int i = 0; i < numSteps; i++)
			{
				var f = SubstepFrame.Acquire();
				f.Init(timestepsSource[i], numBodies, numVessels);
				f.guidVesselMap = vesselIndexMap;
				f.vessels.Slice(0, numVessels).CopyFrom(vesselDataGlobalArray.Slice(i * numVessels, numVessels));
				f.bodies.Slice(0, numBodies).CopyFrom(bodyDataGlobalArray.Slice(i * numBodies, numBodies));
				f.irradiances.Slice(0, numVessels * numBodies).CopyFrom(vesselBodyIrradiance.Slice(i * numVessels * numBodies, numVessels * numBodies));
				frameManager.Frames.Add(timestepsSource[i], f);
			}
			Profiler.EndSample();

			Profiler.BeginSample("Kerbalism.RunSubstepSim.DeliverTimestamps");
			foreach (var v in Vessels)
			{
				if (v.TryGetVesselData(out VesselData vd))
				{
					vd.timestamps.AddRange(timestepsSource);
				}
			}
			Profiler.EndSample();

			/*
			Profiler.BeginSample("Kerbalism.RunSubstepSim.Validator");
			foreach (var f in frameManager.Frames.Values)
			{
				ValidateComputations(Bodies, Vessels, f);
			}
			{
				if (frameManager.Frames.TryGetValue(startUT + duration, out var f))
					ValidateComputations(Bodies, Vessels, f, true);
			}
			*/


			if (runStockCalcs) RunStockCalcs();

			timestepsSource.Dispose();
			rotationsSource.Dispose();
			flagDataSource.Dispose();
			stepOrbitsSource.Dispose();

			rotations.Dispose();
			worldPositions.Dispose();
			bodyDataGlobalArray.Dispose();
			vesselDataGlobalArray.Dispose();

			vesselBodyOcclusionMap.Dispose();
			vesselBodyIrradiance.Dispose();
			Profiler.EndSample();
		}

		/*
		public void ComputeNextStep()
		{
			stepCount++;
			lastStepUT = currentUT + (stepCount * subStepInterval);

			lastStep = SubStepGlobalData.GetFromPool();
			lastStep.ut = lastStepUT;
			lastStep.inverseRotAngle = currentInverseRotAngle;
			lastStep.zup = currentZup;
			steps.Enqueue(lastStep);

			foreach (SubStepBody body in Bodies)
			{
				body.ComputeNextStep();
			}

			foreach (SubStepVessel vessel in vessels.Values)
			{
				vessel.ComputeNextStep();
			}
		}
		*/
		private void GenerateBodyVesselOrbitData(List<CelestialBody> bodies,
			List<Vessel> vessels,
			List<(Orbit, CelestialBody)> orbits,
			CelestialBody placeholder,
			out NativeArray<SubStepOrbit> stepOrbits,
			out NativeArray<RotationCondition> rotationsSource,
			out NativeArray<SubstepComputeFlags> flags,
			out NativeArray<SubstepBody> bodyTemplates,
			out NativeArray<SubstepVessel> vesselTemplates,
			out Dictionary<Guid, int> vesselIndexMap)
		{
			int i = 0;
			var referenceTime = Planetarium.GetUniversalTime();
			int numBodies = bodies.Count;
			int numVessels = vessels.Count;
			int numOrbits = numBodies + numVessels;
			double defaultLuminosity = PhysicsGlobals.SolarLuminosity > 0 ? PhysicsGlobals.SolarLuminosity : PhysicsGlobals.SolarLuminosityAtHome * 4 * math.PI_DBL * Sim.AU * Sim.AU;
			orbits.Clear();

			Profiler.BeginSample("Kerbalism.RunSubstepSim.GenerateBodyVesselOrbitData.Allocator");
			stepOrbits = new NativeArray<SubStepOrbit>(numOrbits, Allocator.TempJob);
			rotationsSource = new NativeArray<RotationCondition>(numOrbits, Allocator.TempJob);
			flags = new NativeArray<SubstepComputeFlags>(numOrbits, Allocator.TempJob);
			bodyTemplates = new NativeArray<SubstepBody>(numBodies, Allocator.TempJob);
			vesselTemplates = new NativeArray<SubstepVessel>(numVessels, Allocator.TempJob);
			Profiler.EndSample();

			Profiler.BeginSample("Kerbalism.RunSubstepSim.GenerateBodyVesselOrbitData.BodyAccumulator");
			foreach (var body in bodies)
			{
				var o = (body.orbit != null) ? body.orbit : placeholder.orbit;
				orbits.Add((o, body.referenceBody));
				flags[i] = new SubstepComputeFlags()
				{
					isVessel = false,
					isLandedVessel = false,
					isValidOrbit = body.orbit != null,
				};
				stepOrbits[i] = new SubStepOrbit(o, body.referenceBody, BodyIndex);
				bodyTemplates[i] = new SubstepBody
				{
					bodyFrame = body.BodyFrame,
					position = double3.zero,
					radius = body.Radius,
					solarLuminosity = body.isStar ? defaultLuminosity : 0,	// FIXME
					albedo = body.albedo,
					bodyCoreThermalFlux = body.coreTemperatureOffset,
				};
				rotationsSource[i++] = new RotationCondition()
				{
					frameTime = referenceTime,
					axis = new double3(0, 1, 0),
					celestialFrame = body.BodyFrame,
					angle = body.rotationAngle,
					velocity = body.angularV,
				};
			}
			Profiler.EndSample();
			Profiler.BeginSample("Kerbalism.RunSubstepSim.GenerateBodyVesselOrbitData.VesselAccumulator");
			vesselIndexMap = new Dictionary<Guid, int>(numVessels);
			foreach (var v in vessels)
			{
				var o = (v.orbit != null) ? v.orbit : placeholder.orbit;
				orbits.Add((o, v.mainBody));
				vesselIndexMap.Add(v.id, i - numBodies);
				flags[i] = new SubstepComputeFlags()
				{
					isVessel = true,
					isLandedVessel = v.Landed || v.Splashed,
					isValidOrbit = v.orbit != null && !(v.Landed || v.Splashed),
				};
				stepOrbits[i] = new SubStepOrbit(o, v.mainBody, BodyIndex);
				// TODO: Fixme!
				vesselTemplates[i - numBodies] = new SubstepVessel
				{
					position = double3.zero,
					rotation = 0,
					isLanded = v.Landed,
					LLA = new double3(v.latitude, v.longitude, v.altitude + v.mainBody.Radius),
					mainBodyIndex = BodyIndex[v.mainBody],
				};
				rotationsSource[i++] = new RotationCondition()
				{
					frameTime = referenceTime,
					axis = new double3(0, 1, 0),
					celestialFrame = default,
					angle = math.length(math.mul(v.transform.rotation, new float3(0, 1, 0))),
					velocity = v.angularVelocityD.magnitude,
				};
			}
			Profiler.EndSample();
		}

		private bool ValidateComputations(in List<CelestialBody> bodies, in List<Vessel> vessels, in SubstepFrame frame, bool checkVessels = false)
		{
			// KSP Vessels only track GetWorldPos3D() at current frame.
			// TODO: Check using the vessel orbit if they are not landed.
			bool res = true;
			var ts = frame.timestamp;
			for (int i=0; i<bodies.Count; i++)
			{
				var fb = frame.bodies[i];
				var body = bodies[i];
				var truePos = body.getTruePositionAtUT(ts);
				double3 truePosD = new double3(truePos.x, truePos.y, truePos.z);
				var error = math.length(truePosD - fb.position);
				if (error > SimErrorThreshold)
				{
					res = false;
					UnityEngine.Debug.LogError($"{body} timestamp {ts} WorldPos delta: {math.length(error)}.  Expected {truePosD} got {fb.position}");
				}
			}
			if (checkVessels)
			{
				for (int i = 0; i < vessels.Count; i++)
				{
					var fv = frame.vessels[i];
					var v = vessels[i];
					var truePos = v.GetWorldPos3D();
					double3 truePosD = new double3(truePos.x, truePos.y, truePos.z);
					var error = math.length(truePosD - fv.position);
					if (error > SimErrorThreshold)
					{
						res = false;
						UnityEngine.Debug.LogWarning($"{v} timestamp {ts} WorldPos delta: {math.length(error)} > threshold {SimErrorThreshold}.  Expected {truePosD} got {fv.position}");
						if (v.Landed)
						{
							Vector3d lla = new Vector3d(v.latitude, v.longitude, v.altitude);
							UnityEngine.Debug.Log($"{v} Landed.  LLA (live): {lla}.  LLA (compute): {fv.LLA}");

							var ind = bodies.IndexOf(v.mainBody);
							var bodyFrame = v.mainBody.BodyFrame;
							var recomputeBodyFrame = frame.bodies[ind].bodyFrame;

							var relSurfPos = v.mainBody.GetRelSurfacePosition(lla.x, lla.y, lla.z);
							var translatedRelPos = bodyFrame.LocalToWorld(relSurfPos.xzy);

							var recomputeRelSurfPos = GetRelSurfacePosition(fv.LLA);
							var rp = new Vector3d(recomputeRelSurfPos.x, recomputeRelSurfPos.y, recomputeRelSurfPos.z);
							var rpw = recomputeBodyFrame.LocalToWorld(rp.xzy);

							UnityEngine.Debug.Log($"RelSurfPos: {relSurfPos}.  Recomputed: {recomputeRelSurfPos}");
							UnityEngine.Debug.Log($"Translated: {translatedRelPos}.  Recomputed: {rpw}");

							var worldSurfPos = v.mainBody.GetWorldSurfacePosition(lla.x, lla.y, lla.z);
							var compWorldSurfPos = fv.position;

							UnityEngine.Debug.Log($"{v} Landed.\nExpected rel: {relSurfPos}\nExprected World: {worldSurfPos}\nComputed world: {compWorldSurfPos}");


						}
					}
				}
			}
			return res;

		}
		private double3 GetRelSurfacePosition(double3 LLA) => GetRelSurfaceNVector(LLA.x, LLA.y) * LLA.z;
		private double3 GetRelSurfaceNVector(double lat, double lon)
		{
			lat *= Math.PI / 180.0;
			lon *= Math.PI / 180.0;
			return SphericalVector(lat, lon).xzy;
		}

		// Ported from Planetarium.SphericalVector
		private double3 SphericalVector(double lat, double lon)
		{
			math.sincos(lat, out double sLat, out double cLat);
			math.sincos(lon, out double sLon, out double cLon);
			return new double3(cLat * cLon, cLat * sLon, sLat);
		}

		private void RunStockCalcs()
		{
			Profiler.BeginSample("Kerbalism.RunSubstepSim.StockCalcs");

			int taskCount = 0;
			foreach (var ut in timestepsSource)
			{
				//ComputeNextStep();
				int orbitIndex = 0;
				foreach (var (stockOrbit, refBody) in Orbits)
				{
					if (flagDataSource[orbitIndex].isValidOrbit)
					{
						Vector3d s = stockOrbit.getRelativePositionAtUT(ut);
						Vector3d s2 = stockOrbit.getTruePositionAtUT(ut);

						double3 stockPos = new double3(s.x, s.y, s.z);
						double3 stockWorldPos = new double3(s2.x, s2.y, s2.z);

						/*
						SubStepOrbitJobs jobOrbit = new SubStepOrbitJobs(stockOrbit, refBody);
						double3 computedPos = jobOrbit.getRelativePositionAtUT(ut);
						double3 error = computedPos - stockPos;
						if (math.length(error) > 1e-3f)
						{
							var stockGetObtAtUT = stockOrbit.getObtAtUT(ut);
							var simGetObtAtUT = jobOrbit.GetObtAtUT(ut);
							var obtErr = simGetObtAtUT - stockGetObtAtUT;
							UnityEngine.Debug.Log($"Orbit {stockOrbit} timestamp {ut} obtDelta: {math.length(obtErr)}");

							var stockEccA = stockOrbit.solveEccentricAnomaly(stockGetObtAtUT * stockOrbit.meanMotion, stockOrbit.eccentricity);
							var simEccA = jobOrbit.solveEccentricAnomaly(simGetObtAtUT * jobOrbit.meanMotion, jobOrbit.eccentricity);
							var eccAnomError = simEccA - stockEccA;
							UnityEngine.Debug.Log($"Orbit {stockOrbit} timestamp {ut} eccAnomalyDelta: {math.length(eccAnomError)}");

							var patT = stockOrbit.getRelativePositionAtT(stockGetObtAtUT);
							var stockgetRelativePositionAtT = new double3(patT.x, patT.y, patT.z);
							var simgetRelativePositionAtT = jobOrbit.getRelativePositionAtT(simGetObtAtUT);
							var posTerr = simgetRelativePositionAtT - stockgetRelativePositionAtT;
							UnityEngine.Debug.Log($"Orbit {stockOrbit} timestamp {ut} posTerr: {math.length(posTerr)}");

							UnityEngine.Debug.Log($"Orbit {stockOrbit} timestamp {ut} Comptutation delta: {math.length(error)}");
						}
						*/
					}
					orbitIndex++;
					taskCount++;
				}
			}
			Profiler.EndSample();
		}
	}

	public class SubstepFrame : IDisposable
	{
		public double timestamp;
		public NativeArray<SubstepBody> bodies;
		public NativeArray<SubstepVessel> vessels;
		public NativeArray<bool> vesselBodyOcclusionMap;
		public NativeArray<VesselBodyIrradiance> irradiances;
		public Dictionary<Guid, int> guidVesselMap;

		private static readonly Queue<SubstepFrame> framePool = new Queue<SubstepFrame>();
		private static int framesCreated = 0;

		public static SubstepFrame Acquire()
		{
			if (framePool.TryDequeue(out SubstepFrame frame))
				return frame;
			framesCreated++;
			return new SubstepFrame();
		}
		public void Release()
		{
			Dispose();
			timestamp = 0;
			guidVesselMap = null;
			framePool.Enqueue(this);
		}

		public void Init(double timestamp, int numBodies, int numVessels)
		{
			this.timestamp = timestamp;
			bodies = new NativeArray<SubstepBody>(numBodies, Allocator.Persistent);
			vessels = new NativeArray<SubstepVessel>(numVessels, Allocator.Persistent);
			vesselBodyOcclusionMap = new NativeArray<bool>(numVessels * numBodies, Allocator.Persistent);
			irradiances = new NativeArray<VesselBodyIrradiance>(numVessels * numBodies, Allocator.Persistent);
		}

		public void Dispose()
		{
			if (bodies.IsCreated) bodies.Dispose();
			if (vessels.IsCreated) vessels.Dispose();
			if (vesselBodyOcclusionMap.IsCreated) vesselBodyOcclusionMap.Dispose();
			if (irradiances.IsCreated) irradiances.Dispose();
		}
	}
}
