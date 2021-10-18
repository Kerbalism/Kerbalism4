using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public class ProcessControllerDefinition : KsmModuleDefinition
	{
		[CFGValue] public string processName = string.Empty;
		[CFGValue] public double capacity = 1.0;
		[CFGValue] public string uiGroupName = null;         // internal name of the UI group
		[CFGValue] public string uiGroupDisplayName = null;  // display name of the UI group
		[CFGValue] public bool running = false; // will the process be running on part creation
		[CFGValue] public string capacityModifier = string.Empty;

		public ProcessDefinition processDefinition;

		public override void OnLoad(ConfigNode definitionNode)
		{
			if (ProcessDefinition.definitionsByName.TryGetValue(processName, out processDefinition))
				processDefinition.isControlled = true;
		}

		public override string ModuleDescription<ModuleKsmProcessController>(ModuleKsmProcessController modulePrefab)
		{
			return processDefinition?.GetInfo(capacity, true);
		}

		public override string ModuleTitle => processDefinition?.title ?? base.ModuleTitle;
	}
}
