using Flee.PublicTypes;
using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public class ProcessControllerHandler :
		KsmModuleHandler<ModuleKsmProcessController, ProcessControllerHandler, ProcessControllerDefinition>,
		IB9Switchable
	{

		private bool isRunning;
		public bool IsRunning
		{
			get => isRunning;
			set
			{
				if (value != isRunning)
				{
					isRunning = value;

					if (IsLoaded)
						loadedModule.running = value;

					// refresh planner and VAB/SPH ui
					if (Lib.IsEditor) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
				}
			}
		}

		public override void OnFirstSetup()
		{
			isRunning = definition.running;
		}

		public void OnSwitchChangeDefinition(KsmModuleDefinition previousDefinition)
		{
			if (definition.processDefinition != null && IsLoaded)
				loadedModule.PAWSetup();
		}

		public void OnSwitchEnable() { }

		public void OnSwitchDisable() { }

		public string GetSubtypeDescription(KsmModuleDefinition subTypeDefinition, string techRequired)
		{
			return subTypeDefinition.ModuleDescription(modulePrefab);
		}

		public override void OnLoad(ConfigNode node)
		{
			isRunning = Lib.ConfigValue(node, "isRunning", true);
		}

		public override void OnSave(ConfigNode node)
		{
			node.AddValue("isRunning", isRunning);
		}

		public override void OnUpdate(double elapsedSec)
		{
			VesselData.VesselProcesses.RegisterProcessController(this);
		}

		public override string ModuleTitle => definition.processDefinition?.title ?? string.Empty;
	}
}
