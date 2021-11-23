using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using KSP.Localization;
using Steamworks;

namespace KERBALISM
{
	public interface IActiveStoredHandler
	{
		/// <summary>
		/// Should that module be instantiated when its part is stored in an inventory ?
		/// This is called between Load() and Start(). You can access the part/module prefabs,
		/// as well as the KsmModule definition, but both the loaded module and protomodule will be null.
		/// </summary>
		bool IsActiveCargo { get; }

		StoredPartData StoredPart { get; set; }

		/// <summary>
		/// Called when the part is being stored.
		/// Will also be called when an already stored part is instantiated, after the handler Start()/OnStart()
		/// </summary>
		void OnCargoStored();

		void OnCargoUnstored();
	}

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
		private static FieldInfo cargoResourceFlightDummyFieldInfo = AccessTools.Field(typeof(ModuleInventoryPartHandler), nameof(cargoResourceFlightDummyField));

		private static UI_KSMCargoResourceEditor cargoResourceEditorControl = new UI_KSMCargoResourceEditor();
		[UI_KSMCargoResourceEditor] private static object cargoResourceEditorDummyField;
		private static FieldInfo cargoResourceEditorDummyFieldInfo = AccessTools.Field(typeof(ModuleInventoryPartHandler), nameof(cargoResourceEditorDummyField));

		public void CreateResourcePAWEntries()
		{
			if (resources == null)
				return;

			BasePAWGroup pawGroup = inventory.loadedModule.Fields[nameof(ModuleInventoryPart.InventorySlots)]?.group;

			if (pawGroup == null)
			{
				if (inventory.resourcesPAWGroup == null)
					inventory.resourcesPAWGroup = new BasePAWGroup("cargoResources", "Inventory resources", true);

				pawGroup = inventory.resourcesPAWGroup;
			}

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

				pawField.group = pawGroup;
				resourcePAWFields.Add(pawField);
				inventory.partData.LoadedPart.Fields.Add(pawField);
			}

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

	public class ModuleInventoryPartHandler : TypedModuleHandler<ModuleInventoryPart>, IPersistentModuleHandler
	{
		private static Action<UIPartActionInventory, float, float, bool> UIPartActionInventory_UpdateMassLimits;

		static ModuleInventoryPartHandler()
		{
			MethodInfo method = AccessTools.Method(typeof(UIPartActionInventory), "UpdateMassLimits");
			UIPartActionInventory_UpdateMassLimits = AccessTools.MethodDelegate<Action<UIPartActionInventory, float, float, bool>>(method, null, false);
		}

		public override ActivationContext Activation => ActivationContext.Editor | ActivationContext.Loaded | ActivationContext.Unloaded;

		public List<StoredPartData> storedParts = new List<StoredPartData>(0);
		public Dictionary<int, StoredPartData> storedPartsBySlotIndex = new Dictionary<int, StoredPartData>(0);
		public List<KsmCargoInstallButtonHandler> installButtonHandlers;

		private double massStored;
		public double massCapacity;
		public bool hasMassLimit;
		public double MassAvailable => hasMassLimit ? massCapacity - massStored : double.MaxValue;

		public double volumeStored;
		public double volumeCapacity;
		public bool hasVolumeLimit;
		public double VolumeAvailable => hasVolumeLimit ? volumeCapacity - volumeStored : double.MaxValue;

		private UI_Grid flightPAWGrid;
		internal BasePAWGroup resourcesPAWGroup;

		private HashSet<int> installedPartsCache;

		public ModuleHandler ModuleHandler => this;
		public bool ConfigLoaded { get; set; }
		public void Load(ConfigNode node)
		{
			string installedPartsValue = node.GetValue("installedParts");
			if (!string.IsNullOrEmpty(installedPartsValue))
			{
				string[] splittedValues = installedPartsValue.Split(',');

				if (splittedValues.Length > 0)
				{
					installedPartsCache = new HashSet<int>(splittedValues.Length);
					foreach (string indexString in splittedValues)
					{
						if (int.TryParse(indexString, out int index))
							installedPartsCache.Add(index);
					}
				}
			}
		}

		public void Save(ConfigNode node)
		{
			if (!handlerIsEnabled)
				return;

			if (storedParts.Count == 0)
				return;

			bool isLoaded = IsLoaded;
			KsmString ks = KsmString.Get;

			foreach (StoredPartData storedPartData in storedParts)
			{
				if (storedPartData.isInstalled)
				{
					if (ks.Length > 0)
						ks.Add(',');

					ks.Add(storedPartData.stockStoredPart.slotIndex.ToString());
				}

				if (!isLoaded)
					storedPartData.SaveToUnloadedNode();
			}

			if (ks.Length > 0)
				node.AddValue("installedParts", ks.GetStringAndRelease());
			else
				ks.Release();
		}

