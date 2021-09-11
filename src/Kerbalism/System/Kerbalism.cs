using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using HarmonyLib;
using KSP.UI.Screens;
using KSP.Localization;
using System.Collections;

namespace KERBALISM
{
	/// <summary>
	/// Main initialization class : for everything that isn't save-game dependant.
	/// For save-dependant things, or things that require the game to be loaded do it in Kerbalism.OnLoad()
	/// </summary>
	[KSPAddon(KSPAddon.Startup.MainMenu, false)]
	public class KerbalismCoreSystems : MonoBehaviour
	{
		public void Start()
		{
			// reset the save game initialized flag
			Kerbalism.IsSaveGameInitDone = false;

			// things in here will be only called once per KSP launch, after loading
			// nearly everything is available at this point, including the Kopernicus patched bodies.
			if (!Kerbalism.IsCoreMainMenuInitDone)
			{
				Kerbalism.IsCoreMainMenuInitDone = true;
			}

			// things in here will be called every the player goes to the main menu 
			RemoteTech.EnableInSPC();                   // allow RemoteTech Core to run in the Space Center
		}
	}

	[KSPScenario(ScenarioCreationOptions.AddToAllGames, new[] { GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.FLIGHT, GameScenes.EDITOR })]
	public sealed class Kerbalism : ScenarioModule
	{
		#region declarations

		/// <summary> global access </summary>
		public static Kerbalism Fetch { get; private set; } = null;

		/// <summary> Is the one-time main menu init done. Becomes true after loading, when the the main menu is shown, and never becomes false again</summary>
		public static bool IsCoreMainMenuInitDone { get; set; } = false;

		/// <summary> Is the one-time on game load init done. Becomes true after the first OnLoad() of a game, and never becomes false again</summary>
		public static bool IsCoreGameInitDone { get; set; } = false;

		/// <summary> Is the savegame (or new game) first load done. Becomes true after the first OnLoad(), and false when returning to the main menu</summary>
		public static bool IsSaveGameInitDone { get; set; } = false;

		// used to setup KSP callbacks
		public static Events.GameEventsHandler GameEvents { get; private set; }

		// the rendering script attached to map camera
		static MapCameraScript map_camera_script;

		// used to update storm data on one body per step
		static int storm_index;
		class Storm_data { public double time; public CelestialBody body; };
		static List<Storm_data> storm_bodies = new List<Storm_data>();

		// equivalent to TimeWarp.fixedDeltaTime
		// note: stored here to avoid converting it to double every time
		public static double elapsed_s;

		// number of steps from last warp blending
		private static uint warp_blending;

		/// <summary>Are we in an intermediary timewarp speed ?</summary>
		public static bool WarpBlending => warp_blending > 2u;

		// last savegame unique id
		static Guid savegameGuid;

		/// <summary> real time of last game loaded event </summary>
		public static float gameLoadTime = 0.0f;

		public static bool SerenityEnabled { get; private set; }

		public static bool UIVisible => KERBALISM.Events.GameEventsUI.UIVisible;

		public Vessel lastLaunchedVessel;
		public SteppedSim.SubStepSim subStepSimJobs;
		public const double FixedUpdateBudgetms = 2.0;


		#endregion

		#region initialization & save/load

		//  constructor
		public Kerbalism()
		{
			// enable global access
			Fetch = this;
			VesselsReady = false;
			SerenityEnabled = Expansions.ExpansionsLoader.IsExpansionInstalled("Serenity");
		}

		private void OnDestroy()
		{
			Fetch = null;
		}

