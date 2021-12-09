using System.Collections.Generic;
using HarmonyLib;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;

namespace KERBALISM
{
	// a part has been stored in the inventory
	[HarmonyPatch(typeof(ModuleInventoryPart))]
	[HarmonyPatch(nameof(ModuleInventoryPart.StoreCargoPartAtSlot), typeof(Part), typeof(int))]
	public class ModuleInventoryPart_StoreCargoPartAtSlot
	{
		static void Postfix(ModuleInventoryPart __instance, Part sPart, int slotIndex, ref bool __result)
		{
			if (!__result)
				return;

			// we don't handle the inventories of kerbals while they are inside a part
			if (__instance.kerbalMode)
				return;

			if (!__instance.storedParts.TryGetValue(slotIndex, out StoredPart storedPart))
				return;

			if (!ModuleHandler.TryGetHandler(__instance, out ModuleInventoryPartHandler handler))
				return;

			handler.CreateStoredPart(storedPart, true);
			handler.UpdateMassAndVolume(false);

		}
	}

	// an inventory part has been grabbed
	[HarmonyPatch(typeof(UIPartActionInventorySlot))]
	[HarmonyPatch("CreatePartFromThisSlot")]
	public class UIPartActionInventorySlot_CreatePartFromThisSlot
	{
		static void Prefix(int slotIndex, ModuleInventoryPart ___moduleInventoryPart)
		{
			// we don't handle the inventories of kerbals while they are inside a part
			if (___moduleInventoryPart.kerbalMode)
				return;

			if (!___moduleInventoryPart.storedParts.TryGetValue(slotIndex, out StoredPart storedPart))
				return;

			if (!ModuleHandler.TryGetHandler(___moduleInventoryPart, out ModuleInventoryPartHandler handler))
				return;

			handler.OnPartUnstored(storedPart);
		}
	}

	// For kerbals on EVA, the stock module is manually calling Load() from its OnStart()
	// Since our FirstSetup()/Start() methods are called before the stock module OnStart(),
	// we can't do anything from here in that case. We use a ModuleInventoryPart.OnStart() postfix
	// to handle that case.
	[HarmonyPatch(typeof(ModuleInventoryPart))]
	[HarmonyPatch(nameof(ModuleInventoryPart.OnStart))]
	public class ModuleInventoryPart_OnStart
	{
		static void Postfix(ModuleInventoryPart __instance)
		{
			if (!__instance.IsKerbalOnEVA)
				return;

			if (!ModuleHandler.TryGetHandler(__instance, out ModuleInventoryPartHandler handler))
				return;

			// force start call
			handler.started = false;
			handler.Start();
			// create stored parts
			handler.ParseStockModule();
			// install default parts
			handler.InstallAllStoredParts();
		}
	}

	[HarmonyPatch(typeof(UIPartActionInventorySlot))]
	[HarmonyPatch("SlotClicked")]
	public class UIPartActionInventorySlot_SlotClicked
	{
		static bool Prefix(UIPartActionInventorySlot __instance, ModuleInventoryPart ___moduleInventoryPart)
		{
			// the only matter in flight, avoid useless processing early
			if (Lib.IsEditor)
				return true;

			if (!ModuleHandler.TryGetHandler(___moduleInventoryPart, out ModuleInventoryPartHandler handler))
				return true;

			if (!handler.storedPartsBySlotIndex.TryGetValue(__instance.slotIndex, out StoredPartData storedPart))
				return true;

			if (storedPart.isInstalled && storedPart.activeCargoInfo != null)
			{
				if (!storedPart.activeCargoInfo.canInstallInFlight)
				{
					Message.Post($"Can't uninstall {storedPart.protoPart.partInfo.title}", "This part can't be uninstalled in flight");
					return false;
				}
				
				if (!storedPart.activeCargoInfo.flightInstallCrewSpecs.Check(___moduleInventoryPart.vessel.GetVesselCrew()))
				{
					string specError = storedPart.activeCargoInfo.flightInstallCrewSpecs.Warning();
					Message.Post($"Can't uninstall {storedPart.protoPart.partInfo.title}", specError);
					return false;
				}
			}

			return true;
		}
	}

