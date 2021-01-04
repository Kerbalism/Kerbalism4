using Flee.PublicTypes;
using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public class ProcessControllerHandler :
		KsmModuleHandler<ModuleKsmProcessController, ProcessControllerHandler, ProcessControllerDefinition>,
		IB9Switchable
	{
		private VirtualPartResource capacityResource;
		private int capacityResourceContainerIndex;

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
			if (definition.Process != null)
				AddCapacityResource();

			isRunning = definition.running;
		}

		public override void OnStart()
		{
			if (definition.Process != null)
				GetCapacityResource();
		}

		public void OnSwitchChangeDefinition(KsmModuleDefinition previousDefinition)
		{
			RemoveCapacityResource();

			if (definition.Process != null)
			{
				AddCapacityResource();

				if (IsLoaded)
					loadedModule.PAWSetup();
			}
		}

		public void OnSwitchEnable() { }

		public void OnSwitchDisable() { }

		private void GetCapacityResource()
		{
			if (definition.Process.UseCapacityResource)
			{
				capacityResource = partData.virtualResources.GetResource(definition.Process.CapacityResourceName, capacityResourceContainerIndex);
			}
		}

		private void AddCapacityResource()
		{
			if (definition.Process.UseCapacityResource)
			{
				capacityResource = partData.virtualResources.AddResource(definition.Process.CapacityResourceName, definition.capacity, definition.capacity, true);
			}
		}

		private void RemoveCapacityResource()
		{
			if (capacityResource != null)
			{
				partData.virtualResources.RemoveResource(capacityResource);
				capacityResource = null;
			}
		}

		public string GetSubtypeDescription(KsmModuleDefinition subTypeDefinition, string techRequired)
		{
			return subTypeDefinition.ModuleDescription(modulePrefab);
		}

		public override void OnLoad(ConfigNode node)
		{
			isRunning = Lib.ConfigValue(node, "isRunning", true);
			capacityResourceContainerIndex = Lib.ConfigValue(node, "capacityIndex", -1);
		}

		public override void OnSave(ConfigNode node)
		{
			node.AddValue("isRunning", isRunning);

			if (capacityResource != null)
			{
				node.AddValue("capacityIndex", capacityResource.ContainerIndex);
			}
		}

		public override void OnVesselDataUpdate()
		{
			double availableCapacity;
			if (capacityResource != null)
			{
				availableCapacity = definition.capacity * capacityResource.Level;
				capacityResource.FlowState = isRunning;
			}
			else
			{
				availableCapacity = definition.capacity;
			}

			VesselData.VesselProcesses.GetOrCreateProcessData(definition.Process).RegisterProcessControllerCapacity(isRunning, availableCapacity);
		}

		public override string ModuleTitle => definition.Process?.title ?? string.Empty;
	}
}
