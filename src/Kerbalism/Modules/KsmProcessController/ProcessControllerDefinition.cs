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

		public Process Process { get; private set; }

		public override void OnLoad(ConfigNode definitionNode)
		{
			Process = Profile.processes.Find(p => p.name == processName);
		}

		public override string ModuleDescription<ModuleKsmProcessController>(ModuleKsmProcessController modulePrefab)
		{
			return Process?.GetInfo(capacity, true);
		}

		public override string ModuleTitle => Process?.title ?? base.ModuleTitle;
	}
}
