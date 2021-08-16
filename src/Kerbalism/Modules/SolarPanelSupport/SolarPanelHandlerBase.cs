// TODO :
// - proper PAW status string
// - automation support
// - timeEffCurve support
// - register exposure in vesseldata for UI purposes


using System;
using System.Reflection;
using HarmonyLib;
using KSP.Localization;
using Steamworks;
using UnityEngine;

namespace KERBALISM
{
	public abstract class SolarPanelHandlerBase : ForeignModuleHandler, IPersistentModuleHandler
	{
		public override ActivationContext Activation => ActivationContext.Editor | ActivationContext.Loaded | ActivationContext.Unloaded;

		public ModuleHandler ModuleHandler => this;
		public int FlightId { get; set; }
		public int ShipId { get; set; }

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
			BadOrientation,
			Submerged
		}

		protected class PersistentTransform
		{
			private SolarPanelHandlerBase handler;
			private Transform transform;
			private Vector3 vesselRelativePosition;
			private Quaternion vesselRelativeRotation;

			public static void Init(ref PersistentTransform persistentTransformRef, SolarPanelHandlerBase handler, Transform transform = null)
			{
				if (handler.VesselData.LoadedOrEditor)
				{
					if (transform == null)
					{
						persistentTransformRef = null;
						return;
					}

					if (persistentTransformRef == null)
						persistentTransformRef = new PersistentTransform();

					persistentTransformRef.transform = transform;
					persistentTransformRef.handler = handler;
				}
				else
				{
					if (persistentTransformRef != null)
						persistentTransformRef.handler = handler;
				}
			}

			private PersistentTransform() {}

			public void UpdateVesselRelativeReferences(VesselDataBase vd)
			{
				if (transform != null && vd is VesselData vdFlight)
				{
					vesselRelativePosition = vdFlight.Vessel.transform.position - transform.position;
					vesselRelativeRotation = transform.rotation * Quaternion.Inverse(vdFlight.Vessel.transform.rotation);
				}
			}

			public PersistentTransform(ConfigNode node)
			{
				if (node == null)
					return;

				vesselRelativePosition = Lib.ConfigValue(node, nameof(vesselRelativePosition), Vector3.zero);
				vesselRelativeRotation = Lib.ConfigValue(node, nameof(vesselRelativeRotation), Quaternion.identity);
			}

			public void Save(ConfigNode node)
			{
				node.AddValue(nameof(vesselRelativePosition), vesselRelativePosition);
				node.AddValue(nameof(vesselRelativeRotation), vesselRelativeRotation);
			}

			protected Transform VesselTransform
			{
				get
				{
					if (handler.VesselData is VesselData vd)
						return vd.Vessel.transform;

					return null;
				}
			}

			public Vector3 Position
			{
				get
				{
					if (transform != null)
						return transform.position;

					return VesselTransform.position + vesselRelativePosition;
				}
			}

			public Quaternion Rotation
			{
				get
				{
					if (transform != null)
						return transform.rotation;

					return vesselRelativeRotation * VesselTransform.rotation;
				}
			}

			public Vector3 Up
			{
				get
				{
					if (transform != null)
						return transform.up;

					return Rotation * Vector3.up;
				}
			}

			public Vector3 Right
			{
				get
				{
					if (transform != null)
						return transform.right;

					return Rotation * Vector3.right;
				}
			}

			public Vector3 Forward
			{
				get
				{
					if (transform != null)
						return transform.forward;

					return Rotation * Vector3.forward;
				}
			}
		}

		protected static int raycastMask = (1 << LayerMask.NameToLayer("PhysicalObjects")) | (1 << LayerMask.NameToLayer("TerrainColliders")) | (1 << LayerMask.NameToLayer("Local Scenery")) | LayerUtil.DefaultEquivalent;

		private static string locTerrain;
		protected static string LocTerrain => locTerrain ?? (locTerrain = Localizer.Format("#autoLOC_438839"));

