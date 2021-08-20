using KERBALISM.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using ThreadState = System.Threading.ThreadState;

namespace KERBALISM
{
	public static class SubStepSim
	{
		private static int maxWarpRateIndex;
		private static float lastMaxWarprate;

		// The interval is the in-game time between each substep evaluation.
		// It determine how many substeps will be required at a given timewarp rate.
		// Lower values give a more precise simulation (more sampling points)
		// Higher values will reduce the amount of substeps consumed per FixedUpdate, preventing the simulation
		// from falling behind when there is a large number of vessels or if higher than stock timewarp rates are used.

		private static Planetarium.CelestialFrame currentZup;
		private static double currentInverseRotAngle;

		private const int maxSubSteps = 100; // max amount of substeps, no matter the interval and max timewarp rate
		private const int subStepsMargin = 25; // substep "buffer"
		private const double minInterval = 30.0; // in seconds
		private const double maxInterval = 120.0; // in seconds
		private const double intervalChange = 15.0; // in seconds

		private const int fuLagCheckFrequency = 50; // 1 second
		private const int maxLaggingFu = 2;
		private const int minNonLaggingFu = 20;
		private const int lagChecksForDecision = 5;
		private const int lagChecksReset = 25;

		private static int fuCounter;
		private static int laggingFuCount;
		private static int nonLaggingFuCount;

		private static int lagChecksCount;
		private static int lagCheckResultsCount;
		private static int laggingLagChecks;
		private static int nonLaggingLagChecks;

		private static double maxUT;
		private static double currentUT;
		private static int bodyCount;
		private static double lastStepUT;
		private static string errorMessage;

		private static Dictionary<Guid, SubStepVessel> vessels = new Dictionary<Guid, SubStepVessel>();
		private static List<Guid> subStepVesselIds = new List<Guid>();

		public static double subStepInterval;
		public static int subStepsToCompute;
		public static int subStepsAtMaxWarp;
		public static int stepCount;
		public static SubStepBody[] Bodies { get; private set; }
		public static IndexedQueue<SubStepGlobalData> steps = new IndexedQueue<SubStepGlobalData>();
		public static SubStepGlobalData lastStep;
		public static Queue<SubStepVessel> vesselsInNeedOfCatchup = new Queue<SubStepVessel>();

		public static void Init()
		{
			maxWarpRateIndex = TimeWarp.fetch.warpRates.Length - 1;

			subStepInterval = minInterval;
			UpdateMaxSubSteps();

			bodyCount = FlightGlobals.Bodies.Count;
			Bodies = new SubStepBody[bodyCount];

			for (int i = 0; i < bodyCount; i++)
			{
				SubStepBody safeBody = new SubStepBody(FlightGlobals.Bodies[i]);
				Bodies[i] = safeBody;
			}
		}

		public static void Load(ConfigNode node)
		{
			if (Lib.IsGameRunning)
			{
				subStepInterval = Lib.ConfigValue(node, nameof(subStepInterval), minInterval);
				UpdateMaxSubSteps();
			}
		}

		public static void Save(ConfigNode node)
		{
			node.AddValue(nameof(subStepInterval), subStepInterval);
		}

		private static void WorkerLoadCheck()
		{
			if (lastMaxWarprate != TimeWarp.fetch.warpRates[maxWarpRateIndex])
			{
				UpdateMaxSubSteps();
				return;
			}

			fuCounter++;

			if (fuCounter < fuLagCheckFrequency)
				return;

			fuCounter = 0;
			lagChecksCount++;

			if (laggingFuCount > maxLaggingFu)
			{
				laggingLagChecks++;
				lagCheckResultsCount++;
			}
			else if (laggingFuCount == 0 && nonLaggingFuCount > minNonLaggingFu)
			{
				nonLaggingLagChecks++;
				lagCheckResultsCount++;
			}

			laggingFuCount = 0;
			nonLaggingFuCount = 0;

			if (lagChecksCount > lagChecksReset)
			{
				lagChecksCount = 0;
				lagCheckResultsCount = 0;
				nonLaggingLagChecks = 0;
				laggingLagChecks = 0;
				return;
			}

			if (lagCheckResultsCount > lagChecksForDecision)
			{
				if (nonLaggingLagChecks == 0 && laggingLagChecks == lagCheckResultsCount && subStepInterval < maxInterval)
				{
					subStepInterval = Math.Min(subStepInterval + intervalChange, maxInterval);
					UpdateMaxSubSteps();
				}
				else if (laggingLagChecks == 0 && nonLaggingLagChecks == lagCheckResultsCount && subStepInterval > minInterval)
				{
					subStepInterval = Math.Max(subStepInterval - intervalChange, minInterval);
					UpdateMaxSubSteps();
				}
			}
		}

		private static void UpdateMaxSubSteps()
		{
			lastMaxWarprate = TimeWarp.fetch.warpRates[maxWarpRateIndex];
			subStepsAtMaxWarp = (int)(lastMaxWarprate * 0.02 / subStepInterval);
			if (subStepsAtMaxWarp > maxSubSteps)
				subStepsAtMaxWarp = maxSubSteps;

			subStepsToCompute = subStepsAtMaxWarp + subStepsMargin;

			fuCounter = 0;
			laggingFuCount = 0;
			nonLaggingFuCount = 0;

			lagChecksCount = 0;
			lagCheckResultsCount = 0;
			laggingLagChecks = 0;
			nonLaggingLagChecks = 0;
		}

