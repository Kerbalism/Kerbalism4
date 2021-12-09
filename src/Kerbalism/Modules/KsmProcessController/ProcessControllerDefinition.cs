using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public class ProcessControllerDefinition : KsmModuleDefinition
	{
		[CFGValue] public string processName = string.Empty;      // process name, mandatory
		[CFGValue] public string controllerTitle = string.Empty;  // module display name, optional (the process title will be used if undefined)
		[CFGValue] public double capacity = 1.0;                  // multipler for the process input/output rates
		[CFGValue] public string uiGroupName = null;              // name of the UI group
		[CFGValue] public string uiGroupDisplayName = null;       // display name of the UI group
		[CFGValue] public bool running = false;                   // is the process running on part creation
		[CFGValue] public string capacityModifier = string.Empty; // optional flee expression, must return a double (will be multiplied to the capacity)

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

		public override string ModuleTitle
		{
			get
			{
				if (controllerTitle.Length == 0)
				{
					return processDefinition?.title ?? base.ModuleTitle;
				}

				return controllerTitle;
			}
		}
	}
}