		// persisted fields
		public double nominalRate;
		public PanelState state;
		public int trackedStarIndex = 0;
		private bool manualTracking = false;
		public double launchUT = -1.0;
		private bool isSubmerged = false;

		// computed fields
		protected bool isSubstepping;
		public double currentOutput;
		private string rateFormat;
		private ExposureState exposureState;
		private double exposureFactor;
		private string occludingObject;
		private double wearFactor;
		private StarFlux trackedStar;

		// PAW UI backing fields
		private string panelStatus;
		private bool editorEnabled;
		private BaseField statusPAWField;
		private BaseEvent trackingPAWEvent;


		private static FieldInfo panelStatusField = AccessTools.Field(typeof(SolarPanelHandlerBase), nameof(panelStatus));
		private static FieldInfo editorEnabledField = AccessTools.Field(typeof(SolarPanelHandlerBase), nameof(editorEnabled));

		public virtual void Load(ConfigNode node)
		{
			nominalRate = Lib.ConfigValue(node, nameof(nominalRate), 0.0);
			state = Lib.ConfigValue(node, nameof(state), PanelState.Unknown);
			trackedStarIndex = Lib.ConfigValue(node, nameof(trackedStarIndex), 0);
			manualTracking = Lib.ConfigValue(node, nameof(manualTracking), false);
			launchUT = Lib.ConfigValue(node, nameof(launchUT), -1.0);
			isSubmerged = Lib.ConfigValue(node, nameof(isSubmerged), false);
		}

		public virtual void Save(ConfigNode node)
		{
			node.AddValue(nameof(nominalRate), nominalRate);
			node.AddValue(nameof(state), state);
			node.AddValue(nameof(trackedStarIndex), trackedStarIndex);
			node.AddValue(nameof(manualTracking), manualTracking);
			node.AddValue(nameof(launchUT), launchUT);
			node.AddValue(nameof(isSubmerged), isSubmerged);
		}

		public override void OnStart()
		{
			if (IsLoaded)
			{
				UI_Toggle toggle = new UI_Toggle();
				toggle.enabledText = Local.SolarPanelFixer_simulated; // <color=#00ff00>simulated</color>
				toggle.disabledText = Local.SolarPanelFixer_ignored; // <color=#ffff00>ignored</color>
				BaseField editorEnabledBaseField = new BaseField(toggle, editorEnabledField, this);
				editorEnabledBaseField.guiName = Local.SolarPanelFixer_Solarpaneloutput; //Solar panel output
				editorEnabledBaseField.guiActive = false;
				editorEnabledBaseField.guiActiveEditor = true;
				editorEnabledBaseField.uiControlEditor = toggle;
				loadedModule.Fields.Add(editorEnabledBaseField);

				UI_Label statusLabel = new UI_Label();
				statusPAWField = new BaseField(statusLabel, panelStatusField, this);
				statusPAWField.guiName = Local.SolarPanelFixer_Solarpanel; //Solar panel
				statusPAWField.uiControlFlight = statusLabel;
				loadedModule.Fields.Add(statusPAWField);

				if (Sim.stars.Count > 1 && IsTracking)
				{
					KSPEvent trackingEvent = new KSPEvent();
					trackingEvent.guiName = Local.SolarPanelFixer_Selecttrackedstar; //Select tracked star
					trackingEvent.active = true;
					trackingEvent.guiActive = true;
					trackingPAWEvent = new BaseEvent(loadedModule.Events, "SelectStar", ManualStarTrackingPopup, trackingEvent);
				}
			}

			if (!Lib.IsEditor && launchUT < 0.0)
				launchUT = Planetarium.GetUniversalTime();

			// setup target module animation for custom star tracking
			// TODO : Calling SetTrackedBody here will likely not work (ModuleHandler.OnStart is called before the module OnStart)
			SetTrackedBody(FlightGlobals.Bodies[trackedStarIndex]);

			// set how many decimal points are needed to show the panel Ec output in the UI
			if (nominalRate < 0.1) rateFormat = "F4";
			else if (nominalRate < 1.0) rateFormat = "F3";
			else if (nominalRate < 10.0) rateFormat = "F2";
			else rateFormat = "F1";
		}

