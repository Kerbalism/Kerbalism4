using System;
using System.Reflection;
using HarmonyLib;
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
			BadOrientation
		}

		protected class PersistentTransform
		{
			private Transform transform;
			private Vector3 vesselRelativePosition;
			private Quaternion vesselRelativeRotation;

			public PersistentTransform(Transform transform, Transform vesselTransform)
			{
				vesselRelativePosition = vesselTransform.position - transform.position;
				vesselRelativeRotation = transform.rotation * Quaternion.Inverse(vesselTransform.rotation);
			}

			public Vector3 Position(Vessel v)
			{
				return v.transform.position + vesselRelativePosition;
			}

			public Quaternion Rotation(Vessel v)
			{
				return vesselRelativeRotation * v.transform.rotation;
			}

			public Vector3 Up(Vessel v)
			{
				return Rotation(v) * Vector3.up;
			}

			public Vector3 Right(Vessel v)
			{
				return Rotation(v) * Vector3.right;
			}

			public Vector3 Forward(Vessel v)
			{
				return Rotation(v) * Vector3.forward;
			}
		}

		public double nominalRate;
		public double persistentFactor;
		public PanelState state;
		public int trackedSunIndex = 0;
		private bool manualTracking = false;
		public double launchUT = -1.0;

		public double currentOutput;

		private string panelStatus;
		private bool editorEnabled;
		private string rateFormat;

		private static FieldInfo panelStatusField = AccessTools.Field(typeof(SolarPanelHandlerBase), nameof(panelStatus));
		private static FieldInfo editorEnabledField = AccessTools.Field(typeof(SolarPanelHandlerBase), nameof(editorEnabled));

		public virtual void Load(ConfigNode node)
		{

		}

		public virtual void Save(ConfigNode node)
		{

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

				UI_Label label = new UI_Label();
				BaseField field = new BaseField(label, panelStatusField, this);
				field.guiName = Local.SolarPanelFixer_Solarpanel; //Solar panel
				field.uiControlFlight = toggle;
				loadedModule.Fields.Add(field);

				if (Sim.stars.Count > 1 && IsTracking)
				{
					// setup target module animation for custom star tracking
					SetTrackedBody(FlightGlobals.Bodies[trackedSunIndex]);

					KSPEvent kspEvent = new KSPEvent();
					kspEvent.guiName = Local.SolarPanelFixer_Selecttrackedstar; //Select tracked star
					kspEvent.active = true;
					kspEvent.guiActive = true;
					BaseEvent baseEvent = new BaseEvent(loadedModule.Events, "SelectStar", ManualStarTrackingPopup, kspEvent);
				}
			}

			if (!Lib.IsEditor && launchUT < 0.0)
				launchUT = Planetarium.GetUniversalTime();

			// set how many decimal points are needed to show the panel Ec output in the UI
			if (nominalRate < 0.1) rateFormat = "F4";
			else if (nominalRate < 1.0) rateFormat = "F3";
			else if (nominalRate < 10.0) rateFormat = "F2";
			else rateFormat = "F1";
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
					trackedSunIndex = body.flightGlobalsIndex;
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

		/// <summary>Must return a [0;1] scalar evaluating the local occlusion factor (usually with a physic raycast already done by the target module)</summary>
		/// <param name="occludingPart">if the occluding object is a part, name of the part. MUST return null in all other cases.</param>
		/// <param name="analytic">if true, the returned scalar must account for the given sunDir, so we can't rely on the target module own raycast</param>
		protected abstract double GetOccludedFactor(Vector3d sunDir, out string occludingPart, bool analytic = false);

		/// <summary>Must return a [0;1] scalar evaluating the angle of the given sunDir on the panel surface (usually a dot product clamped to [0;1])</summary>
		/// <param name="analytic">if true and the panel is orientable, the returned scalar must be the best possible output (must use the rotation around the pivot)</param>
		protected abstract double GetCosineFactor(Vector3d sunDir, bool analytic = false);

		/// <summary>must return the state of the panel, must be able to work before OnStart has been called</summary>
		protected abstract PanelState GetState();

		/// <summary>Can be overridden if the target module implement a time efficiency curve. Keys are in hours, values are a scalar in the [0:1] range.</summary>
		protected virtual FloatCurve GetTimeCurve() => null;

		/// <summary>Called at Update(), can contain target module specific hacks</summary>
		protected virtual void OnLoadedUpdate() { }

		/// <summary>Is the panel a sun-tracking panel</summary>
		public virtual bool IsTracking => false;

		/// <summary>Kopernicus stars support : must set the animation tracked body</summary>
		public virtual void SetTrackedBody(CelestialBody body) { }

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
