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

			// install default parts
			InstallAllStoredParts();
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

		internal void InstallAllStoredParts()
		{
			foreach (StoredPartData storedPartData in storedParts)
			{
				if (storedPartData.activeCargoInfo != null)
				{
					storedPartData.isInstalled = true;
					storedPartData.InstallActivePart(true);
				}
			}
		}

		internal StoredPartData CreateStoredPart(StoredPart stockStoredPart, bool partWasJustStored, bool isInstalled = false, ConfigNode inventoryNode = null)
		{
			ProtoPartSnapshot protoPart = stockStoredPart.snapshot;
			if (protoPart == null)
				return null;

			StoredPartData storedPart = new StoredPartData(this, stockStoredPart, isInstalled, inventoryNode);
			storedPart.volume = InventoryAPI.Utility.GetPartCargoVolume(protoPart);

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
