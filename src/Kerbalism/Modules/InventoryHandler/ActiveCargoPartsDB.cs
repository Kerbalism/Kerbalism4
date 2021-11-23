using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

namespace KERBALISM
{
	[HarmonyPatch(typeof(ModuleCargoPart))]
	[HarmonyPatch(nameof(ModuleCargoPart.OnLoad))]
	static class ModuleCargoPart_OnLoad
	{
		static void Postfix(ModuleCargoPart __instance, ConfigNode node)
		{
			// TODO : instead of doing that, unpatch the method once loading is done
			if (HighLogic.LoadedScene != GameScenes.LOADING)
				return;

			if (!Lib.ConfigValue(node, "isActiveCargoPart", false))
				return;

			ActiveCargoPartInfo info = ActiveCargoPartInfo.Parse(node);
			ActiveCargoPartsDB.activeCargoParts[__instance.part] = info;
		}
	}

	public class ActiveCargoPartInfo
	{
		[CFGValue] public bool requireInstallation = true;
		[CFGValue] public bool canInstallInFlight = true;
		[CFGValue] public bool allowActiveResources = false;

		public CrewSpecs flightInstallCrewSpecs;
		public bool hasActiveResourcesWhiteList = false;
		public HashSet<PartResourceDefinition> activeResources;

		public static ActiveCargoPartInfo Parse(ConfigNode node)
		{
			ActiveCargoPartInfo info = new ActiveCargoPartInfo();
			CFGValue.Parse(info, node);

			info.flightInstallCrewSpecs = new CrewSpecs(node.GetValue(nameof(flightInstallCrewSpecs)));

			ConfigNode activeResourcesNode = node.GetNode("ACTIVE_RESOURCE_WHITELIST");

			if (activeResourcesNode != null)
			{
				info.activeResources = new HashSet<PartResourceDefinition>();
				foreach (ConfigNode.Value nodeValue in activeResourcesNode.values)
				{
					PartResourceDefinition resource = PartResourceLibrary.Instance.GetDefinition(nodeValue.name);
					if (resource != null)
					{
						info.activeResources.Add(resource);
					}
				}
				info.hasActiveResourcesWhiteList = info.activeResources.Count > 0;
			}

			return info;
		}
	}

	public static class ActiveCargoPartsDB
	{
		public static Dictionary<Part, ActiveCargoPartInfo> activeCargoParts = new Dictionary<Part, ActiveCargoPartInfo>();
		public static Dictionary<AvailablePart, ActiveCargoPartInfo> activeCargoPartsInfos = new Dictionary<AvailablePart, ActiveCargoPartInfo>();

		public static void OnPartLoaderLoaded()
		{
			foreach (AvailablePart ap in PartLoader.LoadedPartsList)
			{
				if (activeCargoParts.TryGetValue(ap.partPrefab, out ActiveCargoPartInfo info))
				{
					activeCargoPartsInfos.Add(ap, info);
				}
			}

			activeCargoParts = null;
		}
	}
}