		public override void OnLoad(ConfigNode node)
		{
			// everything in there will be called only one time : the first time a game is loaded from the main menu
			if (!IsCoreGameInitDone)
			{
				try
				{
					PartModuleAPI.Init();
					Sim.Init();         // find suns (Kopernicus support)
					Radiation.Init();   // create the radiation fields
					ScienceDB.Init();   // build the science database (needs Sim.Init() and Radiation.Init() first)
					Science.Init();     // register the science hijacker

					// static graphic components
					LineRenderer.Init();
					ParticleRenderer.Init();
					Highlighter.Init();

					// UI
					Textures.Init();                      // set up the icon textures
					UI.Init();                            // message system, main gui, launcher
					KsmGui.KsmGuiMasterController.Init(); // setup the new gui framework

					// part prefabs post-comilation hacks. Require ScienceDB.Init() to have run first 
					KerbalismLateLoading.DoLoading();

					// Create KsmGui windows
					new ScienceArchiveWindow();

					// GameEvents callbacks
					GameEvents = new Events.GameEventsHandler();

				}
				catch (Exception e)
				{
					ErrorManager.AddError(true, "CORE INITIALIZATION FAILED", e.ToString());
				}
				IsCoreGameInitDone = true;
			}

			// everything in there will be called every time a savegame (or a new game) is loaded from the main menu
			if (!IsSaveGameInitDone)
			{
				try
				{
					Message.Clear();
					Cache.Init();
					BackgroundResources.DisableBackgroundResources();

					// prepare storm data
					foreach (CelestialBody body in FlightGlobals.Bodies)
					{
						if (Storm.Skip_body(body))
							continue;
						Storm_data sd = new Storm_data { body = body };
						storm_bodies.Add(sd);
					}
				}
				catch (Exception e)
				{
					ErrorManager.AddError(true, "SAVEGAME LOAD FAILED", e.ToString());
				}

				IsSaveGameInitDone = true;
			}

			// eveything else will be called on every OnLoad() call :
			// - save/load
			// - every scene change
			// - in various semi-random situations (thanks KSP)

			// Fix for background IMGUI textures being dropped on scene changes since KSP 1.8
			Styles.ReloadBackgroundStyles();

			// always clear the caches
			Cache.Clear();

			// deserialize our database
			try
			{
				UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.DB.Load");
				DB.Load(node);
				UnityEngine.Profiling.Profiler.EndSample();
			}
			catch (Exception e)
			{
				ErrorManager.AddError(true, "SAVEGAME LOAD FAILED", e.ToString());
				ErrorManager.CheckErrors(true);
				return;
			}

			if (DB.version != null && DB.version < DB.LAST_SUPPORTED_VERSION)
			{
				string error = "Cannot load save games from Kerbalism versions before " + DB.LAST_SUPPORTED_VERSION;
				error += "\n\nQuit the game using this popup to avoid corrupting your save and downgrade your Kerbalism version, or start a new game.";
				ErrorManager.AddError(true, "OLD SAVE GAME: this save is from Kerbalism " + DB.version, error);
			}

			ErrorManager.CheckErrors(true);

			// detect if this is a different savegame
			if (DB.Guid != savegameGuid)
			{
				// clear caches
				Message.all_logs.Clear();

				// sync main window pos from db
				UI.Sync();

				// remember savegame id
				savegameGuid = DB.Guid;
			}

			Kerbalism.gameLoadTime = Time.time;
		}

		public override void OnSave(ConfigNode node)
		{
			if (!enabled) return;

			// serialize data
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.DB.Save");
			DB.Save(node);
			UnityEngine.Profiling.Profiler.EndSample();
		}

		#endregion

		public static bool VesselsReady { get; private set; }

		private void Start()
		{
			if (Lib.IsEditor)
				return;

			subStepSimJobs = new SteppedSim.SubStepSim();
			subStepSimJobs.Init();

			Lib.LogDebug("Creating vessels...");

			foreach (Vessel vessel in FlightGlobals.Vessels)
			{
				// In case of a vessel launch, that launched vessel is instantiated by the AssembleForLaunch() patch
				// and the dedicated VesselData ctor, with some special handling. This always happen before Kerbalism.Start()
				// To be able to skip that vessel (which will always be created when Kerbalism.Start() is called), we have put
				// a reference to it in the "lastLaunchedVessel" field. That field must be cleared afterwards.
				if (ReferenceEquals(vessel, lastLaunchedVessel))
					continue;

				if (!vessel.TryGetVesselData(out VesselData vd))
				{
					Lib.LogDebug($"Creating VesselData for unsaved vessel {vessel.vesselName}");
					vd = new VesselData(vessel.protoVessel, null, false);
					DB.AddNewVesselData(vd);
				}

				vd.SceneLoadVesselSetup(vessel);
			}

			// all vessels have now been properly instantiatied, meaning the global dictionary of FlightIds is populated
			// We are now sure that the FlightIDs we affect to the newly launched vessel (if any) will be unique.
			if (!ReferenceEquals(lastLaunchedVessel, null) && lastLaunchedVessel.TryGetVesselData(out VesselData launchedVD))
			{
				Lib.LogDebug($"Assigning FlightIds for launched vessel {launchedVD}");
				((PartDataCollectionVessel)launchedVD.Parts).AssignFlightIdsOnVesselLaunch();
			}

			lastLaunchedVessel = null;
			VesselsReady = true;

			// we need the sim to be ready for environment conditions to be evaluated properly in VesselData.Start()
			Sim.OnFixedUpdate();

			foreach (VesselData vd in DB.VesselDatas)
			{
				if (vd.IsSimulated)
				{
					vd.Start();
				}
			}
		}



