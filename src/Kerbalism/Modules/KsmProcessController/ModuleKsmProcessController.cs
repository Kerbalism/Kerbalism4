using MultipleModuleInPartAPI;
using UnityEngine;

namespace KERBALISM
{
	public class ModuleKsmProcessController :
		KsmPartModule<ModuleKsmProcessController, ProcessControllerHandler, ProcessControllerDefinition>,
		IModuleInfo,
		IAnimatedModule,
		IMultipleModuleInPart
	{
		[KSPField(isPersistant = true)]
		public string modulePartConfigId = string.Empty;
		public string ModulePartConfigId => modulePartConfigId;

		// IModuleInfo : module title
		public string GetModuleTitle()
		{
			if (Definition.processDefinition == null)
				return "Process controller";

			return Definition.processDefinition.title;
		}

		// IModuleInfo : part tooltip module description
		public override string GetInfo()
		{
			return moduleHandler.GetSubtypeDescription(moduleHandler.definition, null) ?? string.Empty;
		}

		// IModuleInfo : part tooltip general part info
		public string GetPrimaryField() => string.Empty;
		public Callback<Rect> GetDrawModulePanelCallback() => null;

		// TODO : animation group support
		public void EnableModule() { }
		public void DisableModule() { }
		public bool ModuleIsActive() { return true; }
		public bool IsSituationValid() { return true; }
	}

} // KERBALISM

