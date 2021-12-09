using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KERBALISM.ModuleUI;
using static KERBALISM.ExperimentHandlerUtils;

namespace KERBALISM
{
	public class ModuleKsmSampleExperiment
		: ModuleKsmExperimentBase<ModuleKsmSampleExperiment, SampleExperimentHandler, SampleExperimentDefinition, ScienceSample>
	{

	}

	public class SampleExperimentHandler
		: ExperimentHandlerBase<ModuleKsmSampleExperiment, SampleExperimentHandler, SampleExperimentDefinition, ScienceSample>,
			ICommonRecipeExecutedCallback
	{
		private Recipe storeRecipe;
		private double currentDataRequest;

		internal ModuleInventoryPartHandler inventory;
		internal List<SampleStorageHandler> sampleStorages = new List<SampleStorageHandler>();
		private SampleStorageHandler currentStorage;

		public override void OnStart()
		{
			base.OnStart();
			inventory = partData.GetModuleHandler<ModuleInventoryPartHandler>();
		}

		private class MaterialInfo : ModuleUILabel<SampleExperimentHandler>
		{
			public override int Position => -10;
			public override bool IsEnabled => handler.definition.ExpInfo != null && !handler.definition.ExpInfo.SampleCollecting && (!handler.definition.HideWhenInvalid || handler.Subject != null);

			public override string GetLabel()
			{
				double availableSize = 0;
				foreach (SampleStorageHandler storageHandler in handler.sampleStorages)
					availableSize += storageHandler.AvailableSize();

				KsmString ks = KsmString.Get;
				ks.Add("Sample material", ": ");

				if (availableSize == 0.0)
					ks.Add(Local.Generic_NONE);
				else
					ks.Add((availableSize / handler.definition.ExpInfo.DataSize).ToString("F2"));

				if (handler.sampleStorages.Count > 1)
					ks.Add(", ", "storages", ": ", handler.sampleStorages.Count.ToString());

				return ks.GetStringAndRelease();
			}
		}

		private class SamplesInfo : ModuleUILabel<SampleExperimentHandler>
		{
			public override int Position => -5;
			public override bool IsEnabled => handler.definition.ExpInfo != null && !handler.definition.HideWhenInvalid || handler.Subject != null;
			public override EnabledContext Context => EnabledContext.Flight;

			public override string GetLabel()
			{
				double samplesSize = 0.0;
				int sampleCount = 0;
				foreach (SampleStorageHandler storageHandler in handler.sampleStorages)
				{
					samplesSize += storageHandler.samplesSize;
					sampleCount += storageHandler.samplesDict.Count;
				}

				KsmString ks = KsmString.Get;
				ks.Add("Samples", ": ");

				if (sampleCount == 0)
				{
					return ks.Add(Local.Generic_NONE).GetStringAndRelease();
				}
				else
				{
					ks.Add((samplesSize / handler.definition.ExpInfo.DataSize).ToString("F2"));
					ks.Add(" (");
					if (sampleCount > 1)
					{
						ks.Add("subjects", ": ", sampleCount.ToString(), ", ");
					}

					ks.Add("size", ": ");
					ks.Add((samplesSize * handler.definition.ExpInfo.VolumePerMB).ToString("0.## L"), ", ");
					ks.Format(KF.ReadableMass(samplesSize * handler.definition.ExpInfo.MassPerMB));
					ks.Add(")");
					return ks.GetStringAndRelease();
				}
			}
		}

		public void RegisterSampleStorage(SampleStorageHandler storageHandler)
		{
			if (!sampleStorages.Contains(storageHandler))
				sampleStorages.Add(storageHandler);
		}

		public void UnregisterSampleStorage(SampleStorageHandler storageHandler)
		{
			if (currentData != null && currentData.sampleStorage == storageHandler)
			{
				currentData = null;
			}

			if (currentStorage != null && currentStorage == storageHandler)
			{
				currentStorage = null;
			}

			sampleStorages.Remove(storageHandler);
		}

		protected override bool CheckConditions(double elapsedSec, out double scienceRemaining)
		{
			if (inventory == null)
			{
				issue = "No inventory space";
				scienceRemaining = 0.0;
				return false;
			}

			return base.CheckConditions(elapsedSec, out scienceRemaining);
		}

		protected override bool RequestDataProduction(double scienceRemaining, double elapsedSec)
		{
			if (!recipesSetupDone)
			{
				recipesSetupDone = true;

				storeRecipe = new Recipe(definition.ExpInfo.Title, RecipeCategory.ScienceSample);

				if (definition.RequiredEC > 0.0)
				{
					storeRecipe.AddInput(VesselResHandler.ElectricChargeId, definition.RequiredEC);
				}

				foreach (ObjectPair<int, double> resource in definition.Resources)
				{
					storeRecipe.AddInput(resource.Key, resource.Value);
				}
			}

			double nominalDataSize = definition.DataRate * elapsedSec;
			double dataSize;
			if (State != RunningState.Forced)
				dataSize = Math.Min(scienceRemaining / Subject.SciencePerMB, nominalDataSize);
			else
				dataSize = nominalDataSize;

			if (Lib.IsZeroOrNegativeOrNaN(dataSize))
			{
				Lib.Log($"Invalid size : dataSize={dataSize}, dataRate={definition.DataRate}, sciencePerMb={Subject.SciencePerMB}, scienceRemaining={Subject.ScienceRemainingToCollect}", Lib.LogLevel.Warning);
				issue = "error";
				return false;
			}

			double availableSize = 0.0;
			if (currentData != null)
			{
				availableSize = currentData.AvailableSize();
			}

			if (availableSize == 0.0)
			{
				FindStorageForSample(out availableSize);
			}

			if (availableSize == 0.0)
			{
				if (definition.ExpInfo.SampleCollecting)
					issue = "no storage space";
				else
					issue = "no sample material";

				return false;
			}

			currentDataRequest = Math.Min(dataSize, availableSize);
			storeRecipe.RequestExecution(VesselData.ResHandler, this, currentDataRequest / nominalDataSize);

			return true;
		}

		private void FindStorageForSample(out double availableSize)
		{
			availableSize = 0.0;
			
			foreach (SampleStorageHandler sampleStorage in sampleStorages)
			{
				availableSize = sampleStorage.AvailableSize();
				if (availableSize == 0.0)
					continue;

				// if the subject exists, increment it
				if (sampleStorage.samplesDict.TryGetValue(subject, out ScienceSample sample))
					currentData = sample;

				// else just keep a reference to the sample storage, we will create a new
				// subject after recipe execution if all conditions are ok
				currentStorage = sampleStorage;
				return;
			}

			// No existing sample storage was found.
			currentStorage = null;

			// If we are collecting, check that the inventory has enough volume for the new cargo part
			// If it has enough, we will create the cargo part after recipe execution
			if (!definition.ExpInfo.SampleCollecting)
				return;

			if (!definition.ExpInfo.sampleStorageParts.TryGetValue(definition.SampleCollectingCargoPart, out SampleStorageDefinition storageDefinition))
				return;

			double requiredVolume = storageDefinition.SampleAmount * definition.ExpInfo.SampleVolume;
			double partBaseMass = definition.SampleCollectingCargoPart.partPrefab.mass; // not sure this is the right one;

			if (inventory.VolumeAvailable < requiredVolume || inventory.MassAvailable < partBaseMass)
				return;

			availableSize = storageDefinition.SampleAmount * definition.ExpInfo.DataSize;

			if (inventory.hasMassLimit)
			{
				double availableMass = inventory.MassAvailable;
				availableSize = Math.Min(availableSize, availableMass / definition.ExpInfo.MassPerMB);
			}

			availableSize = Lib.ClampToPositive(availableSize);
		}

		bool IRecipeExecutedCallback.IsCallbackRegistered { get; set; }
		void ICommonRecipeExecutedCallback.OnRecipesExecuted(double elapsedSec)
		{
			double dataProduced = currentDataRequest * storeRecipe.ExecutedFactor;
			currentDataRate = dataProduced / elapsedSec;

			if (storeRecipe.ExecutedFactor < 1.0)
			{
				foreach (RecipeInputBase storeRecipeInput in storeRecipe.inputs)
				{
					if (storeRecipeInput.ExecutedMaxIOFactor < 1.0)
					{
						issue = Local.Module_Experiment_issue12.Format(storeRecipeInput.vesselResource.Title); //"missing <<1>>"
						break;
					}

					if (issue.Length == 0)
						issue = "unknown issue";

					// no data was produced : no need to store anything
					if (dataProduced == 0.0)
					{
						Status = GetStatus(expState, subject, issue);
						return;
					}
				}
			}

			// currentData isn't null : add data to it
			if (currentData != null)
			{
				currentData.AddSize(dataProduced);
			}
			// currentData is null : we should create a new entry for our subject
			else
			{
				// currentStorage is null, which mean that :
				// - we are sample collecting
				// - there is enough volume available in the inventory to create the cargo part
				if (currentStorage == null)
				{
					StoredPartData storedPart = inventory.CreateNewStoredPart(definition.SampleCollectingCargoPart);
					if (storedPart == null) // shouldn't happen
						return;

					ModuleHandler handler = storedPart.activeHandlers.Find(p => p is SampleStorageHandler sampleStorage && sampleStorage.definition.experimentInfo == definition.ExpInfo);
					if (handler == null) // shouldn't happen
						return;

					currentStorage = (SampleStorageHandler)handler;
				}

				currentData = currentStorage.RecordData(subject, dataProduced);
			}

			if (currentStorage.AvailableSize() == 0.0)
			{
				currentStorage = null;
				currentData = null;
			}

			Status = GetStatus(expState, subject, issue);
		}
	}

