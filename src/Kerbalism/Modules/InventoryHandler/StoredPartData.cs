using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace KERBALISM
{
	public class StoredPartData
	{
		public StoredPart stockStoredPart;
		public double volume;
		public ActiveCargoPartInfo activeCargoInfo;
		public bool isInstalled;
		public ProtoPartSnapshot protoPart;
		public List<ModuleHandler> activeHandlers;
		public List<CargoPartResourceWrapper> resources;
		public List<BaseField> resourcePAWFields;
		public ConfigNode unloadedInventoryNode;
		public ModuleInventoryPartHandler inventory;

		public StoredPartData(ModuleInventoryPartHandler inventory, StoredPart stockStoredPart, bool isInstalled, ConfigNode unloadedInventoryNode)
		{
			this.inventory = inventory;
			this.stockStoredPart = stockStoredPart;
			this.protoPart = stockStoredPart.snapshot;
			this.isInstalled = isInstalled;
			this.unloadedInventoryNode = unloadedInventoryNode;
		}

		public void SaveToUnloadedNode()
		{
			ConfigNode partNode = unloadedInventoryNode.GetNode("PART");
			if (partNode == null)
				partNode = unloadedInventoryNode.AddNode("PART");
			else
				partNode.ClearData();

			protoPart.Save(partNode);
		}

		public void InstallActivePart(bool partWasJustStored)
		{
			for (int i = 0; i < protoPart.modules.Count; i++)
			{
				ProtoPartModuleSnapshot protoModule = protoPart.modules[i];

				if (!ModuleHandler.protoHandlersByProtoModule.TryGetValue(protoModule, out ModuleHandler handler))
					continue;

				if (!handler.handlerType.isActiveCargo)
					continue;

				IActiveStoredHandler storedHandler = (IActiveStoredHandler)handler;

				if (!storedHandler.IsActiveCargo)
					continue;

				int moduleIndex = i;

				if (!Lib.TryFindModulePrefab(protoPart, ref moduleIndex, out PartModule modulePrefab))
					continue;

				handler.partData = inventory.partData;
				handler.moduleIndex = moduleIndex;
				handler.protoModule = protoModule;
				handler.PrefabModuleBase = modulePrefab;

				if (activeHandlers == null)
					activeHandlers = new List<ModuleHandler>();

				storedHandler.StoredPart = this;
				activeHandlers.Add(handler);
				inventory.partData.modules.Add(handler);
			}

			if (activeCargoInfo.allowActiveResources && protoPart.resources.Count != 0)
			{
				if (resources == null)
					resources = new List<CargoPartResourceWrapper>(protoPart.resources.Count);

				foreach (ProtoPartResourceSnapshot protoResource in protoPart.resources)
				{
					if (activeCargoInfo.hasActiveResourcesWhiteList && !activeCargoInfo.activeResources.Contains(protoResource.definition))
						continue;

					CargoPartResourceWrapper wrapper = new CargoPartResourceWrapper(inventory.partData, this, protoResource);
					resources.Add(wrapper);
				}

				if (resources.Count == 0)
				{
					resources = null;
				}
				else if (inventory.IsLoaded)
				{
					CreateResourcePAWEntries();
				}
			}

			if (activeHandlers != null)
			{
				foreach (ModuleHandler handler in activeHandlers)
				{
					handler.FirstSetup();
					handler.Start();
					((IActiveStoredHandler)handler).OnCargoStored();

					if (partWasJustStored && inventory.IsLoaded)
						handler.AddModuleUIToPart(inventory.partData.LoadedPart);
				}
			}
		}

		public void UninstallActivePart()
		{
			if (activeHandlers != null)
			{
				foreach (ModuleHandler activeHandler in activeHandlers)
				{
					((IActiveStoredHandler)activeHandler).StoredPart = null;
					((IActiveStoredHandler)activeHandler).OnCargoUnstored();

					if (activeHandler.handlerType.isPersistent)
					{
						ProtoPartModuleSnapshot protoModule = activeHandler.protoModule;
						ConfigNode moduleNode = protoModule.moduleValues.GetNode(ModuleHandler.NODENAME_KSMMODULE);
						if (moduleNode == null)
							moduleNode = protoModule.moduleValues.AddNode(ModuleHandler.NODENAME_KSMMODULE);
						else
							moduleNode.ClearData();

						moduleNode.AddValue(nameof(ModuleHandler.handlerIsEnabled), activeHandler.handlerIsEnabled);
						((IPersistentModuleHandler)activeHandler).Save(moduleNode);
					}

					activeHandler.partData = null;
					activeHandler.protoModule = null;
					activeHandler.PrefabModuleBase = null;

					inventory.partData.modules.Remove(activeHandler);

					if (inventory.IsLoaded)
					{
						activeHandler.RemoveModuleUIFromPart(inventory.partData.LoadedPart);
					}
				}

				activeHandlers.Clear();
			}

			if (resources != null)
			{
				foreach (CargoPartResourceWrapper partResourceWrapper in resources)
				{
					partResourceWrapper.RemoveFromResHandler(inventory.partData);
				}

				if (inventory.IsLoaded)
					RemoveResourcePAWEntries();

				resources.Clear();
			}
		}

		private static UI_KSMCargoResourceFlight cargoResourceFlightControl = new UI_KSMCargoResourceFlight();
		[UI_KSMCargoResourceFlight] private static object cargoResourceFlightDummyField;
		private static FieldInfo cargoResourceFlightDummyFieldInfo = AccessTools.Field(typeof(StoredPartData), nameof(cargoResourceFlightDummyField));

		private static UI_KSMCargoResourceEditor cargoResourceEditorControl = new UI_KSMCargoResourceEditor();
		[UI_KSMCargoResourceEditor] private static object cargoResourceEditorDummyField;
		private static FieldInfo cargoResourceEditorDummyFieldInfo = AccessTools.Field(typeof(StoredPartData), nameof(cargoResourceEditorDummyField));

		public void CreateResourcePAWEntries()
		{
			if (resources == null)
				return;

			if (inventory.resourcesPAWGroup == null)
				inventory.resourcesPAWGroup = new BasePAWGroup("cargoResources", "Inventory resources", true);

			resourcePAWFields = new List<BaseField>(resources.Count);

			foreach (CargoPartResourceWrapper resource in resources)
			{
				BaseField pawField;
				if (Lib.IsEditor)
				{
					pawField = new BaseField(cargoResourceEditorControl, cargoResourceEditorDummyFieldInfo, resource);
					pawField.guiActive = false;
					pawField.guiActiveEditor = true;
				}
				else
				{
					pawField = new BaseField(cargoResourceFlightControl, cargoResourceFlightDummyFieldInfo, resource);
					pawField.guiActive = true;
					pawField.guiActiveEditor = false;
				}

				pawField.group = inventory.resourcesPAWGroup;
				resourcePAWFields.Add(pawField);
				inventory.partData.LoadedPart.Fields.Add(pawField);
			}

			if (inventory.partData.LoadedPart.PartActionWindow != null)
				inventory.partData.LoadedPart.PartActionWindow.displayDirty = true;
		}

		public void RemoveResourcePAWEntries()
		{
			if (resources == null)
				return;

			FieldInfo partFieldsInfo = AccessTools.Field(typeof(BaseFieldList), "_fields");
			List<BaseField> partFields = (List<BaseField>)partFieldsInfo.GetValue(inventory.partData.LoadedPart.Fields);

			if (partFields == null)
				return;

			foreach (BaseField baseField in resourcePAWFields)
			{
				partFields.Remove(baseField);

				if (inventory.partData.LoadedPart.PartActionWindow != null)
				{
					inventory.partData.LoadedPart.PartActionWindow.RemoveFieldControl(baseField, inventory.partData.LoadedPart, null);
				}
			}

			resourcePAWFields.Clear();

			// TODO : this will cause a bit of flickering if the group is shared by resources on another part.
			if (inventory.resourcesPAWGroup != null)
				inventory.partData.LoadedPart.PartActionWindow.RemoveGroup(inventory.resourcesPAWGroup.name);
		}
	}
}
