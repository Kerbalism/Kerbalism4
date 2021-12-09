using System;
using KERBALISM.ModuleUI;
using static KERBALISM.ExperimentHandlerUtils;
using static KERBALISM.ExperimentRequirements;

namespace KERBALISM
{
	public interface IExperimentHandler
	{
		bool HandlerIsEnabled { get; }
		ExperimentInfo ExperimentInfo { get; }
		bool IsRunningRequested { get; }

		void Toggle(bool setForcedRun = false);
	}

	public abstract class ExperimentHandlerBase<TModule, THandler, TDefinition, TScienceData> :
		KsmModuleHandler<TModule, THandler, TDefinition>,
		IExperimentHandler, IB9Switchable, IActiveStoredHandler
		where TModule : ModuleKsmExperimentBase<TModule, THandler, TDefinition, TScienceData>
		where THandler : ExperimentHandlerBase<TModule, THandler, TDefinition, TScienceData>
		where TDefinition : ExperimentDefinition
		where TScienceData : KsmScienceData
	{
		public bool IsActiveCargo => true;
		public StoredPartData StoredPart { get; set; }

		public void OnCargoStored()
		{
		}

		public void OnCargoUnstored()
		{

		}

		bool IExperimentHandler.HandlerIsEnabled => handlerIsEnabled;
		ExperimentInfo IExperimentHandler.ExperimentInfo => definition.ExpInfo;

		#region FIELDS

		// persistence
		protected RunningState expState;
		protected ExpStatus status;
		public bool shrouded;

		// this was persisted, but this doesn't seem necessary anymore.
		// At worst, there will be a handfull of fixedUpdate were the unloaded vessels won't have it
		// until they get their background update. Since this is now only used for UI purposes, this
		// isn't really a problem.
		public string issue = string.Empty;
		public double currentDataRate;

		protected SubjectData subject;
		protected Situation situation;
		internal TScienceData currentData;

		protected bool recipesSetupDone = false;




		#endregion

		#region PROPERTIES

		public RunningState State
		{
			get => expState;
			set
			{
				expState = value;
				Status = GetStatus(value, Subject, issue);
			}
		}

		public ExpStatus Status
		{
			get => status;
			protected set
			{
				if(status != value)
				{
					status = value;
					//if(!Lib.IsEditor)
					//	API.OnExperimentStateChanged.Notify(((VesselData)partData.vesselData).VesselId, ExperimentID, status);
				}
			}
		}

		public SubjectData Subject => subject;

		public Situation Situation => situation;

		public string ExperimentID => definition.ExpInfo.ExperimentId;

		public string ExperimentTitle => definition.ExpInfo.Title;

		public bool IsExperimentRunning
		{
			get
			{
				switch (status)
				{
					case ExpStatus.Running:
					case ExpStatus.Forced:
						return true;
					default:
						return false;
				}
			}
		}

		public bool IsRunningRequested
		{
			get
			{
				switch (expState)
				{
					case RunningState.Running:
					case RunningState.Forced:
						return true;
					default:
						return false;
				}
			}
		}

		#endregion

		#region LIFECYCLE

		public override void OnFirstSetup()
		{
			expState = RunningState.Stopped;
			status = ExpStatus.Stopped;
			shrouded = false;
			subject = null;
		}


		public override void OnStart()
		{
			//if (partData.vesselData is VesselData vesselData)
			//	API.OnExperimentStateChanged.Notify(vesselData.VesselId, ExperimentID, status);
		}

		public override void OnLoad(ConfigNode node)
		{
			expState = Lib.ConfigEnum(node, "expState", RunningState.Stopped);
			status = Lib.ConfigEnum(node, "status", ExpStatus.Stopped);
			shrouded = Lib.ConfigValue(node, "shrouded", false);
		}

		public override void OnSave(ConfigNode node)
		{
			node.AddValue("expState", expState);
			node.AddValue("status", status);
			node.AddValue("shrouded", shrouded);
		}

		#endregion

		#region IB9SWITCHABLE

		public string GetSubtypeDescription(KsmModuleDefinition subTypeDefinition, string techRequired)
		{
			return subTypeDefinition.ModuleDescription(modulePrefab);
		}

		public override void OnDefinitionChanging(DefinitionChangeEventType eventType, KsmModuleDefinition oldDefinition)
		{
			if (definition.ExpInfo != null)
			{
				uiGroup = CreateUIGroup();
				if (loadedModule != null)
					loadedModule.OnDefinitionChanged();
			}

			ForceUIElementsPAWUpdate();
		}

		#endregion



		protected override ModuleUIGroup CreateUIGroup()
		{
			if (definition.ExpInfo == null)
				return null;

			return new ModuleUIGroup(definition.ExpInfo.ExperimentId, "Experiment" + ": " + definition.ExpInfo.Title);
		}

		private class SubjectLabel : ModuleUILabel<THandler>
		{
			public override int Position => 0;
			public override bool IsEnabled => handler.definition.ExpInfo != null && !handler.definition.HideWhenInvalid || handler.Subject != null;

			public override EnabledContext Context => EnabledContext.Flight;

			public override string GetLabel()
			{
				if (handler.Subject == null)
					return KsmString.Get.Info("Subject", handler.VesselData.VesselSituations.FirstSituationTitle, -1, false).GetStringAndRelease();
				else
					return KsmString.Get.Info("Subject", handler.Subject.SituationTitle, -1, false).GetStringAndRelease();
			}
		}

		private class StateToggle : ModuleUIToggle<THandler>
		{
			public override int Position => 10;
			public override bool IsEnabled => handler.definition.ExpInfo != null && !handler.definition.HideWhenInvalid || handler.Subject != null;

			public override bool State => handler.Status != ExpStatus.Stopped;

			public override string GetLabel()
			{
				return KsmString.Get.Info("State", StatusInfo(handler.Status, handler.currentDataRate, handler.definition.DataRate)).GetStringAndRelease();
			}

			public override void OnToggle()
			{
				handler.Toggle();
			}
		}

		private class ShowPopupButton : ModuleUIButton<THandler>
		{
			public override int Position => 20;
			public override bool IsEnabled => handler.definition.ExpInfo != null && !handler.definition.HideWhenInvalid || handler.Subject != null;

			public override EnabledContext Context => EnabledContext.Flight;

			public override string GetLabel()
			{
				KsmString ks = KsmString.Get;
				ks.Add("Details");
				if (handler.Subject != null)
				{
					ks.Add(": ", ScienceValue(handler.Subject), ", ");
					if (handler.State == RunningState.Forced)
						ks.Add(handler.Subject.PercentCollectedTotal.ToString("P0"), " collected");
					else
						ks.Add(RunningCountdown(handler.definition, handler.Subject, handler.currentDataRate));
				}

				return ks.GetStringAndRelease();
			}

			public override void OnClick()
			{
				new ExperimentPopup<TModule, THandler, TDefinition, TScienceData>(handler);
			}
		}

		public void Toggle(bool setForcedRun = false)
		{
			if (!handlerIsEnabled)
				return;

			// if setting forced run on an already running experiment
			if (setForcedRun && State == RunningState.Running)
			{
				State = RunningState.Forced;
				return;
			}

			// abort if the experiment animation is already playing
			if (loadedModule != null)
			{
				if ((loadedModule.animationGroup != null && loadedModule.animationGroup.DeployAnimation.isPlaying)
					|| loadedModule.deployAnimator.Playing
					|| loadedModule.loopAnimator.IsLoopStopping)
					return;
			}

			// stopping
			if (IsRunningRequested)
			{
				// if vessel is unloaded
				if (loadedModule == null)
				{
					State = RunningState.Stopped;
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
						State = RunningState.Stopped;
						loadedModule.deployAnimator.Play(true, false, null, Lib.IsEditor ? 5f : 1f);
						loadedModule.SetDragCubes(false);
						//if (Lib.IsEditor)
						//	Planner.Planner.RefreshPlanner();
					};

					// wait for loop animation to stop before deploy animation
					if (loadedModule.loopAnimator.Playing)
						loadedModule.loopAnimator.StopLoop(onLoopStop);
					else
						onLoopStop();
				}
			}
			// starting
			else
			{
				CheckMultipleRun();

				// if vessel is unloaded
				if (loadedModule == null)
				{
					State = setForcedRun ? RunningState.Forced : RunningState.Running;
					return;
				}
				// if vessel loaded or in the editor
				else
				{
					// in case of an animation group, we start the experiment immediatly
					if (loadedModule.animationGroup != null)
					{
						if (!loadedModule.animationGroup.isDeployed)
						{
							loadedModule.animationGroup.DeployModule();
							State = setForcedRun ? RunningState.Forced : RunningState.Running;
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
							State = setForcedRun ? RunningState.Forced : RunningState.Running;
							loadedModule.loopAnimator.Play(false, true);
							loadedModule.SetDragCubes(true);
							//if (Lib.IsEditor)
							//	Planner.Planner.RefreshPlanner();
						};

						loadedModule.deployAnimator.Play(false, false, onDeploy, Lib.IsEditor ? 5f : 1f);
					}
				}
			}
		}