	public class SampleExperimentDefinition : ExperimentDefinition
	{
		[CFGValue] public string SampleCollectingPartName { get; private set; }

		public AvailablePart SampleCollectingCargoPart { get; private set; }

		public override void OnLateInit()
		{
			if (string.IsNullOrEmpty(ExperimentId))
				return;

			ExpInfo = ScienceDB.GetExperimentInfo(ExperimentId);

			if (ExpInfo != null && ExpInfo.SampleCollecting)
			{
				if (!string.IsNullOrEmpty(SampleCollectingPartName))
				{
					SampleCollectingCargoPart = PartLoader.getPartInfoByName(SampleCollectingPartName.Replace('_', '.'));
					if (SampleCollectingCargoPart == null)
					{
						ErrorManager.AddError(true, $"Error parsing definition {DefinitionName} for module {ModuleType}",
							$"Experiment {ExperimentId} has SampleCollecting=true but the part defined in SampleCollectingPartName={SampleCollectingPartName} doesn't exists");
					}
					else
					{
						ModuleKsmSampleStorage sampleStoragePrefab = SampleCollectingCargoPart.partPrefab.FindModuleImplementing<ModuleKsmSampleStorage>();
						ExpInfo.sampleStorageParts.Add(SampleCollectingCargoPart, sampleStoragePrefab.Definition);
					}
				}
			}

			base.OnLateInit();
		}
	}
}
