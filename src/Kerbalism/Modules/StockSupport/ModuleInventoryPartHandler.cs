using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using UnityEngine;

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

		void OnCargoStored();

		void OnCargoUnstored();
	}

	public class UnloadedStoredPartResourceWrapper : PartResourceWrapper
	{
		public ConfigNode resourceNode;

		public override string ResName => resName;
		private string resName;
		public override double Amount { get; set; }
		public override double Capacity { get; set; }
		public override double Level => Capacity > 0.0 ? Amount / Capacity : 0.0;
		public override bool FlowState { get; set; }
		public override bool IsTweakable { get; set; }
		public override bool IsVisible { get; set; }

		public static bool TryParse(PartData part, ConfigNode resourceNode, out UnloadedStoredPartResourceWrapper wrapper)
		{
			wrapper = new UnloadedStoredPartResourceWrapper();

			wrapper.resourceNode = resourceNode;

			foreach (ConfigNode.Value value in resourceNode.values)
			{
				switch (value.name)
				{
					case "name":
						wrapper.resName = value.value;
						break;
					case nameof(PartResource.amount):
						if (!double.TryParse(value.value, out double amount))
							return false;
						wrapper.Amount = amount;
						break;
					case nameof(PartResource.maxAmount):
						if (!double.TryParse(value.value, out double maxAmount))
							return false;
						wrapper.Capacity = maxAmount;
						break;
					case nameof(PartResource.flowState):
						if (!bool.TryParse(value.value, out bool flowState))
							return false;
						wrapper.FlowState = flowState;
						break;
					case nameof(PartResource.isTweakable):
						if (!bool.TryParse(value.value, out bool isTweakable))
							return false;
						wrapper.IsTweakable = isTweakable;
						break;
					case nameof(PartResource.isVisible):
						if (!bool.TryParse(value.value, out bool isVisible))
							return false;
						wrapper.IsVisible = isVisible;
						break;
				}
			}

			if (!VesselResHandler.allKSPResourceIdsByName.TryGetValue(wrapper.resName, out wrapper.resId))
				return false;

			wrapper.AddToResHandler(part);
			return true;
		}

		public void Save()
		{
			resourceNode.SetValue(nameof(PartResource.amount), Amount);
			resourceNode.SetValue(nameof(PartResource.maxAmount), Capacity);
			resourceNode.SetValue(nameof(PartResource.flowState), FlowState);
			resourceNode.SetValue(nameof(PartResource.isTweakable), IsTweakable);
			resourceNode.SetValue(nameof(PartResource.isVisible), IsVisible);
		}


	}

	public class StoredPartData
	{
		public static StoredPartData heldPartData;
		public static Part heldPart;

		public Part partPrefab;
		public int slotIndex = -1;
		public List<ModuleHandler> activeHandlers;
		public List<ConfigNode> persistentHandlersData;
		public List<PartResourceWrapper> resources;

		public Dictionary<int, ConfigNode> activeHandlersNodes;

		public bool IsStored => slotIndex > -1;

		public StoredPartData(Part partPrefab, int slotIndex)
		{
			this.partPrefab = partPrefab;
			this.slotIndex = slotIndex;
		}

		// called when an existing part is either :
		// - about to be stored in an inventory
		// - detached in EVA construction mode
		public StoredPartData(PartData part)
		{
			partPrefab = part.PartPrefab;

			// Transfer modules :
			// - if active : transfer and clear part/module references. References will be set in OnStored().
			// - if persistent : save
			foreach (ModuleHandler moduleHandler in part.modules)
			{
				if (moduleHandler is IActiveStoredHandler)
				{
					moduleHandler.partData = null;
					moduleHandler.ClearLoadedAndProtoModuleReferences();

					if (activeHandlers == null)
						activeHandlers = new List<ModuleHandler>();

					activeHandlers.Add(moduleHandler);
				}
				else if (moduleHandler is IPersistentModuleHandler persistentHandler)
				{
					ConfigNode persistentHandlerNode = SavePersistentHandler(persistentHandler, moduleHandler.handlerIsEnabled);
					if (persistentHandlerNode != null)
					{
						if (persistentHandlersData == null)
							persistentHandlersData = new List<ConfigNode>();

						persistentHandlersData.Add(persistentHandlerNode);
					}
				}
			}

			foreach (PartResourceWrapper partResourceWrapper in part.resources)
			{
				partResourceWrapper.RemoveFromResHandler(part);
				resources.Add(partResourceWrapper);
			}
		}

		public void Save(ConfigNode node, bool isLoaded)
		{
			node.AddValue("partName", partPrefab.partInfo.name);
			node.AddValue(nameof(slotIndex), slotIndex);

			if (activeHandlers != null)
			{
				ConfigNode activeHandlerNodes = node.AddNode("ACTIVE_HANDLERS");
				foreach (ModuleHandler activeHandler in activeHandlers)
				{
					if (activeHandler is IPersistentModuleHandler persistentHandler)
					{
						ConfigNode persistentHandlerNode = SavePersistentHandler(persistentHandler, activeHandler.handlerIsEnabled);
						if (persistentHandlerNode != null)
						{
							activeHandlerNodes.AddNode(persistentHandlerNode);
						}
					}
				}
			}

			if (persistentHandlersData != null)
			{
				ConfigNode persistentHandlerNodes = node.AddNode("PERSISTENT_HANDLERS");
				foreach (ConfigNode persistentHandlerNode in persistentHandlersData)
				{
					persistentHandlerNodes.AddNode(persistentHandlerNode);
				}
			}

			if (!isLoaded && resources != null)
			{
				foreach (PartResourceWrapper wrapper in resources)
				{
					((UnloadedStoredPartResourceWrapper)wrapper).Save(); // saving to the original node
				}
			}
		}

		private ConfigNode SavePersistentHandler(IPersistentModuleHandler persistentHandler, bool handlerIsEnabled)
		{
			string nodeName = persistentHandler.GetType().Name;
			ConfigNode handlerNode = new ConfigNode(nodeName);

			if (persistentHandler.FlightId != 0)
			{
				handlerNode.AddValue(ModuleHandler.VALUENAME_FLIGHTID, persistentHandler.FlightId);
			}
			else if (persistentHandler.ShipId != 0)
			{
				handlerNode.AddValue(ModuleHandler.VALUENAME_SHIPID, persistentHandler.ShipId);
			}
			else
			{
				Lib.Log($"Can't save ModuleHandler {nodeName}, both flightId and shipId aren't defined !", Lib.LogLevel.Warning);
				return null;
			}

			handlerNode.AddValue(nameof(ModuleHandler.handlerIsEnabled), handlerIsEnabled);
			persistentHandler.Save(handlerNode);
			return handlerNode;
		}

		public static StoredPartData Load(ConfigNode node)
		{
			string partName = Lib.ConfigValue(node, "partName", string.Empty);
			Part prefab = PartLoader.getPartInfoByName(partName)?.partPrefab;
			if (prefab == null)
				return null;

			int slotIndex = Lib.ConfigValue(node, nameof(slotIndex), -1);
			if (slotIndex == -1)
				return null;

			StoredPartData storedPart = new StoredPartData(prefab, slotIndex);

			ConfigNode activeHandlerNodes = node.GetNode("ACTIVE_HANDLERS");
			if (activeHandlerNodes != null)
			{
				storedPart.activeHandlersNodes = new Dictionary<int, ConfigNode>(activeHandlerNodes.CountNodes);

				foreach (ConfigNode activeHandlerNode in activeHandlerNodes.nodes)
				{
					int id = Lib.ConfigValue(activeHandlerNode, ModuleHandler.VALUENAME_FLIGHTID, 0);
					if (id == 0)
						id = Lib.ConfigValue(activeHandlerNode, ModuleHandler.VALUENAME_SHIPID, 0);
					if (id == 0)
						continue;

					storedPart.activeHandlersNodes.Add(id, activeHandlerNode);
				}
			}

			ConfigNode persistentHandlerNodes = node.GetNode("PERSISTENT_HANDLERS");
			if (persistentHandlerNodes != null)
			{
				storedPart.persistentHandlersData = new List<ConfigNode>(persistentHandlerNodes.CountNodes);
				foreach (ConfigNode persistentHandlerNode in persistentHandlerNodes.nodes)
				{
					storedPart.persistentHandlersData.Add(persistentHandlerNode);
				}
			}

			return storedPart;
		}

		// called when a cargo part is actually stored :
		// - In the editor, when it is dropped on an inventory slot
		// - In the editor/in flight, when a "held" cargo part is dropped on an inventory slot
		// - In EVA construction mode, when a "held" part is dropped on an inventory slot
		public void OnStored(ModuleInventoryPartHandler inventoryHandler, StoredPart storedPart)
		{
			slotIndex = storedPart.slotIndex;

			if (activeHandlers != null)
			{
				foreach (ModuleHandler activeHandler in activeHandlers)
				{
					activeHandler.partData = inventoryHandler.partData;
					// call IActiveCargo.OnBecomeCargo()
				}
			}

			if (resources != null)
			{
				for (int i = resources.Count - 1; i >= 0; i--)
				{
					ProtoPartResourceSnapshot protoResource = storedPart.snapshot.resources.Find(p => p.definition.id == resources[i].resId);
					if (protoResource == null)
					{
						resources.RemoveAt(i);
						continue;
					}

					resources[i].Mutate(protoResource);
					resources[i].AddToResHandler(inventoryHandler.partData);
				}
			}
		}

		// called when a part is pulled out of an inventory
		public void OnUnstored(Part newPart)
		{
			slotIndex = -1;
		}

		// called when a held part becomes activated in the editor
		public void OnEditorDropped(Part part)
		{
			PartData partData = new PartData(VesselDataShip.Instance, part);
			VesselDataShip.ShipParts.Add(partData);

			Dictionary<int, ConfigNode> persistentHandlersById;
			int activeHandlersCount = activeHandlers == null ? 0 : activeHandlers.Count;
			int persistentHandlersCount = persistentHandlersData == null ? 0 : persistentHandlersData.Count;
			if (activeHandlersCount + persistentHandlersCount != 0)
				persistentHandlersById = new Dictionary<int, ConfigNode>(activeHandlersCount + persistentHandlersCount);
			else
				persistentHandlersById = null;

			if (activeHandlersCount != 0)
			{
				foreach (ModuleHandler activeHandler in activeHandlers)
				{
					if (activeHandler is IPersistentModuleHandler persistentHandler && persistentHandler.ShipId != 0)
					{
						ConfigNode handlerNode = new ConfigNode();
						persistentHandler.Save(handlerNode);
						persistentHandlersById.Add(persistentHandler.ShipId, handlerNode);
					}
				}
			}

			if (persistentHandlersCount != 0)
			{
				foreach (ConfigNode handlerNode in persistentHandlersData)
				{
					int shipId = Lib.ConfigValue(handlerNode, ModuleHandler.VALUENAME_SHIPID, 0);
					if (shipId != 0)
					{
						persistentHandlersById.Add(shipId, handlerNode);
					}
				}
			}

			for (int i = 0; i < part.Modules.Count; i++)
			{
				PartModule module = part.Modules[i];
				if (ModuleHandler.handlerShipIdsByModuleInstanceId.TryGetValue(module.GetInstanceID(), out int shipId))
				{
					if (persistentHandlersById != null && persistentHandlersById.TryGetValue(shipId, out ConfigNode persistentHandlerNode))
					{
						ModuleHandler.NewLoadedFromNode(module, i, partData, persistentHandlerNode, ModuleHandler.ActivationContext.Editor);
						continue;
					}
				}

				ModuleHandler.NewEditorLoaded(module, i, partData, ModuleHandler.ActivationContext.Editor, false);
			}

			foreach (ModuleHandler handler in partData.modules)
			{
				handler.FirstSetup();
			}

			partData.Start();
		}

		// called when a held part is attached to an existing vessel
		public void OnEVAAttached(Part newPart)
		{
			if (!newPart.vessel.TryGetVesselData(out VesselData vd))
				return;

			PartData partData = new PartData(vd, newPart);
			vd.Parts.Add(partData);

			Dictionary<int, ConfigNode> persistentHandlersById;
			int activeHandlersCount = activeHandlers == null ? 0 : activeHandlers.Count;
			int persistentHandlersCount = persistentHandlersData == null ? 0 : persistentHandlersData.Count;
			if (activeHandlersCount + persistentHandlersCount != 0)
				persistentHandlersById = new Dictionary<int, ConfigNode>(activeHandlersCount + persistentHandlersCount);
			else
				persistentHandlersById = null;

			if (activeHandlersCount != 0)
			{
				foreach (ModuleHandler activeHandler in activeHandlers)
				{
					if (activeHandler is IPersistentModuleHandler persistentHandler && persistentHandler.FlightId != 0)
					{
						ConfigNode handlerNode = new ConfigNode();
						persistentHandler.Save(handlerNode);
						persistentHandlersById.Add(persistentHandler.FlightId, handlerNode);
					}
				}
			}

			if (persistentHandlersCount != 0)
			{
				foreach (ConfigNode handlerNode in persistentHandlersData)
				{
					int flightId = Lib.ConfigValue(handlerNode, ModuleHandler.VALUENAME_FLIGHTID, 0);
					if (flightId != 0)
					{
						persistentHandlersById.Add(flightId, handlerNode);
					}
				}
			}

			for (int i = 0; i < newPart.Modules.Count; i++)
			{
				PartModule module = newPart.Modules[i];
				if (ModuleHandler.handlerFlightIdsByModuleInstanceId.TryGetValue(module.GetInstanceID(), out int flightId))
				{
					if (persistentHandlersById != null && persistentHandlersById.TryGetValue(flightId, out ConfigNode persistentHandlerNode))
					{
						ModuleHandler.NewLoadedFromNode(module, i, partData, persistentHandlerNode, ModuleHandler.ActivationContext.Loaded);
						continue;
					}
				}

				// if the module is type we haven't a handler for, continue
				if (!ModuleHandler.TryGetModuleHandlerType(newPart.Modules[i].moduleName, out ModuleHandler.ModuleHandlerType handlerType))
					continue;

				// only instaniate handlers that have the loaded context
				if ((handlerType.activation & ModuleHandler.ActivationContext.Loaded) == 0)
					continue;

				ModuleHandler.NewLoaded(handlerType, module, i, partData, true);
			}

			foreach (ModuleHandler handler in partData.modules)
			{
				handler.FirstSetup();
			}

			partData.Start();
		}

		// called when a held part is dropped, creating a new vessel
		public void OnEVADropped()
		{

		}
	}

	public class ModuleInventoryPartHandler : TypedModuleHandler<ModuleInventoryPart>, IPersistentModuleHandler
	{
		public override ActivationContext Activation => ActivationContext.Editor | ActivationContext.Loaded | ActivationContext.Unloaded;

		public List<StoredPartData> storedParts;

		public ModuleHandler ModuleHandler => this;
		public int FlightId { get; set; }
		public int ShipId { get; set; }

		private struct ProtoModuleInfo
		{
			public string moduleName;
			public int moduleIndex;
			public ConfigNode moduleValues;
		}

		public void Load(ConfigNode node)
		{
			bool isShip = VesselData is VesselDataShip;

			if (node.CountNodes != 0)
			{
				storedParts = new List<StoredPartData>(node.CountNodes);

				foreach (ConfigNode storedPartDataNode in node.nodes)
				{
					if (storedPartDataNode.name != "STORED_PART")
						continue;

					StoredPartData storedPart = StoredPartData.Load(storedPartDataNode);
					if (storedPart != null)
						storedParts.Add(storedPart);
				}
			}

			if (IsLoaded)
			{
				if (loadedModule.storedParts.Count == 0)
					return;

				for (int i = 0; i < loadedModule.storedParts.Count; i++)
				{
					StoredPart stockStoredPart = loadedModule.storedParts.At(i);
					if (stockStoredPart.quantity != 1)
						continue;

					ProtoPartSnapshot protoPart = stockStoredPart.snapshot;
					if (protoPart == null)
						continue;

					StoredPartData storedPart = storedParts?.Find(p => p.slotIndex == stockStoredPart.slotIndex);

					if (protoPart.modules.Count != 0)
					{
						bool prefabModulesInSync = ArePrefabModulesInSync(protoPart.partPrefab, protoPart.modules);

						for (int j = 0; j < protoPart.modules.Count; j++)
						{
							ProtoPartModuleSnapshot protoModule = protoPart.modules[j];
							if (!handlerTypesByModuleName.TryGetValue(protoModule.moduleName, out ModuleHandlerType handlerType))
								continue;

							if (!handlerType.isActiveCargo)
								continue;

							PartModule modulePrefab;
							if (prefabModulesInSync)
							{
								modulePrefab = protoPart.partPrefab.Modules[j];
							}
							else if (!TryFindModulePrefab(protoPart.partPrefab, protoPart.modules, protoModule, out modulePrefab))
							{
								continue;
							}

							ModuleHandler moduleHandler = InstantiateActiveModule(storedPart, modulePrefab, handlerType, protoModule.moduleValues, isShip);
							if (moduleHandler == null)
								continue;

							if (storedPart == null)
							{
								storedPart = new StoredPartData(protoPart.partPrefab, stockStoredPart.slotIndex);

								if (storedParts == null)
									storedParts = new List<StoredPartData>();

								storedParts.Add(storedPart);
							}

							if (storedPart.activeHandlers == null)
								storedPart.activeHandlers = new List<ModuleHandler>();

							storedPart.activeHandlers.Add(moduleHandler);
						}
					}

					if (protoPart.resources.Count != 0)
					{
						if (storedPart == null)
						{
							storedPart = new StoredPartData(protoPart.partPrefab, stockStoredPart.slotIndex);
							if (storedParts == null)
								storedParts = new List<StoredPartData>();

							storedParts.Add(storedPart);
						}
						
						if (storedPart.resources == null)
							storedPart.resources = new List<PartResourceWrapper>(protoPart.resources.Count);

						foreach (ProtoPartResourceSnapshot protoResource in protoPart.resources)
						{
							PartResourceWrapper wrapper = new PartResourceWrapper(partData, protoResource);
							storedPart.resources.Add(wrapper);
						}
					}
				}
			}
			else
			{
				ConfigNode storedPartsNode = protoModule.moduleValues.GetNode("STOREDPARTS");
				if (storedPartsNode == null || storedPartsNode.CountNodes == 0)
					return;

				storedParts = new List<StoredPartData>(storedPartsNode.CountNodes);

				foreach (ConfigNode storedPartNode in storedPartsNode.nodes)
				{
					int quantity = Lib.ConfigValue(storedPartNode, nameof(StoredPart.quantity), 0);
					if (quantity != 1)
						continue;

					ConfigNode partNode = storedPartNode.GetNode("PART");
					if (partNode == null)
						continue;

					string partName = storedPartNode.GetValue(nameof(StoredPart.partName));
					if (string.IsNullOrEmpty(partName))
						continue;

					Part partPrefab = PartLoader.getPartInfoByName(partName)?.partPrefab;
					if (partPrefab == null)
						continue;

					int slotIndex = Lib.ConfigValue(storedPartNode, nameof(StoredPart.slotIndex), -1);
					if (slotIndex == -1)
						continue;

					StoredPartData storedPart = storedParts?.Find(p => p.slotIndex == slotIndex);

					List<ProtoModuleInfo> protoModuleInfos = new List<ProtoModuleInfo>();
					int moduleIndex = 0;
					foreach (ConfigNode component in partNode.nodes)
					{
						if (component.name == "MODULE")
						{
							ProtoModuleInfo moduleInfo = new ProtoModuleInfo();
							moduleInfo.moduleName = storedPartNode.GetValue("name");
							if (string.IsNullOrEmpty(moduleInfo.moduleName))
								continue;

							moduleInfo.moduleIndex = moduleIndex;
							moduleInfo.moduleValues = component;
							protoModuleInfos.Add(moduleInfo);
							moduleIndex++;
						}
						else if (component.name == "RESOURCE")
						{
							if (!UnloadedStoredPartResourceWrapper.TryParse(partData, component, out UnloadedStoredPartResourceWrapper wrapper))
								continue;

							if (storedPart == null)
							{
								storedPart = new StoredPartData(partPrefab, slotIndex);
								if (storedParts == null)
									storedParts = new List<StoredPartData>();

								storedParts.Add(storedPart);
							}

							if (storedPart.resources == null)
								storedPart.resources = new List<PartResourceWrapper>();

							storedPart.resources.Add(wrapper);
						}
					}

					if (protoModuleInfos.Count != 0)
					{
						bool prefabModulesInSync = ArePrefabModulesInSync(partPrefab, protoModuleInfos);

						foreach (ProtoModuleInfo moduleInfo in protoModuleInfos)
						{
							if (!handlerTypesByModuleName.TryGetValue(moduleInfo.moduleName, out ModuleHandlerType handlerType))
								continue;

							if (!handlerType.isActiveCargo)
								continue;

							PartModule modulePrefab;
							if (prefabModulesInSync)
							{
								modulePrefab = partPrefab.Modules[moduleInfo.moduleIndex];
							}
							else if (!TryFindModulePrefab(partPrefab, protoModuleInfos, moduleInfo, out modulePrefab))
							{
								continue;
							}

							ModuleHandler moduleHandler = InstantiateActiveModule(storedPart, modulePrefab, handlerType, moduleInfo.moduleValues, isShip);
							if (moduleHandler == null)
								continue;

							if (storedPart == null)
							{
								storedPart = new StoredPartData(partPrefab, slotIndex);

								if (storedParts == null)
									storedParts = new List<StoredPartData>();

								storedParts.Add(storedPart);
							}

							if (storedPart.activeHandlers == null)
								storedPart.activeHandlers = new List<ModuleHandler>();

							storedPart.activeHandlers.Add(moduleHandler);
						}
					}
				}
			}
		}

		private ModuleHandler InstantiateActiveModule(StoredPartData storedPart, PartModule modulePrefab, ModuleHandlerType handlerType, ConfigNode protoModuleValues, bool isShip)
		{
			ModuleHandler moduleHandler = handlerType.activator.Invoke();

			if (handlerType.isPersistent)
			{
				IPersistentModuleHandler persistentHandler = (IPersistentModuleHandler)moduleHandler;

				int protoModuleId;
				if (isShip)
				{
					protoModuleId = Lib.ConfigValue(protoModuleValues, VALUENAME_SHIPID, 0);
					persistentHandler.ShipId = protoModuleId;
				}
				else
				{
					protoModuleId = Lib.ConfigValue(protoModuleValues, VALUENAME_FLIGHTID, 0);
					if (protoModuleId == 0)
					{
						protoModuleId = NewFlightId(persistentHandler);
						protoModuleValues.SetValue(VALUENAME_FLIGHTID, protoModuleId, true);
					}
					else
					{
						persistentFlightModuleHandlers.Add(protoModuleId, persistentHandler);
					}

					persistentHandler.FlightId = protoModuleId;
				}

				moduleHandler.partData = partData;
				moduleHandler.SetModuleReferences(modulePrefab, null);

				if (storedPart != null && storedPart.activeHandlersNodes != null)
				{
					if (storedPart.activeHandlersNodes.TryGetValue(protoModuleId, out ConfigNode handlerNode))
					{
						moduleHandler.handlerIsEnabled = Lib.ConfigValue(handlerNode, nameof(handlerIsEnabled), true);
						persistentHandler.Load(handlerNode);
					}
				}

				if (handlerType.isKsmModule)
					((KsmModuleHandler)moduleHandler).LoadDefinition((KsmPartModule)modulePrefab);

				if (!((IActiveStoredHandler)moduleHandler).IsActiveCargo)
					return null;
			}
			else
			{
				moduleHandler.partData = partData;
				moduleHandler.SetModuleReferences(modulePrefab, null);
				moduleHandler.handlerIsEnabled = Lib.ConfigValue(protoModuleValues, nameof(PartModule.isEnabled), true);

				if (!((IActiveStoredHandler)moduleHandler).IsActiveCargo)
					return null;
			}

			return moduleHandler;
		}

		public static bool ArePrefabModulesInSync(Part partPrefab, List<ProtoPartModuleSnapshot> protoPartModules)
		{
			if (partPrefab.Modules.Count != protoPartModules.Count)
				return false;

			for (int i = 0; i < protoPartModules.Count; i++)
			{
				if (partPrefab.Modules[i].moduleName != protoPartModules[i].moduleName)
				{
					return false;
				}
			}

			return true;
		}

		public static bool TryFindModulePrefab(Part partPrefab, List<ProtoPartModuleSnapshot> protoPartModules, ProtoPartModuleSnapshot module, out PartModule modulePrefab)
		{
			modulePrefab = null;
			int protoIndexInType = 0;
			foreach (ProtoPartModuleSnapshot otherppms in protoPartModules)
			{
				if (otherppms.moduleName == module.moduleName)
				{
					if (otherppms == module)
						break;

					protoIndexInType++;
				}
			}

			int prefabIndexInType = 0;
			foreach (PartModule pm in partPrefab.Modules)
			{
				if (pm.moduleName == module.moduleName)
				{
					if (prefabIndexInType == protoIndexInType)
					{
						modulePrefab = pm;
						break;
					}
					prefabIndexInType++;
				}
			}

			if (modulePrefab == null)
			{
				Lib.Log($"PartModule prefab not found for {module.moduleName} on {partPrefab.partName}, has the part configuration changed ?", Lib.LogLevel.Warning);
				return false;
			}

			return true;
		}

		private static bool ArePrefabModulesInSync(Part partPrefab, List<ProtoModuleInfo> protoPartModules)
		{
			if (partPrefab.Modules.Count != protoPartModules.Count)
				return false;

			foreach (ProtoModuleInfo moduleInfo in protoPartModules)
			{
				if (partPrefab.Modules[moduleInfo.moduleIndex].moduleName != moduleInfo.moduleName)
				{
					return false;
				}
			}

			return true;
		}

		private static bool TryFindModulePrefab(Part partPrefab, List<ProtoModuleInfo> protoPartModules, ProtoModuleInfo module, out PartModule modulePrefab)
		{
			modulePrefab = null;
			int protoIndexInType = 0;
			foreach (ProtoModuleInfo otherppms in protoPartModules)
			{
				if (otherppms.moduleName == module.moduleName)
				{
					if (otherppms.moduleIndex == module.moduleIndex)
						break;

					protoIndexInType++;
				}
			}

			int prefabIndexInType = 0;
			foreach (PartModule pm in partPrefab.Modules)
			{
				if (pm.moduleName == module.moduleName)
				{
					if (prefabIndexInType == protoIndexInType)
					{
						modulePrefab = pm;
						break;
					}
					prefabIndexInType++;
				}
			}

			if (modulePrefab == null)
			{
				Lib.Log($"PartModule prefab not found for {module.moduleName} on {partPrefab.partName}, has the part configuration changed ?", Lib.LogLevel.Warning);
				return false;
			}

			return true;
		}

		public void Save(ConfigNode node)
		{
			if (storedParts != null)
			{
				bool isLoaded = IsLoaded;
				foreach (StoredPartData storedPartData in storedParts)
				{
					ConfigNode storedPartNode = node.AddNode("STORED_PART");
					storedPartData.Save(storedPartNode, isLoaded);
				}
			}
		}

		public override void OnStart()
		{
			if (storedParts != null)
			{
				foreach (StoredPartData storedPartData in storedParts)
				{
					if (storedPartData.activeHandlers != null)
					{
						foreach (ModuleHandler moduleHandler in storedPartData.activeHandlers)
						{
							moduleHandler.Start();
						}
					}
				}
			}
		}



		public override void OnPartWasTransferred(VesselDataBase previousVessel)
		{
			// move resources to the new reshandler
		}

		public override void OnBecomingLoaded()
		{
			// mutate resources from UnloadedStoredPartResourceWrapper instances to a regular proto wrapper
		}

		public override void OnBecomingUnloaded()
		{
			// mutate resources from the proto wrappers to UnloadedStoredPartResourceWrapper instances
		}

		public static void OnPartStored(ModuleInventoryPartHandler inventoryHandler, Part storedPartInstance, StoredPart stockStoredPart)
		{
			StoredPartData storedPart;
			if (storedPartInstance == StoredPartData.heldPart)
			{
				Lib.LogDebug($"Held part stored : {storedPartInstance.partInfo.name}");
				storedPart = StoredPartData.heldPartData;
				StoredPartData.heldPartData = null;
				StoredPartData.heldPart = null;
			}
			else
			{
				if (!PartData.TryGetLoadedPartData(storedPartInstance, out PartData originalPartData))
				{
					Lib.Log($"Can't find PartData instance for stored part {storedPartInstance.partInfo.name}", Lib.LogLevel.Error);
					return;
				}

				Lib.LogDebug($"Loaded part stored : {storedPartInstance.partInfo.name}");
				storedPart = new StoredPartData(originalPartData);
			}

			storedPart.OnStored(inventoryHandler, stockStoredPart);

			if (inventoryHandler.storedParts == null)
				inventoryHandler.storedParts = new List<StoredPartData>();

			inventoryHandler.storedParts.Add(storedPart);
		}

		public static void OnPartPulledFromInventory(ModuleInventoryPartHandler inventoryHandler, Part storedPartInstance, StoredPart stockStoredPart, int slotIndex)
		{
			int storedPartIndex = inventoryHandler.storedParts.FindIndex(p => p.slotIndex == slotIndex);
			if (storedPartIndex < 0)
				return;

			Lib.Log($"Part pulled from inventory : {storedPartInstance.partInfo.name}");

			StoredPartData.heldPart = storedPartInstance;
			StoredPartData.heldPartData = inventoryHandler.storedParts[storedPartIndex];
			inventoryHandler.storedParts.RemoveAt(storedPartIndex);
			StoredPartData.heldPartData.OnUnstored(storedPartInstance);
		}

		public static void OnPulledPartCreated()
		{
			if (StoredPartData.heldPart == null)
				return;

			Lib.Log($"Pulled part created : {StoredPartData.heldPart.partInfo.name}");

			StoredPartData.heldPartData.OnEditorDropped(StoredPartData.heldPart);
			StoredPartData.heldPartData = null;
			StoredPartData.heldPart = null;


		}
	}

	public static class HeldPartData
	{
		public static int partId;
		public static Part heldPart;
		public static List<int> moduleIds;
		public static List<ConfigNode> moduleData;

		public static void Clear()
		{
			heldPart = null;
			moduleIds.Clear();
			moduleData.Clear();
		}

		public static void SetupFromStoredPart(StoredPartData storedPart)
		{
			bool isEditor = Lib.IsEditor;
			foreach (ModuleHandler activeModule in storedPart.activeHandlers)
			{
				if (activeModule is IPersistentModuleHandler persistentModule)
				{
					ConfigNode moduleNode = new ConfigNode();
					persistentModule.Save(moduleNode);
					moduleData.Add(moduleNode);
					moduleIds.Add(isEditor ? persistentModule.ShipId : persistentModule.FlightId);
				}
			}
		}
	}

	// note : the `public bool StoreCargoPartAtSlot(Part sPart, int slotIndex)` method
	// is calling the `public bool StoreCargoPartAtSlot(ProtoPartSnapshot pPart, int slotIndex)` overload.
	// Since we need a reference to the part instance, we are patching the first overload.
	// The second overload is public, but it isn't used directly, so we shoudl be safe, as long as no mod
	// is trying to use that overload directly.
	[HarmonyPatch(typeof(ModuleInventoryPart))]
	[HarmonyPatch(nameof(ModuleInventoryPart.StoreCargoPartAtSlot), typeof(Part), typeof(int))]
	public class ModuleInventoryPart_StoreCargoPartAtSlot
	{
		static void Postfix(ModuleInventoryPart __instance, Part sPart, int slotIndex, ref bool __result)
		{
			if (!__result)
				return;

			if (!__instance.storedParts.TryGetValue(slotIndex, out StoredPart storedPart))
				return;

			if (storedPart.quantity != 1)
				return;

			if (storedPart.snapshot == null)
				return;

			if (!ModuleHandler.loadedHandlersByModuleInstanceId.TryGetValue(__instance.GetInstanceID(), out ModuleHandler moduleHandler))
			{
				Lib.Log($"Can't find the ModuleInventoryPartHandler instance for {__instance.part.partInfo.name}", Lib.LogLevel.Error);
				return;
			}

			ModuleInventoryPartHandler.OnPartStored((ModuleInventoryPartHandler)moduleHandler, sPart, storedPart);
		}
	}

	// an inventory part has been grabbed
	[HarmonyPatch(typeof(UIPartActionInventorySlot))]
	[HarmonyPatch("CreatePartFromThisSlot")]
	public class UIPartActionInventorySlot_CreatePartFromThisSlot
	{
		static void Postfix(int slotIndex, ModuleInventoryPart ___moduleInventoryPart, ref Part __result)
		{
			if (__result == null)
				return;

			if (!___moduleInventoryPart.storedParts.TryGetValue(slotIndex, out StoredPart storedPart))
				return;

			if (storedPart.quantity != 1)
				return;

			if (storedPart.snapshot == null)
				return;

			if (!ModuleHandler.loadedHandlersByModuleInstanceId.TryGetValue(___moduleInventoryPart.GetInstanceID(), out ModuleHandler moduleHandler))
			{
				Lib.Log($"Can't find the ModuleInventoryPartHandler instance for {___moduleInventoryPart.part.partInfo.name}", Lib.LogLevel.Error);
				return;
			}

			ModuleInventoryPartHandler.OnPartPulledFromInventory((ModuleInventoryPartHandler)moduleHandler, __result, storedPart, slotIndex);

		}
	}

	[HarmonyPatch(typeof(UIPartActionControllerInventory))]
	[HarmonyPatch(nameof(UIPartActionControllerInventory.SetIconAsPart))]
	public class UIPartActionControllerInventory_SetIconAsPart
	{
		static void Prefix()
		{
			if (StoredPartData.heldPart != null && UIPartActionControllerInventory.Instance.CurrentCargoPart == StoredPartData.heldPart)
			{
				if (EVAConstructionModeController.Instance != null && EVAConstructionModeController.Instance.IsOpen && EVAConstructionModeController.Instance.evaEditor.SelectedPart == StoredPartData.heldPart)
					return;

				Lib.LogDebug($"SetIconAsPart : {StoredPartData.heldPart.partInfo.name}");
				ModuleInventoryPartHandler.OnPulledPartCreated();
			}
		}
	}

	[HarmonyPatch(typeof(Part))]
	[HarmonyPatch(nameof(Part.OnDetachFlight))]
	public class Part_OnDetachFlight
	{
		static void Postfix(Part __instance)
		{
			if (__instance.State != PartStates.CARGO)
				return;

			if (!PartData.TryGetLoadedPartData(__instance, out PartData partData))
				return;

			VesselData vd = (VesselData) partData.vesselData;

			vd.Parts.Remove(partData);
			vd.OnVesselWasModified();

			StoredPartData storedPart = new StoredPartData(partData);
			StoredPartData.heldPart = __instance;
			StoredPartData.heldPartData = storedPart;
		}
	}

	[HarmonyPatch(typeof(Part))]
	[HarmonyPatch(nameof(Part.OnAttachFlight))]
	public class Part_OnAttachFlight
	{
		static void Postfix(Part __instance)
		{
			if (__instance.State != PartStates.IDLE)
				return;

			// the part being attached can be either :
			// - a just created instance, copied from the held part instance
			// - an existing "backup" part
			// forwarding the references would be a mess, so we assume that checking if this
			// is the same part prefab is enough...
			if (StoredPartData.heldPart.partInfo.partPrefab != __instance.partInfo.partPrefab)
				return;

			StoredPartData.heldPartData.OnEVAAttached(__instance);
			StoredPartData.heldPart = null;
			StoredPartData.heldPartData = null;
		}
	}
}
