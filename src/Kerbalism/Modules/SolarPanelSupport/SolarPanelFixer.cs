// TODO: DEPRECIATED : (very) partially re-implemented (only stock module support) as a ModuleHandler
// Keeping it as a reference until everything is re-implemented

/*

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using UnityEngine;
using KSP.Localization;
using System.Linq;
using EdyCommonTools;

namespace KERBALISM
{
	// - SSTU automation / better reliability support

	// This module is used to disable stock and other plugins solar panel EC output and provide specific support
	// EC must be produced using the resource cache, that give us correct behaviour independent from timewarp speed and vessel EC capacity.
	// To be able to support a custom module, we need to be able to do the following :
	// - (imperative) prevent the module from using the stock API calls to generate EC 
	// - (imperative) get the nominal rate at 1 AU
	// - (imperative) get the "suncatcher" transforms or vectors
	// - (imperative) get the "pivot" transforms or vectors if it's a tracking panel
	// - (imperative) get the "deployed" state if its a deployable panel.
	// - (imperative) get the "broken" state if the target module implement it
	// - (optional)   set the "deployed" state if its a deployable panel (both for unloaded and loaded vessels, with handling of the animation)
	// - (optional)   get the time effiency curve if its supported / defined
	// Notes :
	// - We don't support temperature efficiency curve
	// - We don't have any support for the animations, the target module must be able to keep handling them despite our hacks.
	// - Depending on how "hackable" the target module is, we use different approaches :
	//   either we disable the monobehavior and call the methods manually, or if possible we let it run and we just get/set what we need
	public class SolarPanelFixer : PartModule, IPlannerModule
	{
		#region Declarations
		/// <summary>Unit to show in the UI, this is the only configurable field for this module</summary>
		[KSPField]
		public string EcUIUnit = "EC/s";

		/// <summary>Main PAW info label</summary>
		[KSPField(guiActive = true, guiActiveEditor = false, guiName = "#KERBALISM_SolarPanelFixer_Solarpanel")]//Solar panel
		public string panelStatus = string.Empty;

		[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "#KERBALISM_SolarPanelFixer_Solarpaneloutput")]//Solar panel output
		[UI_Toggle(enabledText = "#KERBALISM_SolarPanelFixer_simulated", disabledText = "#KERBALISM_SolarPanelFixer_ignored")]//<color=#00ff00>simulated</color>""<color=#ffff00>ignored</color>
		public bool editorEnabled = true;

		/// <summary>nominal rate at 1 UA (Kerbin distance from the sun)</summary>
		[KSPField(isPersistant = true)]
		public double nominalRate = 10.0; // doing this on the purpose of not breaking existing saves

		/// <summary>aggregate efficiency factor for angle exposure losses and occlusion from parts</summary>
		[KSPField(isPersistant = true)]
		public double persistentFactor = 1.0; // doing this on the purpose of not breaking existing saves

		/// <summary>current state of the module</summary>
		[KSPField(isPersistant = true)]
		public PanelState state;

		/// <summary>tracked star/sun body index</summary>
		[KSPField(isPersistant = true)]
		public int trackedSunIndex = 0;

		/// <summary>has the player manually selected the star to be tracked ?</summary>
		[KSPField(isPersistant = true)]
		private bool manualTracking = false;

		/// <summary>
		/// Time based output degradation curve. Keys in hours, values in [0;1] range.
		/// Copied from the target solar panel module if supported and present.
		/// If defined in the SolarPanelFixer config, the target module curve will be overriden.
		/// </summary>
		[KSPField(isPersistant = true)]
		public FloatCurve timeEfficCurve;
		private static FloatCurve teCurve = null;
		private bool prefabDefinesTimeEfficCurve = false;

		/// <summary>UT of part creation in flight, used to evaluate the timeEfficCurve</summary>
		[KSPField(isPersistant = true)]
		public double launchUT = -1.0;

		/// <summary>internal object for handling the various hacks depending on the target solar panel module</summary>
		public SupportedPanel SolarPanel { get; private set; }

		/// <summary>current state of the module</summary>
		public bool isInitialized = false;

		/// <summary>for tracking analytic mode changes and ui updating</summary>
		private bool analyticSunlight;

		/// <summary>can be used by external mods to get the current EC/s</summary>
		[KSPField]
		public double currentOutput;

		// The following fields are local to FixedUpdate() but are shared for status string updates in Update()
		// Their value can be inconsistent, don't rely on them for anything else
		private double exposureFactor;
		private double wearFactor;
		private ExposureState exposureState;
		private string mainOccludingPart;
		private string rateFormat;
		private StringBuilder sb;

		public enum PanelState
		{
			Unknown = 0,
			Retracted,
			Extending,
			Extended,
			ExtendedFixed,
			Retracting,
			Static,
			Broken,
			Failure
		}

		public enum ExposureState
		{
			Disabled,
			Exposed,
			InShadow,
			OccludedTerrain,
			OccludedPart,
			BadOrientation
		}
		#endregion

		#region Init/Update methods

		[KSPEvent(active = true, guiActive = true, guiName = "#KERBALISM_SolarPanelFixer_Selecttrackedstar")]//Select tracked star
		public void ManualTracking()
		{
			// Assemble the buttons
			DialogGUIBase[] options = new DialogGUIBase[Sim.stars.Count + 1];
			options[0] = new DialogGUIButton(Local.SolarPanelFixer_Automatic, () => { manualTracking = false; }, true);//"Automatic"
			for (int i = 0; i < Sim.stars.Count; i++)
			{
				CelestialBody body = Sim.stars[i].body;
				options[i + 1] = new DialogGUIButton(body.bodyDisplayName.Replace("^N", ""), () =>
				{
					manualTracking = true;
					trackedSunIndex = body.flightGlobalsIndex;
					SolarPanel.SetTrackedBody(body);
				}, true);
			}

			PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new MultiOptionDialog(
				Local.SolarPanelFixer_SelectTrackingBody,//"SelectTrackingBody"
				Local.SolarPanelFixer_SelectTrackedstar_msg,//"Select the star you want to track with this solar panel."
				Local.SolarPanelFixer_Selecttrackedstar,//"Select tracked star"
				UISkinManager.GetSkin("MainMenuSkin"),
				options), false, UISkinManager.GetSkin("MainMenuSkin"));
		}

		public override void OnAwake()
		{
			if (teCurve == null) teCurve = new FloatCurve();
		}

		public override void OnLoad(ConfigNode node)
		{
			if (HighLogic.LoadedScene == GameScenes.LOADING)
				prefabDefinesTimeEfficCurve = node.HasNode("timeEfficCurve");
			if (SolarPanel == null && !GetSolarPanelModule())
				return;

			if (Lib.IsEditor) return;

			// apply states changes we have done trough automation
			if ((state == PanelState.Retracted || state == PanelState.Extended || state == PanelState.ExtendedFixed) && state != SolarPanel.GetState())
				SolarPanel.SetDeployedStateOnLoad(state);

			// apply reliability broken state and ensure we are correctly initialized (in case we are repaired mid-flight)
			// note : this rely on the fact that the reliability module is disabling the SolarPanelFixer monobehavior from OnStart, after OnLoad has been called
			if (!isEnabled)
			{
				ReliabilityEvent(true);
				Start();
			}
		}

		// we use Start() because it will be called later than OnStart(), which might help in having the target modules properly initialized.
		private void Start()
		{
			sb = new StringBuilder(256);

			// don't break tutorial scenarios
			// TODO : does this actually work ?
			if (Lib.DisableScenario(this)) return;

			if (SolarPanel == null && !GetSolarPanelModule())
			{
				isInitialized = true;
				return;
			}

			// disable everything if the target module data/logic acquisition has failed
			if (!SolarPanel.OnStart(isInitialized, ref nominalRate))
				enabled = isEnabled = moduleIsEnabled = false;

			isInitialized = true;

			if (!prefabDefinesTimeEfficCurve)
				timeEfficCurve = SolarPanel.GetTimeCurve();

			if (Lib.IsFlight && launchUT < 0.0)
				launchUT = Planetarium.GetUniversalTime();

			// setup star selection GUI
			Events["ManualTracking"].active = Sim.stars.Count > 1 && SolarPanel.IsTracking;
			Events["ManualTracking"].guiActive = state == PanelState.Extended || state == PanelState.ExtendedFixed || state == PanelState.Static;

			// setup target module animation for custom star tracking
			SolarPanel.SetTrackedBody(FlightGlobals.Bodies[trackedSunIndex]);

			// set how many decimal points are needed to show the panel Ec output in the UI
			if (nominalRate < 0.1) rateFormat = "F4";
			else if (nominalRate < 1.0) rateFormat = "F3";
			else if (nominalRate < 10.0) rateFormat = "F2";
			else rateFormat = "F1";
		}

		public override void OnSave(ConfigNode node)
		{
			// vessel can be null in OnSave (ex : on vessel creation)
			if (!Lib.IsFlight
				|| vessel == null
				|| !isInitialized
				|| SolarPanel == null
				|| !Lib.Landed(vessel)
				|| exposureState == ExposureState.Disabled) // don't to broken panels ! (issue #492)
				return;

			// get vessel data
			vessel.TryGetVesselDataTemp(out VesselData vd);

			// do nothing if vessel is invalid
			if (!vd.IsSimulated) return;

			// calculate average exposure over a full day when landed, will be used for panel background processing
			double landedPersistentFactor = GetAnalyticalCosineFactorLanded(vd);
			node.SetValue("persistentFactor", landedPersistentFactor);
			vd.SaveSolarPanelExposure(landedPersistentFactor);
		}

		public void Update()
		{
			// sanity check
			if (SolarPanel == null) return;

			// call Update specfic handling, if any
			SolarPanel.OnUpdate();

			// Do nothing else in the editor
			if (Lib.IsEditor) return;

			// Update tracked body selection button (Kopernicus multi-star support)
			if (Events["ManualTracking"].active && (state == PanelState.Extended || state == PanelState.ExtendedFixed || state == PanelState.Static))
			{
				Events["ManualTracking"].guiActive = true;
				Events["ManualTracking"].guiName = Lib.BuildString(Local.SolarPanelFixer_Trackedstar +" ", manualTracking ? ": " : Local.SolarPanelFixer_AutoTrack, FlightGlobals.Bodies[trackedSunIndex].bodyDisplayName.Replace("^N", ""));//"Tracked star"[Auto] : "
			}
			else
			{
				Events["ManualTracking"].guiActive = false;
			}

			// Update main status field visibility
			if (state == PanelState.Failure || state == PanelState.Unknown)
				Fields["panelStatus"].guiActive = false;
			else
				Fields["panelStatus"].guiActive = true;

			// Update main status field text
			switch (exposureState)
			{
				case ExposureState.InShadow:
					panelStatus = "<color=#ff2222>"+Local.SolarPanelFixer_inshadow +"</color>";//in shadow
					if (currentOutput > 0.001) panelStatus = Lib.BuildString(currentOutput.ToString(rateFormat), " ", EcUIUnit, ", ", panelStatus);
					break;
				case ExposureState.OccludedTerrain:
					panelStatus = "<color=#ff2222>"+Local.SolarPanelFixer_occludedbyterrain +"</color>";//occluded by terrain
					if (currentOutput > 0.001) panelStatus = Lib.BuildString(currentOutput.ToString(rateFormat), " ", EcUIUnit, ", ", panelStatus);
					break;
				case ExposureState.OccludedPart:
					panelStatus = Lib.BuildString("<color=#ff2222>", Local.SolarPanelFixer_occludedby.Format(mainOccludingPart), "</color>");//occluded by 
					if (currentOutput > 0.001) panelStatus = Lib.BuildString(currentOutput.ToString(rateFormat), " ", EcUIUnit, ", ", panelStatus);
					break;
				case ExposureState.BadOrientation:
					panelStatus = "<color=#ff2222>"+Local.SolarPanelFixer_badorientation +"</color>";//bad orientation
					if (currentOutput > 0.001) panelStatus = Lib.BuildString(currentOutput.ToString(rateFormat), " ", EcUIUnit, ", ", panelStatus);
					break;
				case ExposureState.Disabled:
					switch (state)
					{
						case PanelState.Retracted: panelStatus = Local.SolarPanelFixer_retracted; break;//"retracted"
						case PanelState.Extending: panelStatus = Local.SolarPanelFixer_extending; break;//"extending"
						case PanelState.Retracting: panelStatus = Local.SolarPanelFixer_retracting; break;//"retracting"
						case PanelState.Broken: panelStatus = Local.SolarPanelFixer_broken; break;//"broken"
						case PanelState.Failure: panelStatus = Local.SolarPanelFixer_failure; break;//"failure"
						case PanelState.Unknown: panelStatus = Local.SolarPanelFixer_invalidstate; break;//"invalid state"
					}
					break;
				case ExposureState.Exposed:
					sb.Length = 0;
					sb.Append(currentOutput.ToString(rateFormat));
					sb.Append(" ");
					sb.Append(EcUIUnit);
					if (analyticSunlight)
					{
						sb.Append(", ");
						sb.Append(Local.SolarPanelFixer_analytic);//analytic
						sb.Append(" ");
						sb.Append(persistentFactor.ToString("P0"));
					}
					else
					{
						sb.Append(", ");
						sb.Append(Local.SolarPanelFixer_exposure);//exposure
						sb.Append(" ");
						sb.Append(exposureFactor.ToString("P0"));
					}
					if (wearFactor < 1.0)
					{
						sb.Append(", ");
						sb.Append(Local.SolarPanelFixer_wear);//wear
						sb.Append(" : ");
						sb.Append((1.0 - wearFactor).ToString("P0"));
					}
					panelStatus = sb.ToString();
					break;
			}
		}

		public void FixedUpdate()
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.SolarPanelFixer.FixedUpdate");
			// sanity check
			if (SolarPanel == null)
			{
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			// can't produce anything if not deployed, broken, etc
			PanelState newState = SolarPanel.GetState();
			if (state != newState)
			{
				state = newState;
				if (Lib.IsEditor && (newState == PanelState.Extended || newState == PanelState.ExtendedFixed || newState == PanelState.Retracted))
					Lib.RefreshPlanner();
			}

			if (!(state == PanelState.Extended || state == PanelState.ExtendedFixed || state == PanelState.Static))
			{
				exposureState = ExposureState.Disabled;
				currentOutput = 0.0;
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			// do nothing else in editor
			if (Lib.IsEditor)
			{
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			// get vessel data from cache
			vessel.TryGetVesselDataTemp(out VesselData vd);

			// do nothing if vessel is invalid
			if (!vd.IsSimulated)
			{
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			// Update tracked sun in auto mode
			if (!manualTracking && trackedSunIndex != vd.MainStar.Star.body.flightGlobalsIndex)
			{
				trackedSunIndex = vd.MainStar.Star.body.flightGlobalsIndex;
				SolarPanel.SetTrackedBody(vd.MainStar.Star.body);
			}

			StarFlux trackedSunInfo = vd.StarsIrradiance.First(p => p.Star.body.flightGlobalsIndex == trackedSunIndex);

			if (trackedSunInfo.sunlightFactor == 0.0)
				exposureState = ExposureState.InShadow;
			else
				exposureState = ExposureState.Exposed;

#if DEBUG_SOLAR
			Vector3d sunDirDebug = trackedSunInfo.direction;

			// flight view sun dir
			DebugDrawer.DebugLine(vessel.transform.position, vessel.transform.position + (sunDirDebug * 100.0), Color.red);

			// GetAnalyticalCosineFactorLanded() map view debugging
			Vector3d sunCircle = Vector3d.Cross(Vector3d.left, sunDirDebug);
			Quaternion qa = Quaternion.AngleAxis(45, sunCircle);
			LineRenderer.CommitWorldVector(vessel.GetWorldPos3D(), sunCircle, 500f, Color.red);
			LineRenderer.CommitWorldVector(vessel.GetWorldPos3D(), sunDirDebug, 500f, Color.yellow);
			for (int i = 0; i < 7; i++)
			{
				sunDirDebug = qa * sunDirDebug;
				LineRenderer.CommitWorldVector(vessel.GetWorldPos3D(), sunDirDebug, 500f, Color.green);
			}
#endif

			if (vd.IsSubstepping)
			{
				// if we are switching to analytic mode and the vessel is landed, get an average exposure over a day
				// TODO : maybe check the rotation speed of the body, this might be inaccurate for tidally-locked bodies (test on the mun ?)
				if (!analyticSunlight && Lib.Landed(vessel)) persistentFactor = GetAnalyticalCosineFactorLanded(vd);
				analyticSunlight = true;
			}
			else
			{
				analyticSunlight = false;
			}

			// cosine / occlusion factor isn't updated when in analyticalSunlight / unloaded states :
			// - evaluting sun_dir / vessel orientation gives random results resulting in inaccurate behavior / random EC rates
			// - using the last calculated factor is a satisfactory simulation of a sun relative vessel attitude keeping behavior
			//   without all the complexity of actually doing it
			if (analyticSunlight)
			{
				exposureFactor = persistentFactor;
			}
			else
			{
				// reset factors
				persistentFactor = 0.0;
				exposureFactor = 0.0;

				// iterate over all stars, compute the exposure factor
				foreach (StarFlux star in vd.StarsIrradiance)
				{
					double sunCosineFactor = 0.0;
					double sunOccludedFactor = 0.0;
					string occludingPart = null;

					// Get the cosine factor (alignement between the sun and the panel surface)
					sunCosineFactor = SolarPanel.GetCosineFactor(star.direction);

					if (sunCosineFactor == 0.0)
					{
						// If this is the tracked sun and the panel is not oriented toward the sun, update the gui info string.
						if (star == trackedSunInfo)
							exposureState = ExposureState.BadOrientation;
					}
					else
					{
						// The panel is oriented toward the sun, do a physic raycast to check occlusion from parts, terrain, buildings...
						sunOccludedFactor = SolarPanel.GetOccludedFactor(star.direction, out occludingPart, star != trackedSunInfo);

						// If this is the tracked sun and the panel is occluded, update the gui info string. 
						if (star == trackedSunInfo && sunOccludedFactor == 0.0)
						{
							if (occludingPart != null)
							{
								exposureState = ExposureState.OccludedPart;
								mainOccludingPart = Lib.EllipsisMiddle(occludingPart, 15);
							}
							else
							{
								exposureState = ExposureState.OccludedTerrain;
							}
						}
					}

					// Compute final aggregate exposure factor
					double sunExposureFactor = sunCosineFactor * sunOccludedFactor * star.directRawFluxProportion;

					// Add the final factor to the saved exposure factor to be used in analytical / unloaded states.
					// If occlusion is from the scene, not a part (terrain, building...) don't save the occlusion factor,
					// as occlusion from the terrain and static objects is too variable over time.
					if (occludingPart != null)
						persistentFactor += sunExposureFactor;
					else
						persistentFactor += sunCosineFactor * star.directRawFluxProportion;

					// Only apply the exposure factor if not in shadow (body occlusion check)
					if (star.sunlightFactor == 1.0) exposureFactor += sunExposureFactor;
					else if (star == trackedSunInfo) exposureState = ExposureState.InShadow;
				}
				vd.SaveSolarPanelExposure(persistentFactor);
			}

			// get solar flux and deduce a scalar based on nominal flux at 1AU
			// - this include atmospheric absorption if inside an atmosphere
			// - at high timewarps speeds, atmospheric absorption is analytical (integrated over a full revolution)
			double distanceFactor = vd.IrradianceStarTotal / Sim.SolarFluxAtHome;

			// get wear factor (time based output degradation)
			wearFactor = 1.0;
			if (timeEfficCurve?.Curve.keys.Length > 1)
				wearFactor = Lib.Clamp(timeEfficCurve.Evaluate((float)((Planetarium.GetUniversalTime() - launchUT) / 3600.0)), 0.0, 1.0);

			// get final output rate in EC/s
			currentOutput = nominalRate * wearFactor * distanceFactor * exposureFactor;

			// ignore very small outputs
			if (currentOutput < 1e-10)
			{
				currentOutput = 0.0;
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			// produce EC
			vd.ResHandler.ElectricCharge.Produce(currentOutput * Kerbalism.elapsed_s, RecipeCategory.SolarPanel);
			UnityEngine.Profiling.Profiler.EndSample();
		}

		public static void BackgroundUpdate(Vessel v, ProtoPartModuleSnapshot m, SolarPanelFixer prefab, VesselData vd, VesselResource ec, double elapsed_s)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.SolarPanelFixer.BackgroundUpdate");
			// this is ugly spaghetti code but initializing the prefab at loading time is messy because the targeted solar panel module may not be loaded yet
			if (!prefab.isInitialized) prefab.OnStartFinished(StartState.None);

			string state = Lib.Proto.GetString(m, "state");
			if (!(state == "Static" || state == "Extended" || state == "ExtendedFixed"))
			{
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			// We don't recalculate panel orientation factor for unloaded vessels :
			// - this ensure output consistency and prevent timestep-dependant fluctuations
			// - the player has no way to keep an optimal attitude while unloaded
			// - it's a good way of simulating sun-relative attitude keeping 
			// - it's fast and easy
			double efficiencyFactor = Lib.Proto.GetDouble(m, "persistentFactor");

			// calculate normalized solar flux factor
			// - this include atmospheric absorption if inside an atmosphere
			// - this is zero when the vessel is in shadow when evaluation is non-analytic (low timewarp rates)
			// - if integrated over orbit (analytic evaluation), this include fractional sunlight / atmo absorbtion
			efficiencyFactor *= vd.IrradianceStarTotal / Sim.SolarFluxAtHome;

			// get wear factor (output degradation with time)
			if (m.moduleValues.HasNode("timeEfficCurve"))
			{
				teCurve.Load(m.moduleValues.GetNode("timeEfficCurve"));
				double launchUT = Lib.Proto.GetDouble(m, "launchUT");
				efficiencyFactor *= Lib.Clamp(teCurve.Evaluate((float)((Planetarium.GetUniversalTime() - launchUT) / 3600.0)), 0.0, 1.0);
			}

			// get nominal panel charge rate at 1 AU
			// don't use the prefab value as some modules that does dynamic switching (SSTU) may have changed it
			double nominalRate = Lib.Proto.GetDouble(m, "nominalRate");

			// calculate output
			double output = nominalRate * efficiencyFactor;

			// produce EC
			ec.Produce(output * elapsed_s, RecipeCategory.SolarPanel);
			UnityEngine.Profiling.Profiler.EndSample();
		}

		public void PlannerUpdate(VesselResHandler resHandler, VesselDataShip vesselData)
		{
			if (part.editorStarted && isInitialized && isEnabled && editorEnabled)
			{
				double editorOutput = 0.0;
				switch (Planner.Planner.Sunlight)
				{
					case Planner.Planner.SunlightState.SunlightNominal:
						editorOutput = nominalRate * (vesselData.IrradianceStarTotal / Sim.SolarFluxAtHome);
						if (editorOutput > 0.0) resHandler.ElectricCharge.Produce(editorOutput, RecipeCategory.GetOrCreate("solar panel (nominal)", RecipeCategory.BrokerCategory.SolarPanel, "solar panel (nominal)"));
						break;
					case Planner.Planner.SunlightState.SunlightSimulated:
						// create a sun direction according to the shadows direction in the VAB / SPH
						Vector3d sunDir = EditorDriver.editorFacility == EditorFacility.VAB ? new Vector3d(1.0, 1.0, 0.0).normalized : new Vector3d(0.0, 1.0, -1.0).normalized;
						double effiencyFactor = SolarPanel.GetCosineFactor(sunDir, true) * SolarPanel.GetOccludedFactor(sunDir, out string occludingPart, true);
						double distanceFactor = vesselData.IrradianceStarTotal / Sim.SolarFluxAtHome;
						editorOutput = nominalRate * effiencyFactor * distanceFactor;
						if (editorOutput > 0.0) resHandler.ElectricCharge.Produce(editorOutput, RecipeCategory.GetOrCreate("solar panel (estimated)", RecipeCategory.BrokerCategory.SolarPanel, "solar panel (estimated)"));
						break;
				}
			}
		}

		#endregion

		#region Other methods
		public bool GetSolarPanelModule()
		{
			// handle the possibility of multiple solar panel and SolarPanelFixer modules on the part
			List<SolarPanelFixer> fixerModules = new List<SolarPanelFixer>();
			foreach (PartModule pm in part.Modules)
			{
				if (pm is SolarPanelFixer fixerModule)
					fixerModules.Add(fixerModule);
			}

			// find the module based on explicitely supported modules
			foreach (PartModule pm in part.Modules)
			{
				if (fixerModules.Exists(p => p.SolarPanel != null && p.SolarPanel.TargetModule == pm))
					continue;

				// mod supported modules
				switch (pm.moduleName)
				{
					case "ModuleCurvedSolarPanel": SolarPanel = new NFSCurvedPanel(); break;
					case "SSTUSolarPanelStatic": SolarPanel = new SSTUStaticPanel();  break;
					case "SSTUSolarPanelDeployable": SolarPanel = new SSTUVeryComplexPanel(); break;
					case "SSTUModularPart": SolarPanel = new SSTUVeryComplexPanel(); break;
					case "ModuleROSolar": SolarPanel = new ROConfigurablePanel(); break;
					case "KopernicusSolarPanel":
					case "KopernicusSolarPanels":
						Lib.Log("Part '" + part.partInfo.title + "' use the KopernicusSolarPanel module, please remove it from your config. Kerbalism has it's own support for Kopernicus", Lib.LogLevel.Warning);
						continue;
					default:
						if (pm is ModuleDeployableSolarPanel)
							SolarPanel = new StockPanel(); break;
				}

				if (SolarPanel != null)
				{
					SolarPanel.OnLoad(this, pm);
					break;
				}
			}

			if (SolarPanel == null)
			{
				Lib.Log("Could not find a supported solar panel module, disabling SolarPanelFixer module...", Lib.LogLevel.Warning);
				enabled = isEnabled = moduleIsEnabled = false;
				return false;
			}

			return true;
		}

		private static PanelState GetProtoState(ProtoPartModuleSnapshot protoModule)
		{
			return (PanelState)Enum.Parse(typeof(PanelState), Lib.Proto.GetString(protoModule, "state"));
		}

		private static void SetProtoState(ProtoPartModuleSnapshot protoModule, PanelState newState)
		{
			Lib.Proto.Set(protoModule, "state", newState.ToString());
		}

		public static void ProtoToggleState(SolarPanelFixer prefab, ProtoPartModuleSnapshot protoModule, PanelState currentState)
		{
			switch (currentState)
			{
				case PanelState.Retracted:
					if (prefab.SolarPanel.IsRetractable()) { SetProtoState(protoModule, PanelState.Extended); return; }
					SetProtoState(protoModule, PanelState.ExtendedFixed); return;
				case PanelState.Extended: SetProtoState(protoModule, PanelState.Retracted); return;
			}
		}

		public void ToggleState()
		{
			SolarPanel.ToggleState(state);
		}

		public void ReliabilityEvent(bool isBroken)
		{
			state = isBroken ? PanelState.Failure : SolarPanel.GetState();
			SolarPanel.Break(isBroken);
		}

		private double GetAnalyticalCosineFactorLanded(VesselData vd)
		{
			double finalFactor = 0.0;
			foreach (StarFlux star in vd.StarsIrradiance)
			{
				Vector3d sunDir = star.direction;
				// get a rotation of 45?? perpendicular to the sun direction
				Quaternion sunRot = Quaternion.AngleAxis(45, Vector3d.Cross(Vector3d.left, sunDir));

				double factor = 0.0;
				string occluding;
				for (int i = 0; i < 8; i++)
				{
					sunDir = sunRot * sunDir;
					factor += SolarPanel.GetCosineFactor(sunDir, true);
					factor += SolarPanel.GetOccludedFactor(sunDir, out occluding, true);
				}
				factor /= 16.0;
				finalFactor += factor * star.directRawFluxProportion;
			}
			return finalFactor;
		}

		public static double GetSolarPanelsAverageExposure(List<double> exposures)
		{
			if (exposures.Count == 0) return -1.0;
			double averageExposure = 0.0;
			foreach (double exposure in exposures) averageExposure += exposure;
			return averageExposure / exposures.Count;
		}
		#endregion

		#region Abstract class for common interaction with supported PartModules
		public abstract class SupportedPanel 
		{
			/// <summary>Reference to the SolarPanelFixer, must be set from OnLoad</summary>
			protected SolarPanelFixer fixerModule;

			/// <summary>Reference to the target module</summary>
			public abstract PartModule TargetModule { get; }

			/// <summary>
			/// Will be called by the SolarPanelFixer OnLoad, must set the partmodule reference.
			/// GetState() must be able to return the correct state after this has been called
			/// </summary>
			public abstract void OnLoad(SolarPanelFixer fixerModule, PartModule targetModule);

			/// <summary> Main inititalization method called from OnStart, every hack we do must be done here (In particular the one preventing the target module from generating EC)</summary>
			/// <param name="initialized">will be true if the method has already been called for this module (OnStart can be called multiple times in the editor)</param>
			/// <param name="nominalRate">nominal rate at 1AU</param>
			/// <returns>must return false is something has gone wrong, will disable the whole module</returns>
			public abstract bool OnStart(bool initialized, ref double nominalRate);

			/// <summary>Must return a [0;1] scalar evaluating the local occlusion factor (usually with a physic raycast already done by the target module)</summary>
			/// <param name="occludingPart">if the occluding object is a part, name of the part. MUST return null in all other cases.</param>
			/// <param name="analytic">if true, the returned scalar must account for the given sunDir, so we can't rely on the target module own raycast</param>
			public abstract double GetOccludedFactor(Vector3d sunDir, out string occludingPart, bool analytic = false);

			/// <summary>Must return a [0;1] scalar evaluating the angle of the given sunDir on the panel surface (usually a dot product clamped to [0;1])</summary>
			/// <param name="analytic">if true and the panel is orientable, the returned scalar must be the best possible output (must use the rotation around the pivot)</param>
			public abstract double GetCosineFactor(Vector3d sunDir, bool analytic = false);

			/// <summary>must return the state of the panel, must be able to work before OnStart has been called</summary>
			public abstract PanelState GetState();

			/// <summary>Can be overridden if the target module implement a time efficiency curve. Keys are in hours, values are a scalar in the [0:1] range.</summary>
			public virtual FloatCurve GetTimeCurve() { return new FloatCurve(new Keyframe[] { new Keyframe(0f, 1f) }); }

			/// <summary>Called at Update(), can contain target module specific hacks</summary>
			public virtual void OnUpdate() { }

			/// <summary>Is the panel a sun-tracking panel</summary>
			public virtual bool IsTracking => false;

			/// <summary>Kopernicus stars support : must set the animation tracked body</summary>
			public virtual void SetTrackedBody(CelestialBody body) { }

			/// <summary>Reliability : specific hacks for the target module that must be applied when the panel is disabled by a failure</summary>
			public virtual void Break(bool isBroken) { }

			/// <summary>Automation : override this with "return false" if the module doesn't support automation when loaded</summary>
			public virtual bool SupportAutomation(PanelState state)
			{
				switch (state)
				{
					case PanelState.Retracted:
					case PanelState.Extending:
					case PanelState.Extended:
					case PanelState.Retracting:
						return true;
					default:
						return false;
				}
			}

			/// <summary>Automation : override this with "return false" if the module doesn't support automation when unloaded</summary>
			public virtual bool SupportProtoAutomation(ProtoPartModuleSnapshot protoModule)
			{
				switch (Lib.Proto.GetString(protoModule, "state"))
				{
					case "Retracted":
					case "Extended":
						return true;
					default:
						return false;
				}
			}

			/// <summary>Automation : this must work when called on the prefab module</summary>
			public virtual bool IsRetractable() { return false; }

			/// <summary>Automation : must be implemented if the panel is extendable</summary>
			public virtual void Extend() { }

			/// <summary>Automation : must be implemented if the panel is retractable</summary>
			public virtual void Retract() { }

			///<summary>Automation : Called OnLoad, must set the target module persisted extended/retracted fields to reflect changes done trough automation while unloaded</summary>
			public virtual void SetDeployedStateOnLoad(PanelState state) { }

			///<summary>Automation : convenience method</summary>
			public void ToggleState(PanelState state)
			{
				switch (state)
				{
					case PanelState.Retracted: Extend(); return;
					case PanelState.Extended: Retract(); return;
				}
			}
		}

		private abstract class SupportedPanel<T> : SupportedPanel where T : PartModule
		{
			public T panelModule;
			public override PartModule TargetModule => panelModule;
		}
		#endregion

		#region Stock module support (ModuleDeployableSolarPanel)
		// stock solar panel module support
		// - we don't support the temperatureEfficCurve
		// - we override the stock UI
		// - we still reuse most of the stock calculations
		// - we let the module fixedupdate/update handle animations/suncatching
		// - we prevent stock EC generation by reseting the reshandler rate
		// - we don't support cylindrical/spherical panel types
		private class StockPanel : SupportedPanel<ModuleDeployableSolarPanel>
		{
			private Transform sunCatcherPosition;   // middle point of the panel surface (usually). Use only position, panel surface direction depend on the pivot transform, even for static panels.
			private Transform sunCatcherPivot;      // If it's a tracking panel, "up" is the pivot axis and "position" is the pivot position. In any case "forward" is the panel surface normal.

			public override void OnLoad(SolarPanelFixer fixerModule, PartModule targetModule)
			{
				this.fixerModule = fixerModule;
				panelModule = (ModuleDeployableSolarPanel)targetModule;
			}

			public override bool OnStart(bool initialized, ref double nominalRate)
			{
				// hide stock ui
				panelModule.Fields["sunAOA"].guiActive = false;
				panelModule.Fields["flowRate"].guiActive = false;
				panelModule.Fields["status"].guiActive = false;

				if (sunCatcherPivot == null)
					sunCatcherPivot = panelModule.part.FindModelComponent<Transform>(panelModule.pivotName);
				if (sunCatcherPosition == null)
					sunCatcherPosition = panelModule.part.FindModelTransform(panelModule.secondaryTransformName);

				if (sunCatcherPosition == null)
				{
					Lib.Log("Could not find suncatcher transform `{0}` in part `{1}`", Lib.LogLevel.Error, panelModule.secondaryTransformName, panelModule.part.name);
					return false;
				}

				// avoid rate lost due to OnStart being called multiple times in the editor
				if (panelModule.resHandler.outputResources[0].rate == 0.0)
					return true;

				nominalRate = panelModule.resHandler.outputResources[0].rate;
				// reset target module rate
				// - This can break mods that evaluate solar panel output for a reason or another (eg: AmpYear, BonVoyage).
				//   We fix that by exploiting the fact that resHandler was introduced in KSP recently, and most of
				//   these mods weren't updated to reflect the changes or are not aware of them, and are still reading
				//   chargeRate. However the stock solar panel ignore chargeRate value during FixedUpdate.
				//   So we only reset resHandler rate.
				panelModule.resHandler.outputResources[0].rate = 0.0;

				return true;
			}

			// akwardness award : stock timeEfficCurve use 24 hours days (1/(24*60/60)) as unit for the curve keys, we convert that to hours
			public override FloatCurve GetTimeCurve()
			{

				if (panelModule.timeEfficCurve?.Curve.keys.Length > 1)
				{
					FloatCurve timeCurve = new FloatCurve();
					foreach (Keyframe key in panelModule.timeEfficCurve.Curve.keys)
						timeCurve.Add(key.time * 24f, key.value, key.inTangent * (1f / 24f), key.outTangent * (1f / 24f));
					return timeCurve;
				}
				return base.GetTimeCurve();
			}

			// detect occlusion from the scene colliders using the stock module physics raycast, or our own if analytic mode = true
			public override double GetOccludedFactor(Vector3d sunDir, out string occludingPart, bool analytic = false)
			{
				double occludingFactor = 1.0;
				occludingPart = null;
				RaycastHit raycastHit;
				if (analytic)
				{
					if (sunCatcherPosition == null)
						sunCatcherPosition = panelModule.part.FindModelTransform(panelModule.secondaryTransformName);

					Physics.Raycast(sunCatcherPosition.position + (sunDir * panelModule.raycastOffset), sunDir, out raycastHit, 10000f);
				}
				else
				{
					raycastHit = panelModule.hit;
				}

				if (raycastHit.collider != null)
				{
					Part blockingPart = Part.GetComponentUpwards<Part>(raycastHit.collider.gameObject);
					if (blockingPart != null)
					{
						// avoid panels from occluding themselves
						if (blockingPart == panelModule.part)
							return occludingFactor;

						occludingPart = blockingPart.partInfo.title;
					}
					occludingFactor = 0.0;
				}
				return occludingFactor;
			}

			// we use the current panel orientation, only doing it ourself when analytic = true
			public override double GetCosineFactor(Vector3d sunDir, bool analytic = false)
			{
#if DEBUG_SOLAR
				DebugDrawer.DebugLine(sunCatcherPosition.position, sunCatcherPosition.position + sunCatcherPivot.forward, Color.yellow);
				if (panelModule.isTracking) DebugDrawer.DebugLine(sunCatcherPivot.position, sunCatcherPivot.position + (sunCatcherPivot.up * -1f), Color.blue);
#endif
				switch (panelModule.panelType)
				{
					case ModuleDeployableSolarPanel.PanelType.FLAT:
						if (!analytic)
							return Math.Max(Vector3d.Dot(sunDir, panelModule.trackingDotTransform.forward), 0.0);

						if (panelModule.isTracking)
							return Math.Cos(1.57079632679 - Math.Acos(Vector3d.Dot(sunDir, sunCatcherPivot.up)));
						else
							return Math.Max(Vector3d.Dot(sunDir, sunCatcherPivot.forward), 0.0);

					case ModuleDeployableSolarPanel.PanelType.CYLINDRICAL:
						return Math.Max((1.0 - Math.Abs(Vector3d.Dot(sunDir, panelModule.trackingDotTransform.forward))) * (1.0 / Math.PI), 0.0);
					case ModuleDeployableSolarPanel.PanelType.SPHERICAL:
						return 0.25;
					default:
						return 0.0;
				}
			}

			public override PanelState GetState()
			{
				// Detect modified TotalEnergyRate (B9PS switching of the stock module or ROSolar built-in switching)
				if (panelModule.resHandler.outputResources[0].rate != 0.0)
				{
					OnStart(false, ref fixerModule.nominalRate);
				}

				if (!panelModule.useAnimation)
				{
					if (panelModule.deployState == ModuleDeployablePart.DeployState.BROKEN)
						return PanelState.Broken;

					return PanelState.Static;
				}

				switch (panelModule.deployState)
				{
					case ModuleDeployablePart.DeployState.EXTENDED:
						if (!IsRetractable()) return PanelState.ExtendedFixed;
						return PanelState.Extended;
					case ModuleDeployablePart.DeployState.RETRACTED: return PanelState.Retracted;
					case ModuleDeployablePart.DeployState.RETRACTING: return PanelState.Retracting;
					case ModuleDeployablePart.DeployState.EXTENDING: return PanelState.Extending;
					case ModuleDeployablePart.DeployState.BROKEN: return PanelState.Broken;
				}
				return PanelState.Unknown;
			}

			public override void SetDeployedStateOnLoad(PanelState state)
			{
				switch (state)
				{
					case PanelState.Retracted:
						panelModule.deployState = ModuleDeployablePart.DeployState.RETRACTED;
						break;
					case PanelState.Extended:
					case PanelState.ExtendedFixed:
						panelModule.deployState = ModuleDeployablePart.DeployState.EXTENDED;
						break;
				}
			}

			public override void Extend() { panelModule.Extend(); }

			public override void Retract() { panelModule.Retract(); }

			public override bool IsRetractable() { return panelModule.retractable; }

			public override void Break(bool isBroken)
			{
				// reenable the target module
				panelModule.isEnabled = !isBroken;
				panelModule.enabled = !isBroken;
				if (isBroken) panelModule.part.FindModelComponents<Animation>().ForEach(k => k.Stop()); // stop the animations if we are disabling it
			}

			public override bool IsTracking => panelModule.isTracking;

			public override void SetTrackedBody(CelestialBody body)
			{
				panelModule.trackingBody = body;
				panelModule.GetTrackingBodyTransforms();
			}

			public override void OnUpdate()
			{
				panelModule.flowRate = (float)fixerModule.currentOutput;
			}
		}
#endregion

		#region Near Future Solar support (ModuleCurvedSolarPanel)
		// Near future solar curved panel support
		// - We prevent the NFS module from running (disabled at MonoBehavior level)
		// - We replicate the behavior of its FixedUpdate()
		// - We call its Update() method but we disable the KSPFields UI visibility.
		private class NFSCurvedPanel : SupportedPanel<PartModule>
		{
			private Transform[] sunCatchers;    // model transforms named after the "PanelTransformName" field
			private bool deployable;            // "Deployable" field
			private Action panelModuleUpdate;   // delegate for the module Update() method

			public override void OnLoad(SolarPanelFixer fixerModule, PartModule targetModule)
			{
				this.fixerModule = fixerModule;
				panelModule = targetModule;
				deployable = Lib.ReflectionValue<bool>(panelModule, "Deployable");
			}

			public override bool OnStart(bool initialized, ref double nominalRate)
			{
#if !DEBUG_SOLAR
				try
				{
#endif
					// get a delegate for Update() method (avoid performance penality of reflection)
					panelModuleUpdate = (Action)Delegate.CreateDelegate(typeof(Action), panelModule, "Update");

					// since we are disabling the MonoBehavior, ensure the module Start() has been called
					Lib.ReflectionCall(panelModule, "Start");

					// get transform name from module
					string transform_name = Lib.ReflectionValue<string>(panelModule, "PanelTransformName");

					// get panel components
					sunCatchers = panelModule.part.FindModelTransforms(transform_name);
					if (sunCatchers.Length == 0) return false;

					// disable the module at the Unity level, we will handle its updates manually
					panelModule.enabled = false;

					// return panel nominal rate
					nominalRate = Lib.ReflectionValue<float>(panelModule, "TotalEnergyRate");

					return true;
#if !DEBUG_SOLAR
				}
				catch (Exception ex) 
				{
					Lib.Log("SolarPanelFixer : exception while getting ModuleCurvedSolarPanel data : " + ex.Message);
					return false;
				}
#endif
			}

			public override double GetOccludedFactor(Vector3d sunDir, out string occludingPart, bool analytic = false)
			{
				double occludedFactor = 1.0;
				occludingPart = null;

				RaycastHit raycastHit;
				foreach (Transform panel in sunCatchers)
				{
					if (Physics.Raycast(panel.position + (sunDir * 0.25), sunDir, out raycastHit, 10000f))
					{
						if (occludingPart == null && raycastHit.collider != null)
						{
							Part blockingPart = Part.GetComponentUpwards<Part>(raycastHit.transform.gameObject);
							if (blockingPart != null)
							{
								// avoid panels from occluding themselves
								if (blockingPart == panelModule.part)
									continue;

								occludingPart = blockingPart.partInfo.title;
							}
							occludedFactor -= 1.0 / sunCatchers.Length;
						}
					}
				}

				if (occludedFactor < 1E-5) occludedFactor = 0.0;
				return occludedFactor;
			}

			public override double GetCosineFactor(Vector3d sunDir, bool analytic = false)
			{
				double cosineFactor = 0.0;

				foreach (Transform panel in sunCatchers)
				{
					cosineFactor += Math.Max(Vector3d.Dot(sunDir, panel.forward), 0.0);
#if DEBUG_SOLAR
					DebugDrawer.DebugLine(panel.position, panel.position + panel.forward, Color.yellow);
#endif
				}

				return cosineFactor / sunCatchers.Length;
			}

			public override void OnUpdate()
			{
				// manually call the module Update() method since we have disabled the unity Monobehavior
				panelModuleUpdate();

				// hide ui fields
				foreach (BaseField field in panelModule.Fields)
				{
					field.guiActive = false;
				}
			}

			public override PanelState GetState()
			{
				// Detect modified TotalEnergyRate (B9PS switching of the target module)
				double newrate = Lib.ReflectionValue<float>(panelModule, "TotalEnergyRate");
				if (newrate != fixerModule.nominalRate)
				{
					OnStart(false, ref fixerModule.nominalRate);
				}

				string stateStr = Lib.ReflectionValue<string>(panelModule, "SavedState");
				Type enumtype = typeof(ModuleDeployablePart.DeployState);
				if (!Enum.IsDefined(enumtype, stateStr))
				{
					if (!deployable) return PanelState.Static;
					return PanelState.Unknown;
				}

				ModuleDeployablePart.DeployState state = (ModuleDeployablePart.DeployState)Enum.Parse(enumtype, stateStr);

				switch (state)
				{
					case ModuleDeployablePart.DeployState.EXTENDED:
						if (!deployable) return PanelState.Static;
						return PanelState.Extended;
					case ModuleDeployablePart.DeployState.RETRACTED: return PanelState.Retracted;
					case ModuleDeployablePart.DeployState.RETRACTING: return PanelState.Retracting;
					case ModuleDeployablePart.DeployState.EXTENDING: return PanelState.Extending;
					case ModuleDeployablePart.DeployState.BROKEN: return PanelState.Broken;
				}
				return PanelState.Unknown;
			}

			public override void SetDeployedStateOnLoad(PanelState state)
			{
				switch (state)
				{
					case PanelState.Retracted:
						Lib.ReflectionValue(panelModule, "SavedState", "RETRACTED");
						break;
					case PanelState.Extended:
						Lib.ReflectionValue(panelModule, "SavedState", "EXTENDED");
						break;
				}
			}

			public override void Extend() { Lib.ReflectionCall(panelModule, "DeployPanels"); }

			public override void Retract() { Lib.ReflectionCall(panelModule, "RetractPanels"); }

			public override bool IsRetractable() { return true; }

			public override void Break(bool isBroken)
			{
				// in any case, the monobehavior stays disabled
				panelModule.enabled = false;
				if (isBroken)
					panelModule.isEnabled = false; // hide the extend/retract UI
				else
					panelModule.isEnabled = true; // show the extend/retract UI
			}
		}
		#endregion

		#region SSTU static multi-panel module support (SSTUSolarPanelStatic)
		// - We prevent the module from running (disabled at MonoBehavior level and KSP level)
		// - We replicate the behavior by ourselves
		private class SSTUStaticPanel : SupportedPanel<PartModule>
		{
			private Transform[] sunCatchers;    // model transforms named after the "PanelTransformName" field

			public override void OnLoad(SolarPanelFixer fixerModule, PartModule targetModule)
			{ this.fixerModule = fixerModule; panelModule = targetModule; }

			public override bool OnStart(bool initialized, ref double nominalRate)
			{
				// disable it completely
				panelModule.enabled = panelModule.isEnabled = panelModule.moduleIsEnabled = false;
#if !DEBUG_SOLAR
				try
				{
#endif
					// method that parse the suncatchers "suncatcherTransforms" config string into a List<string>
					Lib.ReflectionCall(panelModule, "parseTransformData");
					// method that get the transform list (panelData) from the List<string>
					Lib.ReflectionCall(panelModule, "findTransforms");
					// get the transforms
					sunCatchers = Lib.ReflectionValue<List<Transform>>(panelModule, "panelData").ToArray();
					// the nominal rate defined in SSTU is per transform
					nominalRate = Lib.ReflectionValue<float>(panelModule, "resourceAmount") * sunCatchers.Length;
					return true;
#if !DEBUG_SOLAR
				}
				catch (Exception ex)
				{
					Lib.Log("SolarPanelFixer : exception while getting SSTUSolarPanelStatic data : " + ex.Message);
					return false;
				}
#endif
			}

			// exactly the same code as NFS curved panel
			public override double GetCosineFactor(Vector3d sunDir, bool analytic = false)
			{
				double cosineFactor = 0.0;

				foreach (Transform panel in sunCatchers)
				{
					cosineFactor += Math.Max(Vector3d.Dot(sunDir, panel.forward), 0.0);
#if DEBUG_SOLAR
					DebugDrawer.DebugLine(panel.position, panel.position + panel.forward, Color.yellow);
#endif
				}

				return cosineFactor / sunCatchers.Length;
			}

			// exactly the same code as NFS curved panel
			public override double GetOccludedFactor(Vector3d sunDir, out string occludingPart, bool analytic = false)
			{
				double occludedFactor = 1.0;
				occludingPart = null;

				RaycastHit raycastHit;
				foreach (Transform panel in sunCatchers)
				{
					if (Physics.Raycast(panel.position + (sunDir * 0.25), sunDir, out raycastHit, 10000f))
					{
						if (occludingPart == null && raycastHit.collider != null)
						{
							Part blockingPart = Part.GetComponentUpwards<Part>(raycastHit.transform.gameObject);
							if (blockingPart != null)
							{
								// avoid panels from occluding themselves
								if (blockingPart == panelModule.part)
									continue;

								occludingPart = blockingPart.partInfo.title;
							}
							occludedFactor -= 1.0 / sunCatchers.Length;
						}
					}
				}

				if (occludedFactor < 1E-5) occludedFactor = 0.0;
				return occludedFactor;
			}

			public override PanelState GetState() { return PanelState.Static; }

			public override bool SupportAutomation(PanelState state) { return false; }

			public override bool SupportProtoAutomation(ProtoPartModuleSnapshot protoModule) { return false; }

			public override void Break(bool isBroken)
			{
				// in any case, everything stays disabled
				panelModule.enabled = panelModule.isEnabled = panelModule.moduleIsEnabled = false;
			}
		}
		#endregion

		#region SSTU deployable/tracking multi-panel support (SSTUSolarPanelDeployable/SSTUModularPart)
		// SSTU common support for all solar panels that rely on the SolarModule/AnimationModule classes
		// - We prevent stock EC generation by setting to 0.0 the fields from where SSTU is getting the rates
		// - We use our own data structure that replicate the multiple panel per part possibilities, it store the transforms we need
		// - We use an aggregate of the nominal rate of each panel and assume all panels on the part are the same (not an issue currently, but the possibility exists in SSTU)
		// - Double-pivot panels that use multiple partmodules (I think there is only the "ST-MST-ISS solar truss" that does that) aren't supported
		// - Automation is currently not supported. Might be doable, but I don't have to mental strength to deal with it.
		// - Reliability is 100% untested and has a very barebones support. It should disable the EC output but not animations nor extend/retract ability.
		private class SSTUVeryComplexPanel : SupportedPanel<PartModule>
		{
			private object solarModuleSSTU; // instance of the "SolarModule" class
			private object animationModuleSSTU; // instance of the "AnimationModule" class
			private Func<string> getAnimationState; // delegate for the AnimationModule.persistentData property (string of the animState struct)
			private List<SSTUPanelData> panels;
			private TrackingType trackingType = TrackingType.Unknown;
			private enum TrackingType {Unknown = 0, Fixed, SinglePivot, DoublePivot }
			private string currentModularVariant;

			private class SSTUPanelData
			{
				public Transform pivot;
				public Axis pivotAxis;
				public SSTUSunCatcher[] suncatchers;

				public class SSTUSunCatcher
				{
					public object objectRef; // reference to the "SuncatcherData" class instance, used to get the raycast hit (direct ref to the RaycastHit doesn't work)
					public Transform transform;
					public Axis axis;
				}

				public bool IsValid => suncatchers[0].transform != null;
				public Vector3 PivotAxisVector => GetDirection(pivot, pivotAxis);
				public int SuncatcherCount => suncatchers.Length;
				public Vector3 SuncatcherPosition(int index) => suncatchers[index].transform.position;
				public Vector3 SuncatcherAxisVector(int index) => GetDirection(suncatchers[index].transform, suncatchers[index].axis);
				public RaycastHit SuncatcherHit(int index) => Lib.ReflectionValue<RaycastHit>(suncatchers[index].objectRef, "hitData");

				public enum Axis {XPlus, XNeg, YPlus, YNeg, ZPlus, ZNeg}
				public static Axis ParseSSTUAxis(object sstuAxis) { return (Axis)Enum.Parse(typeof(Axis), sstuAxis.ToString()); }
				private Vector3 GetDirection(Transform transform, Axis axis)
				{
					switch (axis) // I hope I got this right
					{
						case Axis.XPlus: return transform.right;
						case Axis.XNeg: return transform.right * -1f;
						case Axis.YPlus: return transform.up;
						case Axis.YNeg: return transform.up * -1f;
						case Axis.ZPlus: return transform.forward;
						case Axis.ZNeg: return transform.forward * -1f;
						default: return Vector3.zero;
					}
				}
			}

			public override void OnLoad(SolarPanelFixer fixerModule, PartModule targetModule)
			{ this.fixerModule = fixerModule; panelModule = targetModule; }

			public override bool OnStart(bool initialized, ref double nominalRate)
			{
#if !DEBUG_SOLAR
				try
				{
#endif
					// get a reference to the "SolarModule" class instance, it has everything we need (transforms, rates, etc...)
					switch (panelModule.moduleName)
					{
						case "SSTUModularPart":
						solarModuleSSTU = Lib.ReflectionValue<object>(panelModule, "solarFunctionsModule");
						currentModularVariant = Lib.ReflectionValue<string>(panelModule, "currentSolar");
						break;
						case "SSTUSolarPanelDeployable":
						solarModuleSSTU = Lib.ReflectionValue<object>(panelModule, "solarModule");
						break;
						default:
						return false;
					}

					// Get animation module
					animationModuleSSTU = Lib.ReflectionValue<object>(solarModuleSSTU, "animModule");
					// Get animation state property delegate
					PropertyInfo prop = animationModuleSSTU.GetType().GetProperty("persistentData");
					getAnimationState = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), animationModuleSSTU, prop.GetGetMethod());

					// SSTU stores the sum of the nominal output for all panels in the part, we retrieve it
					float newNominalrate = Lib.ReflectionValue<float>(solarModuleSSTU, "standardPotentialOutput");
					// OnStart can be called multiple times in the editor, but we might already have reset the rate
					// In the editor, if the "no panel" variant is selected, newNominalrate will be 0.0, so also check initialized
					if (newNominalrate > 0.0 || initialized == false)
					{
						nominalRate = newNominalrate;
						// reset the rate sum in the SSTU module. This won't prevent SSTU from generating EC, but this way we can keep track of what we did
						// don't doit in the editor as it isn't needed and we need it in case of variant switching
						if (Lib.IsFlight) Lib.ReflectionValue(solarModuleSSTU, "standardPotentialOutput", 0f); 
					}

					panels = new List<SSTUPanelData>();
					object[] panelDataArray = Lib.ReflectionValue<object[]>(solarModuleSSTU, "panelData"); // retrieve the PanelData class array that contain suncatchers and pivots data arrays
					foreach (object panel in panelDataArray)
					{
						object[] suncatchers = Lib.ReflectionValue<object[]>(panel, "suncatchers"); // retrieve the SuncatcherData class array
						object[] pivots = Lib.ReflectionValue<object[]>(panel, "pivots"); // retrieve the SolarPivotData class array

						int suncatchersCount = suncatchers.Length;
						if (suncatchers == null || pivots == null || suncatchersCount == 0) continue;

						// instantiate our data class
						SSTUPanelData panelData = new SSTUPanelData();  

						// get suncatcher transforms and the orientation of the panel surface normal
						panelData.suncatchers = new SSTUPanelData.SSTUSunCatcher[suncatchersCount];
						for (int i = 0; i < suncatchersCount; i++)
						{
							object suncatcher = suncatchers[i];
							if (Lib.IsFlight) Lib.ReflectionValue(suncatcher, "resourceRate", 0f); // actually prevent SSTU modules from generating EC, but not in the editor
							panelData.suncatchers[i] = new SSTUPanelData.SSTUSunCatcher();
							panelData.suncatchers[i].objectRef = suncatcher; // keep a reference to the original suncatcher instance, for raycast hit acquisition
							panelData.suncatchers[i].transform = Lib.ReflectionValue<Transform>(suncatcher, "suncatcher"); // get suncatcher transform
							panelData.suncatchers[i].axis = SSTUPanelData.ParseSSTUAxis(Lib.ReflectionValue<object>(suncatcher, "suncatcherAxis")); // get suncatcher axis
						}

						// get pivot transform and the pivot axis. Only needed for single-pivot tracking panels
						// double axis panels can have 2 pivots. Its seems the suncatching one is always the second.
						// For our purpose we can just assume always perfect alignement anyway.
						// Note : some double-pivot panels seems to use a second SSTUSolarPanelDeployable instead, we don't support those.
						switch (pivots.Length) 
						{
							case 0:
								trackingType = TrackingType.Fixed; break;
							case 1:
								trackingType = TrackingType.SinglePivot;
								panelData.pivot = Lib.ReflectionValue<Transform>(pivots[0], "pivot");
								panelData.pivotAxis = SSTUPanelData.ParseSSTUAxis(Lib.ReflectionValue<object>(pivots[0], "pivotRotationAxis"));
								break;
							case 2:
								trackingType = TrackingType.DoublePivot; break;
							default: continue;
						}

						panels.Add(panelData);
					}

					// disable ourselves if no panel was found
					if (panels.Count == 0) return false;

					// hide PAW status fields
					switch (panelModule.moduleName)
					{
						case "SSTUModularPart": panelModule.Fields["solarPanelStatus"].guiActive = false; break;
						case "SSTUSolarPanelDeployable": foreach(var field in panelModule.Fields) field.guiActive = false; break;
					}
					return true;
#if !DEBUG_SOLAR
				}
				catch (Exception ex)
				{
					Lib.Log("SolarPanelFixer : exception while getting SSTUModularPart/SSTUSolarPanelDeployable solar panel data : " + ex.Message);
					return false;
				}
#endif
			}

			public override double GetCosineFactor(Vector3d sunDir, bool analytic = false)
			{
				double cosineFactor = 0.0;
				int suncatcherTotalCount = 0;
				foreach (SSTUPanelData panel in panels)
				{
					if (!panel.IsValid) continue;
					suncatcherTotalCount += panel.SuncatcherCount;
					for (int i = 0; i < panel.SuncatcherCount; i++)
					{
#if DEBUG_SOLAR
						DebugDrawer.DebugLine(panel.SuncatcherPosition(i), panel.SuncatcherPosition(i) + panel.SuncatcherAxisVector(i), Color.yellow);
						if (trackingType == TrackingType.SinglePivot) DebugDrawer.DebugLine(panel.pivot.position, panel.pivot.position + (panel.PivotAxisVector * -1f), Color.blue);
#endif

						if (!analytic) { cosineFactor += Math.Max(Vector3d.Dot(sunDir, panel.SuncatcherAxisVector(i)), 0.0); continue; }

						switch (trackingType)
						{
							case TrackingType.Fixed:		cosineFactor += Math.Max(Vector3d.Dot(sunDir, panel.SuncatcherAxisVector(i)), 0.0); continue;
							case TrackingType.SinglePivot:	cosineFactor += Math.Cos(1.57079632679 - Math.Acos(Vector3d.Dot(sunDir, panel.PivotAxisVector))); continue;
							case TrackingType.DoublePivot:	cosineFactor += 1.0; continue;
						}
					}
				}
				return cosineFactor / suncatcherTotalCount;
			}

			public override double GetOccludedFactor(Vector3d sunDir, out string occludingPart, bool analytic = false)
			{
				double occludingFactor = 0.0;
				occludingPart = null;
				int suncatcherTotalCount = 0;
				foreach (SSTUPanelData panel in panels)
				{
					if (!panel.IsValid) continue;
					suncatcherTotalCount += panel.SuncatcherCount;
					for (int i = 0; i < panel.SuncatcherCount; i++)
					{
						RaycastHit raycastHit;
						if (analytic)
							Physics.Raycast(panel.SuncatcherPosition(i) + (sunDir * 0.25), sunDir, out raycastHit, 10000f);
						else
							raycastHit = panel.SuncatcherHit(i);

						if (raycastHit.collider != null)
						{
							occludingFactor += 1.0; // in case of multiple panels per part, it is perfectly valid for panels to occlude themselves so we don't do the usual check
							Part blockingPart = Part.GetComponentUpwards<Part>(raycastHit.transform.gameObject);
							if (occludingPart == null && blockingPart != null) // don't update if occlusion is from multiple parts
								occludingPart = blockingPart.partInfo.title;
						}
					}
				}
				occludingFactor = 1.0 - (occludingFactor / suncatcherTotalCount);
				if (occludingFactor < 0.01) occludingFactor = 0.0; // avoid precison issues
				return occludingFactor;
			}

			public override PanelState GetState()
			{
				switch (trackingType)
				{
					case TrackingType.Fixed: return PanelState.Static;
					case TrackingType.Unknown: return PanelState.Unknown;
				}
#if !DEBUG_SOLAR
				try
				{
#endif
					// handle solar panel variant switching in SSTUModularPart
					if (Lib.IsEditor && panelModule.ClassName == "SSTUModularPart")
					{
						string newVariant = Lib.ReflectionValue<string>(panelModule, "currentSolar");
						if (newVariant != currentModularVariant)
						{
							currentModularVariant = newVariant;
							OnStart(false, ref fixerModule.nominalRate);
						}
					}
					// get animation state
					switch (getAnimationState())
					{
						case "STOPPED_START": return PanelState.Retracted;
						case "STOPPED_END": return PanelState.Extended;
						case "PLAYING_FORWARD": return PanelState.Extending;
						case "PLAYING_BACKWARD": return PanelState.Retracting;
					}
#if !DEBUG_SOLAR
				}
				catch { return PanelState.Unknown; }
#endif
				return PanelState.Unknown;
			}

			public override bool IsTracking => trackingType == TrackingType.SinglePivot || trackingType == TrackingType.DoublePivot;

			public override void SetTrackedBody(CelestialBody body)
			{
				Lib.ReflectionValue(solarModuleSSTU, "trackedBodyIndex", body.flightGlobalsIndex);
			}

			public override bool SupportAutomation(PanelState state) { return false; }

			public override bool SupportProtoAutomation(ProtoPartModuleSnapshot protoModule) { return false; }
		}
		#endregion

		//#region ROSolar switcheable/resizeable MDSP derivative (ModuleROSolar)
		//// Made by Pap for RO. Implement in-editor model switching / resizing on top of the stock module.
		//// TODO: Tracking panels implemented in v1.1 (May 2020).  Need further work here to get those working?
		//// Plugin is here : https://github.com/KSP-RO/ROLibrary/blob/master/Source/ROLib/Modules/ModuleROSolar.cs
		//// Configs are here : https://github.com/KSP-RO/ROSolar
		//// Require the following MM patch to work :
		//
		////@PART:HAS[@MODULE[ModuleROSolar]]:AFTER[zzzKerbalism] { %MODULE[SolarPanelFixer]{} }
		//
		//private class ROConfigurablePanel : StockPanel
		//{
		//	// Note : this has been implemented in the base class (StockPanel) because
		//	// we have the same issue with NearFutureSolar B9PS-switching its MDSP modules.

		//
		//	public override PanelState GetState()
		//	{
		//		// We set the resHandler rate to 0 in StockPanel.OnStart(), and ModuleROSolar set it back
		//		// to the new nominal rate after some switching/resizing has been done (see ModuleROSolar.RecalculateStats()),
		//		// so don't complicate things by using events and just call StockPanel.OnStart() if we detect a non-zero rate.
		//		if (Lib.IsEditor && panelModule.resHandler.outputResources[0].rate != 0.0)
		//			OnStart(false, ref fixerModule.nominalRate);

		//		return base.GetState();
		//	}
		//
		//}

		//#endregion
	}
} // KERBALISM
*/
