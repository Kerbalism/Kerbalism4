using Harmony;
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
	//IBackgroundModule
	public class ModuleKsmHabitat : KsmPartModule<ModuleKsmHabitat, HabitatData>, ISpecifics, IModuleInfo, IPartCostModifier, IVolumeAndSurfaceModule
	{
		#region FIELDS / PROPERTIES
		// general config
		[KSPField] public bool canPressurize = true;              // can the habitat be pressurized ?
		[KSPField] public double maxShieldingFactor = 1.0;        // how much shielding can be applied, in % of the habitat surface (can be > 1.0)
		[KSPField] public double reclaimFactor = 0.4;  // % of atmosphere that will be recovered when depressurizing (producing "reclaimResource" back)
		[KSPField] public double reclaimStorageFactor = 0.0;		// Amount of nitrogen storage, in % of the amount needed to pressurize the part
		[KSPField] public bool canRetract = true;                 // if false, can't be retracted once deployed
		[KSPField] public bool deployWithPressure = false;        // if true, deploying is done by pressurizing
		[KSPField] public double depressurizeECRate = 0.5;        // EC/s consumed while depressurizing and reclaiming the reclaim resource
		[KSPField] public double deployECRate = 1.0;              // EC/s consumed while deploying / inflating
		[KSPField] public double accelerateECRate = 5.0;         // EC/s consumed while accelerating a centrifuge (note : decelerating is free)
		[KSPField] public double rotateECRate = 2.0;              // EC/s consumed to sustain the centrifuge rotation

		// volume / surface config
		[KSPField] public double volume = 0.0;  // habitable volume in m^3, deduced from model if not specified
		[KSPField] public double surface = 0.0; // external surface in m^2, deduced from model if not specified
		[KSPField] public PartVolumeAndSurface.Method volumeAndSurfaceMethod = PartVolumeAndSurface.Method.Best;
		[KSPField] public bool substractAttachementNodesSurface = true;

		// resources config
		[KSPField] public string reclaimResource = "Nitrogen"; // Nitrogen
		[KSPField] public string shieldingResource = "KsmShielding"; // KsmShielding

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

		// ModuleDockingNode handling
		[KSPField] public bool controlModuleDockingNode = false;     // should all ModuleDockingNode on the part be controlled by us and made dependant on the deployed state
		private List<ModuleDockingNode> modulesDockingNode;

		// fixed caracteristics determined at prefab compilation from OnLoad()
		// do not use these in configs, they are KSPField just so they are automatically copied over on part instancing
		[KSPField] public bool isDeployable;
		[KSPField] public bool isCentrifuge;
		[KSPField] public bool hasShielding;
		[KSPField] public int baseComfortsMask;
		[KSPField] public double depressurizationSpeed;

		// animation handlers
		public Animator deployAnimator;
		public Transformator rotateAnimator;
		public Transformator counterweightAnimator;

		// caching frequently used things
		private string reclaimResAbbr;
		private BaseField mainInfoField;
		private BaseField secInfoField;
		private BaseField enableField;
		private BaseField pressureField;
		private BaseField deployField;
		private BaseField rotateField;
		private float shieldingCost;

		// PAW UI
		// Note : don't change the 4 UI_Toggle bool from code, they are UI only "read-only" 
		// To change the state from code, use the static Toggle() methods
		[KSPField(groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat", guiActiveUnfocused = true)]//Habitat
		public string mainPAWInfo;

		[KSPField(groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat", guiActiveUnfocused = true)]//Habitat
		public string secPAWInfo;

		[KSPField(groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
		[UI_Toggle(scene = UI_Scene.All, requireFullControl = false, affectSymCounterparts = UI_Scene.None)]
		public bool habitatEnabled;

		[KSPField(groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat", guiActiveUnfocused = true)]//Habitat
		[UI_Toggle(scene = UI_Scene.All, affectSymCounterparts = UI_Scene.None)]
		public bool pressureEnabled;

		[KSPField(groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat", guiActiveUnfocused = true)]//Habitat
		[UI_Toggle(scene = UI_Scene.All, affectSymCounterparts = UI_Scene.None)]
		public bool deployEnabled;


		[KSPField(groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat", guiActiveUnfocused = true)]//Habitat
		[UI_Toggle(scene = UI_Scene.All, affectSymCounterparts = UI_Scene.None)]
		public bool rotationEnabled;

		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "dbg", groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
		public string debugInfo;

		#endregion

		#region INIT

		// IVolumeAndSurfaceModule
		public void SetupPrefabPartModel()
		{
			if (isDeployable)
				deployAnimator.Still(1f);
		}

		// IVolumeAndSurfaceModule
		public void GetVolumeAndSurfaceResults(PartVolumeAndSurface.Definition result)
		{
			// SSTU specific support copypasted from the old system, not sure how well this works
			if (volume <= 0.0 || surface <= 0.0)
			{
				foreach (PartModule pm in part.Modules)
				{
					if (pm.moduleName == "SSTUModularPart")
					{
						Bounds bb = Lib.ReflectionCall<Bounds>(pm, "getModuleBounds", new Type[] { typeof(string) }, new string[] { "CORE" });
						if (bb != null)
						{
							if (volume <= 0.0) volume = PartVolumeAndSurface.BoundsVolume(bb) * 0.785398; // assume it's a cylinder
							if (surface <= 0.0) surface = PartVolumeAndSurface.BoundsSurface(bb) * 0.95493; // assume it's a cylinder
						}
						return;
					}
				}
			}

			result.GetUsingMethod(volumeAndSurfaceMethod, out double calcVolume, out double calcSurface, substractAttachementNodesSurface);

			if (volume <= 0.0) volume = calcVolume;
			if (surface <= 0.0) surface = calcSurface;

			result.volume = volume;
			result.surface = surface;

			// use config defined depressurization duration or fallback to the default setting
			if (depressurizationSpeed > 0.0)
				depressurizationSpeed = M3ToL(volume) / depressurizationSpeed;
			else
				depressurizationSpeed = M3ToL(volume) / (Settings.DepressuriationDefaultDuration * volume);
		}

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

				// parse config defined depressurization duration
				if (!Lib.ConfigDuration(node, "depressurizationDuration", false, out depressurizationSpeed))
					depressurizationSpeed = -1.0;

				SetupAnimations();

				// determine habitat permanent state based on if animations exists and are valid
				isDeployable = deployAnimator.IsDefined;
				isCentrifuge = rotateAnimator.IsDefined;
				hasShielding = Features.Radiation && maxShieldingFactor > 0.0;

				// ensure correct state
				if (!isDeployable)
					deployWithPressure = false;
			}
		}

		public override void OnStart(StartState state)
		{
			double volumeLiters = M3ToL(volume);
			double currentVolumeLiters = canPressurize ? volumeLiters : 0.0;

			moduleData.crewCount = Lib.CrewCount(part);

			bool isFlight = Lib.IsFlight;

			// setup animations / transformators
			SetupAnimations();

			if (hasShielding)
			{
				//shieldRes = new LoadedPartResourceWrapper(part.Resources[shieldingResource]);
				// note : we are using IPartCostModifier to add the shielding capacity cost because
				// KSP evaluate part cost on part instantiation assuming all capacities are filled,
				// so it substract the shielding cost to the config-defined part cost, and since
				// we set set the amount to zero, this cause a lower or even negative final cost.
				// note 2 : As of KSP 1.8 IPartCostModifier isn't applied when the first editor
				// part is instantiated. It will fix itself at the first vessel modified event,
				// so not really worth a fix.
				shieldingCost = (float)(moduleData.ShieldRes.Capacity * PartResourceLibrary.Instance.GetDefinition(shieldingResource).unitCost);
			}

			reclaimResAbbr = PartResourceLibrary.Instance.GetDefinition(reclaimResource).abbreviation;

			// setup animations state
			if (isDeployable)
			{
				if (moduleData.IsDeployed)
					deployAnimator.Still(1f);
				else if (moduleData.animState == AnimState.Retracted)
					deployAnimator.Still(0f);
				else if (moduleData.animState == AnimState.Deploying)
					deployAnimator.Still(1f - (moduleData.animTimer / deployAnimator.AnimDuration));
				else
					deployAnimator.Still(moduleData.animTimer / deployAnimator.AnimDuration);
			}

			if (isCentrifuge && moduleData.IsRotationNominal)
			{
				rotateAnimator.StartSpinInstantly();
				counterweightAnimator.StartSpinInstantly();
			}

			// linking ModuleDockingNode state to the deploy animation state
			if (controlModuleDockingNode)
			{
				modulesDockingNode = part.FindModulesImplementing<ModuleDockingNode>();
				if (!isDeployable || modulesDockingNode.Count == 0 )
				{
					controlModuleDockingNode = false;
				}
				else
				{
					StartCoroutine(SetupModuleDockingNode());
				}
			}

			// PAW setup

			// synchronize PAW state with data state
			habitatEnabled = moduleData.isEnabled;
			pressureEnabled = moduleData.IsPressurizationRequested;
			deployEnabled = moduleData.IsDeployingRequested;
			rotationEnabled = moduleData.IsRotationEnabled;

			// get BaseField references
			mainInfoField = Fields["mainPAWInfo"];
			secInfoField = Fields["secPAWInfo"];
			enableField = Fields["habitatEnabled"];
			pressureField = Fields["pressureEnabled"];
			deployField = Fields["deployEnabled"];
			rotateField = Fields["rotationEnabled"];

			if (vessel != null && vessel.isEVA)
			{
				mainInfoField.guiActive = false;
				secInfoField.guiActive = false;
				enableField.guiActive = false;
				pressureField.guiActive = false;
				deployField.guiActive = false;
				rotateField.guiActive = false;
			}
			else
			{
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
			}

#if DEBUG
			Events["LogVolumeAndSurface"].guiActiveEditor = true;
#else
			Events["LogVolumeAndSurface"].guiActiveEditor = Settings.VolumeAndSurfaceLogging;
#endif

		}

		public override void OnStartFinished(StartState state)
		{
			if (part.internalModel != null)
				part.internalModel.SetVisible(moduleData.IsDeployed);

			// This is usually handled by the moduledata state machine and additionally in the internalmodel patch
			// but there is a corner case when a shipconstruct is assembled for launch where both calls will fail
			// due to the moduledata flightIds being assigned later. So do an additional check here.
			moduleData.UpdateHelmets();
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

		private IEnumerator SetupModuleDockingNode()
		{
			foreach (ModuleDockingNode mdn in modulesDockingNode)
			{
				while (mdn.on_disable == null || mdn.on_enable == null)
				{
					yield return null;
				}
			}

			foreach (ModuleDockingNode mdn in modulesDockingNode)
			{
				mdn.on_disable.OnCheckCondition = (KFSMState st) => !moduleData.IsDeployed;
				mdn.on_enable.OnCheckCondition = (KFSMState st) => moduleData.IsDeployed;
			}

			yield break;
		}

		#endregion

		#region UPDATE

		private bool IsSecInfoVisible => moduleData.pressureState != PressureState.AlwaysDepressurized && moduleData.pressureState != PressureState.Breatheable;
		private bool CanToggleHabitat => moduleData.IsDeployed;
		private bool CanTogglePressure => pressureField.guiActiveEditor = canPressurize && moduleData.IsDeployed;
		private bool CanToggleDeploy => isDeployable && moduleData.IsRotationStopped && !(moduleData.IsDeployed && !canRetract && !Lib.IsEditor);
		private bool CanToggleRotate => isCentrifuge && moduleData.IsDeployed;


		private bool IsDockingPortDocked()
		{
			foreach (ModuleDockingNode mdn in modulesDockingNode)
			{
				if (mdn.fsm.CurrentState != null && mdn.fsm.CurrentState != mdn.st_ready && mdn.fsm.CurrentState != mdn.st_disabled)
					return true;
			}
			return false;
		}

		public void Update()
		{
			if (vessel != null && vessel.isEVA)
				return;

			// TODO : Find a reliable way to have that f**** PAW correctly updated when we change guiActive...
			switch (moduleData.animState)
			{
				case AnimState.Accelerating:
				case AnimState.Decelerating:
				case AnimState.Rotating:
				case AnimState.RotatingNotEnoughEC:
					bool loosingSpeed = moduleData.animState == AnimState.RotatingNotEnoughEC;
					if (!Lib.IsEditor && (rotateECRate > 0.0 || accelerateECRate > 0.0))
					{
						rotateAnimator.Update(rotationEnabled, loosingSpeed, (float)moduleData.EcResInfo.AvailabilityFactor);
						counterweightAnimator.Update(rotationEnabled, loosingSpeed, (float)moduleData.EcResInfo.AvailabilityFactor);
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

			debugInfo = (moduleData.isEnabled ? "Enabled - " : "Disabled - ") + moduleData.pressureState.ToString() + " - " + moduleData.animState.ToString();

			mainPAWInfo = MainInfoString(this, moduleData);

			if (secInfoField.guiActive)
			{
				switch (moduleData.pressureState)
				{
					case PressureState.Pressurized:
					case PressureState.DepressurizingAboveThreshold:
					case PressureState.DepressurizingBelowThreshold:
						double reclaimedResAmount = Math.Max(moduleData.AtmoRes.Amount - (moduleData.AtmoRes.Capacity * (1.0 - reclaimFactor)), 0.0);
						secInfoField.guiName = Lib.BuildString(Lib.HumanReadableCountdown(moduleData.AtmoRes.Amount / depressurizationSpeed, true), " ", "for depressurization");
						secPAWInfo = Lib.BuildString("+", Lib.HumanReadableAmountCompact(reclaimedResAmount), " ", reclaimResAbbr);
						break;
					case PressureState.Depressurized:
					case PressureState.Pressurizing:
						secInfoField.guiName = "Pressurization";
						double requiredResAmount = Math.Max((moduleData.AtmoRes.Capacity * Settings.PressureThreshold) - moduleData.AtmoRes.Amount, 0.0);
						secPAWInfo = Lib.BuildString(Lib.HumanReadableAmountCompact(requiredResAmount), " ", reclaimResAbbr, " ", "required");
						break;
				}
			}


			if (enableField.guiActive)
			{
				habitatEnabled = moduleData.isEnabled;
			}

			if (deployField.guiActive)
			{
				deployEnabled = moduleData.IsDeployingRequested;

				string state = string.Empty;
				switch (moduleData.animState)
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

			if (pressureField.guiActive)
			{
				pressureEnabled = moduleData.IsPressurizationRequested;

				string state = PressureStateString(moduleData);
				((UIPartActionToggle)pressureField.uiControlEditor?.partActionItem)?.fieldStatus?.SetText(state);
				((UIPartActionToggle)pressureField.uiControlFlight?.partActionItem)?.fieldStatus?.SetText(state);
			}

			if (rotateField.guiActive)
			{
				rotationEnabled = moduleData.IsRotationEnabled;

				string label = string.Empty;
				string status = string.Empty;
				switch (moduleData.animState)
				{
					case AnimState.Deployed:
						label = "Rotation";
						status = Lib.Color("stopped", Lib.Kolor.Yellow);
						break;
					case AnimState.Accelerating:
						label = Lib.BuildString("Rotation", " ", rotateAnimator.CurrentSpeed.ToString("F1"), "/", rotateAnimator.NominalSpeed.ToString("F1"), "°/s");
						status = Lib.Color("starting", Lib.Kolor.Yellow);
						break;
					case AnimState.Decelerating:
						label = Lib.BuildString("Rotation", " ", rotateAnimator.CurrentSpeed.ToString("0.0°/s"));
						status = Lib.Color("stopping", Lib.Kolor.Yellow);
						break;
					case AnimState.Rotating:
						label = Lib.BuildString("Rotation", " ", rotateAnimator.CurrentSpeed.ToString("0.0°/s"));
						status = Lib.Color("nominal", Lib.Kolor.Green);
						break;
					case AnimState.RotatingNotEnoughEC:
						label = Lib.BuildString("Rotation", " ", rotateAnimator.CurrentSpeed.ToString("F1"), "/", rotateAnimator.NominalSpeed.ToString("F1"), "°/s");
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

		public static string MainInfoString(ModuleKsmHabitat habitat, HabitatData moduleData)
		{
			double habPressure = moduleData.AtmoRes.Amount / moduleData.AtmoRes.Capacity;

			return Lib.BuildString(
			   Lib.Color(habPressure > Settings.PressureThreshold, habPressure.ToString("P2"), Lib.Kolor.Green, Lib.Kolor.Orange),
			   habitat.volume.ToString(" (0.0 m3)"),
			   " Crew:", " ", moduleData.crewCount.ToString(), "/", habitat.part.CrewCapacity.ToString());
		}

		public static string PressureStateString(HabitatData moduleData)
		{
			switch (moduleData.pressureState)
			{
				case PressureState.Pressurized:
					return Lib.Color("pressurized", Lib.Kolor.Green);
				case PressureState.Depressurized:
					return Lib.Color("depressurized", Lib.Kolor.Yellow);
				case PressureState.Breatheable:
					return Lib.Color("external", Lib.Kolor.Green);
				case PressureState.Pressurizing:
					return Lib.Color("pressurizing", Lib.Kolor.Yellow);
				case PressureState.DepressurizingAboveThreshold:
					return Lib.Color("depressurizing", Lib.Kolor.Green);
				case PressureState.DepressurizingBelowThreshold:
					return Lib.Color("depressurizing", Lib.Kolor.Yellow);
			}
			return string.Empty;
		}

		public void FixedUpdate()
		{
			// TODO : temporary, waiting for an implementation of the ModuleData.OnFixedUpdate() in-editor automatic invocation
			if (Lib.IsEditor)
			{
				moduleData.OnFixedUpdate(Kerbalism.elapsed_s);
			}
		}

		#endregion

		#region ENABLE / DISABLE LOGIC & UI

		private void OnToggleHabitat(object field) => TryToggleHabitat(this, moduleData, true);

		public static bool TryToggleHabitat(ModuleKsmHabitat module, HabitatData data, bool isLoaded)
		{
			if (data.isEnabled && data.crewCount > 0)
			{
				if (!isLoaded)
				{
					Message.Post($"Can't disable a crewed habitat on an unloaded vessel");
					return false;
				}

				if (Lib.IsEditor)
				{
					Lib.EditorClearPartCrew(module.part);
				}
				else
				{
					List<ProtoCrewMember> crewLeft = Lib.TryTransferCrewElsewhere(module.part, false);

					if (crewLeft.Count > 0)
					{
						string message = "Not enough crew capacity in the vessel to transfer those Kerbals :\n";
						crewLeft.ForEach(a => message += a.displayName + "\n");
						Message.Post($"Habitat in {module.part.partInfo.title} couldn't be disabled.", message);
						return false;
					}
					else
					{
						Message.Post($"Habitat in {module.part.partInfo.title} has been disabled.", "Crew was transfered in the rest of the vessel");
					}
				}
			}

			if (data.isEnabled)
			{
				if (data.IsRotationEnabled)
					ToggleRotate(module, data, isLoaded);

				data.isEnabled = false;
			}
			else
			{
				if (!data.IsDeployed)
					return false;

				data.isEnabled = true;
			}
			return true;
		}

		#endregion

		#region ENABLE / DISABLE PRESSURE & UI

		public void OnTogglePressure(object field) => TryTogglePressure(this, moduleData);

		/// <summary> try to deploy or retract the habitat.</summary>
		public static bool TryTogglePressure(ModuleKsmHabitat module, HabitatData data)
		{
			if (!module.canPressurize || !data.IsDeployed)
			{
				return false;
			}

			switch (data.pressureState)
			{
				case PressureState.Pressurized:
				case PressureState.Pressurizing:
					data.DepressurizingStartEvt();
					break;
				case PressureState.Breatheable:
				case PressureState.Depressurized:
				case PressureState.DepressurizingAboveThreshold:
				case PressureState.DepressurizingBelowThreshold:
					data.PressurizingStartEvt();
					break;
			}

			return true;
		}

		public override AutomationAdapter[] CreateAutomationAdapter(KsmPartModule moduleOrPrefab, ModuleData moduleData)
		{
			var habitat = moduleOrPrefab as ModuleKsmHabitat;
			if(habitat.canPressurize)
				return new AutomationAdapter[] { new HabitatAutomationAdapter(moduleOrPrefab, moduleData) };
			return null;
		}

		#endregion

		#region DEPLOY & ROTATE

		public void OnDeployCallback()
		{
			moduleData.animState = AnimState.Deployed;
			TryToggleHabitat(this, moduleData, true);
		}

		private void OnToggleDeploy(object field) => TryToggleDeploy(this, moduleData, true);

		/// <summary> try to deploy or retract the habitat. isLoaded must be set to true in the editor and for a loaded vessel, false for an unloaded vessel</summary>
		public static bool TryToggleDeploy(ModuleKsmHabitat module, HabitatData data, bool isLoaded)
		{
			if (!module.isDeployable)
			{
				return false;
			}

			bool isEditor = Lib.IsEditor;

			// retract
			if (data.IsDeployingRequested)
			{
				if (isEditor)
				{
					if (module.canPressurize && data.IsPressurizedAboveThreshold)
						data.DepressurizingStartEvt();

					if (data.isEnabled && !TryToggleHabitat(module, data, isLoaded))
						return false;
				}
				else
				{
					if (!module.canRetract)
						return false;

					if (data.isEnabled && !TryToggleHabitat(module, data, isLoaded))
						return false;

					if (module.controlModuleDockingNode)
					{
						if (!isLoaded)
						{
							Message.Post($"Can't retract {module.part.partInfo.title}", "A dockable and deployable habitat can't be retracted while the vessel is unloaded");
							return false;
						}
						else if (module.IsDockingPortDocked())
						{
							Message.Post($"Can't retract {module.part.partInfo.title}", "It's docked to another part !");
							return false;
						}
					}

					if (module.deployWithPressure)
					{
						if (data.IsDeployed && !data.IsPressurizedAboveThreshold)
						{
							Message.Post($"Can't retract {module.part.partInfo.title}", $"An inflatable must be pressurized at a minimum of {Settings.PressureThreshold.ToString("P0")} before it can be retracted");
							return false;
						}

						data.animState = AnimState.Retracting;
						data.animTimer = module.deployAnimator.AnimDuration;
						data.DepressurizingStartEvt();
						if (isLoaded && module.part.internalModel != null)
							module.part.internalModel.SetVisible(false);
						return true;
					}
					else
					{
						if (data.pressureState == PressureState.Breatheable)
						{
							data.ForceBreathableDepressurizationEvt();
						}
						else
						{
							if (!data.IsFullyDepressurized)
							{
								Message.Post($"Can't retract {module.part.partInfo.title}", "It's still pressurized !");
								return false;
							}
						}
					}
				}

				data.animState = AnimState.Retracting;

				if (isLoaded)
				{
					module.deployAnimator.Play(true, false, null, isEditor ? 5f : 1f);

					if (module.part.internalModel != null)
						module.part.internalModel.SetVisible(false);
				}


				data.animTimer = module.deployAnimator.AnimDuration;
			}
			// deploy
			else
			{
				data.animState = AnimState.Deploying;

				if (module.deployWithPressure)
				{
					data.PressurizingStartEvt();
				}
				else
				{
					if (isLoaded)
						module.deployAnimator.Play(false, false, null, isEditor ? 5f : 1f);

					data.animTimer = module.deployAnimator.AnimDuration;
				}
			}

			return true;
		}

		private void OnToggleRotation(object field) => ToggleRotate(this, moduleData, true);

		public static void ToggleRotate(ModuleKsmHabitat module, HabitatData data, bool isLoaded)
		{
			bool isEditor = Lib.IsEditor;

			if (data.IsRotationEnabled)
			{
				data.animState = AnimState.Decelerating;
				if (!isLoaded)
				{
					data.animTimer = module.rotateAnimator.TimeNeededToStartOrStop;
				}
				else
				{
					if (isEditor)
					{
						module.rotateAnimator.StopSpinInstantly();
						module.counterweightAnimator.StopSpinInstantly();
					}
				}
			}
			else if (data.IsDeployed)
			{
				data.animState = AnimState.Accelerating;

				if (!isLoaded)
				{
					data.animTimer = module.rotateAnimator.TimeNeededToStartOrStop;
				}
				else
				{
					if (isEditor)
					{
						module.rotateAnimator.StartSpinInstantly();
						module.counterweightAnimator.StartSpinInstantly();
					}
				}
			}
		}

		// debug
		[KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "[Debug] log volume/surface", groupName = "Habitat", groupDisplayName = "#KERBALISM_Group_Habitat")]//Habitat
		public void LogVolumeAndSurface() => PartVolumeAndSurface.GetPartVolumeAndSurface(part, true);

		#endregion

		#region UI INFO

		// specifics support
		public Specifics Specs()
		{
			string ecAbbr = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").abbreviation;

			Specifics specs = new Specifics();
			specs.Add(Local.Habitat_info1, Lib.HumanReadableVolume(volume > 0.0 ? volume : PartVolumeAndSurface.PartBoundsVolume(part)) + (volume > 0.0 ? "" : " (bounds)"));//"Volume"
			specs.Add(Local.Habitat_info2, Lib.HumanReadableSurface(surface > 0.0 ? surface : PartVolumeAndSurface.PartBoundsSurface(part)) + (surface > 0.0 ? "" : " (bounds)"));//"Surface"
			specs.Add("");

			if (!canPressurize)
			{
				specs.Add(Lib.Color("Unpressurized", Lib.Kolor.Orange, true));
				specs.Add(Lib.Italic("Living in a suit is stressful"));
			}
			else
			{
				specs.Add("Depressurization", depressurizationSpeed.ToString("0.0 L/s"));
				if (canPressurize && reclaimFactor > 0.0)
				{
					double reclaimedAmount = reclaimFactor * M3ToL(volume);
					specs.Add(Lib.Bold(reclaimFactor.ToString("P0")) + " " + "reclaimed",  Lib.HumanReadableAmountCompact(reclaimedAmount) + " " + PartResourceLibrary.Instance.GetDefinition(reclaimResource).abbreviation);
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
				specs.Add("Comfort bonus", (Settings.ComfortFirmGround + Settings.ComfortExercise).ToString("P0"));
				specs.Add("Acceleration", Lib.Color(Lib.HumanReadableRate(accelerateECRate, "F3", ecAbbr), Lib.Kolor.NegRate));
				specs.Add("Steady state", Lib.Color(Lib.HumanReadableRate(rotateECRate, "F3", ecAbbr), Lib.Kolor.NegRate));
			}

			if (baseComfortsMask > 0)
			{
				specs.Add("");
				specs.Add(Lib.Color("Comfort", Lib.Kolor.Cyan), ComfortCommaList(baseComfortsMask));
				specs.Add("Bonus", GetComfortFactor(baseComfortsMask).ToString("P0"));
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
				Lib.HumanReadableVolume(volume > 0.0 ? volume : PartVolumeAndSurface.PartBoundsVolume(part)),
				volume > 0.0 ? "" : " (bounds)");
		}

		#endregion

		#region PART COST INTERFACE

		public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) => shieldingCost;
		public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.CONSTANTLY;

		#endregion

	}

	#region STATIC METHODS

	public static class HabitatLib
	{
		public static double M3ToL(double cubicMeters) => cubicMeters * 1000.0;

		public static double LToM3(double liters) => liters * 0.001;

		public static double GetComfortFactor(int comfortMask)
		{
			double factor = 0.0;

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
