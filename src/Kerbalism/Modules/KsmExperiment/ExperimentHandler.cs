using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static KERBALISM.ExperimentRequirements;

namespace KERBALISM
{
	public class ExperimentHandler :
		KsmModuleHandler<ModuleKsmExperiment, ExperimentHandler, ExperimentDefinition>,
		IB9Switchable
	{
		public enum ExpStatus { Stopped, Running, Forced, Waiting, Issue, Broken }
		public enum RunningState { Stopped, Running, Forced, Broken }

		#region FIELDS

		// persistence
		private RunningState expState;
		private ExpStatus status;
		public bool shrouded;
		public double remainingSampleMass;

		// this was persisted, but this doesn't seem necessary anymore.
		// At worst, there will be a handfull of fixedUpdate were the unloaded vessels won't have it
		// until they get their background update. Since this is now only used for UI purposes, this
		// isn't really a problem.
		public string issue = string.Empty;

		private SubjectData subject;
		private Situation situation;

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
			private set
			{
				if(status != value)
				{
					status = value;
					if(!Lib.IsEditor)
						API.OnExperimentStateChanged.Notify(((VesselData)partData.vesselData).VesselId, ExperimentID, status);
				}
			}
		}

		public SubjectData Subject => subject;

		public Situation Situation => situation;

		public string ExperimentID => definition.ExpInfo.ExperimentId;

		public string ExperimentTitle => definition.ExpInfo.Title;

		public DriveHandler PrivateDrive;

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

		public bool IsBroken => expState == RunningState.Broken;

		#endregion

		#region LIFECYCLE

		public override void OnFirstSetup()
		{
			expState = RunningState.Stopped;
			status = ExpStatus.Stopped;
			shrouded = false;
			subject = null;

			if (definition.ExpInfo != null && !definition.SampleCollecting && definition.ExpInfo.SampleMass > 0.0)
				remainingSampleMass = definition.ExpInfo.SampleMass * definition.Samples;
			else
				remainingSampleMass = 0.0;
		}


		public override void OnStart()
		{
			if (partData.vesselData is VesselData vesselData)
				API.OnExperimentStateChanged.Notify(vesselData.VesselId, ExperimentID, status);
		}

		public override void OnLoad(ConfigNode node)
		{
			expState = Lib.ConfigEnum(node, "expState", RunningState.Stopped);
			status = Lib.ConfigEnum(node, "status", ExpStatus.Stopped);
			shrouded = Lib.ConfigValue(node, "shrouded", false);
			remainingSampleMass = Lib.ConfigValue(node, "sampleMass", 0.0);
		}

		public override void OnSave(ConfigNode node)
		{
			node.AddValue("expState", expState);
			node.AddValue("status", status);
			node.AddValue("shrouded", shrouded);
			node.AddValue("sampleMass", remainingSampleMass);
		}

		#endregion

		#region IB9SWITCHABLE

		public void OnSwitchChangeDefinition(KsmModuleDefinition previousDefinition)
		{
			if (definition.ExpInfo != null && !definition.SampleCollecting && definition.ExpInfo.SampleMass > 0.0)
				remainingSampleMass = definition.ExpInfo.SampleMass * definition.Samples;
			else
				remainingSampleMass = 0.0;

			loadedModule.OnDefinitionChanged();
		}

		public void OnSwitchEnable() { }

		public void OnSwitchDisable()
		{
			remainingSampleMass = 0.0;
		}

		public string GetSubtypeDescription(KsmModuleDefinition subTypeDefinition, string techRequired)
		{
			return subTypeDefinition.ModuleDescription(modulePrefab);
		}

		#endregion

		#region EVALUATION

		public override void OnVesselDataUpdate()
		{
			if (Lib.IsEditor)
				return;

			situation = ((VesselData)VesselData).VesselSituations.GetExperimentSituation(definition.ExpInfo);
			subject = ScienceDB.GetSubjectData(definition.ExpInfo, situation);
		}

