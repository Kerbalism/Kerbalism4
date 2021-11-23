using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Steamworks;

namespace KERBALISM
{
	public class SampleStorageHandler :
		KsmModuleHandler<ModuleKsmSampleStorage, SampleStorageHandler, SampleStorageDefinition>,
		IActiveStoredHandler, IKsmModuleHandlerLateInit, IKsmScienceDataStorage
	{
		private ModuleInventoryPartHandler inventory;
		public bool IsActiveCargo => true;
		public StoredPartData StoredPart { get; set; }

		/// <summary> amount of stored samples in Mb </summary>
		public double samplesSize;

		/// <summary> amount of sample material remaining in Mb </summary>
		public double sampleMaterialSize;

		public Dictionary<SubjectData, ScienceSample> samplesDict = new Dictionary<SubjectData, ScienceSample>();
		public Dictionary<SubjectData, ScienceSample>.ValueCollection Samples => samplesDict.Values;

		public double Mass => definition.experimentInfo.SampleCollecting ? samplesSize * definition.experimentInfo.MassPerMB : definition.SampleAmount * definition.experimentInfo.SampleMass;
		public double PartVolume => definition.SampleAmount * definition.experimentInfo.SampleVolume;

		public void OnLatePrefabInit(AvailablePart availablePart)
		{
			if (!ScienceDB.TryGetExperimentInfo(definition.ExperimentId, out ExperimentInfo experimentInfo))
				return;

			//experimentInfo.sampleStorageParts.Add(availablePart, definition);

			foreach (PartModule partPrefabModule in availablePart.partPrefab.Modules)
			{
				if (partPrefabModule is ModuleCargoPart cargoModule)
				{
					cargoModule.packedVolume = (float)(definition.SampleAmount * experimentInfo.SampleVolume);
				}
			}
		}

		public override void OnFirstSetup()
		{
			if (!definition.experimentInfo.SampleCollecting)
				sampleMaterialSize = definition.experimentInfo.DataSize * definition.SampleAmount;
		}

		public override void OnStart()
		{
			inventory = partData.GetModuleHandler<ModuleInventoryPartHandler>();
		}

		public override void OnLoad(ConfigNode node)
		{
			sampleMaterialSize = Lib.ConfigValue(node, nameof(sampleMaterialSize), 0.0);

			samplesDict.Clear();
			ConfigNode samplesNode = node.GetNode("SAMPLES");
			if (samplesNode != null)
			{
				foreach (ConfigNode sampleNode in samplesNode.GetNodes())
				{
					KsmScienceData.Load(sampleNode, this);
				}
			}
		}

		public override void OnSave(ConfigNode node)
		{
			node.AddValue(nameof(sampleMaterialSize), sampleMaterialSize);

			if (samplesDict.Count > 0)
			{
				ConfigNode samplesNode = node.AddNode("SAMPLES");
				foreach (ScienceSample sample in samplesDict.Values)
				{
					ConfigNode sampleNode = samplesNode.AddNode("SAMPLE");
					sample.Save(sampleNode);
					
				}
			}
		}

		public override void OnFlightPartWillDie()
		{
			DeleteAllData();
		}

		public double AvailableSize()
		{
			if (!definition.experimentInfo.SampleCollecting)
			{
				// mass isn't dynamic, no need to check it
				return sampleMaterialSize;
			}

			double availableSize = (definition.SampleAmount * definition.experimentInfo.DataSize) - samplesSize;

			if (inventory.hasMassLimit)
			{
				double availableMass = inventory.MassAvailable;
				availableSize = Math.Min(availableSize, availableMass / definition.experimentInfo.MassPerMB);
			}
			return Lib.ClampToPositive(availableSize);
		}

		public ScienceSample RecordData(SubjectData subject, double size, bool generateResultText = true, string resultText = null, bool useStockCrediting = false)
		{
			if (subject.ExpInfo != definition.experimentInfo)
				return null;

			if (!samplesDict.TryGetValue(subject, out ScienceSample sample))
			{
				sample = new ScienceSample(this, subject, size, generateResultText, resultText, useStockCrediting, false);
			}
			else
			{
				sample.AddSize(size);
			}

			return sample;
		}

		internal void UpdateInventoryMassOnSizeModified()
		{
			float newMassf = (float)Mass;
			ProtoPartSnapshot protoPart = StoredPart.protoPart;
			// note : we assume that we are the only IPartMassModifier on the cargo part
			// ProtoPartSnapshot.moduleMass is the sum of all IPartMassModifier modules on the part.
			// Also note that this value isn't used by anything in stock KSP (and I doubt any mod is using it either)
			protoPart.moduleMass = newMassf;
			// mass is the part configured mass + modules mass
			protoPart.mass = protoPart.partPrefab.mass + newMassf;

			inventory.UpdateMassAndVolume(true);
		}

		public void OnCargoStored()
		{
			foreach (ModuleHandler module in partData.modules)
			{
				if (module is SampleExperimentHandler experiment && experiment.definition.ExpInfo == definition.experimentInfo)
				{
					experiment.RegisterSampleStorage(this);
				}
			}
		}

		public void OnCargoUnstored()
		{
			foreach (ScienceSample scienceSample in Samples)
			{
				scienceSample.SubjectData.RemoveDataCollectedInFlight(scienceSample.Size);
			}

			foreach (ModuleHandler module in partData.modules)
			{
				if (module is SampleExperimentHandler experiment && experiment.definition.ExpInfo == definition.experimentInfo)
				{
					experiment.UnregisterSampleStorage(this);
				}
			}
		}

		/// <summary> Get all drives on the vessel, including private drives </summary>
		public static IEnumerable<SampleStorageHandler> GetAllSampleStorages(VesselDataBase vd)
		{
			return vd.Parts.AllModulesOfType<SampleStorageHandler>();
		}

		/// <summary>
		/// Delete all files/samples in the drive. Use with care, this should onyl be used if all files/samples
		/// are garanteed to be permanently removed from the game and aren't referenced by anything anymore.
		/// </summary>
		public void DeleteAllData()
		{
			foreach (ScienceSample sample in samplesDict.Values)
				sample.SubjectData.RemoveDataCollectedInFlight(sample.Size);

			samplesDict.Clear();
			samplesSize = 0.0;
		}
	}
}