		public override void OnBecomingLoaded()
		{
			OnStart();
		}

		public override void OnBecomingUnloaded()
		{
			base.OnBecomingUnloaded();
		}

		private void ManualStarTrackingPopup()
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
					SetTrackedBody(body);
				}, true);
			}

			PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new MultiOptionDialog(
				Local.SolarPanelFixer_SelectTrackingBody,//"SelectTrackingBody"
				Local.SolarPanelFixer_SelectTrackedstar_msg,//"Select the star you want to track with this solar panel."
				Local.SolarPanelFixer_Selecttrackedstar,//"Select tracked star"
				UISkinManager.GetSkin("MainMenuSkin"),
				options), false, UISkinManager.GetSkin("MainMenuSkin"));
		}



		public override void OnFixedUpdate(double elapsedSec)
		{
			isSubstepping = ((VesselData)VesselData).IsSubstepping;

			StateUpdate(elapsedSec);

			if (IsLoaded && !Lib.IsEditor && Lib.IsPAWOpen(partData.LoadedPart))
			{
				PAWUpdate();
			}
#if DEBUG_SOLAR
			panelStatus = $"{currentOutput:F2}Ec/s (max {nominalRate:F2}), exposure:{exposureFactor:P1} ({exposureState})";
#endif
		}

		private void StateUpdate(double elapsedSec)
		{
			if (IsLoaded)
			{
				state = GetState();
				isSubmerged = partData.LoadedPart.submergedPortion >= 1.0;
			}

			if (!(state == PanelState.Extended || state == PanelState.ExtendedFixed || state == PanelState.Static))
			{
				exposureState = ExposureState.Disabled;
				currentOutput = 0.0;
				return;
			}

			if (Lib.IsEditor)
			{
				return;
			}

			if (isSubmerged)
			{
				exposureState = ExposureState.Submerged;
				currentOutput = 0.0;
				return;
			}

			if (trackedStar.sunlightFactor == 0.0)
				exposureState = ExposureState.InShadow;
			else
				exposureState = ExposureState.Exposed;

			exposureFactor = 0.0;
			double starsFlux = 0.0;
			// iterate over all stars, compute the exposure factor
			foreach (StarFlux starFlux in VesselData.StarsIrradiance)
			{
				// update tracked body in auto mode
				if (!manualTracking && trackedStar.directRawFluxProportion < starFlux.directRawFluxProportion)
				{
					SetTrackedBody(starFlux.Star.body);
				}

				// ignore non-visible stars
				if (starFlux.sunlightFactor == 0.0)
				{
					if (starFlux == trackedStar)
						exposureState = ExposureState.InShadow;

					continue;
				}

				// ignore stars whose luminosity is less than 1% of the total flux
				if (starFlux.directRawFluxProportion < 0.01)
					continue;

				starsFlux += starFlux.directFlux;

				double cosineFactor = 1.0;
				double occlusionFactor = 1.0;

				// Get the cosine factor (alignement between the sun and the panel surface)
				cosineFactor = GetCosineFactor(starFlux.direction);

				if (cosineFactor == 0.0)
				{
					// If this is the tracked sun and the panel is not oriented toward the sun
					if (starFlux == trackedStar)
						exposureState = ExposureState.BadOrientation;
				}
				else
				{
					// The panel is oriented toward the sun, do a physic raycast to check occlusion from parts, terrain, buildings...
					// TODO : occlusion checking is disabled at high timewarp speeds
					// In any case, we can't rely on a single raycast to determine the occlusion state of large timesteps
					// In the case of terrain occlusion, we are already considering the main body occlusion in the sim.
					// Applying another factor on top will always be inconsistent.
					// Ideally, for landed vessels, we need to factor in the terrain occlusion at the sim level by mass-raycasting 
					// the terrain in all directions, saving that data, and checking the individual substeps against it.
					bool occluderIsPart = false;
					if (IsLoaded && !isSubstepping)
					{
						occlusionFactor = GetOccludedFactor(starFlux.direction, out occludingObject, out occluderIsPart);
					}

					// If this is the tracked sun and the panel is occluded, update the gui info string. 
					if (starFlux == trackedStar && occlusionFactor == 0.0)
					{
						exposureState = occluderIsPart ? ExposureState.OccludedPart : ExposureState.OccludedTerrain;
					}
				}

				// Compute final aggregate exposure factor
				exposureFactor += cosineFactor * occlusionFactor * starFlux.directRawFluxProportion;
			}

			//VesselData.SaveSolarPanelExposure(persistentFactor);

			// get solar flux and deduce a scalar based on nominal flux at 1AU
			// - this include atmospheric absorption if inside an atmosphere
			// - at high timewarps speeds, atmospheric absorption is analytical (integrated over a full revolution)
			double distanceFactor = starsFlux / Sim.SolarFluxAtHome;

			// get wear factor (time based output degradation)
			wearFactor = 1.0;
			//if (timeEfficCurve?.Curve.keys.Length > 1)
			//	wearFactor = Lib.Clamp(timeEfficCurve.Evaluate((float)((Planetarium.GetUniversalTime() - launchUT) / 3600.0)), 0.0, 1.0);


			// get final output rate in EC/s
			currentOutput = nominalRate * wearFactor * distanceFactor * exposureFactor;

			// produce EC
			VesselData.ResHandler.ElectricCharge.Produce(currentOutput * elapsedSec, ResourceBroker.GetOrCreate("sp2", ResourceBroker.BrokerCategory.SolarPanel, "sp2"));

		}

		private void PAWUpdate()
		{
			if (trackingPAWEvent != null)
			{
				trackingPAWEvent.guiActive = state == PanelState.Extended || state == PanelState.ExtendedFixed || state == PanelState.Static;
			}

			statusPAWField.guiActive = state != PanelState.Failure && state != PanelState.Unknown;

			if (statusPAWField.guiActive)
			{
				if (exposureState == ExposureState.Disabled)
				{
					switch (state)
					{
						case PanelState.Retracted: panelStatus = Local.SolarPanelFixer_retracted; break;//"retracted"
						case PanelState.Extending: panelStatus = Local.SolarPanelFixer_extending; break;//"extending"
						case PanelState.Retracting: panelStatus = Local.SolarPanelFixer_retracting; break;//"retracting"
						case PanelState.Broken: panelStatus = Local.SolarPanelFixer_broken; break;//"broken"
						case PanelState.Failure: panelStatus = Local.SolarPanelFixer_failure; break;//"failure"
						case PanelState.Unknown: panelStatus = Local.SolarPanelFixer_invalidstate; break;//"invalid state"
					}
				}
				else
				{
					KsmString status = KsmString.Get;

					switch (exposureState)
					{
						case ExposureState.Exposed:
							status.Add(currentOutput.ToString(rateFormat), KF.WhiteSpace, Lib.ECAbbreviation, "/s, ", Local.SolarPanelFixer_exposure, KF.WhiteSpace);
							status.Format(exposureFactor, "P0");
							break;
						case ExposureState.InShadow:
							if (currentOutput > 0.001) status.Add(currentOutput.ToString(rateFormat), KF.WhiteSpace, Lib.ECAbbreviation, "/s, ");
							status.Format(Local.SolarPanelFixer_inshadow, KF.KolorYellow); //in shadow
							break;
						case ExposureState.OccludedTerrain:
							if (currentOutput > 0.001) status.Add(currentOutput.ToString(rateFormat), KF.WhiteSpace, Lib.ECAbbreviation, "/s, ");
							status.Format(Local.SolarPanelFixer_occludedbyterrain, KF.KolorYellow); //occluded by terrain
							break;
						case ExposureState.OccludedPart:
							if (currentOutput > 0.001) status.Add(currentOutput.ToString(rateFormat), KF.WhiteSpace, Lib.ECAbbreviation, "/s, ");
							status.Format(Local.SolarPanelFixer_occludedby.Format(occludingObject), KF.KolorYellow); //occluded by <<object>>
							break;
						case ExposureState.BadOrientation:
							if (currentOutput > 0.001) status.Add(currentOutput.ToString(rateFormat), KF.WhiteSpace, Lib.ECAbbreviation, "/s, ");
							status.Format(Local.SolarPanelFixer_badorientation, KF.KolorYellow); //bad orientation
							break;
						case ExposureState.Submerged:
							status.Format("Submerged", KF.KolorYellow); //bad orientation
							break;
					}

					panelStatus = status.End();
				}
			}
		}

		/// <summary>Must return a [0;1] scalar evaluating the local occlusion factor (usually with a physic raycast already done by the target module)</summary>
		/// <param name="occludingPart">if the occluding object is a part, name of the part. MUST return null in all other cases.</param>
		/// <param name="analytic">if true, the returned scalar must account for the given sunDir, so we can't rely on the target module own raycast</param>
		protected abstract double GetOccludedFactor(Vector3d sunDir, out string occludingObjectTitle, out bool occluderIsPart);

		/// <summary>Must return a [0;1] scalar evaluating the angle of the given sunDir on the panel surface (usually a dot product clamped to [0;1])</summary>
		/// <param name="analytic">if true and the panel is orientable, the returned scalar must be the best possible output (must use the rotation around the pivot)</param>
		protected abstract double GetCosineFactor(Vector3d sunDir);

		/// <summary> Called only when loaded, must return the state of the loaded panel module</summary>
		protected abstract PanelState GetState();

		/// <summary>Can be overridden if the target module implement a time efficiency curve. Keys are in hours, values are a scalar in the [0:1] range.</summary>
		protected virtual FloatCurve GetTimeCurve() => null;

		/// <summary>Is the panel a sun-tracking panel</summary>
		public virtual bool IsTracking => false;

		private void SetTrackedBody(CelestialBody body)
		{
			trackedStarIndex = body.flightGlobalsIndex;

			for (int i = 0; i < VesselData.StarsIrradiance.Length; i++)
			{
				if (VesselData.StarsIrradiance[i].Star.body.flightGlobalsIndex == trackedStarIndex)
				{
					trackedStar = VesselData.StarsIrradiance[i];
					break;
				}
			}

			if (IsLoaded)
			{
				OnSetTrackedBody(body);
			}

			if (trackingPAWEvent != null)
			{
				trackingPAWEvent.guiName = KsmString.Get
					.Add(Local.SolarPanelFixer_Trackedstar, KF.WhiteSpace)
					.Add(manualTracking ? ":" : Local.SolarPanelFixer_AutoTrack, KF.WhiteSpace)
					.Add(trackedStar.Star.body.name)
					.End();
			}
		}

		/// <summary>Kopernicus stars support : must set the animation tracked body</summary>
		public virtual void OnSetTrackedBody(CelestialBody body) { }

		/// <summary>Automation : override this with "return false" if the module doesn't support automation when loaded</summary>
		public virtual bool SupportAutomation(SolarPanelFixer.PanelState state)
		{
			switch (state)
			{
				case SolarPanelFixer.PanelState.Retracted:
				case SolarPanelFixer.PanelState.Extending:
				case SolarPanelFixer.PanelState.Extended:
				case SolarPanelFixer.PanelState.Retracting:
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
		public virtual bool IsRetractable() => false;

		/// <summary>Automation : must be implemented if the panel is extendable</summary>
		public virtual void Extend() { }

		/// <summary>Automation : must be implemented if the panel is retractable</summary>
		public virtual void Retract() { }

		///<summary>Automation : Called OnLoad, must set the target module persisted extended/retracted fields to reflect changes done trough automation while unloaded</summary>
		protected virtual void SetDeployedStateOnLoad(SolarPanelFixer.PanelState state) { }
	}
}
