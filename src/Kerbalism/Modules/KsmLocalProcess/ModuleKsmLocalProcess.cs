using MultipleModuleInPartAPI;
using UnityEngine;

namespace KERBALISM
{
	public class ModuleKsmLocalProcess :
		KsmPartModule<ModuleKsmLocalProcess, LocalProcessHandler, LocalProcessDefinition>,
		IModuleInfo, IMultipleModuleInPart
	{
		[KSPField(isPersistant = true)]
		public string modulePartConfigId = string.Empty;
		public string ModulePartConfigId => modulePartConfigId;

		// IModuleInfo : module title
		public string GetModuleTitle()
		{
			return Definition.title;
		}

		// IModuleInfo : part tooltip module description
		public override string GetInfo()
		{
			return moduleHandler.GetSubtypeDescription(moduleHandler.definition, null) ?? string.Empty;
		}

		// IModuleInfo : part tooltip general part info
		public string GetPrimaryField() => string.Empty;
		public Callback<Rect> GetDrawModulePanelCallback() => null;
	}

} // KERBALISM