		/// <summary>
		/// Check if the same experiment is already running on the vessel, and disable it if toggleOther is true
		/// 
		/// </summary>
		public bool CheckMultipleRun(bool toggleOther = true)
		{
			bool hasOtherRunning = false;

			foreach (PartData partData in VesselData.Parts)
			{
				for (int i = 0; i < partData.modules.Count; i++)
				{
					if (partData.modules[i] is THandler expHandler
					    && expHandler.handlerIsEnabled
					    && expHandler.definition.ExpInfo == definition.ExpInfo
						&& expHandler.IsRunningRequested)
					{
						if (toggleOther)
						{
							expHandler.Toggle();

							Message.Post(
								Lib.Color(Local.Module_Experiment_MultipleRunsMessage_title, Lib.Kolor.Orange, true),
								string.Format("{0} was already running on vessel {1}\nThe module on {2} has been disabled",
									expHandler.ExperimentTitle, expHandler.VesselData.VesselName, partData.Title));
						}
						hasOtherRunning |= true;
					}
				}
			}

			return hasOtherRunning;
		}



		#region EVALUATION

		protected ExpStatus GetStatus(RunningState state, SubjectData subject, string issue)
		{
			switch (state)
			{
				case RunningState.Stopped:
					return ExpStatus.Stopped;
				case RunningState.Running:
					if (issue.Length > 0) return ExpStatus.Issue;
					if (subject == null || subject.ScienceRemainingToCollect <= 0.0) return ExpStatus.Waiting;
					return ExpStatus.Running;
				case RunningState.Forced:
					if (issue.Length > 0) return ExpStatus.Issue;
					return ExpStatus.Forced;
				default:
					return ExpStatus.Stopped;
			}
		}

