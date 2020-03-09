﻿using Harmony;
using KERBALISM.Planner;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static KERBALISM.HabitatData;
using static KERBALISM.HabitatLib;

namespace KERBALISM
{
	public sealed class ModuleKsmHabitat : PartModule, ISpecifics, IModuleInfo
	{
		#region FIELDS / PROPERTIES
		// general config
		[KSPField] public bool canPressurize = true;              // can the habitat be pressurized ?
		[KSPField] public double maxShieldingFactor = 1.0;        // how much shielding can be applied, in % of the habitat surface (can be > 1.0)
		[KSPField] public double depressurizationSpeed = -1.0;    // liters/second, auto scaled at 10 liters / second / √(m3) of habitat volume if not specified, adjustable in settings
		[KSPField] public double reclaimAtmosphereFactor = 0.7;  // % of atmosphere that will be recovered when depressurizing (producing "reclaimResource" back)
		[KSPField] public bool canRetract = true;                 // if false, can't be retracted once deployed
		[KSPField] public bool deployWithPressure = false;        // if true, deploying is done by pressurizing
		[KSPField] public double depressurizeECRate = 1.0;        // EC/s consumed while depressurizing
		[KSPField] public double deployECRate = 5.0;              // EC/s consumed while deploying / inflating
		[KSPField] public double accelerateECRate = 15.0;         // EC/s consumed while accelerating a centrifuge (note : decelerating is free)
		[KSPField] public double rotateECRate = 2.0;              // EC/s consumed to sustain the centrifuge rotation

		// volume / surface config
		[KSPField] public double volume = 0.0;  // habitable volume in m^3, deduced from model if not specified
		[KSPField] public double surface = 0.0; // external surface in m^2, deduced from model if not specified
		[KSPField] public Lib.VolumeAndSurfaceMethod volumeAndSurfaceMethod = Lib.VolumeAndSurfaceMethod.Best;
		[KSPField] public bool substractAttachementNodesSurface = true;

		// resources config
		[KSPField] public string reclaimResource = "Nitrogen"; // Nitrogen
		[KSPField] public string shieldingResource = "Shielding"; // KsmShielding

		// animations config
		[KSPField] public string deployAnim = string.Empty; // deploy / inflate animation, if any
		[KSPField] public bool deployAnimReverse = false;   // deploy / inflate animation is reversed

		[KSPField] public string rotateAnim = string.Empty;        // rotate animation, if any
		[KSPField] public bool rotateIsReversed = false;           // inverse rotation direction
		[KSPField] public bool rotateIsTransform = false;          // rotateAnim is not an animation, but a transform
		[KSPField] public Vector3 rotateAxis = Vector3.forward;    // axis around which to rotate (transform only)
		[KSPField] public float rotateSpinRate = 30.0f;            // centrifuge rotation speed (deg/s)
		[KSPField] public float rotateAccelerationRate = 1.0f;     // centrifuge transform acceleration (deg/s/s)
		[KSPField] public bool rotateIVA = true;                   // should the IVA rotate with the transform ?

		[KSPField] public string counterweightAnim = string.Empty;        // inflate animation, if any
		[KSPField] public bool counterweightIsReversed = false;           // inverse rotation direction
		[KSPField] public bool counterweightIsTransform = false;          // rotateAnim is not an animation, but a Transform
		[KSPField] public Vector3 counterweightAxis = Vector3.forward;    // axis around which to rotate (transform only)
		[KSPField] public float counterweightSpinRate = 60.0f;            // counterweight rotation speed (deg/s)
		[KSPField] public float counterweightAccelerationRate = 2.0f;     // counterweight acceleration (deg/s/s)

		// fixed caracteristics determined at prefab compilation from OnLoad()
		// do not use these in configs, they are KSPField just so they are automatically copied over on part instancing
		[KSPField] public bool isDeployable;
		[KSPField] public bool isCentrifuge;
		[KSPField] public bool hasShielding;
		[KSPField] public int baseComfortsMask;

		// internal state
		private HabitatData data;
		public HabitatData HabitatData => data;

		// animation handlers
		private Animator deployAnimator;
		private Transformator rotateAnimator;
		private Transformator counterweightAnimator;

		// caching frequently used things
		private VesselData vd;
		private PartResourceWrapper atmoRes;
		private PartResourceWrapper wasteRes;
		private PartResourceWrapper shieldRes;
		private VesselResHandler vesselResHandler;
		private VesselKSPResource ecResInfo;
		private VesselKSPResource atmoResInfo;
		private VesselKSPResource wasteResInfo;
		private VesselKSPResource breathableResInfo;
		private string reclaimResAbbr;
		private BaseField mainInfoField;
		private BaseField secInfoField;
		private BaseField enableField;
		private BaseField pressureField;
		private BaseField deployField;
		private BaseField rotateField;

		public PartResourceWrapper WasteRes => wasteRes;

		// static game wide volume / surface cache
		public static Dictionary<string, Lib.PartVolumeAndSurfaceInfo> habitatDatabase;
		public const string habitatDataCacheNodeName = "KERBALISM_HABITAT_INFO";
		public static string HabitatDataCachePath => Path.Combine(Lib.KerbalismRootPath, "HabitatData.cache");

