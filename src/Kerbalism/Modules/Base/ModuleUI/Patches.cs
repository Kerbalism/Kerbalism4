using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM.ModuleUI
{
	[HarmonyPatch(typeof(UIPartActionController))]
	[HarmonyPatch("CreatePartUI")]
	class UIPartActionController_CreatePartUI
	{
		static void Prefix(Part part)
		{
			if (part == null)
				return;

			if (!PartData.TryGetLoadedPartData(part, out PartData partData))
				return;

			foreach (ModuleHandler module in partData.modules)
			{
				foreach (ModuleUIBase moduleUiElement in module.UIElements)
				{
					moduleUiElement.CreatePAWItem(part);
				}
			}
		}
	}

	[HarmonyPatch(typeof(UIPartActionController))]
	[HarmonyPatch("Awake")]
	class UIPartActionController_Awake
	{
		private static bool prefabsCreated = false;
		private static List<UIPartActionFieldItem> customPrefabs = new List<UIPartActionFieldItem>();

		static void Postfix(UIPartActionController __instance)
		{
			if (!prefabsCreated)
			{
				prefabsCreated = true;

				foreach (UIPartActionFieldItem item in __instance.fieldPrefabs)
				{
					if (item is UIPartActionLabel uiLabel)
						customPrefabs.Add(UIPartActionKsmLabel.CreatePrefab(uiLabel));
					else if (item is UIPartActionToggle uiToggle)
						customPrefabs.Add(UIPartActionKsmToggle.CreatePrefab(uiToggle));
				}

				customPrefabs.Add(UIPartActionKsmButton.CreatePrefab(__instance.eventItemPrefab));
				customPrefabs.Add(UIPartActionKsmCargoResourceFlight.CreatePrefab(__instance.resourceItemPrefab));
				customPrefabs.Add(UIPartActionKsmCargoResourceEditor.CreatePrefab(__instance.resourceItemEditorPrefab));
			}

			__instance.fieldPrefabs.AddRange(customPrefabs);
		}
	}
}
