using KERBALISM.ModuleUI;
using System;
using static KERBALISM.DriveHandler;
using static KERBALISM.ExperimentHandlerUtils;

namespace KERBALISM
{
	public class ModuleKsmFileExperiment
		: ModuleKsmExperimentBase<ModuleKsmFileExperiment, FileExperimentHandler, ExperimentDefinition, ScienceFile> { }

	public class FileExperimentHandler
		: ExperimentHandlerBase<ModuleKsmFileExperiment, FileExperimentHandler, ExperimentDefinition, ScienceFile>,
			ICommonRecipeExecutedCallback
	{
		public enum DataProductionOption
		{
			store = 0,
			transmit = 1,
			transmitAndStore = 2
		}

		public DataProductionOption dataProductionOption = DataProductionOption.transmitAndStore;

		private VesselResourceAbstract dataResource;
		private Recipe transmitRecipe;
		private RecipeInput transmitDataInput;
		private RecipeInput transmitCapacityInput;
		private RecipeInput transmitECInput;
		private Recipe storeRecipe;
		private RecipeInput storeDataInput;
		private RecipeInput storeCapacityInput;

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			dataProductionOption = Lib.ConfigValue(node, nameof(dataProductionOption), DataProductionOption.transmitAndStore);
		}

		public override void OnSave(ConfigNode node)
		{
			base.OnSave(node);
			node.AddValue(nameof(dataProductionOption), dataProductionOption);
		}

		private class DataProductionOptionButton : ModuleUIButton<FileExperimentHandler>
		{
			public override int Position => 30;
			public override bool IsEnabled => !handler.definition.HideWhenInvalid || handler.Subject != null;

			public override string GetLabel()
			{
				return KsmString.Get.InfoRight("Data", GetDataProductionString(), KF.Bold).GetStringAndRelease();
			}

			private string GetDataProductionString()
			{
				switch (handler.dataProductionOption)
				{
					case DataProductionOption.store: return "store only";
					case DataProductionOption.transmit: return "transmit only";
					case DataProductionOption.transmitAndStore: return "transmit and store";
					default: return string.Empty;
				}
			}

			public override void OnClick()
			{
				switch (handler.dataProductionOption)
				{
					case DataProductionOption.store:
						handler.dataProductionOption = DataProductionOption.transmit;
						if (handler.currentData != null)
							handler.currentData.transmit = true;
						break;
					case DataProductionOption.transmit:
						handler.dataProductionOption = DataProductionOption.transmitAndStore;
						if (handler.currentData != null)
							handler.currentData.transmit = true;
						break;
					case DataProductionOption.transmitAndStore:
						handler.dataProductionOption = DataProductionOption.store;
						if (handler.currentData != null)
							handler.currentData.transmit = false;
						break;
				}
			}
		}

		protected override bool RequestDataProduction(double scienceRemaining, double elapsedSec)
		{
			if (!recipesSetupDone)
			{
				recipesSetupDone = true;

				dataResource = VesselData.ResHandler.AddNewAbstractResourceToHandler();
				storeRecipe = new Recipe(definition.ExpInfo.Title + " (stored)", RecipeCategory.ScienceData);
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

			if (dataProductionOption == DataProductionOption.transmit || dataProductionOption == DataProductionOption.transmitAndStore)
			{
				transmitECInput.NominalRate = VesselData.vesselComms.transmitECRatePerMb * dataSize;
				transmitRecipe.RequestExecution(VesselData.ResHandler);
			}

			if (dataProductionOption == DataProductionOption.store || dataProductionOption == DataProductionOption.transmitAndStore)
			{
				storeRecipe.RequestExecution(VesselData.ResHandler);
			}

			VesselData.ResHandler.RequestRecipeCallback(this);

			return true;
		}

		bool IRecipeExecutedCallback.IsCallbackRegistered { get; set; }
		void ICommonRecipeExecutedCallback.OnRecipesExecuted(double elapsedSec)
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
					currentData = null;
					return;
				}
			}

			double storedFactor;
			switch (dataProductionOption)
			{
				case DataProductionOption.store:
					storedFactor = 1.0;
					break;
				case DataProductionOption.transmit:
					storedFactor = 0.0;
					break;
				case DataProductionOption.transmitAndStore:
					storedFactor = storeRecipe.ExecutedFactor / (transmitRecipe.ExecutedFactor + storeRecipe.ExecutedFactor);
					break;
				default:
					throw new NotImplementedException();
			}

			// some data is transmitted
			if (storedFactor < 1.0)
			{
				VesselData.vesselComms.TransmitScienceData(subject, dataProduced * (1.0 - storedFactor), elapsedSec);
			}

			// some data is stored on drive(s)
			if (storedFactor > 0.0)
			{
				double dataToStore = dataProduced * storedFactor;

				if (currentData != null && !currentData.IsDeleted && currentData.SubjectData == subject)
				{
					double availableSize = currentData.AvailableSize();
					if (availableSize > 0.0)
					{
						double addedToFile = Math.Min(dataToStore, availableSize);
						currentData.AddSize(addedToFile);
						dataToStore -= addedToFile;
					}
				}
				else
				{
					currentData = null;
				}

				while (dataToStore > 0.0)
				{
					DriveHandler drive = FindBestDriveForFile(VesselData, subject, out double availableSize);
					if (availableSize == 0.0) // shouldn't happen in theory
						break;

					double addedToDrive = Math.Min(dataToStore, availableSize);
					currentData = drive.RecordFile(subject, addedToDrive);
					currentData.transmit = dataProductionOption != DataProductionOption.store;
					dataToStore -= addedToDrive;
				}
			}

			Status = GetStatus(expState, subject, issue);
		}
	}
}
