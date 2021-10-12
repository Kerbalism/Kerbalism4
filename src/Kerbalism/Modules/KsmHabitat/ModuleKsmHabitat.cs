using HarmonyLib;
using KERBALISM.Planner;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static KERBALISM.HabitatHandler;
using static KERBALISM.HabitatLib;

namespace KERBALISM
{
	public class ModuleKsmHabitat : KsmPartModule<ModuleKsmHabitat, HabitatHandler, HabitatDefinition>,
		ISpecifics,
		IModuleInfo,
		IPartCostModifier,
		IVolumeAndSurfaceModule
	{
		#region FIELDS / PROPERTIES

		// docking port state handling for deployables
		private List<ModuleDockingNode> modulesDockingNode;

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
			if (Definition.isDeployable)
				deployAnimator.Still(1f);


			
		}

		// IVolumeAndSurfaceModule
		public void GetVolumeAndSurfaceResults(PartVolumeAndSurface.Definition result)
		{
			// SSTU specific support copypasted from the old system, not sure how well this works
			if (Definition.volume <= 0.0 || Definition.surface <= 0.0)
			{
				foreach (PartModule pm in part.Modules)
				{
					if (pm.moduleName == "SSTUModularPart")
					{
						Bounds bb = Lib.ReflectionCall<Bounds>(pm, "getModuleBounds", new Type[] { typeof(string) }, new string[] { "CORE" });
						if (bb != null)
						{
							if (Definition.volume <= 0.0) Definition.volume = PartVolumeAndSurface.BoundsVolume(bb) * 0.785398; // assume it's a cylinder
							if (Definition.surface <= 0.0) Definition.surface = PartVolumeAndSurface.BoundsSurface(bb) * 0.95493; // assume it's a cylinder
						}
						return;
					}
				}
			}

			result.GetUsingMethod(Definition.volumeAndSurfaceMethod, out double calcVolume, out double calcSurface, Definition.substractAttachementNodesSurface);

			if (Definition.volume <= 0.0) Definition.volume = calcVolume;
			if (Definition.surface <= 0.0) Definition.surface = calcSurface;

			result.volume = Definition.volume;
			result.surface = Definition.surface;

			// use config defined depressurization duration or fallback to the default setting
			if (Definition.depressurizationSpeed > 0.0)
				Definition.depressurizationSpeed = M3ToL(Definition.volume) / Definition.depressurizationSpeed;
			else
				Definition.depressurizationSpeed = M3ToL(Definition.volume) / (Settings.DepressuriationDefaultDuration * Definition.volume);
		}

