using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;
using static KERBALISM.DriveHandler;
using static KERBALISM.ExperimentRequirements;

namespace KERBALISM
{
	public class ExperimentHandler :
		KsmModuleHandler<ModuleKsmExperiment, ExperimentHandler, ExperimentDefinition>,
		IB9Switchable, IActiveStoredHandler
	{
		public enum ExpStatus { Stopped, Running, Forced, Waiting, Issue, Broken }
		public enum RunningState { Stopped, Running, Forced, Broken }

		public bool IsActiveCargo => true;

		public void OnCargoStored()
		{
		}

		public void OnCargoUnstored()
		{

		}

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
		public double currentDataRate;

		private SubjectData subject;
		private Situation situation;
		private KsmScienceData lastData;

		private bool recipesSetupDone = false;
		private Recipe transmitRecipe;
		private RecipeInput transmitDataInput;
		private RecipeInput transmitCapacityInput;
		private RecipeInput transmitECInput;
		private Recipe storeRecipe;
		private RecipeInput storeDataInput;
		private RecipeInput storeCapacityInput;
		private VesselResourceAbstract dataResource;

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
					//if(!Lib.IsEditor)
					//	API.OnExperimentStateChanged.Notify(((VesselData)partData.vesselData).VesselId, ExperimentID, status);
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
			//if (partData.vesselData is VesselData vesselData)
			//	API.OnExperimentStateChanged.Notify(vesselData.VesselId, ExperimentID, status);
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
			if (definition.ExpInfo == null)
			{
				remainingSampleMass = 0.0;
			}
			else
			{
				remainingSampleMass = definition.SampleCollecting ? 0.0 : definition.ExpInfo.SampleMass * definition.Samples;
				loadedModule.OnDefinitionChanged();
			}
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

		public override void OnUpdate(double elapsedSec)
		{
			if (Lib.IsEditor)
				return;

			situation = ((VesselData)VesselData).VesselSituations.GetExperimentSituation(definition.ExpInfo);
			subject = ScienceDB.GetSubjectData(definition.ExpInfo, situation);

			if (!IsRunningRequested)
				return;

			issue = string.Empty;
			if (!RunningUpdate(elapsedSec))
			{
				lastData = null;
				Status = GetStatus(expState, subject, issue);
			}
		}

		private bool RunningUpdate(double elapsedSec)
		{
			if (Subject == null)
			{
				issue = Local.Module_Experiment_issue1;//"invalid situation"
				return false;
			}

			if (shrouded && !definition.AllowShrouded)
			{
				issue = Local.Module_Experiment_issue2;//"shrouded"
				return false;
			}

			double scienceRemaining = Subject.ScienceRemainingToCollect;

			if (State != RunningState.Forced && scienceRemaining <= 0.0)
			{
				return false;
			}

			if (definition.CrewOperate && !definition.CrewOperate.Check(((VesselData)VesselData).Vessel))
			{
				issue = definition.CrewOperate.Warning();
				return false;
			}

			if (definition.ExpInfo.SampleMass > 0.0 && !definition.SampleCollecting && remainingSampleMass <= 0.0)
			{
				issue = Local.Module_Experiment_issue6;//"depleted"
				return false;
			}

			if (!IsLoaded && Subject.Situation.AtmosphericFlight())
			{
				issue = Local.Module_Experiment_issue8;//"background flight"
				return false;
			}

			if (!definition.Requirements.TestRequirements((VesselData)VesselData, out RequireResult[] reqResults))
			{
				issue = Local.Module_Experiment_issue9;//"unmet requirement"
				return false;
			}

			if (!recipesSetupDone)
			{
				recipesSetupDone = true;

				dataResource = VesselData.ResHandler.AddNewAbstractResourceToHandler();
				storeRecipe = new Recipe(definition.ExpInfo.Title + " (stored)", RecipeCategory.ScienceData, OnRecipesExecuted);
				storeRecipe.priority = 1;
				transmitRecipe = new Recipe(definition.ExpInfo.Title + " (transmitted)", RecipeCategory.ScienceData);
				transmitRecipe.priority = 2;

				double dataRate = definition.DataRate;

				storeDataInput = storeRecipe.AddInput(dataResource.id, dataRate);
				storeCapacityInput = storeRecipe.AddInput(VesselData.vesselComms.DriveCapacityId, dataRate);

				transmitDataInput = transmitRecipe.AddInput(dataResource.id, dataRate);
				transmitCapacityInput = transmitRecipe.AddInput(VesselData.vesselComms.TransmitCapacityId, dataRate);
				transmitECInput = transmitRecipe.AddInput(VesselResHandler.ElectricChargeId, 0.0);
				transmitECInput.Title = definition.ExpInfo.Title + " data transmission";

				if (definition.RequiredEC > 0.0)
				{
					storeRecipe.AddInput(VesselResHandler.ElectricChargeId, definition.RequiredEC);
					transmitRecipe.AddInput(VesselResHandler.ElectricChargeId, definition.RequiredEC);
				}

				foreach (ObjectPair<int, double> resource in definition.Resources)
				{
					storeRecipe.AddInput(resource.Key, resource.Value);
					transmitRecipe.AddInput(resource.Key, resource.Value);
				}
			}

			double dataSize = definition.DataRate * elapsedSec;

			if (State != RunningState.Forced)
				dataSize = Math.Min(scienceRemaining / Subject.SciencePerMB, dataSize);

			if (Lib.IsZeroOrNegativeOrNaN(dataSize))
			{
				Lib.Log($"Invalid size : dataSize={dataSize}, dataRate={definition.DataRate}, sciencePerMb={Subject.SciencePerMB}, scienceRemaining={Subject.ScienceRemainingToCollect}", Lib.LogLevel.Warning);
				issue = "error";
				return false;
			}

			dataResource.SetAmountAndCapacity(dataSize);
			transmitECInput.NominalRate = VesselData.vesselComms.TransmitECRatePerMb * dataSize;

			transmitRecipe.RequestExecution(VesselData.ResHandler);
			storeRecipe.RequestExecution(VesselData.ResHandler);


			return true;
		}

		public void OnRecipesExecuted(double elapsedSec)
		{
			double dataProduced = dataResource.Capacity - dataResource.Amount;
			currentDataRate = dataProduced / elapsedSec;

			// if data production is less than nominal
			if (dataResource.Level > 0.0)
			{
				if (storeCapacityInput.ExecutedMaxIOFactor < 1.0)
				{
					issue = "not enough storage capacity";
				}
				else if (transmitCapacityInput.ExecutedMaxIOFactor < 1.0)
				{
					issue = "not enough transmit capacity";
				}
				else if (transmitECInput.ExecutedMaxIOFactor < 1.0) // TODO : add checks for other EC inputs
				{
					issue = "not enough electricity";
				}
				else
				{
					// TODO : check other resource inputs
					// issue = Local.Module_Experiment_issue12.Format(vr.Title); //"missing <<1>>"

					if (issue.Length == 0)
						issue = "unknown issue";
				}

				// no data was produced : no need to store/transmit anything
				if (currentDataRate == 0)
				{
					Status = GetStatus(expState, subject, issue);
					lastData = null;
					return;
				}
			}

			double storedFactor = storeRecipe.ExecutedFactor / (transmitRecipe.ExecutedFactor + storeRecipe.ExecutedFactor);

			// some data is transmitted
			if (storedFactor < 1.0)
			{
				VesselData.vesselComms.TransmitScienceData(subject, dataProduced * (1.0 - storedFactor), elapsedSec);
			}

			// some data is stored on drive(s)
			if (storedFactor > 0.0)
			{
				double dataToStore = dataProduced * storedFactor;

				if (lastData != null && !lastData.IsDeleted && lastData.SubjectData == subject)
				{
					double availableSize = lastData.Drive.AvailableFileSize();
					if (availableSize > 0.0)
					{
						double addedToFile = Math.Min(dataToStore, availableSize);
						lastData.AddSizeNoCapacityCheck(addedToFile);
						dataToStore -= addedToFile;
					}
				}
				else
				{
					lastData = null;
				}

				while (dataToStore > 0.0)
				{
					DriveHandler drive = FindBestDriveForFile(VesselData, subject, out double availableSize);
					if (availableSize == 0.0) // shouldn't happen in theory
						break;

					double addedToDrive = Math.Min(dataToStore, availableSize);
					lastData = drive.RecordFile(subject, addedToDrive);
					dataToStore -= addedToDrive;
				}
			}
		}

		/*
		private void RunningUpdate(double elapsedSec)
		{
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
					VesselResource vr = VesselData.ResHandler.GetKSPResource(p.Key);
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

			// TODO : refactor all this, keeping a reference to the file/sample that is being generated instead of re-instantiating it
			// on every update when the file is transmitted immediately. This would also allow getting ride the drive-finding calls,
			// wich are costly.

			DriveHandler drive = PrivateDrive;
			if (drive == null)
			{
				if (isSample)
					drive = FindBestDriveForSamples(VesselData, chunkSize);
				else
					drive = FindBestDriveForFiles(VesselData, chunkSize);
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
				available = drive.AvailableSampleSize(true);
			}
			else
			{
				available = drive.AvailableFileSize();
				if (double.IsNaN(available)) Lib.LogStack("drive.FileCapacityAvailable() returned NaN", Lib.LogLevel.Error);

				// TODO : also choose to transmit based on the vesseldata-level setting
				if (!drive.FileDictionary.TryGetValue(Subject, out ScienceFile file) || file.Transmit)
				{
					bufferDrive = ((VesselData)VesselData).TransmitBuffer;
					available += bufferDrive.AvailableFileSize();
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
				drive.RecordSample(Subject, chunkSize);
			}
			else
			{
				if (bufferDrive != null)
				{
					double s = Math.Min(chunkSize, bufferDrive.AvailableFileSize());
					bufferDrive.RecordFile(Subject, s, true);

					if (chunkSize > s) // only write to persisted drive if the data cannot be transmitted in this tick
						drive.RecordFile(Subject, chunkSize - s, true);
					//else if (!drive.files.ContainsKey(Subject)) // if everything is transmitted, create an empty file so the player know what is happening
					//	drive.RecordFile(Subject, 0.0, true);
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
			VesselData.ResHandler.ElectricCharge.Consume(definition.RequiredEC * elapsedSec * chunkProdFactor, RecipeCategory.Experiment);

			foreach (ObjectPair<string, double> p in definition.Resources)
				VesselData.ResHandler.Consume(p.Key, p.Value * elapsedSec * chunkProdFactor, RecipeCategory.Experiment);

			if (!definition.SampleCollecting)
			{
				remainingSampleMass = Math.Max(remainingSampleMass - massDelta, 0.0);
			}
		}
		*/

		#endregion

		#region INFO

		public override string ModuleTitle => definition.ExpInfo?.Title ?? string.Empty;



		#endregion
	}
}
