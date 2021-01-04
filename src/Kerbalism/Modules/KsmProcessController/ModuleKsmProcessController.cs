using UnityEngine;

namespace KERBALISM
{
	public class ModuleKsmProcessController :
		KsmPartModule<ModuleKsmProcessController, ProcessControllerHandler, ProcessControllerDefinition>,
		IModuleInfo,
		IAnimatedModule
	{
		[KSPField]
		[UI_Toggle(scene = UI_Scene.All, affectSymCounterparts = UI_Scene.None)]
		public bool running;

		private BaseField runningField;

		public override void KsmStart()
		{
			runningField = Fields["running"];
			runningField.OnValueModified += ToggleRunning;

			((UI_Toggle)runningField.uiControlFlight).enabledText = Lib.Color(Local.Generic_ENABLED.ToLower(), Lib.Kolor.Green);
			((UI_Toggle)runningField.uiControlFlight).disabledText = Lib.Color(Local.Generic_DISABLED.ToLower(), Lib.Kolor.Yellow);
			((UI_Toggle)runningField.uiControlEditor).enabledText = Lib.Color(Local.Generic_ENABLED.ToLower(), Lib.Kolor.Green);
			((UI_Toggle)runningField.uiControlEditor).disabledText = Lib.Color(Local.Generic_DISABLED.ToLower(), Lib.Kolor.Yellow);

			if (Definition.Process != null)
				PAWSetup();
		}

		public void PAWSetup()
		{
			running = moduleHandler.IsRunning;
			runningField.guiActive = runningField.guiActiveEditor = Definition.Process.canToggle;
			runningField.guiName = Definition.Process.title;

			if (Definition.uiGroupName != null)
				runningField.group = new BasePAWGroup(Definition.uiGroupName, Definition.uiGroupDisplayName ?? Definition.uiGroupName, false);
		}

		private void ToggleRunning(object field)
		{
			moduleHandler.IsRunning = !moduleHandler.IsRunning;
		}

		// IModuleInfo : module title
		public string GetModuleTitle()
		{
			if (Definition.Process == null)
				return "Process controller";

			return Definition.Process.title;
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

