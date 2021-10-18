using System;
using System.Collections.Generic;
using Experience;
using System.Linq;
using UnityEngine;
using KSP.Localization;
using System.Collections;
using static KERBALISM.ExperimentRequirements;
using static KERBALISM.ExperimentHandler;

namespace KERBALISM
{


	public class ModuleKsmExperiment :
		KsmPartModule<ModuleKsmExperiment, ExperimentHandler, ExperimentDefinition>,
		IPartMassModifier,
		IMultipleDragCube
	{
		#region FIELDS

		// animations definition
		[KSPField] public string anim_deploy = string.Empty; // deploy animation
		[KSPField] public bool anim_deploy_reverse = false;

		[KSPField] public string anim_loop = string.Empty; // loop animation
		[KSPField] public bool anim_loop_reverse = false;

		/// <summary>
		/// if true, deploy/retract animations will managed by the first (by index) found ModuleAnimationGroup
		/// Note that using an animation group is incompatible with using a loop animation
		/// </summary>
		[KSPField] public bool useAnimationGroup = false;

		// optional : custom drag cubes definitions
		[KSPField] public string retractedDragCube = "Retracted";
		[KSPField] public string deployedDragCube = "Deployed";

		// animation handlers
		private Animator deployAnimator;
		private Animator loopAnimator;
		private ModuleAnimationGroup animationGroup;

		#endregion

		#region LIFECYCLE

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);

			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				moduleHandler.Start();
			}

			if (useAnimationGroup)
				animationGroup = part.Modules.OfType<ModuleAnimationGroup>().FirstOrDefault();
		}


		public override void KsmStart()
		{
			// create animators
			deployAnimator = new Animator(part, anim_deploy, anim_deploy_reverse);
			loopAnimator = new Animator(part, anim_loop, anim_loop_reverse);

			// set initial animation states
			if (moduleHandler.IsRunningRequested)
			{
				deployAnimator.Still(1f);
				loopAnimator.Play(false, true);
				SetDragCubes(true);
			}
			else
			{
				deployAnimator.Still(0f);
				SetDragCubes(false);
			}

			if (useAnimationGroup && animationGroup == null)
				animationGroup = part.Modules.OfType<ModuleAnimationGroup>().FirstOrDefault();

			if (animationGroup != null && !animationGroup.isDeployed && moduleHandler.IsRunningRequested)
			{
				animationGroup.DeployModule();
			}

			Events["ToggleEvent"].guiActiveUncommand = true;
			Events["ToggleEvent"].externalToEVAOnly = true;
			Events["ToggleEvent"].requireFullControl = false;

			Events["ShowPopup"].guiActiveUncommand = true;
			Events["ShowPopup"].externalToEVAOnly = true;
			Events["ShowPopup"].requireFullControl = false;
		}

		public void OnDefinitionChanged()
		{
			Actions["StartAction"].guiName = Lib.BuildString(Local.Generic_START, ": ", moduleHandler.definition.ExpInfo.Title);
			Actions["StopAction"].guiName = Lib.BuildString(Local.Generic_STOP, ": ", moduleHandler.definition.ExpInfo.Title);
		}

		#endregion

		#region EVALUATION

		public virtual void Update()
		{
			if (Lib.IsEditor || vessel == null) // in the editor just update the gui name
			{
				// update ui
				Events["ToggleEvent"].guiName = Lib.StatusToggle(moduleHandler.ExperimentTitle, StatusInfo(moduleHandler.Status, moduleHandler.issue));
				return;
			}

			if (!vessel.TryGetVesselDataTemp(out VesselData vd) || !vd.IsSimulated)
				return;

			bool hide = Definition.HideWhenInvalid && moduleHandler.Subject == null;

			if (hide)
			{
				Events["ToggleEvent"].active = false;
				Events["ShowPopup"].active = false;
			}
			else
			{
				Events["ToggleEvent"].active = true;
				Events["ShowPopup"].active = true;

				Events["ToggleEvent"].guiName = Lib.StatusToggle(Lib.Ellipsis(moduleHandler.ExperimentTitle, 25), StatusInfo(moduleHandler.Status, moduleHandler.issue));

				if (moduleHandler.Subject != null)
				{
					Events["ShowPopup"].guiName = Lib.StatusToggle(Local.StatuToggle_info,
						Lib.BuildString(
							ScienceValue(moduleHandler.Subject),
							" ",
							moduleHandler.State == RunningState.Forced
							? moduleHandler.Subject.PercentCollectedTotal.ToString("P0")
							: RunningCountdown(Definition.ExpInfo, moduleHandler.Subject, Definition.DataRate)));
				}
				else
				{
					Events["ShowPopup"].guiName = Lib.StatusToggle(Local.StatuToggle_info, vd.VesselSituations.FirstSituationTitle);//"info"
				}
			}

			if (animationGroup != null && !animationGroup.isDeployed && moduleHandler.IsRunningRequested)
			{
				Toggle(moduleHandler);
			}
		}

		public virtual void FixedUpdate()
		{
			moduleHandler.shrouded = part.ShieldedFromAirstream;
		}

		#endregion

		#region USER INTERACTION

		[KSPEvent(guiActiveUnfocused = true, guiActive = true, guiActiveEditor = true, guiName = "_", active = true, groupName = "Science", groupDisplayName = "#KERBALISM_Group_Science")]//Science
		public void ToggleEvent()
		{
			Toggle(moduleHandler);
		}

		[KSPEvent(guiActiveUnfocused = true, guiActive = true, guiName = "_", active = true, groupName = "Science", groupDisplayName = "#KERBALISM_Group_Science")]//Science
		public void ShowPopup()
		{
			new ExperimentPopup(moduleHandler);
		}

		// action groups
		[KSPAction("Start")]
		public void StartAction(KSPActionParam param)
		{
			if (!moduleHandler.IsRunningRequested) Toggle(moduleHandler);
		}

		[KSPAction("Stop")]
		public void StopAction(KSPActionParam param)
		{
			if (moduleHandler.IsRunningRequested) Toggle(moduleHandler);
		}

		public static void Toggle(ExperimentHandler ed, bool setForcedRun = false)
		{
			if (ed.IsBroken || !ed.handlerIsEnabled)
				return;

			// if setting forced run on an already running experiment
			if (setForcedRun && ed.State == RunningState.Running)
			{
				ed.State = RunningState.Forced;
				return;
			}

			// abort if the experiment animation is already playing
			if (ed.loadedModule != null)
			{
				if ((ed.loadedModule.animationGroup != null && ed.loadedModule.animationGroup.DeployAnimation.isPlaying)
					|| ed.loadedModule.deployAnimator.Playing
					|| ed.loadedModule.loopAnimator.IsLoopStopping)
					return;
			}

			// stopping
			if (ed.IsRunningRequested)
			{
				// if vessel is unloaded
				if (ed.loadedModule == null)
				{
					ed.State = RunningState.Stopped;
					return;
				}
				// if vessel loaded or in the editor
				else
				{
					// stop experiment
					// plays the deploy animation in reverse
					// if an external deploy animation module is used, we don't retract automatically
					Action onLoopStop = delegate ()
					{
						ed.State = RunningState.Stopped;
						ed.loadedModule.deployAnimator.Play(true, false, null, Lib.IsEditor ? 5f : 1f);
						ed.loadedModule.SetDragCubes(false);
						//if (Lib.IsEditor)
						//	Planner.Planner.RefreshPlanner();
					};

					// wait for loop animation to stop before deploy animation
					if (ed.loadedModule.loopAnimator.Playing)
						ed.loadedModule.loopAnimator.StopLoop(onLoopStop);
					else
						onLoopStop();
				}
			}
			// starting
			else
			{
				CheckMultipleRun(ed);

				// if vessel is unloaded
				if (ed.loadedModule == null)
				{
					ed.State = setForcedRun ? RunningState.Forced : RunningState.Running;
					return;
				}
				// if vessel loaded or in the editor
				else
				{
					// in case of an animation group, we start the experiment immediatly
					if (ed.loadedModule.animationGroup != null)
					{
						if (!ed.loadedModule.animationGroup.isDeployed)
						{
							ed.loadedModule.animationGroup.DeployModule();
							ed.State = setForcedRun ? RunningState.Forced : RunningState.Running;
							//if (Lib.IsEditor)
							//	Planner.Planner.RefreshPlanner();
						}
					}
					// if using our own animation handler, when the animation is done playing,
					// set the experiment running state and start the loop animation
					else
					{
						Action onDeploy = delegate ()
						{
							ed.State = setForcedRun ? RunningState.Forced : RunningState.Running;
							ed.loadedModule.loopAnimator.Play(false, true);
							ed.loadedModule.SetDragCubes(true);
							//if (Lib.IsEditor)
							//	Planner.Planner.RefreshPlanner();
						};

						ed.loadedModule.deployAnimator.Play(false, false, onDeploy, Lib.IsEditor ? 5f : 1f);
					}
				}
			}
		}

		#endregion

		#region INFO / UI

		public static string RunningStateInfo(RunningState state)
		{
			switch (state)
			{
				case RunningState.Stopped: return Lib.Color(Local.Module_Experiment_runningstate1, Lib.Kolor.Yellow);//"stopped"
				case RunningState.Running: return Lib.Color(Local.Module_Experiment_runningstate2, Lib.Kolor.Green);//"started"
				case RunningState.Forced: return Lib.Color(Local.Module_Experiment_runningstate3, Lib.Kolor.Red);//"forced run"
				case RunningState.Broken: return Lib.Color(Local.Module_Experiment_runningstate4, Lib.Kolor.Red);//"broken"
				default: return string.Empty;
			}

		}

		public static string StatusInfo(ExpStatus status, string issue = null)
		{
			switch (status)
			{
				case ExpStatus.Stopped: return Lib.Color(Local.Module_Experiment_runningstate1, Lib.Kolor.Yellow);//"stopped"
				case ExpStatus.Running: return Lib.Color(Local.Module_Experiment_runningstate5, Lib.Kolor.Green);//"running"
				case ExpStatus.Forced: return Lib.Color(Local.Module_Experiment_runningstate3, Lib.Kolor.Red);//"forced run"
				case ExpStatus.Waiting: return Lib.Color(Local.Module_Experiment_runningstate6, Lib.Kolor.Science);//"waiting"
				case ExpStatus.Broken: return Lib.Color(Local.Module_Experiment_runningstate4, Lib.Kolor.Red);//"broken"
				case ExpStatus.Issue: return Lib.Color(string.IsNullOrEmpty(issue) ? Local.Module_Experiment_issue_title : issue, Lib.Kolor.Orange);//"issue"
				default: return string.Empty;
			}
		}

		public static string RunningCountdown(ExperimentInfo expInfo, SubjectData subjectData, double dataRate, bool compact = true)
		{
			double count;
			if (subjectData != null)
				count = Math.Max(1.0 - subjectData.PercentCollectedTotal, 0.0) * (expInfo.DataSize / dataRate);
			else
				count = expInfo.DataSize / dataRate;

			return Lib.HumanReadableCountdown(count, compact);
		}

		public static string ScienceValue(SubjectData subjectData)
		{
			if (subjectData != null)
				return Lib.BuildString(Lib.HumanReadableScience(subjectData.ScienceCollectedTotal), " / ", Lib.HumanReadableScience(subjectData.ScienceMaxValue));
			else
				return Lib.Color(Local.Module_Experiment_ScienceValuenone, Lib.Kolor.Science, true);//"none"
		}


		#endregion

		#region UTILITY

		public void ReliablityEvent(bool breakdown)
		{
			if (breakdown)
				moduleHandler.State = RunningState.Broken;
			else
				moduleHandler.State = RunningState.Stopped;
		}

		/// <summary>
		/// Check if the same same experiment is already running on the vessel, and disable it if toggleOther is true
		/// 
		/// </summary>
		public static bool CheckMultipleRun(ExperimentHandler thisExpData, bool toggleOther = true)
		{
			VesselDataBase vd = thisExpData.partData.vesselData;
			bool hasOtherRunning = false;

			foreach (PartData partData in vd.Parts)
			{
				for (int i = 0; i < partData.modules.Count; i++)
				{
					if (partData.modules[i] is ExperimentHandler expData
						&& expData.handlerIsEnabled
						&& expData.ExperimentID == thisExpData.ExperimentID
						&& expData.IsRunningRequested)
					{
						if (toggleOther)
						{
							Toggle(expData);

							Message.Post(
								Lib.Color(Local.Module_Experiment_MultipleRunsMessage_title, Lib.Kolor.Orange, true),
								string.Format("{0} was already running on vessel {1}\nThe module on {2} has been disabled",
								expData.ExperimentTitle, expData.VesselData.VesselName, partData.Title));
						}
						hasOtherRunning |= true;
					}
				}
			}

			return hasOtherRunning;
		}

		#endregion

		#region SAMPLE MASS

		// IPartMassModifier
		public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
		{
			if (moduleHandler == null)
				return 0f;

			if (double.IsNaN(moduleHandler.remainingSampleMass))
			{
				Lib.LogDebug("Experiment remaining sample mass is NaN " + definition, Lib.LogLevel.Error);
				return 0f;
			}
			return (float)moduleHandler.remainingSampleMass;
		}
		public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

		#endregion

		#region DRAG CUBES

		private void SetDragCubes(bool deployed)
		{
			if (deployAnimator == null)
				return;

			part.DragCubes.SetCubeWeight(retractedDragCube, deployed ? 0f : 1f);
			part.DragCubes.SetCubeWeight(deployedDragCube, deployed ? 1f : 0f);
		}


		public bool IsMultipleCubesActive
		{
			get
			{
				if (deployAnimator == null)
				{
					deployAnimator = new Animator(part, anim_deploy, anim_deploy_reverse);
				}
				return deployAnimator.IsDefined;
			}
		}

		public string[] GetDragCubeNames() => new string[] { retractedDragCube, deployedDragCube };

		// called at prefab compilation, after OnLoad()
		public void AssumeDragCubePosition(string name)
		{
			if (deployAnimator == null)
			{
				deployAnimator = new Animator(part, anim_deploy, anim_deploy_reverse);
			}

			if (name == retractedDragCube)
				deployAnimator.Still(0f);
			else if (name == deployedDragCube)
				deployAnimator.Still(1f);
		}

		public bool UsesProceduralDragCubes() => false;

		#endregion

		#region MULTIPLE RUN CHECK

		private static List<string> editorRunningExperiments = new List<string>();

		public static void CheckEditorExperimentMultipleRun()
		{
			foreach (PartData partData in VesselDataShip.ShipParts.AllLoadedParts)
			{
				for (int i = 0; i < partData.modules.Count; i++)
				{
					if (partData.modules[i] is ExperimentHandler expData && expData.handlerIsEnabled && expData.IsRunningRequested)
					{
						if (editorRunningExperiments.Contains(expData.ExperimentID))
						{
							Toggle(expData);
						}
						else
						{
							editorRunningExperiments.Add(expData.ExperimentID);
						}
					}
				}
			}

			editorRunningExperiments.Clear();
		}
		#endregion
	}
}