		public override void OnFirstSetup()
		{
			// For kerbals on EVA, the stock module is manually calling Load() from its OnStart()
			// Since our FirstSetup()/Start() methods are called before the stock module OnStart(),
			// we can't do anything from here in that case. We use a ModuleInventoryPart.OnStart() postfix
			// to handle that case.
			if (IsLoaded && loadedModule.IsKerbalOnEVA)
				return;

			// call Start() : will call OnStart() and load "default" stored parts (DEFAULTPARTS{} node in the inventory module config)
			Start();

			// call FirstSetup/Start for the default stored parts
			ForceActiveHandlersSetupAndStart();
		}

		public override void OnStart()
		{
			// For kerbals on EVA, the stock module is manually calling Load() from its OnStart()
			// Since our FirstSetup()/Start() methods are called before the stock module OnStart(),
			// we can't do anything from here in that case. We use a ModuleInventoryPart.OnStart() postfix
			// to handle that case.
			if (IsLoaded && loadedModule.IsKerbalOnEVA)
				return;

			ParseStockModule();
		}

		internal void ParseStockModule()
		{
			// no support for upgrades / module switching
			massCapacity = prefabModule.massLimit;
			hasMassLimit = prefabModule.HasMassLimit;

			volumeCapacity = prefabModule.packedVolumeLimit;
			hasVolumeLimit = prefabModule.HasPackedVolumeLimit;

			if (IsLoaded)
			{
				// we don't handle the inventories of kerbals while they are inside a part
				if (loadedModule.kerbalMode)
				{
					handlerIsEnabled = false;
					return;
				}

				flightPAWGrid = loadedModule.Fields[nameof(ModuleInventoryPart.InventorySlots)].uiControlFlight as UI_Grid;
				
				massStored = loadedModule.massCapacity;
				volumeStored = loadedModule.volumeCapacity;

				if (loadedModule.storedParts == null)
					return;

				int storedPartsCount = loadedModule.storedParts.Count;
				storedParts = new List<StoredPartData>(storedPartsCount);
				storedPartsBySlotIndex = new Dictionary<int, StoredPartData>(storedPartsCount);

				for (int i = 0; i < storedPartsCount; i++)
				{
					StoredPart storedPart = loadedModule.storedParts.At(i);
					bool isInstalled = installedPartsCache?.Contains(storedPart.slotIndex) ?? false;
					CreateStoredPart(storedPart, false, isInstalled);
				}
			}
			else
			{
				ConfigNode storedPartsNode = protoModule.moduleValues.GetNode("STOREDPARTS");

				int storedPartsCount = storedPartsNode == null ? 0 : storedPartsNode.CountNodes;
				storedParts = new List<StoredPartData>(storedPartsCount);
				storedPartsBySlotIndex = new Dictionary<int, StoredPartData>(storedPartsCount);

				if (storedPartsCount == 0)
					return;

				foreach (ConfigNode storedPartNode in storedPartsNode.nodes)
				{
					if (storedPartNode.name == "STOREDPART")
					{
						StoredPart storedPart = new StoredPart(null, 0);
						storedPart.Load(storedPartNode);
						bool isInstalled = installedPartsCache?.Contains(storedPart.slotIndex) ?? false;
						CreateStoredPart(storedPart, false, isInstalled, storedPartNode);
					}
				}
			}

			UpdateMassAndVolume(false);
		}

		internal void ForceActiveHandlersSetupAndStart()
		{
			foreach (StoredPartData storedPartData in storedParts)
			{
				if (storedPartData.activeHandlers == null)
					continue;

				foreach (ModuleHandler activeHandler in storedPartData.activeHandlers)
				{
					activeHandler.FirstSetup();
					activeHandler.Start();
				}
			}
		}

		internal StoredPartData CreateStoredPart(StoredPart stockStoredPart, bool partWasJustStored, bool isInstalled = false, ConfigNode inventoryNode = null)
		{
			ProtoPartSnapshot protoPart = stockStoredPart.snapshot;
			if (protoPart == null)
				return null;

			StoredPartData storedPart = new StoredPartData(this, stockStoredPart, isInstalled, inventoryNode);
			storedPart.volume = InventoryAPI.Utils.GetPartCargoVolume(protoPart);

			storedParts.Add(storedPart);
			storedPartsBySlotIndex.Add(stockStoredPart.slotIndex, storedPart);

			if (stockStoredPart.quantity != 1)
				return storedPart;

			if (!ActiveCargoPartsDB.activeCargoPartsInfos.TryGetValue(protoPart.partInfo, out ActiveCargoPartInfo cargoInfo))
				return storedPart;

			storedPart.activeCargoInfo = cargoInfo;

			if (!cargoInfo.requireInstallation && !isInstalled)
			{
				isInstalled = true;
				storedPart.isInstalled = true;
			}

			if (isInstalled)
				storedPart.InstallActivePart(partWasJustStored);

			if (partWasJustStored && installButtonHandlers != null)
			{
				foreach (KsmCargoInstallButtonHandler ksmCargoInstallButtonHandler in installButtonHandlers)
				{
					if (ksmCargoInstallButtonHandler.slotIndex == stockStoredPart.slotIndex)
					{
						ksmCargoInstallButtonHandler.UpdateVisibilityAndColor();
					}
				}
			}

			return storedPart;
		}

