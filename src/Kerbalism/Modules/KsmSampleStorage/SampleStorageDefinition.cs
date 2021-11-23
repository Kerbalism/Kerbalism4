using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class SampleStorageDefinition : KsmModuleDefinition, IKsmModuleDefinitionLateInit
	{
		/// <summary> If defined, that sample container can only store this experiment </summary>
		[CFGValue] public string ExperimentId { get; private set; } = string.Empty;

		/// <summary> How many experiments can be stored. Will define the cargo part volume. </summary>
		[CFGValue] public double SampleAmount { get; private set; } = 1.0;

		public ExperimentInfo experimentInfo;

		public void OnLateInit()
		{
			if (string.IsNullOrEmpty(ExperimentId) || !ScienceDB.TryGetExperimentInfo(ExperimentId, out experimentInfo))
			{
				ErrorManager.AddError(false, $"Error parsing module definition {DefinitionName} for {ModuleType}", $"ExperimentId={ExperimentId} : the experiment definition doesn't exists");
			}
		}
	}
}
