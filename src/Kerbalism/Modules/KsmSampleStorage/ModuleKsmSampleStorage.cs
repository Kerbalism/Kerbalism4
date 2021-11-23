using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InventoryAPI;

namespace KERBALISM
{
	public class ModuleKsmSampleStorage :
		KsmPartModule<ModuleKsmSampleStorage, SampleStorageHandler, SampleStorageDefinition>,
		IPartMassModifier, IVariablePackedVolumeModule, ICargoModuleCustomInfo
	{
		public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
		{
			return (float) (moduleHandler?.Mass ?? 0f); // TODO : moduleHandler is null when this is called on protopart creation. This will probably have side issues.
		}

		public ModifierChangeWhen GetModuleMassChangeWhen()
		{
			return ModifierChangeWhen.CONSTANTLY;
		}

		public bool UseMultipleVolume => true;

		public float CurrentPackedVolume()
		{
			if (moduleHandler == null)
			{
				SampleStorageDefinition storageDefinition = (SampleStorageDefinition)KsmModuleDefinitionLibrary.GetDefinition(this);
				ExperimentInfo expInfo = ScienceDB.GetExperimentInfo(storageDefinition.ExperimentId);
				if (storageDefinition == null || expInfo == null)
					return 0f;

				return (float)(storageDefinition.SampleAmount * expInfo.SampleVolume);
			}

			return (float)moduleHandler.PartVolume;
		}

		public string CargoModuleInfo()
		{
			return null;
		}

		public bool OverwriteDefaultWidget => true;

		private struct SampleInfo
		{
			public SubjectData subjectData;
			public double size;
			public string resultText;

			public SampleInfo(SubjectData subjectData, double size, string resultText)
			{
				this.subjectData = subjectData;
				this.size = size;
				this.resultText = resultText;
			}
		}

		public IEnumerable<WidgetInfo> GetWidgets(StoredPart storedPart, ProtoPartModuleSnapshot protoModule)
		{
			if (!ModuleHandler.TryGetHandler(protoModule, out SampleStorageHandler handler))
				return null;

			List<WidgetInfo> widgets = new List<WidgetInfo>(handler.samplesDict.Count + 1);
			ExperimentInfo expInfo = handler.definition.experimentInfo;

			KsmString moduleInfo = KsmString.Get;
			if (!expInfo.SampleCollecting)
			{
				moduleInfo.Info("Sample material", (handler.sampleMaterialSize / expInfo.DataSize).ToString("F2"));
			}

			moduleInfo.Info("Total mass", KF.ReadableMass((handler.samplesSize + handler.sampleMaterialSize) * expInfo.MassPerMB));

			widgets.Add(new WidgetInfo(KsmString.Get.Add("Sample", ": ", expInfo.Title).GetStringAndRelease(), moduleInfo.GetStringAndRelease()));

			foreach (ScienceSample sample in handler.Samples)
			{
				KsmString info = KsmString.Get;
				info.Info("Size", (sample.Size / expInfo.DataSize).ToString("F2"));
				info.Info("Mass", KF.ReadableMass(sample.Size * expInfo.MassPerMB));
				info.Info("Science value", KF.ReadableScience(sample.Size * sample.SubjectData.SciencePerMB));
				if (!string.IsNullOrEmpty(sample.ResultText))
					info.Add(sample.ResultText);

				widgets.Add(new WidgetInfo(sample.SubjectData.FullTitle, info.GetStringAndRelease(), Kolor.Science));
			}

			return widgets;
		}


	}
}