		internal void UpdateMassAndVolume(bool updateStockModule)
		{
			massStored = 0.0;
			volumeStored = 0.0;

			double partMass;
			double quantity;

			foreach (StoredPartData storedPartData in storedParts)
			{
				quantity = storedPartData.stockStoredPart.quantity;
				partMass = storedPartData.protoPart.mass;

				foreach (ProtoPartResourceSnapshot protoResource in storedPartData.protoPart.resources)
				{
					partMass += protoResource.amount * protoResource.definition.density;
				}

				volumeStored += storedPartData.volume * quantity;
				massStored += partMass * quantity;
			}

			if (updateStockModule && IsLoaded)
			{
				float massStoredF = (float) massStored;
				bool overlimit = massStoredF >= loadedModule.massLimit;
				if (overlimit)
					massStoredF = loadedModule.massLimit;

				if (loadedModule.constructorModeInventory != null)
				{
					UIPartActionInventory_UpdateMassLimits(loadedModule.constructorModeInventory, massStoredF, loadedModule.massLimit, overlimit);
				}

				if (flightPAWGrid?.pawInventory != null)
				{
					UIPartActionInventory_UpdateMassLimits(flightPAWGrid.pawInventory, massStoredF, loadedModule.massLimit, overlimit);
				}
			}
		}

		internal void UpdateMass(double delta)
		{
			massStored = Lib.Clamp(massStored + delta, 0.0, massCapacity);
		}

		public void OnPartUnstored(StoredPart storedPart)
		{
			if (!storedPartsBySlotIndex.TryGetValue(storedPart.slotIndex, out StoredPartData storedPartData))
				return;

			storedPartsBySlotIndex.Remove(storedPart.slotIndex);
			storedParts.Remove(storedPartData);

			if (storedPartData.isInstalled)
				storedPartData.UninstallActivePart();

			if (installButtonHandlers != null)
			{
				foreach (KsmCargoInstallButtonHandler ksmCargoInstallButtonHandler in installButtonHandlers)
				{
					if (ksmCargoInstallButtonHandler.slotIndex == storedPart.slotIndex)
					{
						ksmCargoInstallButtonHandler.UpdateVisibilityAndColor();
					}
				}
			}

			UpdateMassAndVolume(false);
		}

		public StoredPartData CreateNewStoredPart(AvailablePart part)
		{
			if (IsLoaded)
			{
				int slotIndex = loadedModule.FirstEmptySlot();
				loadedModule.StoreCargoPartAtSlot(part.partPrefab, slotIndex);
				return storedPartsBySlotIndex[slotIndex];
			}

			List<int> indexes = new List<int>(storedParts.Count);
			foreach (StoredPartData storedPartData in storedParts)
			{
				indexes.Add(storedPartData.stockStoredPart.slotIndex);
			}
			indexes.Sort();
			int freeSlotIndex = -1;
			for (int i = 0; i < indexes.Count; i++)
			{
				if (indexes[i] > i)
				{
					freeSlotIndex = i;
					break;
				}
			}

			if (freeSlotIndex == -1)
				freeSlotIndex = indexes.Count;

			StoredPart stockStoredPart = new StoredPart(part.name, freeSlotIndex);
			stockStoredPart.snapshot = new ProtoPartSnapshot(part.partPrefab, null);
			stockStoredPart.quantity = 1;
			stockStoredPart.stackCapacity = stockStoredPart.snapshot.moduleCargoStackableQuantity;
			stockStoredPart.variantName = stockStoredPart.snapshot.moduleVariantName;
			ConfigNode stockStoredPartNode = new ConfigNode("STOREDPART");
			stockStoredPart.Save(stockStoredPartNode);

			ConfigNode inventoryStoredPartsNode = protoModule.moduleValues.GetNode("STOREDPARTS");
			if (inventoryStoredPartsNode == null)
				inventoryStoredPartsNode = protoModule.moduleValues.AddNode("STOREDPARTS");

			inventoryStoredPartsNode.AddNode(stockStoredPartNode);

			StoredPartData newStoredPartData = CreateStoredPart(stockStoredPart, true, false, stockStoredPartNode);
			if (newStoredPartData != null)
				UpdateMassAndVolume(false);

			return newStoredPartData;
		}

		// TODO : Add a hard dependency on KSPCommunityFixes and replace this method
		public static float GetPartCargoVolume(ProtoPartSnapshot protoPart)
		{
			if (protoPart.partStateValues.TryGetValue("cargoVolume", out KSPParseable parseable))
			{
				return parseable.value_float;
			}
			//else
			//{
			//	foreach (ProtoPartModuleSnapshot protoModule in protoPart.modules)
			//	{
			//		float cargoVolume = 0f;
			//		if (BetterCargoPartVolume.cargoModulesNames.Contains(protoModule.moduleName) && protoModule.moduleValues.TryGetValue(nameof(ModuleCargoPart.packedVolume), ref cargoVolume))
			//		{
			//			return cargoVolume;
			//		}
			//	}
			//}

			return 0f;
		}


	}
}