		// parsing configs at prefab compilation
		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);

			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				SetupAnimations();

				// determine habitat permanent state based on if animations exists and are valid
				Definition.isDeployable = deployAnimator.IsDefined;
				Definition.isCentrifuge = rotateAnimator.IsDefined;

				// ensure correct definition state
				if (!Definition.isDeployable)
					Definition.deployWithPressure = false;
			}
		}

		public override void KsmStart()
		{
			double volumeLiters = M3ToL(Definition.volume);
			double currentVolumeLiters = Definition.canPressurize ? volumeLiters : 0.0;

			// make sure crew count is synchronized
			moduleHandler.crewCount = Lib.CrewCount(part);

			// setup animations / transformators
			SetupAnimations();

			if (Definition.hasShielding)
			{
				// note : we are using IPartCostModifier to add the shielding capacity cost because
				// KSP evaluate part cost on part instantiation assuming all capacities are filled,
				// so it substract the shielding cost to the config-defined part cost, and since
				// we set set the amount to zero, this cause a lower or even negative final cost.
				// note 2 : As of KSP 1.8 IPartCostModifier isn't applied when the first editor
				// part is instantiated. It will fix itself at the first vessel modified event,
				// so not really worth a fix.
				shieldingCost = (float)(moduleHandler.ShieldRes.Capacity * PartResourceLibrary.Instance.GetDefinition(Definition.shieldingResource).unitCost);
			}

			reclaimResAbbr = PartResourceLibrary.Instance.GetDefinition(Definition.reclaimResource).abbreviation;

			// setup animations state
			if (Definition.isDeployable)
			{
				if (moduleHandler.IsDeployed)
					deployAnimator.Still(1f);
				else if (moduleHandler.animState == AnimState.Retracted)
					deployAnimator.Still(0f);
				else if (moduleHandler.animState == AnimState.Deploying)
					deployAnimator.Still(1f - (moduleHandler.animTimer / deployAnimator.AnimDuration));
				else
					deployAnimator.Still(moduleHandler.animTimer / deployAnimator.AnimDuration);
			}

			if (Definition.isCentrifuge && moduleHandler.IsRotationNominal)
			{
				rotateAnimator.StartSpinInstantly();
				counterweightAnimator.StartSpinInstantly();
			}

			// linking ModuleDockingNode state to the deploy animation state
			if (Definition.controlModuleDockingNode)
			{
				modulesDockingNode = part.FindModulesImplementing<ModuleDockingNode>();
				if (!Definition.isDeployable || modulesDockingNode.Count == 0 )
				{
					Definition.controlModuleDockingNode = false;
				}
				else
				{
					StartCoroutine(SetupModuleDockingNode());
				}
			}

			// PAW setup

			// synchronize PAW state with data state
			habitatEnabled = moduleHandler.isEnabled;
			pressureEnabled = moduleHandler.IsPressurizationRequested;
			deployEnabled = moduleHandler.IsDeployingRequested;
			rotationEnabled = moduleHandler.IsRotationEnabled;

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
				part.internalModel.SetVisible(moduleHandler.IsDeployed);

			// This is usually handled by the moduledata state machine and additionally in the internalmodel patch
			// but there is a corner case when a shipconstruct is assembled for launch where both calls will fail
			// due to the moduledata flightIds being assigned later. So do an additional check here.
			moduleHandler.UpdateHelmets();
		}

		private void SetupAnimations()
		{
			deployAnimator = new Animator(part, Definition.deployAnim, Definition.deployAnimReverse);

			if (Definition.rotateIsTransform)
				rotateAnimator = new Transformator(part, Definition.rotateAnim, Definition.rotateAxis, Definition.rotateSpinRate, Definition.rotateAccelerationRate, Definition.rotateIsReversed, Definition.rotateIVA);
			else
				rotateAnimator = new Transformator(part, Definition.rotateAnim, Definition.rotateSpinRate, Definition.rotateAccelerationRate, Definition.rotateIsReversed);

			if (Definition.counterweightIsTransform)
				counterweightAnimator = new Transformator(part, Definition.counterweightAnim, Definition.counterweightAxis, Definition.counterweightSpinRate, Definition.counterweightAccelerationRate, Definition.counterweightIsReversed, false);
			else
				counterweightAnimator = new Transformator(part, Definition.counterweightAnim, Definition.counterweightSpinRate, Definition.counterweightAccelerationRate, Definition.counterweightIsReversed);
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
				mdn.on_disable.OnCheckCondition = (KFSMState st) => !moduleHandler.IsDeployed;
				mdn.on_enable.OnCheckCondition = (KFSMState st) => moduleHandler.IsDeployed;
			}

			yield break;
		}

		#endregion

		#region UPDATE

		private bool IsSecInfoVisible => moduleHandler.pressureState != PressureState.AlwaysDepressurized && moduleHandler.pressureState != PressureState.Breatheable;
		private bool CanToggleHabitat => moduleHandler.IsDeployed;
		private bool CanTogglePressure => pressureField.guiActiveEditor = Definition.canPressurize && moduleHandler.IsDeployed;
		private bool CanToggleDeploy => Definition.isDeployable && moduleHandler.IsRotationStopped && !(moduleHandler.IsDeployed && !Definition.canRetract && !Lib.IsEditor);
		private bool CanToggleRotate => Definition.isCentrifuge && moduleHandler.IsDeployed;


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
			switch (moduleHandler.animState)
			{
				case AnimState.Accelerating:
				case AnimState.Decelerating:
				case AnimState.Rotating:
				case AnimState.RotatingNotEnoughEC:
					bool loosingSpeed = moduleHandler.animState == AnimState.RotatingNotEnoughEC;
					if (!Lib.IsEditor && (Definition.rotateECRate > 0.0 || Definition.accelerateECRate > 0.0))
					{
						rotateAnimator.Update(rotationEnabled, loosingSpeed, (float)moduleHandler.EcResInfo.AvailabilityFactor);
						counterweightAnimator.Update(rotationEnabled, loosingSpeed, (float)moduleHandler.EcResInfo.AvailabilityFactor);
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

			debugInfo = (moduleHandler.isEnabled ? "Enabled - " : "Disabled - ") + moduleHandler.pressureState.ToString() + " - " + moduleHandler.animState.ToString();

			mainPAWInfo = MainInfoString(this, moduleHandler);

			if (secInfoField.guiActive)
			{
				switch (moduleHandler.pressureState)
				{
					case PressureState.Pressurized:
					case PressureState.DepressurizingAboveThreshold:
					case PressureState.DepressurizingBelowThreshold:
						double reclaimedResAmount = Math.Max(moduleHandler.AtmoRes.Amount - (moduleHandler.AtmoRes.Capacity * (1.0 - Definition.reclaimFactor)), 0.0);
						secInfoField.guiName = Lib.BuildString(Lib.HumanReadableCountdown(moduleHandler.AtmoRes.Amount / Definition.depressurizationSpeed, true), " ", "for depressurization");
						secPAWInfo = Lib.BuildString("+", Lib.HumanReadableAmountCompact(reclaimedResAmount), " ", reclaimResAbbr);
						break;
					case PressureState.Depressurized:
					case PressureState.Pressurizing:
						secInfoField.guiName = "Pressurization";
						double requiredResAmount = Math.Max((moduleHandler.AtmoRes.Capacity * Settings.PressureThreshold) - moduleHandler.AtmoRes.Amount, 0.0);
						secPAWInfo = Lib.BuildString(Lib.HumanReadableAmountCompact(requiredResAmount), " ", reclaimResAbbr, " ", "required");
						break;
				}
			}


			if (enableField.guiActive)
			{
				habitatEnabled = moduleHandler.isEnabled;
			}

			if (deployField.guiActive)
			{
				deployEnabled = moduleHandler.IsDeployingRequested;

				string state = string.Empty;
				switch (moduleHandler.animState)
				{
					case AnimState.Retracted:
						state = Lib.Color(Definition.deployWithPressure ? "deflated" : "retracted", Lib.Kolor.Yellow);
						break;
					case AnimState.Deploying:
						state = Lib.Color(Definition.deployWithPressure ? "inflating" : "deploying", Lib.Kolor.Yellow);
						break;
					case AnimState.Retracting:
						state = Lib.Color(Definition.deployWithPressure ? "deflating" : "retracting", Lib.Kolor.Yellow);
						break;
					case AnimState.Deployed:
					case AnimState.Accelerating:
					case AnimState.Decelerating:
					case AnimState.Rotating:
					case AnimState.RotatingNotEnoughEC:
						state = Lib.Color(Definition.deployWithPressure ? "inflated" : "deployed", Lib.Kolor.Green);
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
				pressureEnabled = moduleHandler.IsPressurizationRequested;

				string state = PressureStateString(moduleHandler);
				((UIPartActionToggle)pressureField.uiControlEditor?.partActionItem)?.fieldStatus?.SetText(state);
				((UIPartActionToggle)pressureField.uiControlFlight?.partActionItem)?.fieldStatus?.SetText(state);
			}

			if (rotateField.guiActive)
			{
				rotationEnabled = moduleHandler.IsRotationEnabled;

				string label = string.Empty;
				string status = string.Empty;
				switch (moduleHandler.animState)
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

		public static string MainInfoString(ModuleKsmHabitat habitat, HabitatHandler moduleData)
		{
			double habPressure = moduleData.AtmoRes.Amount / moduleData.AtmoRes.Capacity;

			return Lib.BuildString(
			   Lib.Color(habPressure > Settings.PressureThreshold, habPressure.ToString("P2"), Lib.Kolor.Green, Lib.Kolor.Orange),
			   habitat.Definition.volume.ToString(" (0.0 m3)"),
			   " Crew:", " ", moduleData.crewCount.ToString(), "/", habitat.part.CrewCapacity.ToString());
		}

		public static string PressureStateString(HabitatHandler moduleData)
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
				moduleHandler.OnFixedUpdate(Kerbalism.fixedUpdateElapsedSec);
			}
		}

		#endregion

		#region ENABLE / DISABLE LOGIC & UI

		private void OnToggleHabitat(object field) => TryToggleHabitat(this, moduleHandler, true);

		public static bool TryToggleHabitat(ModuleKsmHabitat module, HabitatHandler data, bool isLoaded)
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

		public void OnTogglePressure(object field) => TryTogglePressure(this, moduleHandler);

		/// <summary> try to deploy or retract the habitat.</summary>
		public static bool TryTogglePressure(ModuleKsmHabitat module, HabitatHandler data)
		{
			if (!module.Definition.canPressurize || !data.IsDeployed)
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

		public override AutomationAdapter[] CreateAutomationAdapter(KsmPartModule moduleOrPrefab, ModuleHandler moduleData)
		{
			var habitat = moduleOrPrefab as ModuleKsmHabitat;
			if(habitat.Definition.canPressurize)
				return new AutomationAdapter[] { new HabitatAutomationAdapter(moduleOrPrefab, moduleData) };
			return null;
		}

		#endregion

		#region DEPLOY & ROTATE

		public void OnDeployCallback()
		{
			moduleHandler.animState = AnimState.Deployed;
			TryToggleHabitat(this, moduleHandler, true);
		}

		private void OnToggleDeploy(object field) => TryToggleDeploy(this, moduleHandler, true);

		/// <summary> try to deploy or retract the habitat. isLoaded must be set to true in the editor and for a loaded vessel, false for an unloaded vessel</summary>
		public static bool TryToggleDeploy(ModuleKsmHabitat module, HabitatHandler data, bool isLoaded)
		{
			if (!data.definition.isDeployable)
			{
				return false;
			}

			bool isEditor = Lib.IsEditor;

			// retract
			if (data.IsDeployingRequested)
			{
				if (isEditor)
				{
					if (data.definition.canPressurize && data.IsPressurizedAboveThreshold)
						data.DepressurizingStartEvt();

					if (data.isEnabled && !TryToggleHabitat(module, data, isLoaded))
						return false;
				}
				else
				{
					if (!data.definition.canRetract)
						return false;

					if (data.isEnabled && !TryToggleHabitat(module, data, isLoaded))
						return false;

					if (data.definition.controlModuleDockingNode)
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

					if (data.definition.deployWithPressure)
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

				if (data.definition.deployWithPressure)
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

		private void OnToggleRotation(object field) => ToggleRotate(this, moduleHandler, true);

		public static void ToggleRotate(ModuleKsmHabitat module, HabitatHandler data, bool isLoaded)
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
			specs.Add(Local.Habitat_info1, Lib.HumanReadableVolume(Definition.volume > 0.0 ? Definition.volume : PartVolumeAndSurface.PartBoundsVolume(part)) + (Definition.volume > 0.0 ? "" : " (bounds)"));//"Volume"
			specs.Add(Local.Habitat_info2, Lib.HumanReadableSurface(Definition.surface > 0.0 ? Definition.surface : PartVolumeAndSurface.PartBoundsSurface(part)) + (Definition.surface > 0.0 ? "" : " (bounds)"));//"Surface"
			specs.Add("");

			if (!Definition.canPressurize)
			{
				specs.Add(Lib.Color("Unpressurized", Lib.Kolor.Orange, true));
				specs.Add(Lib.Italic("Living in a suit is stressful"));
			}
			else
			{
				specs.Add("Depressurization", Definition.depressurizationSpeed.ToString("0.0 L/s"));
				if (Definition.canPressurize && Definition.reclaimFactor > 0.0)
				{
					double reclaimedAmount = Definition.reclaimFactor * M3ToL(Definition.volume);
					specs.Add(Lib.Bold(Definition.reclaimFactor.ToString("P0")) + " " + "reclaimed",  Lib.HumanReadableAmountCompact(reclaimedAmount) + " " + PartResourceLibrary.Instance.GetDefinition(Definition.reclaimResource).abbreviation);
					if (Definition.depressurizeECRate > 0.0)
						specs.Add("Require", Lib.Color(Lib.HumanReadableRate(Definition.depressurizeECRate, "F3", ecAbbr), Lib.Kolor.NegRate));
				}
			}

			specs.Add("");
			if (Definition.maxShieldingFactor > 0.0)
				specs.Add("Shielding", "max" + " " + Definition.maxShieldingFactor.ToString("P0"));
			else
				specs.Add(Lib.Color("No radiation shielding", Lib.Kolor.Orange, true));

			

			if (Definition.isDeployable)
			{
				specs.Add("");
				specs.Add(Lib.Color(Definition.deployWithPressure ? Local.Habitat_info4 : "Deployable", Lib.Kolor.Cyan));

				if (Definition.deployWithPressure || !Definition.canRetract)
					specs.Add("Non-retractable");

				if (Definition.deployECRate > 0.0)
					specs.Add("Require", Lib.Color(Lib.HumanReadableRate(Definition.deployECRate, "F3", ecAbbr), Lib.Kolor.NegRate));
			}

			if (Definition.isCentrifuge)
			{
				specs.Add("");
				specs.Add(Lib.Color("Gravity ring", Lib.Kolor.Cyan));
				specs.Add("Comfort bonus", (Settings.ComfortFirmGround + Settings.ComfortExercise).ToString("P0"));
				specs.Add("Acceleration", Lib.Color(Lib.HumanReadableRate(Definition.accelerateECRate, "F3", ecAbbr), Lib.Kolor.NegRate));
				specs.Add("Steady state", Lib.Color(Lib.HumanReadableRate(Definition.rotateECRate, "F3", ecAbbr), Lib.Kolor.NegRate));
			}

			if (Definition.comforts.Count > 0)
			{
				specs.Add("");
				specs.Add(Lib.Color("Comfort", Lib.Kolor.Cyan));
				foreach (ComfortValue comfort in Definition.comforts)
				{
					specs.Add(comfort.Title, Lib.BuildString(comfort.seats.ToString(), " ", "seats", ", ", "quality", " ", comfort.quality.ToString("P0")));
				}
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
				Lib.HumanReadableVolume(Definition.volume > 0.0 ? Definition.volume : PartVolumeAndSurface.PartBoundsVolume(part)),
				Definition.volume > 0.0 ? "" : " (bounds)");
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
	}
	#endregion
}