		private ExpStatus GetStatus(RunningState state, SubjectData subject, string issue)
		{
			switch (state)
			{
				case RunningState.Broken:
					return ExpStatus.Broken;
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

		public override void OnFixedUpdate(double elapsedSec)
		{
			if (!IsRunningRequested)
				return;

			RunningUpdate(elapsedSec);

			Status = GetStatus(expState, subject, issue);
		}

		private void RunningUpdate(double elapsedSec)
		{
			issue = string.Empty;

			if (Subject == null)
			{
				issue = Local.Module_Experiment_issue1;//"invalid situation"
				return;
			}

			double scienceRemaining = Subject.ScienceRemainingToCollect;

			if (State != RunningState.Forced && scienceRemaining <= 0.0)
				return;

			if (shrouded && !definition.AllowShrouded)
			{
				issue = Local.Module_Experiment_issue2;//"shrouded"
				return;
			}

			// note : since we can't scale the consume() amount by availability, when one of the resources (including EC)
			// is partially available but not the others, this will cause over-consumption of these other resources
			// Idally we should use a pure input recipe to avoid that but currently, recipes only scale inputs
			// if they have an output, it might be interresting to lift that limitation.
			double resourcesProdFactor = 1.0;

			if (definition.RequiredEC > 0.0)
			{
				if (VesselData.ResHandler.ElectricCharge.AvailabilityFactor == 0.0)
				{
					issue = Local.Module_Experiment_issue4;//"no Electricity"
					return;
				}
				else
				{
					resourcesProdFactor = Math.Min(resourcesProdFactor, VesselData.ResHandler.ElectricCharge.AvailabilityFactor);
				}
			}

			if (definition.Resources.Count > 0)
			{
				// test if there are enough resources on the vessel
				foreach (var p in definition.Resources)
				{
					VesselResource vr = VesselData.ResHandler.GetResource(p.Key);
					if (vr.AvailabilityFactor == 0.0)
					{
						issue = Local.Module_Experiment_issue12.Format(vr.Title); //"missing <<1>>"
						return;
					}
					else
					{
						resourcesProdFactor = Math.Min(resourcesProdFactor, vr.AvailabilityFactor);
					}
				}
			}

			if (definition.CrewOperate && !definition.CrewOperate.Check(((VesselData)VesselData).Vessel))
			{
				issue = definition.CrewOperate.Warning();
				return;
			}

			ExperimentInfo expInfo = definition.ExpInfo;

			if (!definition.SampleCollecting && remainingSampleMass <= 0.0 && expInfo.SampleMass > 0.0)
			{
				issue = Local.Module_Experiment_issue6;//"depleted"
				return;
			}

			if (!IsLoaded && Subject.Situation.AtmosphericFlight())
			{
				issue = Local.Module_Experiment_issue8;//"background flight"
				return;
			}

			if (!definition.Requirements.TestRequirements((VesselData)VesselData, out RequireResult[] reqResults))
			{
				issue = Local.Module_Experiment_issue9;//"unmet requirement"
				return;
			}

			double chunkSizeMax = definition.DataRate * elapsedSec;

			// Never again generate NaNs
			if (chunkSizeMax <= 0.0)
			{
				issue = "Error : chunkSizeMax is 0.0";
				return;
			}

			double chunkSize;
			if (State != RunningState.Forced)
				chunkSize = Math.Min(chunkSizeMax, scienceRemaining / Subject.SciencePerMB);
			else
				chunkSize = chunkSizeMax;

			bool isSample = expInfo.IsSample;
			DriveHandler drive = PrivateDrive;
			if (drive == null)
			{
				if (isSample)
					drive = DriveHandler.SampleDrive((VesselData)VesselData, chunkSize, Subject);
				else
					drive = DriveHandler.FileDrive((VesselData)VesselData, chunkSize);
			}

			if (drive == null)
			{
				issue = Local.Module_Experiment_issue11;//"no storage space"
				return;
			}

			DriveHandler bufferDrive = null;
			double available;
			if (isSample)
			{
				available = drive.SampleCapacityAvailable(Subject);
			}
			else
			{
				available = drive.FileCapacityAvailable();
				if (double.IsNaN(available)) Lib.LogStack("drive.FileCapacityAvailable() returned NaN", Lib.LogLevel.Error);

				if (drive.GetFileSend(Subject.Id))
				{
					bufferDrive = ((VesselData)VesselData).TransmitBuffer;
					available += bufferDrive.FileCapacityAvailable();
					if (double.IsNaN(available)) Lib.LogStack("warpDrive.FileCapacityAvailable() returned NaN", Lib.LogLevel.Error);
				}
			}

			if (available <= 0.0)
			{
				issue = Local.Module_Experiment_issue11;//"no storage space"
				return;
			}

			chunkSizeMax = Math.Min(chunkSize, available);

			double chunkProdFactor = chunkSizeMax / chunkSize;

			chunkSize = chunkSizeMax * resourcesProdFactor;

			double massDelta = chunkSize * expInfo.MassPerMB;

#if DEBUG || DEVBUILD
			if (double.IsNaN(chunkSize))
				Lib.Log("chunkSize is NaN " + expInfo.ExperimentId + " " + chunkSizeMax + " / " + chunkProdFactor + " / " + resourcesProdFactor + " / " + available + " / " + VesselData.ResHandler.ElectricCharge.Amount + " / " + definition.RequiredEC + " / " + definition.DataRate, Lib.LogLevel.Error);

			if (double.IsNaN(massDelta))
				Lib.Log("mass delta is NaN " + expInfo.ExperimentId + " " + expInfo.SampleMass + " / " + chunkSize + " / " + expInfo.DataSize, Lib.LogLevel.Error);
#endif

			if (isSample)
			{
				drive.RecordSample(Subject, chunkSize, massDelta);
			}
			else
			{
				if (bufferDrive != null)
				{
					double s = Math.Min(chunkSize, bufferDrive.FileCapacityAvailable());
					bufferDrive.RecordFile(Subject, s, true);

					if (chunkSize > s) // only write to persisted drive if the data cannot be transmitted in this tick
						drive.RecordFile(Subject, chunkSize - s, true);
					else if (!drive.files.ContainsKey(Subject)) // if everything is transmitted, create an empty file so the player know what is happening
						drive.RecordFile(Subject, 0.0, true);
				}
				else
				{
					drive.RecordFile(Subject, chunkSize, true);
				}
			}

			// Consume EC and resources
			// note : Consume() calls only factor in the drive available space limitation and not the resource availability factor, this is intended
			// note 2 : Since drive available space is determined by the transmit buffer drive space, itself determined by EC availability,
			// we don't totally escape a feeback effect
			VesselData.ResHandler.ElectricCharge.Consume(definition.RequiredEC * elapsedSec * chunkProdFactor, ResourceBroker.Experiment);

			foreach (ObjectPair<string, double> p in definition.Resources)
				VesselData.ResHandler.Consume(p.Key, p.Value * elapsedSec * chunkProdFactor, ResourceBroker.Experiment);

			if (!definition.SampleCollecting)
			{
				remainingSampleMass = Math.Max(remainingSampleMass - massDelta, 0.0);
			}
		}




		#endregion

		#region INFO

		public override string ModuleTitle => definition.ExpInfo?.Title ?? string.Empty;



		#endregion
	}
}