		public override void OnUpdate(double elapsedSec)
		{
			if (Lib.IsEditor)
				return;

			situation = ((VesselData)VesselData).VesselSituations.GetExperimentSituation(definition.ExpInfo);
			subject = ScienceDB.GetSubjectData(definition.ExpInfo, situation);
			currentDataRate = 0.0;

			if (!IsRunningRequested)
				return;

			if (currentData != null && (currentData.IsDeleted || currentData.SubjectData != subject))
				currentData = null;

			issue = string.Empty;
			bool canRun = CheckConditions(elapsedSec, out double scienceRemaining);

			if (canRun)
			{
				canRun = RequestDataProduction(scienceRemaining, elapsedSec);
			}

			// if we can run, GetStatus() should be called by the recipe executed callback 
			if (!canRun)
			{
				Status = GetStatus(expState, subject, issue);
			}
		}

		protected virtual bool CheckConditions(double elapsedSec, out double scienceRemaining)
		{
			if (Subject == null)
			{
				issue = Local.Module_Experiment_issue1; //"invalid situation"
				scienceRemaining = 0.0;
				return false;
			}

			if (shrouded && !definition.AllowShrouded)
			{
				issue = Local.Module_Experiment_issue2; //"shrouded"
				scienceRemaining = 0.0;
				return false;
			}

			scienceRemaining = Subject.ScienceRemainingToCollect;

			if (State != RunningState.Forced && scienceRemaining <= 0.0)
			{
				return false;
			}

			if (definition.CrewOperate && !definition.CrewOperate.Check(((VesselData) VesselData).Vessel))
			{
				issue = definition.CrewOperate.Warning();
				return false;
			}

			if (!IsLoaded && Subject.Situation.AtmosphericFlight())
			{
				issue = Local.Module_Experiment_issue8; //"background flight"
				return false;
			}

			if (!definition.Requirements.TestRequirements((VesselData) VesselData, out RequireResult[] reqResults))
			{
				issue = Local.Module_Experiment_issue9; //"unmet requirement"
				return false;
			}

			return true;
		}

		protected abstract bool RequestDataProduction(double scienceRemaining, double elapsedSec);

		#endregion

		#region INFO

		public override string ModuleTitle => definition.ExpInfo?.Title ?? string.Empty;

		#endregion
	}
}
