using Flee.PublicTypes;
using System;
using System.Collections.Generic;
using KERBALISM.ModuleUI;

namespace KERBALISM
{
	public class ProcessControllerHandler :
		KsmModuleHandler<ModuleKsmProcessController, ProcessControllerHandler, ProcessControllerDefinition>,
		IB9Switchable, IActiveStoredHandler
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
			//if (definition.processDefinition != null && IsLoaded)
			//	loadedModule.PAWSetup();
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

		public bool IsActiveCargo => true;
		public StoredPartData StoredPart { get; set; }
		public void OnCargoStored()
		{
			//throw new NotImplementedException();
		}

		public void OnCargoUnstored()
		{
			//throw new NotImplementedException();
		}

		public override string ModuleTitle => definition.processDefinition?.title ?? string.Empty;

		protected override ModuleUIGroup CreateUIGroup()
		{
			if (definition.uiGroupName == null)
				return null;
			
			return new ModuleUIGroup(definition.uiGroupName, definition.uiGroupDisplayName);
		}

		private class RunningToggle : ModuleUIToggle<ProcessControllerHandler>
		{
			public override bool State => handler.isRunning;

			public override string GetLabel()
			{
				KsmString ks = KsmString.Get;
				if (handler.isRunning)
					ks.InfoRight(handler.definition.processDefinition.title, Local.Generic_ENABLED, KF.Bold, KF.KolorGreen);
				else
					ks.InfoRight(handler.definition.processDefinition.title, Local.Generic_DISABLED, KF.Bold, KF.KolorYellow);

				return ks.GetStringAndRelease(); 
			}

			public override void OnToggle()
			{
				handler.IsRunning = !handler.IsRunning;
			}

			public override bool IsEnabled => handler.definition.processDefinition?.canToggle ?? false;
		}
	}
}