		private static ManualResetEvent waitHandle = new ManualResetEvent(true);

		public static void OnFixedUpdate()
		{
			if (!Lib.IsGameRunning)
				return;

			MiniProfiler.lastFuTicks = fuWatch.ElapsedTicks;
			fuWatch.Restart();

			subStepsToCompute = (int)(TimeWarp.fetch.warpRates[7] * 0.02 * 1.5 / subStepInterval);

			otherWatch.Restart();
			waitHandle.WaitOne();
			otherWatch.Stop();
			MiniProfiler.workerLag = otherWatch.Elapsed.TotalMilliseconds;

			if (errorMessage != null)
			{
				Lib.Log(errorMessage, Lib.LogLevel.Warning);
				errorMessage = null;
			}

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.SubStepSim.Update");
			Synchronize();
			WorkerLoadCheck();
			UnityEngine.Profiling.Profiler.EndSample();

			waitHandle.Reset();
			ThreadPool.QueueUserWorkItem(x => RunSubStepSim());
		}

		private static void Synchronize()
		{
			MiniProfiler.lastWorkerTicks = currentWorkerTicks;
			currentWorkerTicks = 0;

			currentUT = Planetarium.GetUniversalTime();

			maxUT = currentUT + (subStepsToCompute * subStepInterval);

			int stepsToConsume = 0;
			// remove old steps
			while (steps.Count > 0 && steps.Peek().ut < currentUT)
			{
				stepsToConsume++;
				steps.Dequeue().ReleaseToPool();
			}

			stepCount = steps.Count;

			MiniProfiler.workerTimeUsed = stepsToConsume * subStepInterval;

			if (stepCount == 0)
			{
				MiniProfiler.workerTimeMissed = (Math.Floor(TimeWarp.fixedDeltaTime / subStepInterval) - stepsToConsume) * subStepInterval;
				laggingFuCount++;
			}
			else if (stepsToConsume >= subStepsAtMaxWarp)
			{
				MiniProfiler.workerTimeMissed = 0.0;
				nonLaggingFuCount++;
			}
			else
			{
				MiniProfiler.workerTimeMissed = 0.0;
			}
			
			// copy things from Planetarium
			currentZup = Planetarium.Zup;
			currentInverseRotAngle = Planetarium.InverseRotAngle;

			// update bodies and their orbit
			foreach (SubStepBody body in Bodies)
			{
				body.Update();
			}

			// update vessels and their orbit
			foreach (VesselData vd in DB.VesselDatas)
			{
				if (!vessels.TryGetValue(vd.VesselId, out SubStepVessel vessel))
				{
					if (vd.IsSimulated && vd.Vessel != null)
					{
						vessel = new SubStepVessel(vd.Vessel);
						vessels.Add(vd.VesselId, vessel);
						subStepVesselIds.Add(vd.VesselId);
					}
					else
					{
						continue;
					}
				}
				else
				{
					if (!vd.IsSimulated || vd.Vessel == null)
					{
						vessels.Remove(vd.VesselId);
						subStepVesselIds.Remove(vd.VesselId);
						foreach (SimStep step in vd.subSteps)
							step.ReleaseToPool();
						vd.subSteps.Clear();
						continue;
					}
				}

				vessel.UpdatePosition(vd);
				vessel.Synchronize(vd, stepsToConsume);
			}

			for (int i = subStepVesselIds.Count - 1; i >= 0; i--)
			{
				if (!DB.VesselExist(subStepVesselIds[i]))
				{
					vessels.Remove(subStepVesselIds[i]);
					subStepVesselIds.RemoveAt(i);
				}
			}
		}

		static Stopwatch fuWatch = new Stopwatch();
		static Stopwatch workerWatch = new Stopwatch();
		static Stopwatch otherWatch = new Stopwatch();

		static long currentWorkerTicks;

		private static void RunSubStepSim()
		{
			try
			{
				while (lastStepUT < maxUT || vesselsInNeedOfCatchup.Count > 0)
				{
					if (lastStepUT < maxUT)
					{
						workerWatch.Restart();
						ComputeNextStep();
						workerWatch.Stop();
						currentWorkerTicks += workerWatch.ElapsedTicks;
					}

					if (vesselsInNeedOfCatchup.Count > 0)
					{
						workerWatch.Restart();

						if (vesselsInNeedOfCatchup.Peek().TryComputeMissingSteps())
							vesselsInNeedOfCatchup.Dequeue();

						workerWatch.Stop();
						currentWorkerTicks += workerWatch.ElapsedTicks;
					}
				}
			}
			catch (Exception e)
			{
				errorMessage = $"Sim thread has crashed\n{e.Message}\n{e.StackTrace}";
			}
			finally
			{
				waitHandle.Set();
			}
		}

		public static void ComputeNextStep()
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
	}
}
