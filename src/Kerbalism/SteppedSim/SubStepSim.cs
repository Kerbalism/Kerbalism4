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
	internal struct SparseSimData
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
		private const float SimErrorThreshold = 10;	// Detect positional errors > 10m

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

		private readonly List<SubstepFrame> FrameList = new List<SubstepFrame>();


		private JobHandle stepGeneratorJob;
		// Source lists: timesteps to compute, orbit data per Body/Vessel
		private NativeArray<double> timestepsSource;
		private NativeArray<RotationCondition> rotationsSource;
		private NativeArray<SparseSimData> sparseDataSource;

		private NativeArray<SubstepBody> bodyTemplates;
		private NativeArray<SubstepVessel> vesselTemplates;

		//private NativeArray<double3> relativePositions;
		//private NativeArray<RotationCondition> rotations;

		public IndexedQueue<SubStepGlobalData> steps = new IndexedQueue<SubStepGlobalData>();

		private static SubStepSim _instance = null;
		public static SubStepSim Instance
		{
			get => _instance ?? (_instance = new SubStepSim(120));
			private set => _instance = value;
		}

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
			Bodies.Clear();
			Bodies.AddRange(FlightGlobals.Bodies.Where(x => x.orbit == null || x.referenceBody == x));
			while (Bodies.Count < FlightGlobals.Bodies.Count)
			{
				Bodies.AddUniqueRange(FlightGlobals.Bodies.Where(x => Bodies.Contains(x.referenceBody)));
			}
			BodyIndex.Clear();
			int i = 0;
			foreach (var b in Bodies)
				BodyIndex.Add(b, i++);
		}

		public void Load(ConfigNode node)
		{
			if (Lib.IsGameRunning)
			{
			}
		}

		public void Save(ConfigNode node)
		{
			//node.AddValue(nameof(subStepInterval), subStepInterval);
		}

		public void OnFixedUpdate()
		{
			if (!Lib.IsGameRunning)
				return;

			MiniProfiler.lastFuTicks = fuWatch.ElapsedTicks;
			fuWatch.Restart();

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

		private void RegenerateOrbits(List<CelestialBody> bodies, List<Vessel> vessels, List<(Orbit,CelestialBody)> orbits, CelestialBody placeholder)
		{
			int i = 0;
			var referenceTime = Planetarium.GetUniversalTime();
			orbits.Clear();
			foreach (var body in bodies)
			{
				var o = (body.orbit != null) ? body.orbit : placeholder.orbit;
				orbits.Add((o, body.referenceBody));
				sparseDataSource[i] = new SparseSimData()
				{
					isVessel = false,
					isLandedVessel = false,
					isValidOrbit = body.orbit != null,
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
			foreach (var v in vessels)
			{
				var o = (v.orbit != null) ? v.orbit : placeholder.orbit;
				orbits.Add((o, v.mainBody));
				sparseDataSource[i] = new SparseSimData()
				{
					isVessel = true,
					isLandedVessel = v.Landed,
					isValidOrbit = v.orbit != null && !v.Landed,
				};
				// TODO: Fixme!
				rotationsSource[i++] = new RotationCondition()
				{
					frameTime = referenceTime,
					axis = new double3(0, 1, 0),
					celestialFrame = default,
					angle = math.length(math.mul(v.transform.rotation, new float3(0, 1, 0))),
					velocity = v.angularVelocityD.magnitude,
				};
			}
		}

		private readonly Stopwatch fuWatch = new Stopwatch();

		private void RunSubStepSim()
		{
			Profiler.BeginSample("Kerbalism.RunSubstepSim");
			Profiler.BeginSample("Kerbalism.RunSubstepSim.Jobs");

			// Generate steps
			int numSteps = (int)math.ceil(duration / maxSubstepTime);
			timestepsSource = new NativeArray<double>(numSteps, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			stepGeneratorJob = new StepGeneratorJob(startUT, duration, maxSubstepTime)
			{
				times = timestepsSource
			}.Schedule();
			JobHandle.ScheduleBatchedJobs();

			// This is effectively what SubStepBody.Update() and SubStepVessel.Synchronize() do.
			Profiler.BeginSample("Kerbalism.RunSubstepSim.RegenerateOrbits");
			rotationsSource = new NativeArray<RotationCondition>(Bodies.Count + Vessels.Count, Allocator.TempJob);
			sparseDataSource = new NativeArray<SparseSimData>(Bodies.Count + Vessels.Count, Allocator.TempJob);
			RegenerateOrbits(Bodies, Vessels, Orbits, placeholderBody);
			Profiler.EndSample();
			Profiler.BeginSample("Kerbalism.RunSubstepSim.CreateTemplates");
			CreateTemplates(out bodyTemplates, out vesselTemplates);
			Profiler.EndSample();

			PositionComputeFactory.ComputePositions(timestepsSource,
				rotationsSource,
				sparseDataSource,
				bodyTemplates,
				vesselTemplates,
				Orbits,
				BodyIndex,
				Bodies[0].position,
				ref stepGeneratorJob,
				out var computeWorldPosJob,
				out NativeArray<RotationCondition> rotations,
				out NativeArray<double3> relativePositions,
				out NativeArray<double3> worldPositions,
				out NativeArray<SubstepBody> bodyDataGlobalArray,
				out NativeArray<SubstepVessel> vesselDataGlobalArray);

			computeWorldPosJob.Complete();
			Profiler.EndSample();

			Profiler.BeginSample("Kerbalism.RunSubstepSim.FrameCollector");
			GatherFrames(FrameList, Bodies, Vessels, BodyIndex, relativePositions, worldPositions, rotations);
			Profiler.EndSample();

			// Do things
			Profiler.BeginSample("Kerbalism.RunSubstepSim.Validator");
			foreach (var f in FrameList)
			{
				ValidateComputations(Bodies, Vessels, f);
			}
			{
				var f = FrameList.Last();
				ValidateComputations(Bodies, Vessels, f, true);
			}

			foreach (var f in FrameList) f.Release();	// Will Dispose() its own contents

			Profiler.EndSample();

			RunStockCalcs();

			timestepsSource.Dispose();
			rotationsSource.Dispose();
			sparseDataSource.Dispose();

			bodyTemplates.Dispose();
			vesselTemplates.Dispose();

			rotations.Dispose();
			relativePositions.Dispose();
			worldPositions.Dispose();
			bodyDataGlobalArray.Dispose();
			vesselDataGlobalArray.Dispose();
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
		private void CreateTemplates(out NativeArray<SubstepBody> bodyTemplates, out NativeArray<SubstepVessel> vesselTemplates)
		{
			bodyTemplates = new NativeArray<SubstepBody>(FlightGlobals.Bodies.Count, Allocator.TempJob);
			int i = 0;
			foreach (var body in Bodies)
			{
				bodyTemplates[i++] = new SubstepBody
				{
					bodyFrame = body.BodyFrame,
					position = double3.zero,
					radius = body.Radius,
				};
			}
			vesselTemplates = new NativeArray<SubstepVessel>(Vessels.Count, Allocator.TempJob);
			i = 0;
			foreach (var vessel in Vessels)
			{
				vesselTemplates[i++] = new SubstepVessel
				{
					position = double3.zero,
					relPosition = double3.zero,
					rotation = 0,
					isLanded = vessel.Landed,
					LLA = new double3(vessel.latitude, vessel.longitude, vessel.altitude + vessel.mainBody.Radius),
					mainBodyIndex = BodyIndex[vessel.mainBody],
				};
			}
		}

		// This method is really slow...
		public void GatherFrames(
			List<SubstepFrame> frameList,
			in List<CelestialBody> bodies,
			in List<Vessel> vessels,
			in Dictionary<CelestialBody, int> bodyLookupIndex,
			in NativeArray<double3> relPositions,
			in NativeArray<double3> worldPositions,
			in NativeArray<RotationCondition> rotations)
		{
			// Build out compute frames.
			// Each frame has a timestamp, a list of SubStepBodies and SubStepVessels
			frameList.Clear();
			int globalIndex = 0;
			foreach (var ut in timestepsSource)
			{
				Profiler.BeginSample("Kerbalism.RunSubstepSim.FrameCollector.Acquire");
				var frame = SubstepFrame.Acquire();
				frame.Init(ut, bodies.Count, vessels.Count);
				Profiler.EndSample();

				Profiler.BeginSample("Kerbalism.RunSubstepSim.FrameCollector.FillBody");
				int bodyIndex = 0;
				foreach (var body in bodies)
				{
					frame.bodies[bodyIndex++] = new SubstepBody
					{
						position = worldPositions[globalIndex],
						radius = body.Radius,
						bodyFrame = rotations[globalIndex].celestialFrame,
					};
					globalIndex++;
				}
				Profiler.EndSample();

				Profiler.BeginSample("Kerbalism.RunSubstepSim.FrameCollector.FillVessel");

				int vesselIndex = 0;
				foreach (var vessel in vessels)
				{
					frame.vessels[vesselIndex++] = new SubstepVessel
					{
						position = worldPositions[globalIndex],
						relPosition = relPositions[globalIndex],
						rotation = rotations[globalIndex].angle,
						isLanded = vessel.Landed,
						LLA = new double3(vessel.latitude, vessel.longitude, vessel.altitude + vessel.mainBody.Radius),
						mainBodyIndex = bodyLookupIndex[vessel.mainBody],
					};
					globalIndex++;
				}
				Profiler.EndSample();

				Profiler.BeginSample("Kerbalism.RunSubstepSim.FrameCollector.Add");
				frameList.Add(frame);
				Profiler.EndSample();
			}
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

							UnityEngine.Debug.Log($"Claimed recompute: {fv.relPosition}");

							var worldSurfPos = v.mainBody.GetWorldSurfacePosition(lla.x, lla.y, lla.z);

							var compRelSurfPos = fv.relPosition;
							var compWorldSurfPos = fv.position;

							UnityEngine.Debug.Log($"{v} Landed.\nExpected rel: {relSurfPos}\nExprected World: {worldSurfPos}\nComputed rel: {compRelSurfPos}\nComputed world: {compWorldSurfPos}");


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
					if (sparseDataSource[orbitIndex].isValidOrbit)
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
			framePool.Enqueue(this);
		}

		public void Init(double timestamp, int numBodies, int numVessels)
		{
			this.timestamp = timestamp;
			bodies = new NativeArray<SubstepBody>(numBodies, Allocator.TempJob);
			vessels = new NativeArray<SubstepVessel>(numVessels, Allocator.TempJob);
		}

		public void Dispose()
		{
			if (bodies.IsCreated) bodies.Dispose();
			if (vessels.IsCreated) vessels.Dispose();
		}
	}
}