		#region fixedupdate

		static System.Diagnostics.Stopwatch fuWatch = new System.Diagnostics.Stopwatch();
		private readonly Queue<Vessel> loadedWorkQueue = new Queue<Vessel>();
		private readonly Queue<Vessel> unloadedWorkQueue = new Queue<Vessel>();
		void FixedUpdate()
		{

			MiniProfiler.lastKerbalismFuTicks = fuWatch.ElapsedTicks;
			fuWatch.Restart();

			// remove control locks in any case
			Misc.ClearLocks();

			// do nothing if paused (note : this is true in the editor)
			if (Lib.IsPaused())
				return;

			// synchronize the bodies positions for the substep sim
			Sim.OnFixedUpdate();

			// convert elapsed time to double only once
			double fixedDeltaTime = TimeWarp.fixedDeltaTime;

			// and detect warp blending
			if (Math.Abs(fixedDeltaTime - elapsed_s) < 0.001)
				warp_blending = 0;
			else
				++warp_blending;

			// update elapsed time
			elapsed_s = fixedDeltaTime;

			ValidateVesselData();
			ManageWorkQueues(loadedWorkQueue, unloadedWorkQueue);

			// synchronize the threaded environment simulation
			subStepSimJobs.OnFixedUpdate();

			// credit science at regular interval
			ScienceDB.CreditScienceBuffers(elapsed_s);

			var currentTime = Planetarium.GetUniversalTime();

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.VesselProcessing");
			bool jobsCompleted = false;
			int vesselProcessedCount = 0;
			while (GetNextVesselToEval(loadedWorkQueue, unloadedWorkQueue, vesselProcessedCount, fuWatch, FixedUpdateBudgetms, out Vessel v, out VesselData vd))
			{
				if (!jobsCompleted && (v.loaded || subStepSimJobs.FluxJob.IsCompleted))
				{
					subStepSimJobs.Complete();
					jobsCompleted = true;
				}
				UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.VesselProcessing.Single");
				vesselProcessedCount++;
				var timeSinceLastUpdate = currentTime - vd.lastEvalUT;
				// get resource cache
				VesselResHandler resources = vd.ResHandler;

				//UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.VesselDataEval");
				// update the vessel info
				vd.Evaluate(timeSinceLastUpdate, subStepSimJobs);
				//UnityEngine.Profiling.Profiler.EndSample();

				UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Radiation");
				// show belt warnings
				Radiation.BeltWarnings(v, vd);

				// update storm data
				Storm.Update(v, vd, timeSinceLastUpdate);
				UnityEngine.Profiling.Profiler.EndSample();

				UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Comms");
				CommsMessages.Update(v, vd, timeSinceLastUpdate);
				UnityEngine.Profiling.Profiler.EndSample();

				UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Science");
				// transmit science data
				Science.Update(v, vd, timeSinceLastUpdate);
				UnityEngine.Profiling.Profiler.EndSample();

				UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Profile");
				// execute rules and processes
				Profile.Execute(v, vd, resources, timeSinceLastUpdate);
				UnityEngine.Profiling.Profiler.EndSample();

				//UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Loaded.ResourceAPI");
				//// part module resource updates
				//ResourceAPI.ResourceUpdate(v, vd, resources, elapsed_s);
				//UnityEngine.Profiling.Profiler.EndSample();

				UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Resource");
				// apply deferred requests
				resources.ResourceUpdate(timeSinceLastUpdate);
				UnityEngine.Profiling.Profiler.EndSample();

				// call automation scripts
				vd.computer.Automate(v, vd);
				UnityEngine.Profiling.Profiler.EndSample();
			}
			UnityEngine.Profiling.Profiler.EndSample();
			if (!jobsCompleted)
			{
				subStepSimJobs.Complete();
				jobsCompleted = true;
			}

			DB.UpdateVesselDataDictionary();

			// update storm data for one body per-step
			if (storm_bodies.Count > 0)
			{
				storm_bodies.ForEach(k => k.time += elapsed_s);
				Storm_data sd = storm_bodies[storm_index];
				Storm.Update(sd.body, sd.time);
				sd.time = 0.0;
				storm_index = (storm_index + 1) % storm_bodies.Count;
			}

			fuWatch.Stop();
		}