		// PAW UI
		// Note : the 4 bool using a UI_Toggle shouldn't by used from code.
		// To change the state from code, use the static Toggle() methods
		[KSPField(groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
		public string mainPAWInfo;

		[KSPField(groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
		public string secPAWInfo;

		[KSPField(groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
		[UI_Toggle(scene = UI_Scene.All, requireFullControl = false, affectSymCounterparts = UI_Scene.None)]
		public bool habitatEnabled;

		[KSPField(groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
		[UI_Toggle(scene = UI_Scene.All, affectSymCounterparts = UI_Scene.None)]
		public bool pressureEnabled;

		[KSPField(groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
		[UI_Toggle(scene = UI_Scene.All, affectSymCounterparts = UI_Scene.None)]
		public bool deployEnabled;


		[KSPField(groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
		[UI_Toggle(scene = UI_Scene.All, affectSymCounterparts = UI_Scene.None)]
		public bool rotationEnabled;

		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "debug", groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
		public string debugInfo;

		#endregion

		#region INIT

		// parsing configs at prefab compilation
		public override void OnLoad(ConfigNode node)
		{
			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				// Parse comforts
				baseComfortsMask = 0;
				foreach (string comfortString in node.GetValues("comfort"))
				{
					if (Enum.TryParse(comfortString, out Comfort comfort))
						baseComfortsMask |= (int)comfort;
					else
						Lib.Log($"Unrecognized comfort `{comfortString}` in ModuleKsmHabitat config for part {part.partName}");
				}

				// instanciate animations from config
				SetupAnimations();

				// volume/surface calcs are quite slow and memory intensive, so we do them only once on the prefab
				// then get the prefab values from OnStart. Moreover, we cache the results in the 
				// Kerbalism\HabitatData.cache file and reuse those cached results on next game launch.
				if (volume <= 0.0 || surface <= 0.0)
				{
					if (habitatDatabase == null)
					{
						ConfigNode dbRootNode = ConfigNode.Load(HabitatDataCachePath);
						ConfigNode[] habInfoNodes = dbRootNode?.GetNodes(habitatDataCacheNodeName);
						habitatDatabase = new Dictionary<string, Lib.PartVolumeAndSurfaceInfo>();

						if (habInfoNodes != null)
						{
							for (int i = 0; i < habInfoNodes.Length; i++)
							{
								string partName = habInfoNodes[i].GetValue("partName") ?? string.Empty;
								if (!string.IsNullOrEmpty(partName) && !habitatDatabase.ContainsKey(partName))
									habitatDatabase.Add(partName, new Lib.PartVolumeAndSurfaceInfo(habInfoNodes[i]));
							}
						}
					}

					// SSTU specific support copypasted from the old system, not sure how well this works
					foreach (PartModule pm in part.Modules)
					{
						if (pm.moduleName == "SSTUModularPart")
						{
							Bounds bb = Lib.ReflectionCall<Bounds>(pm, "getModuleBounds", new Type[] { typeof(string) }, new string[] { "CORE" });
							if (bb != null)
							{
								if (volume <= 0.0) volume = Lib.BoundsVolume(bb) * 0.785398; // assume it's a cylinder
								if (surface <= 0.0) surface = Lib.BoundsSurface(bb) * 0.95493; // assume it's a cylinder
							}
							return;
						}
					}

					string configPartName = part.name.Replace('.', '_');
					Lib.PartVolumeAndSurfaceInfo partInfo;
					if (!habitatDatabase.TryGetValue(configPartName, out partInfo))
					{
						// set the model to the deployed state
						if (isDeployable)
							deployAnimator.Still(1f);

						// get surface and volume
						partInfo = Lib.GetPartVolumeAndSurface(part, Settings.VolumeAndSurfaceLogging);

						habitatDatabase.Add(configPartName, partInfo);
					}

					partInfo.GetUsingMethod(
						volumeAndSurfaceMethod != Lib.VolumeAndSurfaceMethod.Best ? volumeAndSurfaceMethod : partInfo.bestMethod,
						out double infoVolume, out double infoSurface, substractAttachementNodesSurface);

					if (volume <= 0.0) volume = infoVolume;
					if (surface <= 0.0) surface = infoSurface;
				}

				// determine habitat permanent state based on if animations exists and are valid
				isDeployable = deployAnimator.IsDefined;
				isCentrifuge = rotateAnimator.IsDefined;
				hasShielding = Features.Radiation && maxShieldingFactor > 0.0;

				if (isDeployable && deployWithPressure)
					canRetract = false; // inflatables can't be retracted

				// precalculate shielding cost and add resources
				double volumeLiters = M3ToL(volume);
				double currentVolumeLiters = canPressurize ? volumeLiters : 0.0;
				Lib.AddResource(part, Settings.HabitatAtmoResource, currentVolumeLiters, volumeLiters);
				Lib.AddResource(part, Settings.HabitatWasteResource, 0.0, volumeLiters);

				// adjust depressurization speed if not specified
				if (depressurizationSpeed < 0.0)
					depressurizationSpeed = 10.0 * Math.Sqrt(volume);

				if (hasShielding)
				{
					Lib.AddResource(part, shieldingResource, 0.0, surface * maxShieldingFactor);
				}
			}
			else
			{
				ConfigNode editorDataNode = node.GetNode("EditorHabitatData");

				if (editorDataNode != null)
					data = new HabitatData(editorDataNode);
			}
		}

		// this is only for editor <--> editor and editor -> flight persistence
		public override void OnSave(ConfigNode node)
		{
			if (Lib.IsEditor() && data != null)
			{
				ConfigNode habDataNode = node.AddNode("EditorHabitatData");
				data.Save(habDataNode);
			}
		}


		public override void OnStart(StartState state)
		{
			bool isFlight = Lib.IsFlight();

			// setup animations / transformators
			SetupAnimations();

			// get references to stuff we need often
			atmoRes = new LoadedPartResourceWrapper(part.Resources[Settings.HabitatAtmoResource]);
			wasteRes = new LoadedPartResourceWrapper(part.Resources[Settings.HabitatWasteResource]);
			shieldRes = hasShielding ? new LoadedPartResourceWrapper(part.Resources[shieldingResource]) : null;

			if (isFlight)
			{
				vd = vessel.KerbalismData();
				vesselResHandler = vd.ResHandler;
			}
			else
			{
				vesselResHandler = PlannerResourceSimulator.Handler;
			}

			ecResInfo = vesselResHandler.ElectricCharge;
			atmoResInfo = (VesselKSPResource)vesselResHandler.GetResource(Settings.HabitatAtmoResource);
			wasteResInfo = (VesselKSPResource)vesselResHandler.GetResource(Settings.HabitatWasteResource);

			if (Settings.HabitatBreathableResourceRate > 0.0)
				breathableResInfo = (VesselKSPResource)vesselResHandler.GetResource(Settings.HabitatBreathableResource);

			reclaimResAbbr = PartResourceLibrary.Instance.GetDefinition(reclaimResource).abbreviation;

			// get persistent data
			// data will be restored from OnLoad (and therefore not null) only in the following cases :
			// - Part created in the editor from a saved ship (not a freshly instantiated part from the part list)
			// - Part created in flight from a just launched vessel
			if (data == null)
			{
				// in flight, we should have the data stored in VesselData > PartData, unless the part was created in flight (rescue, KIS...)
				if (isFlight)
					data = HabitatData.GetFlightReferenceFromPart(part);

				// if all other cases, this is newly instantiated part from prefab. Create the data object and put the default values.
				if (data == null)
				{
					data = new HabitatData
					{
						crewCount = Lib.CrewCount(part),
						baseVolume = volume,
						baseSurface = surface,
						baseComfortsMask = baseComfortsMask,
						animState = isDeployable ? AnimState.Retracted : AnimState.Deployed,
						isEnabled = !isDeployable
					};

					// part was created in flight (rescue, KIS...)
					if (isFlight)
					{
						// set the VesselData / PartData reference
						HabitatData.SetFlightReferenceFromPart(part, data);

						// if part is manned (rescue vessel), force enabled and deployed
						if (data.crewCount > 0)
						{
							data.animState = AnimState.Deployed;
							data.isEnabled = true;
						}

						// don't pressurize (if it's a rescue, the player will likely go on EVA immediatly anyway)
						data.pressureState = canPressurize ? PressureState.Depressurized : PressureState.AlwaysDepressurized;
					}
					// part was created in the editor : set pressurized state if available
					else
					{
						if (!canPressurize)
							data.pressureState = PressureState.AlwaysDepressurized;
						if (isDeployable && !data.IsDeployed)
							data.pressureState = PressureState.Depressurized;
						else
							data.pressureState = PressureState.Pressurized;
					}
				}
			}
			else if (isFlight)
			{
				HabitatData.SetFlightReferenceFromPart(part, data);
			}

			data.updateHandler = new HabitatUpdateHandler(vessel, vd, data, atmoRes, wasteRes, shieldRes, atmoResInfo, wasteResInfo, breathableResInfo, ecResInfo);

			// acquire a reference to the partmodule
			data.module = this;

			// ensure proper initialization by calling the right event for the current state
			if (!canPressurize)
				data.pressureState = PressureState.AlwaysDepressurized;

			switch (data.pressureState)
			{
				case PressureState.Pressurized:
					data.updateHandler.PressurizingEndEvt(); break;
				case PressureState.Pressurizing:
					data.updateHandler.PressurizingStartEvt(); break;
				case PressureState.DepressurizingAboveThreshold:
					data.updateHandler.DepressurizingStartEvt(); break;
				case PressureState.DepressurizingBelowThreshold:
					data.updateHandler.DepressurizingPassThresholdEvt(); break;
				case PressureState.Depressurized:
					data.updateHandler.DepressurizingEndEvt(); break;
				case PressureState.Breatheable:
					data.updateHandler.BreatheableStartEvt(); break;
				case PressureState.AlwaysDepressurized:
					data.updateHandler.AlwaysDepressurizedStartEvt(); break;
			}

			// setup animations state
			if (isDeployable)
			{
				deployAnimator.Still(data.IsDeployed ? 1f : 0f);
			}

			if (isCentrifuge && data.IsRotationNominal)
			{
				rotateAnimator.StartSpinInstantly();
				counterweightAnimator.StartSpinInstantly();
			}

			// PAW setup

			// synchronize PAW state with data state
			habitatEnabled = data.isEnabled;
			pressureEnabled = data.IsPressurized;
			deployEnabled = data.IsDeployed;
			rotationEnabled = data.IsRotationEnabled;

			// get BaseField references
			mainInfoField = Fields["mainPAWInfo"];
			secInfoField = Fields["secPAWInfo"];
			enableField = Fields["habitatEnabled"];
			pressureField = Fields["pressureEnabled"];
			deployField = Fields["deployEnabled"];
			rotateField = Fields["rotationEnabled"];

			// add value modified callbacks to the toggles
			enableField.OnValueModified += OnToggleHabitat;
			pressureField.OnValueModified += OnTogglePressure;
			deployField.OnValueModified += OnToggleDeploy;
			rotateField.OnValueModified += OnToggleRotation;

			// set visibility
			mainInfoField.guiActive = mainInfoField.guiActiveEditor = true;
			secInfoField.guiActive = secInfoField.guiActiveEditor = IsSecInfoVisible;
			enableField.guiActive = enableField.guiActiveEditor = CanToggleHabitat;
			pressureField.guiActive = pressureField.guiActiveEditor = CanTogglePressure;
			deployField.guiActive = deployField.guiActiveEditor = CanToggleDeploy;
			rotateField.guiActive = rotateField.guiActiveEditor = CanToggleRotate;

			// set names
			mainInfoField.guiName = "Pressure";
			enableField.guiName = "Habitat";
			pressureField.guiName = "Pressure";
			deployField.guiName = "Deployement";

			((UI_Toggle)enableField.uiControlFlight).enabledText = Lib.Color("enabled", Lib.Kolor.Green);
			((UI_Toggle)enableField.uiControlFlight).disabledText = Lib.Color("disabled", Lib.Kolor.Yellow);
			((UI_Toggle)enableField.uiControlEditor).enabledText = Lib.Color("enabled", Lib.Kolor.Green);
			((UI_Toggle)enableField.uiControlEditor).disabledText = Lib.Color("disabled", Lib.Kolor.Yellow);

#if DEBUG
			Events["LogVolumeAndSurface"].guiActiveEditor = true;
#else
			Events["LogVolumeAndSurface"].guiActiveEditor = Settings.VolumeAndSurfaceLogging;
#endif

		}

		public void OnDestroy()
		{
			// clear loaded module reference to avoid memory leaks
			if (data != null)
				data.module = null;
		}

		private void SetupAnimations()
		{
			deployAnimator = new Animator(part, deployAnim, deployAnimReverse);

			if (rotateIsTransform)
				rotateAnimator = new Transformator(part, rotateAnim, rotateAxis, rotateSpinRate, rotateAccelerationRate, rotateIsReversed, rotateIVA);
			else
				rotateAnimator = new Transformator(part, rotateAnim, rotateSpinRate, rotateAccelerationRate, rotateIsReversed);

			if (counterweightIsTransform)
				counterweightAnimator = new Transformator(part, counterweightAnim, counterweightAxis, counterweightSpinRate, counterweightAccelerationRate, counterweightIsReversed, false);
			else
				counterweightAnimator = new Transformator(part, counterweightAnim, counterweightSpinRate, counterweightAccelerationRate, counterweightIsReversed);
		}

		#endregion

		#region UPDATE

		private bool IsSecInfoVisible => data.pressureState != PressureState.AlwaysDepressurized && data.pressureState != PressureState.Breatheable;
		private bool CanToggleHabitat => data.IsDeployed;
		private bool CanTogglePressure => pressureField.guiActiveEditor = canPressurize && data.IsDeployed;
		private bool CanToggleDeploy => isDeployable && data.IsRotationStopped && !(data.IsDeployed && !canRetract && !Lib.IsEditor());
		private bool CanToggleRotate => isCentrifuge && data.IsDeployed;

		public void Update()
		{
			// TODO : Find a reliable way to have that f**** PAW correctly updated when we change guiActive...
			switch (data.animState)
			{
				case AnimState.Accelerating:
				case AnimState.Decelerating:
				case AnimState.Rotating:
				case AnimState.RotatingNotEnoughEC:
					bool loosingSpeed = data.animState == AnimState.RotatingNotEnoughEC;
					if (data.module.rotateECRate > 0.0 || data.module.accelerateECRate > 0.0)
					{
						rotateAnimator.Update(rotationEnabled, loosingSpeed, (float)ecResInfo.AvailabilityFactor);
						counterweightAnimator.Update(rotationEnabled, loosingSpeed, (float)ecResInfo.AvailabilityFactor);
					}
					else
					{
						rotateAnimator.Update(rotationEnabled, false, 1f);
						counterweightAnimator.Update(rotationEnabled, false, 1f);
					}

					break;
			}

			secInfoField.guiActive = secInfoField.guiActiveEditor = IsSecInfoVisible;
			enableField.guiActive = enableField.guiActiveEditor = CanToggleHabitat;
			pressureField.guiActive = pressureField.guiActiveEditor = CanTogglePressure;
			deployField.guiActive = deployField.guiActiveEditor = CanToggleDeploy;
			rotateField.guiActive = rotateField.guiActiveEditor = CanToggleRotate;

			if (part.PartActionWindow == null)
				return;

			debugInfo = (data.isEnabled ? "Enabled - " : "Disabled - ") + data.pressureState.ToString() + " - " + data.animState.ToString();

			double habPressure = atmoRes.Amount / atmoRes.Capacity;

			mainPAWInfo = Lib.BuildString(
				Lib.Color(habPressure > Settings.PressureThreshold, habPressure.ToString("0.00 atm"), Lib.Kolor.Green, Lib.Kolor.Orange),
				volume.ToString(" (0.0 m3)"),
				" Crew:", " ", data.crewCount.ToString(), "/", part.CrewCapacity.ToString());

			if (IsSecInfoVisible)
			{
				switch (data.pressureState)
				{
					case PressureState.Pressurized:
					case PressureState.DepressurizingAboveThreshold:
					case PressureState.DepressurizingBelowThreshold:
						secInfoField.guiName = "Depressurization";
						double reclaimedResAmount = Math.Max(atmoRes.Amount - (atmoRes.Capacity * (1.0 - reclaimAtmosphereFactor)), 0.0);
						secPAWInfo = Lib.BuildString(
							Lib.HumanReadableCountdown(atmoRes.Amount / depressurizationSpeed), ", +",
							Lib.HumanReadableAmountCompact(reclaimedResAmount), " ", reclaimResAbbr);
						break;

					case PressureState.Depressurized:
					case PressureState.Pressurizing:
						secInfoField.guiName = "Pressurization";
						double requiredResAmount = Math.Max((atmoRes.Capacity * Settings.PressureThreshold) - atmoRes.Amount, 0.0);
						secPAWInfo = Lib.BuildString(Lib.HumanReadableAmountCompact(requiredResAmount), " ", reclaimResAbbr, " ", "required");
						break;
				}
			}

			if (CanToggleDeploy)
			{
				string state = string.Empty;
				switch (data.animState)
				{
					case AnimState.Retracted:
						state = Lib.Color(deployWithPressure ? "deflated" : "retracted", Lib.Kolor.Yellow);
						break;
					case AnimState.Deploying:
						state = Lib.Color(deployWithPressure ? "inflating" : "deploying", Lib.Kolor.Yellow);
						break;
					case AnimState.Retracting:
						state = Lib.Color(deployWithPressure ? "deflating" : "retracting", Lib.Kolor.Yellow);
						break;
					case AnimState.Deployed:
					case AnimState.Accelerating:
					case AnimState.Decelerating:
					case AnimState.Rotating:
					case AnimState.RotatingNotEnoughEC:
						state = Lib.Color(deployWithPressure ? "inflated" : "deployed", Lib.Kolor.Green);
						break;
					case AnimState.Stuck:
						state = Lib.Color("stuck", Lib.Kolor.Orange);
						break;
				}

				((UIPartActionToggle)deployField.uiControlEditor?.partActionItem)?.fieldStatus?.SetText(state);
				((UIPartActionToggle)deployField.uiControlFlight?.partActionItem)?.fieldStatus?.SetText(state);
			}

			if (CanTogglePressure)
			{
				string state = string.Empty;
				switch (data.pressureState)
				{
					case PressureState.Pressurized:
						state = Lib.Color("pressurized", Lib.Kolor.Green);
						break;
					case PressureState.Depressurized:
						state = Lib.Color("depressurized", Lib.Kolor.Yellow);
						break;
					case PressureState.Breatheable:
						state = Lib.Color("external", Lib.Kolor.Green);
						break;
					case PressureState.Pressurizing:
						state = Lib.Color("pressurizing", Lib.Kolor.Yellow);
						break;
					case PressureState.DepressurizingAboveThreshold:
						state = Lib.Color("depressurizing", Lib.Kolor.Green);
						break;
					case PressureState.DepressurizingBelowThreshold:
						state = Lib.Color("depressurizing", Lib.Kolor.Yellow);
						break;
				}

				((UIPartActionToggle)pressureField.uiControlEditor?.partActionItem)?.fieldStatus?.SetText(state);
				((UIPartActionToggle)pressureField.uiControlFlight?.partActionItem)?.fieldStatus?.SetText(state);
			}

			if (CanToggleRotate)
			{
				string label = string.Empty;
				string status = string.Empty;
				switch (data.animState)
				{
					case AnimState.Deployed:
						label = "Rotation";
						status = Lib.Color("stopped", Lib.Kolor.Yellow);
						break;
					case AnimState.Accelerating:
						label = Lib.BuildString("Rotation", " ", data.module.rotateAnimator.CurrentSpeed.ToString("F1"), "/", data.module.rotateAnimator.NominalSpeed.ToString("F1"), "°/s");
						status = Lib.Color("starting", Lib.Kolor.Yellow);
						break;
					case AnimState.Decelerating:
						label = Lib.BuildString("Rotation", " ", data.module.rotateAnimator.CurrentSpeed.ToString("0.0°/s"));
						status = Lib.Color("stopping", Lib.Kolor.Yellow);
						break;
					case AnimState.Rotating:
						label = Lib.BuildString("Rotation", " ", data.module.rotateAnimator.CurrentSpeed.ToString("0.0°/s"));
						status = Lib.Color("nominal", Lib.Kolor.Green);
						break;
					case AnimState.RotatingNotEnoughEC:
						label = Lib.BuildString("Rotation", " ", data.module.rotateAnimator.CurrentSpeed.ToString("F1"), "/", data.module.rotateAnimator.NominalSpeed.ToString("F1"), "°/s");
						status = Lib.Color("missing EC", Lib.Kolor.Orange);
						break;
					case AnimState.Stuck:
						label = "Rotation";
						status = Lib.Color("bad position", Lib.Kolor.Orange);
						break;
				}

				((UIPartActionToggle)rotateField.uiControlEditor?.partActionItem)?.fieldName?.SetText(label);
				((UIPartActionToggle)rotateField.uiControlEditor?.partActionItem)?.fieldStatus?.SetText(status);
				((UIPartActionToggle)rotateField.uiControlFlight?.partActionItem)?.fieldName?.SetText(label);
				((UIPartActionToggle)rotateField.uiControlFlight?.partActionItem)?.fieldStatus?.SetText(status);
			}
		}

		private void FixedUpdate()
		{
			data.updateHandler.Update(Kerbalism.elapsed_s);
		}

		public static void BackgroundUpdate(Vessel v, VesselData vd, ProtoPartSnapshot protoPart, ModuleKsmHabitat prefab, double elapsed_s)
		{
			PartData partData;
			if (!vd.Parts.TryGet(protoPart.flightID, out partData))
			{
				Lib.Log($"PartData wasn't found for {protoPart.partName} with id {protoPart.flightID}", Lib.LogLevel.Error);
				return;
			}

			if (partData.Habitat == null)
			{
				Lib.Log($"HabitatData wasn't found for {protoPart.partName} with id {protoPart.flightID}", Lib.LogLevel.Error);
				return;
			}

			if (partData.Habitat.updateHandler == null || !partData.Habitat.updateHandler.IsValid())
			{
				HabitatData data = partData.Habitat;

				data.module = prefab;

				PartResourceWrapper atmoRes = null;
				PartResourceWrapper wasteRes = null;
				PartResourceWrapper shieldRes = null;

				foreach (ProtoPartResourceSnapshot protoResource in protoPart.resources)
				{
					if (protoResource.resourceName == Settings.HabitatAtmoResource)
						atmoRes = new ProtoPartResourceWrapper(protoResource);
					else if (protoResource.resourceName == Settings.HabitatWasteResource)
						wasteRes = new ProtoPartResourceWrapper(protoResource);
					else if (protoResource.resourceName == data.module.shieldingResource)
						shieldRes = new ProtoPartResourceWrapper(protoResource);
				}

				if (atmoRes == null)
				{
					Lib.Log($"Atmosphere resource wasn't found on {protoPart.partName}", Lib.LogLevel.Warning);
					return;
				}

				VesselKSPResource ecResInfo = vd.ResHandler.ElectricCharge;
				VesselKSPResource atmoResInfo = (VesselKSPResource)vd.ResHandler.GetResource(Settings.HabitatAtmoResource);
				VesselKSPResource wasteResInfo = (VesselKSPResource)vd.ResHandler.GetResource(Settings.HabitatWasteResource);
				VesselKSPResource breathableResInfo;
				if (Settings.HabitatBreathableResourceRate > 0.0)
					breathableResInfo = (VesselKSPResource)vd.ResHandler.GetResource(Settings.HabitatBreathableResource);
				else
					breathableResInfo = null;

				data.updateHandler = new HabitatUpdateHandler(v, vd, data, atmoRes, wasteRes, shieldRes, atmoResInfo, wasteResInfo, breathableResInfo, ecResInfo);
			}

			partData.Habitat.updateHandler.Update(elapsed_s);
		}

		#endregion

		#region EDITOR/LOADED/UNLOADED STATE MACHINE
		public class HabitatUpdateHandler
		{
			private Vessel vessel;
			private VesselData vd;
			private HabitatData data;

			private PartResourceWrapper atmoRes;
			private PartResourceWrapper wasteRes;
			private PartResourceWrapper shieldRes;

			private VesselKSPResource atmoResInfo;
			private VesselKSPResource wasteResInfo;
			private VesselKSPResource breathableResInfo;
			private VesselKSPResource ecResInfo;

			bool isEditor;
			bool isLoaded;

			public HabitatUpdateHandler(Vessel vessel, VesselData vd, HabitatData data,
				PartResourceWrapper atmoRes, PartResourceWrapper wasteRes, PartResourceWrapper shieldRes,
				VesselKSPResource atmoResInfo, VesselKSPResource wasteResInfo, VesselKSPResource breathableResInfo, VesselKSPResource ecResInfo)
			{
				this.vessel = vessel;
				this.vd = vd;
				this.data = data;
				this.atmoRes = atmoRes;
				this.wasteRes = wasteRes;
				this.shieldRes = shieldRes;
				this.atmoResInfo = atmoResInfo;
				this.wasteResInfo = wasteResInfo;
				this.ecResInfo = ecResInfo;
				this.breathableResInfo = breathableResInfo;
				isEditor = Lib.IsEditor();
				isLoaded = (!isEditor && vessel.loaded) || isEditor;
			}

			public bool IsValid() => isEditor == Lib.IsEditor() && isLoaded == ((!isEditor && vessel.loaded) || isEditor);

			public void Update(double elapsed_s)
			{
				AnimationsUpdate(elapsed_s);
				PressureUpdate(elapsed_s);
				data.shieldingAmount = data.module.hasShielding ? shieldRes.Amount : 0.0;
			}

			private void AnimationsUpdate(double elapsed_s)
			{
				// animations state machine
				if (isLoaded)
				{
					switch (data.animState)
					{
						case AnimState.Deploying:
							if (data.module.deployWithPressure)
								break;

							if (data.module.deployAnimator.Playing)
							{
								if (!isEditor && data.module.deployECRate > 0.0)
								{
									ecResInfo.Consume(data.module.deployECRate * elapsed_s, ResourceBroker.Habitat);
									data.module.deployAnimator.ChangeSpeed((float)ecResInfo.AvailabilityFactor);
								}
							}
							else
							{
								data.animState = AnimState.Deployed;
								TryToggleHabitat(data, true);
							}
							break;
						case AnimState.Retracting:
							if (data.module.deployAnimator.Playing)
							{
								if (!isEditor && data.module.deployECRate > 0.0)
								{
									ecResInfo.Consume(data.module.deployECRate * elapsed_s, ResourceBroker.Habitat);
									data.module.deployAnimator.ChangeSpeed((float)ecResInfo.AvailabilityFactor);
								}
							}
							else
							{
								data.animState = AnimState.Retracted;
							}
							break;
						case AnimState.Accelerating:
							if (data.module.rotateAnimator.IsSpinningNominal)
							{
								if (!isEditor && data.module.rotateECRate > 0.0)
									ecResInfo.Consume(data.module.rotateECRate * elapsed_s, ResourceBroker.GravityRing);

								data.animState = AnimState.Rotating;
							}
							else if (data.module.rotateAnimator.IsStopped)
							{
								data.animState = AnimState.Stuck;
								data.module.rotationEnabled = false;
							}
							else
							{
								if (!isEditor && data.module.accelerateECRate > 0.0)
									ecResInfo.Consume(data.module.accelerateECRate * elapsed_s, ResourceBroker.GravityRing);
							}
							break;
						case AnimState.Decelerating:

							if (data.module.rotateAnimator.IsStopped)
							{
								data.animState = AnimState.Deployed;
								data.module.rotationEnabled = false;
							}

							break;
						case AnimState.Rotating:
							if (data.module.rotateECRate > 0.0)
							{
								ecResInfo.Consume(data.module.rotateECRate * elapsed_s, ResourceBroker.GravityRing);

								if (ecResInfo.AvailabilityFactor < 1.0)
									data.animState = AnimState.RotatingNotEnoughEC;
							}
							break;
						case AnimState.RotatingNotEnoughEC:
							if (!isEditor && data.module.rotateECRate > 0.0)
							{
								ecResInfo.Consume(data.module.rotateECRate * elapsed_s, ResourceBroker.GravityRing);

								if (ecResInfo.AvailabilityFactor == 1.0)
								{
									data.animState = AnimState.Accelerating;
								}
								else if (data.module.rotateAnimator.IsStopped)
								{
									data.animState = AnimState.Stuck;
									data.module.rotationEnabled = false;
								}
							}
							break;
					}
				}
				else
				{
					switch (data.animState)
					{
						case AnimState.Deploying:
						case AnimState.Retracting:
							double deploySpeedFactor = 1.0;
							if (data.module.deployECRate > 0.0)
							{
								double timeSpent = Math.Min(elapsed_s, data.animTimer);
								ecResInfo.Consume(data.module.deployECRate * timeSpent, ResourceBroker.Habitat);
								deploySpeedFactor = ecResInfo.AvailabilityFactor;
							}
							data.animTimer -= elapsed_s * deploySpeedFactor;

							if (data.animTimer <= 0.0)
								data.animState = data.animState == AnimState.Deploying ? AnimState.Deployed : AnimState.Retracted;

							break;

						case AnimState.Accelerating:

							double accelSpeedFactor = 1.0;
							if (data.module.accelerateECRate > 0.0)
							{
								double timeSpent = Math.Min(elapsed_s, data.animTimer);
								ecResInfo.Consume((data.module.accelerateECRate + data.module.rotateECRate) * timeSpent, ResourceBroker.GravityRing);
								accelSpeedFactor = ecResInfo.AvailabilityFactor;
							}

							//accelSpeedFactor -= Transformator.spinLosses / data.module.rotateAccelerationRate;

							data.animTimer -= elapsed_s * accelSpeedFactor;

							if (data.animTimer > data.module.rotateAnimator.TimeNeededToStartOrStop)
								data.animState = AnimState.Deployed;
							else if (data.animTimer <= 0.0)
								data.animState = AnimState.Rotating;

							break;
						case AnimState.Decelerating:

							data.animTimer -= elapsed_s;

							if (data.animTimer <= 0.0)
								data.animState = AnimState.Deployed;

							break;
						case AnimState.Rotating:

							if (data.module.rotateECRate > 0.0)
								ecResInfo.Consume(data.module.rotateECRate * elapsed_s, ResourceBroker.GravityRing);

							if (ecResInfo.AvailabilityFactor < 1.0)
							{
								//double speedLost = Transformator.spinLosses * elapsed_s * ecResInfo.AvailabilityFactor;
								double speedLost = elapsed_s * ecResInfo.AvailabilityFactor;
								data.animTimer = data.module.rotateAnimator.TimeNeededToStartOrStop * Math.Min(speedLost / data.module.rotateSpinRate, 1.0);
								data.animState = AnimState.Accelerating;
							}
							break;
					}
				}
			}

			private void PressureUpdate(double elapsed_s)
			{
				switch (data.pressureState)
				{
					case PressureState.Pressurized:
						// if pressure drop below the minimum habitable pressure, switch to partial pressure state
						if (atmoRes.Amount / atmoRes.Capacity < Settings.PressureThreshold)
							PressureDroppedEvt();
						break;
					case PressureState.Breatheable:

						//if (!isEditor)
						//	atmoResInfo.equalizeMode = ResourceInfo.EqualizeMode.Disabled;

						if (!isEditor && !vd.EnvInBreathableAtmosphere)
						{
							if (data.module.canPressurize)
								PressureDroppedEvt();
							else
								AlwaysDepressurizedStartEvt();
							break;
						}

						// magic oxygen supply
						if (breathableResInfo != null && data.crewCount > 0)
							breathableResInfo.Produce(data.crewCount * Settings.HabitatBreathableResourceRate * elapsed_s, ResourceBroker.Environment);

						// magic scrubbing
						wasteRes.Amount = 0.0; 

						// equalize pressure with external pressure
						atmoRes.Amount = Math.Min(vd.EnvStaticPressure * atmoRes.Capacity, atmoRes.Capacity);

						break;
					case PressureState.AlwaysDepressurized:

						if (!isEditor)
						{
							if (vd.EnvInBreathableAtmosphere)
							{
								BreatheableStartEvt();
								break;
							}
							else if (vd.EnvInOxygenAtmosphere)
							{
								atmoRes.Amount = Math.Min(vd.EnvStaticPressure * atmoRes.Capacity, atmoRes.Capacity);
							}
						}

						break;

					case PressureState.Depressurized:

						if (!isEditor)
						{
							if (data.module.isDeployable && data.module.deployWithPressure && !data.IsDeployed)
							{
								break;
							}

							if (vd.EnvInBreathableAtmosphere)
							{
								BreatheableStartEvt();
								break;
							}
							else if (vd.EnvInOxygenAtmosphere)
							{
								atmoRes.Amount = Math.Min(vd.EnvStaticPressure * atmoRes.Capacity, atmoRes.Capacity);
							}
						}
						break;
					case PressureState.Pressurizing:

						if (!isEditor)
							atmoResInfo.equalizeMode = VesselKSPResource.EqualizeMode.Disabled;

						if (vessel.loaded && data.module.deployWithPressure && data.animState == AnimState.Deploying)
							data.module.deployAnimator.Still(Math.Min((float)(atmoRes.Amount / (atmoRes.Capacity * Settings.PressureThreshold)), 1f));

						// if pressure go back to the minimum habitable pressure, switch to pressurized state
						if (atmoRes.Amount / atmoRes.Capacity > Settings.PressureThreshold)
							PressurizingEndEvt();
						break;
					case PressureState.DepressurizingAboveThreshold:
					case PressureState.DepressurizingBelowThreshold:

						// if fully depressurized, go to the depressurized state
						if (atmoRes.Amount <= 0.0)
						{
							DepressurizingEndEvt();
							break;
						}
						// if external pressure is less than the hab pressure, stop depressurization and go to the breathable state
						else if (vd.EnvInOxygenAtmosphere && atmoRes.Amount / atmoRes.Capacity < vd.EnvStaticPressure)
						{
							DepressurizingEndEvt();
							break;
						}
						// pressure is going below the survivable threshold : time for kerbals to put their helmets
						else if (data.pressureState == PressureState.DepressurizingAboveThreshold && atmoRes.Amount / atmoRes.Capacity < Settings.PressureThreshold)
						{
							DepressurizingPassThresholdEvt();
						}

						double ecFactor;
						if (data.module.depressurizeECRate > 0.0)
						{
							ecResInfo.Consume(data.module.depressurizeECRate * elapsed_s, ResourceBroker.Habitat);
							ecFactor = ecResInfo.AvailabilityFactor;
						}
						else
						{
							ecFactor = 1.0;
						}

						double newAtmoAmount = atmoRes.Amount - (data.module.depressurizationSpeed * elapsed_s * ecFactor);
						newAtmoAmount = Math.Max(newAtmoAmount, 0.0);

						// we only vent CO2 when the kerbals aren't yet in their helmets
						if (data.pressureState == PressureState.DepressurizingAboveThreshold)
						{
							wasteRes.Amount *= atmoRes.Amount > 0.0 ? newAtmoAmount / atmoRes.Amount : 1.0;
							wasteRes.Amount = Lib.Clamp(wasteRes.Amount, 0.0, wasteRes.Capacity);
						}

						// convert the atmosphere into the reclaimed resource if the pressure is above the pressure threshold defined in the module config 
						if (data.module.reclaimAtmosphereFactor > 0.0 && newAtmoAmount / atmoRes.Capacity >= 1.0 - data.module.reclaimAtmosphereFactor)
						{
							vd.ResHandler.Produce(data.module.reclaimResource, atmoRes.Amount - newAtmoAmount, ResourceBroker.Habitat);
						}

						atmoRes.Amount = newAtmoAmount;

						break;
				}

				// synchronize resource amounts to the persisted data
				data.atmoAmount = LToM3(atmoRes.Amount);
				data.wasteLevel = wasteRes.Capacity > 0.0 ? wasteRes.Amount / wasteRes.Capacity : 0.0;

				// set equalizaton mode if it hasn't been explictely disabled in the breathable / depressurizing states
				if (!isEditor)
				{
					if (atmoResInfo.equalizeMode == VesselKSPResource.EqualizeMode.NotSet)
						atmoResInfo.equalizeMode = VesselKSPResource.EqualizeMode.Enabled;

					if (wasteResInfo.equalizeMode == VesselKSPResource.EqualizeMode.NotSet)
						wasteResInfo.equalizeMode = VesselKSPResource.EqualizeMode.Enabled;
				}
			}

			private void PressureDroppedEvt()
			{
				OnHelmetStateChanged();

				// go to pressurizing state
				data.pressureState = PressureState.Pressurizing;
			}

			public void BreatheableStartEvt()
			{
				atmoRes.FlowState = false;

				wasteRes.Capacity = M3ToL(data.module.volume);
				wasteRes.FlowState = false;

				OnHelmetStateChanged();

				data.pressureState = PressureState.Breatheable;
			}

			public void AlwaysDepressurizedStartEvt()
			{
				atmoRes.FlowState = false;

				wasteRes.Capacity = data.crewCount * Settings.PressureSuitVolume;
				wasteRes.FlowState = true;

				OnHelmetStateChanged();

				data.pressureState = PressureState.AlwaysDepressurized;
			}

			public void PressurizingStartEvt()
			{
				atmoRes.FlowState = true;

				if (!isEditor)
				{
					atmoResInfo.equalizeMode = VesselKSPResource.EqualizeMode.Disabled;
					data.pressureState = PressureState.Pressurizing;
				}
				else
				{
					PressurizingEndEvt();
				}
			}

			public void PressurizingEndEvt()
			{
				wasteRes.Capacity = M3ToL(data.module.volume);
				wasteRes.FlowState = true;
				atmoRes.FlowState = true;

				if (!isEditor)
				{
					OnHelmetStateChanged();

					if (data.module.deployWithPressure && data.animState == AnimState.Deploying)
					{
						if (vessel.loaded)
							data.module.deployAnimator.Still(1f);

						data.animState = AnimState.Deployed;
						TryToggleHabitat(data, true);
					}
				}
				else
				{
					atmoRes.Amount = M3ToL(data.module.volume);

					if (data.module.isDeployable && data.module.deployWithPressure && !data.IsDeployed)
						data.module.deployAnimator.Play(false, false, data.module.OnDeployCallback, 5f);
				}

				data.pressureState = PressureState.Pressurized;
			}

			public void DepressurizingStartEvt()
			{
				atmoRes.FlowState = false;
				wasteRes.FlowState = false;

				if (isEditor)
					DepressurizingEndEvt();
				else if (atmoRes.Amount / atmoRes.Capacity >= Settings.PressureThreshold)
					data.pressureState = PressureState.DepressurizingAboveThreshold;
				else
					DepressurizingPassThresholdEvt();
			}

			public void DepressurizingPassThresholdEvt()
			{
				atmoRes.FlowState = false;

				double suitsVolume = data.crewCount * Settings.PressureSuitVolume;
				// make the CO2 level in the suit the same as the current CO2 level in the part by adjusting the amount
				// We only do it if the part is crewed, because since it discard nearly all the CO2 in the part, it can
				// be exploited to remove CO2, by stopping the depressurization immediatly.
				if (data.crewCount > 0)
					wasteRes.Amount *= suitsVolume / wasteRes.Capacity;

				wasteRes.Capacity = suitsVolume;
				wasteRes.FlowState = true; // kerbals are now in their helmets and CO2 won't be vented anymore, let the suits CO2 level equalize with the vessel CO2 level

				OnHelmetStateChanged();

				data.pressureState = PressureState.DepressurizingBelowThreshold;
			}

			public void DepressurizingEndEvt()
			{
				atmoRes.FlowState = false;

				if (isEditor)
				{
					atmoRes.Amount = 0.0;
					wasteRes.Amount = 0.0;

					wasteRes.Capacity = data.crewCount * Settings.PressureSuitVolume;
					wasteRes.FlowState = true;
					data.pressureState = PressureState.Depressurized;

					if (data.module.isDeployable && data.module.deployWithPressure && data.module.deployAnimator.NormalizedTime != 0f)
						data.module.deployAnimator.Play(true, false, null, 5f);
				}
				else
				{
					if (vd.EnvInBreathableAtmosphere)
					{
						wasteRes.Capacity = M3ToL(data.module.volume);
						wasteRes.FlowState = false;
						data.pressureState = PressureState.Breatheable; // don't go to breathable start to avoid resetting the portraits, we already have locked flowstate anyway
					}
					else
					{
						wasteRes.Capacity = data.crewCount * Settings.PressureSuitVolume;
						wasteRes.FlowState = true;
						data.pressureState = PressureState.Depressurized;
					}
				}
			}


			// make the kerbals put or remove their helmets
			// this works in conjunction with the SpawnCrew prefix patch that check if the part is pressurized or not on spawning the IVA.
			private void OnHelmetStateChanged()
			{
				if (!isEditor && data.crewCount > 0 && vessel.isActiveVessel && data.module.part.internalModel != null)
					Lib.RefreshIVAAndPortraits();
			}

		}

		#endregion

		#region ENABLE / DISABLE LOGIC & UI

		private void OnToggleHabitat(object field) => TryToggleHabitat(data, true);

		public static bool TryToggleHabitat(HabitatData data, bool isLoaded)
		{
			if (data.isEnabled && data.crewCount > 0)
			{
				if (!isLoaded)
				{
					Message.Post($"Can't disable a crewed habitat on an unloaded vessel");
					return false;
				}

				if (Lib.IsEditor())
				{
					Lib.EditorClearPartCrew(data.module.part);
				}
				else
				{
					List<ProtoCrewMember> crewLeft = Lib.TryTransferCrewElsewhere(data.module.part, false);

					if (crewLeft.Count > 0)
					{
						string message = "Not enough crew capacity in the vessel to transfer those Kerbals :\n";
						crewLeft.ForEach(a => message += a.displayName + "\n");
						Message.Post($"Habitat in {data.module.part.partInfo.title} couldn't be disabled.", message);
						if (isLoaded)
							data.module.habitatEnabled = true;
						return false;
					}
					else
					{
						Message.Post($"Habitat in {data.module.part.partInfo.title} has been disabled.", "Crew was transfered in the rest of the vessel");
					}
				}
			}

			if (data.isEnabled)
			{
				if (data.IsRotationEnabled)
					ToggleRotate(data, isLoaded);

				if (isLoaded)
					data.module.habitatEnabled = false;

				data.isEnabled = false;
			}
			else
			{
				if (!data.IsDeployed)
					return false;

				if (isLoaded)
					data.module.habitatEnabled = true;

				data.isEnabled = true;
			}
			return true;
		}

		#endregion

		#region ENABLE / DISABLE PRESSURE & UI

		public void OnTogglePressure(object field) => TryTogglePressure(data, true);

		/// <summary> try to deploy or retract the habitat. isLoaded must be set to true in the editor and for a loaded vessel, false for an unloaded vessel</summary>
		public static bool TryTogglePressure(HabitatData data, bool isLoaded)
		{
			if (!data.module.canPressurize || !data.IsDeployed)
			{
				return false;
			}

			switch (data.pressureState)
			{
				case PressureState.Pressurized:
				case PressureState.Pressurizing:
					data.updateHandler.DepressurizingStartEvt();
					if (isLoaded)
						data.module.pressureEnabled = false;
					break;
				case PressureState.Breatheable:
				case PressureState.Depressurized:
				case PressureState.DepressurizingAboveThreshold:
				case PressureState.DepressurizingBelowThreshold:
					data.updateHandler.PressurizingStartEvt();
					if (isLoaded)
						data.module.pressureEnabled = true;
					break;
			}

			return true;
		}

		#endregion

		#region DEPLOY & ROTATE

		private void OnDeployCallback()
		{
			data.animState = AnimState.Deployed;
			TryToggleHabitat(data, true);
		}

		private void OnToggleDeploy(object field) => TryToggleDeploy(data, true);

		/// <summary> try to deploy or retract the habitat. isLoaded must be set to true in the editor and for a loaded vessel, false for an unloaded vessel</summary>
		public static bool TryToggleDeploy(HabitatData data, bool isLoaded)
		{
			if (!data.module.isDeployable)
			{
				data.module.deployEnabled = false;
				return false;
			}

			bool isEditor = Lib.IsEditor();

			// retract
			if (data.IsDeployed)
			{
				if (isEditor)
				{
					if (data.module.canPressurize && data.IsPressurized)
						TryTogglePressure(data, isLoaded);

					if (data.isEnabled && !TryToggleHabitat(data, isLoaded))
					{
						data.module.deployEnabled = true;
						return false;
					}
				}
				else
				{
					if (!data.module.canRetract)
					{
						data.module.deployEnabled = false;
						return false;
					}

					if (!data.IsFullyDepressurized)
					{
						Message.Post($"Can't retract \n{data.module.part.partInfo.title}", "It's still pressurized !");

						if (isLoaded)
							data.module.deployEnabled = true;

						return false;
					}

					if (data.isEnabled && !TryToggleHabitat(data, isLoaded))
					{
						if (isLoaded)
							data.module.deployEnabled = true;

						return false;
					}
				}

				data.animState = AnimState.Retracting;

				if (isLoaded)
				{
					data.module.deployAnimator.Play(true, false, null, isEditor ? 5f : 1f);
					data.module.deployEnabled = false;
				}
				else
				{
					data.animTimer = data.module.deployAnimator.AnimDuration;
				}
			}
			// deploy
			else
			{
				data.animState = AnimState.Deploying;

				if (data.module.deployWithPressure)
				{
					data.updateHandler.PressurizingStartEvt();
					if (isLoaded)
						data.module.deployEnabled = true;
				}
				else
				{
					if (isLoaded)
					{
						data.module.deployAnimator.Play(false, false, null, isEditor ? 5f : 1f);
						data.module.deployEnabled = true;
					}
					else
					{
						data.animTimer = data.module.deployAnimator.AnimDuration;
					}
				}

			}

			return true;

		}

		private void OnToggleRotation(object field) => ToggleRotate(data, true);

		public static void ToggleRotate(HabitatData data, bool isLoaded)
		{
			bool isEditor = Lib.IsEditor();

			if (data.IsRotationEnabled)
			{
				data.animState = AnimState.Decelerating;
				if (!isLoaded)
				{
					data.animTimer = data.module.rotateAnimator.TimeNeededToStartOrStop;
				}
				else
				{
					data.module.rotationEnabled = false;
					if (isEditor)
					{
						data.module.rotateAnimator.StopSpinInstantly();
						data.module.counterweightAnimator.StopSpinInstantly();
					}
				}
			}
			else if (data.IsDeployed)
			{
				data.animState = AnimState.Accelerating;

				if (!isLoaded)
				{
					data.animTimer = data.module.rotateAnimator.TimeNeededToStartOrStop;
				}
				else
				{
					data.module.rotationEnabled = true;
					if (isEditor)
					{
						data.module.rotateAnimator.StartSpinInstantly();
						data.module.counterweightAnimator.StartSpinInstantly();
					}
				}
			}
		}

		// debug
		[KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "[Debug] log volume/surface", groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
		public void LogVolumeAndSurface() => Lib.GetPartVolumeAndSurface(part, true);

		#endregion

		#region UI INFO

		// specifics support
		public Specifics Specs()
		{
			string ecAbbr = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").abbreviation;

			Specifics specs = new Specifics();
			specs.Add(Local.Habitat_info1, Lib.HumanReadableVolume(volume > 0.0 ? volume : Lib.PartBoundsVolume(part)) + (volume > 0.0 ? "" : " (bounds)"));//"Volume"
			specs.Add(Local.Habitat_info2, Lib.HumanReadableSurface(surface > 0.0 ? surface : Lib.PartBoundsSurface(part)) + (surface > 0.0 ? "" : " (bounds)"));//"Surface"
			specs.Add("");

			if (!canPressurize)
			{
				specs.Add(Lib.Color("Unpressurized", Lib.Kolor.Orange, true));
				specs.Add(Lib.Italic("Living in a suit is stressful"));
			}
			else
			{
				specs.Add("Depressurization", depressurizationSpeed.ToString("0.0 L/s"));
				if (canPressurize && reclaimAtmosphereFactor > 0.0)
				{
					double reclaimedAmount = reclaimAtmosphereFactor * M3ToL(volume);
					specs.Add(Lib.Bold(reclaimAtmosphereFactor.ToString("P0")) + " " + "reclaimed",  Lib.HumanReadableAmountCompact(reclaimedAmount) + " " + PartResourceLibrary.Instance.GetDefinition(reclaimResource).abbreviation);
					if (depressurizeECRate > 0.0)
						specs.Add("Require", Lib.Color(Lib.HumanReadableRate(depressurizeECRate, "F3", ecAbbr), Lib.Kolor.NegRate));
				}
			}

			specs.Add("");
			if (maxShieldingFactor > 0.0)
				specs.Add("Shielding", "max" + " " + maxShieldingFactor.ToString("P0"));
			else
				specs.Add(Lib.Color("No radiation shielding", Lib.Kolor.Orange, true));

			

			if (isDeployable)
			{
				specs.Add("");
				specs.Add(Lib.Color(deployWithPressure ? Local.Habitat_info4 : "Deployable", Lib.Kolor.Cyan));

				if (deployWithPressure || !canRetract)
					specs.Add("Non-retractable");

				if (deployECRate > 0.0)
					specs.Add("Require", Lib.Color(Lib.HumanReadableRate(deployECRate, "F3", ecAbbr), Lib.Kolor.NegRate));
			}

			if (isCentrifuge)
			{
				specs.Add("");
				specs.Add(Lib.Color("Gravity ring", Lib.Kolor.Cyan));
				specs.Add("Comfort bonus", Settings.ComfortFirmGround.ToString("P0"));
				specs.Add("Acceleration", Lib.Color(Lib.HumanReadableRate(accelerateECRate, "F3", ecAbbr), Lib.Kolor.NegRate));
				specs.Add("Steady state", Lib.Color(Lib.HumanReadableRate(rotateECRate, "F3", ecAbbr), Lib.Kolor.NegRate));
			}

			if (baseComfortsMask > 0)
			{
				specs.Add("");
				specs.Add(Lib.Color("Comfort", Lib.Kolor.Cyan), ComfortCommaList(baseComfortsMask));
				specs.Add("Bonus", GetComfortFactor(baseComfortsMask, false).ToString("P0"));
			}

			return specs;
		}

		// part tooltip
		public override string GetInfo()
		{
			return Specs().Info();
		}

		public override string GetModuleDisplayName() { return Local.Habitat; }//"Habitat"

		public string GetModuleTitle() => Local.Habitat;

		public Callback<Rect> GetDrawModulePanelCallback() => null;

		public string GetPrimaryField()
		{
			return Lib.BuildString(
				Lib.Bold(Local.Habitat + " " + Local.Habitat_info1), // "Habitat" + "Volume"
				" : ",
				Lib.HumanReadableVolume(volume > 0.0 ? volume : Lib.PartBoundsVolume(part)),
				volume > 0.0 ? "" : " (bounds)");
		}

#endregion

	}

	#region STATIC METHODS

	public static class HabitatLib
	{
		public static double M3ToL(double cubicMeters) => cubicMeters * 1000.0;

		public static double LToM3(double liters) => liters * 0.001;

		public static double GetComfortFactor(int comfortMask, bool clamped = true)
		{
			double factor = clamped ? 0.1 : 0.0;

			if (PreferencesComfort.Instance != null)
			{
				if ((comfortMask & (int)Comfort.firmGround) != 0) factor += PreferencesComfort.Instance.firmGround;
				if ((comfortMask & (int)Comfort.notAlone) != 0) factor += PreferencesComfort.Instance.notAlone;
				if ((comfortMask & (int)Comfort.callHome) != 0) factor += PreferencesComfort.Instance.callHome;
				if ((comfortMask & (int)Comfort.exercice) != 0) factor += PreferencesComfort.Instance.exercise;
				if ((comfortMask & (int)Comfort.panorama) != 0) factor += PreferencesComfort.Instance.panorama;
				if ((comfortMask & (int)Comfort.plants) != 0) factor += PreferencesComfort.Instance.plants;
			}
			else
			{
				if ((comfortMask & (int)Comfort.firmGround) != 0) factor += Settings.ComfortFirmGround;
				if ((comfortMask & (int)Comfort.notAlone) != 0) factor += Settings.ComfortNotAlone;
				if ((comfortMask & (int)Comfort.callHome) != 0) factor += Settings.ComfortCallHome;
				if ((comfortMask & (int)Comfort.exercice) != 0) factor += Settings.ComfortExercise;
				if ((comfortMask & (int)Comfort.panorama) != 0) factor += Settings.ComfortPanorama;
				if ((comfortMask & (int)Comfort.plants) != 0) factor += Settings.ComfortPlants;
			}

			return Math.Min(factor, 1.0);
		}

		public static string ComfortCommaList(int comfortMask)
		{
			string[] comforts = new string[6];
			if ((comfortMask & (int)Comfort.firmGround) != 0) comforts[0] = Local.Comfort_firmground;
			if ((comfortMask & (int)Comfort.notAlone) != 0) comforts[1] = Local.Comfort_notalone;
			if ((comfortMask & (int)Comfort.callHome) != 0) comforts[2] = Local.Comfort_callhome;
			if ((comfortMask & (int)Comfort.exercice) != 0) comforts[3] = Local.Comfort_exercise;
			if ((comfortMask & (int)Comfort.panorama) != 0) comforts[4] = Local.Comfort_panorama;
			if ((comfortMask & (int)Comfort.plants) != 0) comforts[5] = Local.Comfort_plants;

			string list = string.Empty;
			for (int i = 0; i < 6; i++)
			{
				if (!string.IsNullOrEmpty(comforts[i]))
				{
					if (list.Length > 0) list += ", ";
					list += comforts[i];
				}
			}
			return list;
		}

		public static string ComfortTooltip(int comfortMask, double comfortFactor)
		{
			string yes = Lib.BuildString("<b><color=#00ff00>", Local.Generic_YES, " </color></b>");
			string no = Lib.BuildString("<b><color=#ffaa00>", Local.Generic_NO, " </color></b>");
			return Lib.BuildString
			(
				"<align=left />",
				String.Format("{0,-14}\t{1}\n", Local.Comfort_firmground, (comfortMask & (int)Comfort.firmGround) != 0 ? yes : no),
				String.Format("{0,-14}\t{1}\n", Local.Comfort_exercise, (comfortMask & (int)Comfort.exercice) != 0 ? yes : no),
				String.Format("{0,-14}\t{1}\n", Local.Comfort_notalone, (comfortMask & (int)Comfort.notAlone) != 0 ? yes : no),
				String.Format("{0,-14}\t{1}\n", Local.Comfort_callhome, (comfortMask & (int)Comfort.callHome) != 0 ? yes : no),
				String.Format("{0,-14}\t{1}\n", Local.Comfort_panorama, (comfortMask & (int)Comfort.panorama) != 0 ? yes : no),
				String.Format("{0,-14}\t{1}\n", Local.Comfort_plants, (comfortMask & (int)Comfort.plants) != 0 ? yes : no),
				String.Format("<i>{0,-14}</i>\t{1}", Local.Comfort_factor, Lib.HumanReadablePerc(comfortFactor))
			);
		}

		public static string ComfortSummary(double comfortFactor)
		{
			if (comfortFactor >= 0.99) return Local.Module_Comfort_Summary1;//"ideal"
			else if (comfortFactor >= 0.66) return Local.Module_Comfort_Summary2;//"good"
			else if (comfortFactor >= 0.33) return Local.Module_Comfort_Summary3;//"modest"
			else if (comfortFactor > 0.1) return Local.Module_Comfort_Summary4;//"poor"
			else return Local.Module_Comfort_Summary5;//"none"
		}



		// traduce living space value to string
		public static string LivingSpaceFactorToString(double livingSpaceFactor)
		{
			if (livingSpaceFactor >= 0.99) return Local.Habitat_Summary1;//"ideal"
			else if (livingSpaceFactor >= 0.75) return Local.Habitat_Summary2;//"good"
			else if (livingSpaceFactor >= 0.5) return Local.Habitat_Summary3;//"modest"
			else if (livingSpaceFactor >= 0.25) return Local.Habitat_Summary4;//"poor"
			else return Local.Habitat_Summary5;//"cramped"
		}
	}
	#endregion
}