	[HarmonyPatch(typeof(UIPartActionInventorySlot))]
	[HarmonyPatch(nameof(UIPartActionInventorySlot.Setup))]
	public class UIPartActionInventorySlot_Setup
	{
		static void Postfix(UIPartActionInventorySlot __instance, ModuleInventoryPart ___moduleInventoryPart)
		{
			if (__instance.transform.Find("ButtonKsmInstall"))
				return;

			if (!ModuleHandler.TryGetHandler(___moduleInventoryPart, out ModuleInventoryPartHandler handler))
				return;

			// ignore kerbals
			if (!handler.handlerIsEnabled)
				return;

			GameObject placePartButton = __instance.transform.Find("ButtonPlacePart")?.gameObject;
			GameObject installButton = Object.Instantiate(placePartButton);
			installButton.name = "ButtonKsmInstall";
			installButton.SetActive(true);
			installButton.transform.SetParent(__instance.transform);
			RectTransform rectTransform = (RectTransform) installButton.transform;
			rectTransform.anchorMin = new Vector2(0f, 1f);
			rectTransform.anchorMax = new Vector2(0f, 1f);
			rectTransform.anchoredPosition = new Vector2(18f, -18f);
			KsmCargoInstallButtonHandler buttonHandler = installButton.AddComponent<KsmCargoInstallButtonHandler>();
			buttonHandler.inventoryHandler = handler;
			buttonHandler.slotIndex = __instance.slotIndex;
		}
	}

	public class KsmCargoInstallButtonHandler : MonoBehaviour
	{
		public ModuleInventoryPartHandler inventoryHandler;
		public int slotIndex;
		public Image image;
		public Button button;

		private void Start()
		{
			image = GetComponent<Image>();
			button = GetComponent<Button>();
			button.onClick.AddListener(OnClick);

			if (inventoryHandler.installButtonHandlers == null)
				inventoryHandler.installButtonHandlers = new List<KsmCargoInstallButtonHandler>();

			inventoryHandler.installButtonHandlers.Add(this);
			UpdateVisibilityAndColor();
		}

		private void OnClick()
		{
			if (!inventoryHandler.storedPartsBySlotIndex .TryGetValue(slotIndex, out StoredPartData storedPart))
				return;

			if (Lib.IsFlight && !storedPart.activeCargoInfo.flightInstallCrewSpecs.Check(inventoryHandler.loadedModule.vessel.GetVesselCrew()))
			{
				string specError = storedPart.activeCargoInfo.flightInstallCrewSpecs.Warning();
				Message.Post($"Can't {(storedPart.isInstalled ? "uninstall" : "install")} {storedPart.protoPart.partInfo.title}", specError);
				return;
			}

			if (storedPart.isInstalled)
			{
				storedPart.UninstallActivePart();
				storedPart.isInstalled = false;
			}
			else
			{
				storedPart.InstallActivePart(true);
				storedPart.isInstalled = true;
			}

			UpdateIcon(storedPart.isInstalled);
		}

		private void OnDestroy()
		{
			inventoryHandler.installButtonHandlers?.Remove(this);
		}

		public void UpdateVisibilityAndColor()
		{
			if (!inventoryHandler.storedPartsBySlotIndex.TryGetValue(slotIndex, out StoredPartData storedPart))
			{
				gameObject.SetActive(false);
				return;
			}

			if (storedPart.activeCargoInfo == null)
			{
				gameObject.SetActive(false);
				return;
			}

			if (Lib.IsFlight && !storedPart.activeCargoInfo.canInstallInFlight)
			{
				gameObject.SetActive(false);
				return;
			}

			gameObject.SetActive(true);
			UpdateIcon(storedPart.isInstalled);
		}

		private void UpdateIcon(bool isInstalled)
		{
			if (isInstalled)
			{
				image.sprite = Textures.cargoInstalled32;
				image.color = Kolor.Green;
			}
			else
			{
				image.sprite = Textures.cargoInstall32;
				image.color = Kolor.Yellow;
			}
		}

	}
}