		private bool GetNextVesselToEval(Queue<Vessel> loadedQueue,
			Queue<Vessel> unloadedQueue,
			int iteration,
			in System.Diagnostics.Stopwatch watch,
			double budget,
			out Vessel v,
			out VesselData vd)
		{
			double elapsed = watch.Elapsed.TotalMilliseconds;
			var nextQueue = (elapsed > budget || unloadedQueue.Count == 0) ? loadedQueue : unloadedQueue;
			if (iteration == 0 && unloadedQueue.Count > 0)
				nextQueue = unloadedQueue;
			if (nextQueue.Count == 0)
			{
				// Nothing required, and/or out of time
				v = default;
				vd = default;
				return false;
			}
			v = nextQueue.Dequeue();
			v.TryGetVesselData(out vd);
			return true;
		}
		private void ManageWorkQueues(Queue<Vessel> loaded, Queue<Vessel> unloaded)
		{
			// Enqueue all valid loaded vessels
			loaded.Clear();
			foreach (var v in FlightGlobals.VesselsLoaded)
				if (v.TryGetVesselData(out VesselData vd) && vd.SimulatedCheck(v) && vd.IsSimulated)
					loaded.Enqueue(v);

			// If the discretionary queue has emptied, close completed frames and refill with all valid unloaded vessels
			if (unloaded.Count == 0)
			{
				double oldest = double.PositiveInfinity;
				foreach (var vd in DB.VesselDatas)
					if (vd.IsSimulated)
						oldest = Math.Min(oldest, vd.lastEvalUT);
				subStepSimJobs.ClearExpiredFrames(oldest);

				foreach (var v in FlightGlobals.VesselsUnloaded)
					if (v.TryGetVesselData(out VesselData vd) && vd.IsSimulated)
						unloaded.Enqueue(v);
			}
		}

		private void ValidateVesselData()
		{
			foreach (var v in FlightGlobals.Vessels)
			{
				if (!v.TryGetVesselData(out VesselData _))
				{
					// ignore vessels for which we never create a VesselData (flags)
					if (VesselData.VesselNeedVesselData(v.protoVessel))
					{
						Lib.LogDebug($"Creating VesselData for new vessel {v.vesselName}");
						DB.AddNewVesselData(new VesselData(v));
					}
				}
			}
		}

		#endregion

		#region Update and GUI

		void Update()
		{
			// attach map renderer to planetarium camera once
			if (MapView.MapIsEnabled && map_camera_script == null)
			map_camera_script = PlanetariumCamera.Camera.gameObject.AddComponent<MapCameraScript>();

			// process keyboard input
			Misc.KeyboardInput();

			// set part highlight colors
			Highlighter.Update();

			// prepare gui content
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.UI.Update");
			UI.Update(UIVisible);
			UnityEngine.Profiling.Profiler.EndSample();
		}

		void OnGUI()
		{
			UI.On_gui(UIVisible);
		}

		#endregion
	}

	public sealed class MapCameraScript : MonoBehaviour
	{
		void OnPostRender()
		{
			// do nothing when not in map view
			// - avoid weird situation when in some user installation MapIsEnabled is true in the space center
			if (!MapView.MapIsEnabled || HighLogic.LoadedScene == GameScenes.SPACECENTER)
				return;

			// commit all geometry
			Radiation.Render();

			// render all committed geometry
			LineRenderer.Render();
			ParticleRenderer.Render();
		}
	}

	// misc functions
	public static class Misc
	{
		public static void ClearLocks()
		{
			// remove control locks
			InputLockManager.RemoveControlLock("eva_dead_lock");
			InputLockManager.RemoveControlLock("no_signal_lock");
		}

		public static void KeyboardInput()
		{
			// mute/unmute messages with keyboard
			if (Input.GetKeyDown(KeyCode.Pause))
			{
				if (!Message.IsMuted())
				{
					Message.Post(Local.Messagesmuted, Local.Messagesmuted_subtext);//"Messages muted""Be careful out there"
					Message.Mute();
				}
				else
				{
					Message.Unmute();
					Message.Post(Local.Messagesunmuted);//"Messages unmuted"
				}
			}

			// toggle body info window with keyboard
			if (MapView.MapIsEnabled && Input.GetKeyDown(KeyCode.B))
			{
				UI.Open(BodyInfo.Body_info);
			}

			// call action scripts
			// - avoid creating vessel data for invalid vessels
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null) return;
			v.TryGetVesselDataTemp(out VesselData vd);
			if (!vd.IsSimulated) return;

			// call scripts with 1-5 key
			if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
			{ vd.computer.Execute(v, ScriptType.action1); }
			if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
			{ vd.computer.Execute(v, ScriptType.action2); }
			if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
			{ vd.computer.Execute(v, ScriptType.action3); }
			if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
			{ vd.computer.Execute(v, ScriptType.action4); }
			if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
			{ vd.computer.Execute(v, ScriptType.action5); }
		}
	}


} // KERBALISM
