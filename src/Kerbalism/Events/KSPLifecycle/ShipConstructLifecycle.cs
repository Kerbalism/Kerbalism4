using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM.Events
{
	public class ShipConstructLifecycle
	{
		public void OnEditorShipModified(ShipConstruct data)
		{
			ModuleKsmExperiment.CheckEditorExperimentMultipleRun();
			//Planner.Planner.EditorShipModifiedEvent(data);
		}

		// fix for B9PS disabled modules being re-enabled when the part is placed in the editor
		public void OnEditorPartEvent(ConstructionEventType data0, Part data1)
		{
			if (data0 == ConstructionEventType.PartAttached)
			{
				foreach (PartModule module in data1.Modules)
				{
					if (module is KsmPartModule ksmModule && !ksmModule.switchLastModuleEnabled && ksmModule.enabled)
					{
						ksmModule.enabled = false;
					}
				}
			}
		}
	}

	[HarmonyPatch(typeof(ShipConstruct))]
	[HarmonyPatch("LoadShip")]
	[HarmonyPatch(new Type[] { typeof(ConfigNode), typeof(uint), typeof(bool), typeof(string) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out })]
	public class ShipConstruct_LoadShip
	{
		private static uint editorPreviousShipId;
		public static ConfigNode kerbalismDataNode;

		// See comment on the ShipConstruct.SaveShip patch. From my tests, having our extra node in the first
		// part node doesn't cause any issue, but just in case, better remove it.
		static void Prefix(ConfigNode root)
		{
			ModuleHandler.handlerShipIdsByModuleInstanceId.Clear();

			editorPreviousShipId = EditorLogic.fetch?.ship?.persistentId ?? 0u;
			kerbalismDataNode = null;

			if (root != null && root.CountNodes > 0)
			{
				kerbalismDataNode = root.nodes[0].GetNode(VesselDataBase.NODENAME_VESSEL);
			}

			if (kerbalismDataNode != null)
			{
				root.nodes[0].RemoveNode(VesselDataBase.NODENAME_VESSEL);
			}
		}

		static void Postfix(ShipConstruct __instance, bool __result)
		{
			// LoadShip can be in a chain of events that deleted all/some loaded parts, or not at all.
			// Exemple : in the editor, loading a ship will remove the current ship, but merging a ship won't.
			// There are just too many different cases, and our options for detecting them are very limited.
			// So we take a brute force approach and always verify that our PartData collection doesn't contain
			// a part that doesn't exist anymore, by iterating over Part.allParts (which seems reliable in that matter)
			if (Part.allParts.Count == 0)
			{
				VesselDataShip.ShipParts.Clear();
			}
			else
			{
				HashSet<int> loadedPartsId = new HashSet<int>();
				for (int i = 0; i < Part.allParts.Count; i++)
				{
					loadedPartsId.Add(Part.allParts[i].GetInstanceID());
				}

				List<int> loadedPartDataIds = new List<int>(VesselDataShip.ShipParts.AllInstanceIDs);
				foreach (int key in loadedPartDataIds)
				{
					if (!loadedPartsId.Contains(key))
					{
						VesselDataShip.ShipParts.Remove(key);
					}
				}
			}

			// LoadShip will return false if an error happened
			if (!__result)
				return;

			Lib.LogDebug($"Loading VesselData for ship {__instance.shipName}");

			// we don't want to overwrite VesselData when loading a subassembly or when merging
			uint editorNewShipId = EditorLogic.fetch?.ship?.persistentId ?? 0u;
			bool isNewShip = editorPreviousShipId == 0 || editorNewShipId == 0 || editorPreviousShipId != editorNewShipId;

			VesselDataBase.LoadShipConstruct(__instance, kerbalismDataNode, isNewShip);
			ModuleHandler.handlerShipIdsByModuleInstanceId.Clear();
			kerbalismDataNode = null;

		}
	}

	// There is no "public" way to save non-module specific data into a shipconstruct.
	// So the purpose of this patch is to save our VesselDataShip in the shipconstruct confignode,
	// which is what KSP saves in *.craft files.
	// Unfortunately, this node only has values and all child nodes are expected to be PART nodes,
	// and KSP doesn't even check that they are named "PART", it just do a node.GetNodes(), so that cause
	// a crash if we put our node there, and there are other issues (like for showing the part count of
	// ship saves, it just do nodes.count on the root node.
	// So what we do is put our KERBALISMDATA node inside the first PART node, where there are multiple other
	// stock nodes with various names, so we can expect that it will never be confused by it.
	// For an additional safety, we remove that node in the ShipConstruct.LoadShip prefix
	[HarmonyPatch(typeof(ShipConstruct))]
	[HarmonyPatch("SaveShip")]
	class ShipConstruct_SaveShip
	{
		public static List<PartData> newPartDatas = new List<PartData>();

		static void Prefix(ShipConstruct __instance)
		{
			Lib.LogDebug($"Saving shipconstruct {__instance.shipName}...");
			newPartDatas.Clear();
		}


		// In the postfix, we grab the ShipConstruct node, and put our data in the first PART node
		static void Postfix(ConfigNode __result)
		{
			if (__result.CountNodes == 0)
				return;

			ConfigNode firstPartNode = __result.nodes[0];
			if (firstPartNode == null)
				return;

			foreach (PartData part in newPartDatas)
			{
				foreach (ModuleHandler handler in part.modules)
				{
					handler.FirstSetup();
				}

				part.Start();
			}

			newPartDatas.Clear();

			VesselDataShip.Instance.Save(firstPartNode);

		}
	}

	// This patch handle the creation of a new VesselData from a VesselDataShip.
	// When launching a new ship, KSP does the following :
	// - Call ShipConstruct.LoadShip() to create the parts
	// - Call ShipConstruct.SaveShip() (for the purpose of reverting to editor)
	// - Call ShipConstruction.AssembleForLaunch() to create the Vessel, and assign those parts to it.
	// Since we are patching ShipConstruct.LoadShip(), that mean all our PartData/ModuleData have been loaded.
	// Note that all of this happens after Kerbalism.OnLoad(), this is important because we need the global
	// dictionary of ModuleData flightIds to be populated so we can check the uniqueness of the flightIds
	// we are about to create for this new vessel.
	// So, what we are about to do :
	// - Get the confignode that was just loaded by the ShipConstruct.LoadShip() call
	// - Create a new VesselData with a specific ctor that :
	//   - Copy the PartDatas/ModuleDatas from the current PartDataCollectionShip to a new PartDataCollectionVessel
	//   - Assign flightId for every PartData/ModuleData
	//   - call VesselDataBase.Load() with that confignode we grabbed to load the data that is meant to be transfered
	//     from editor to flight. That call won't call VesselData.OnLoad(), but initialize the default values instead.
	// - Add the new VesselData to the DB.
	// We are doing it that way because that avoid a full serialization/deserialization cycle, but more importantly
	// because the "in between" ShipConstruct.SaveShip() call by KSP causes a new shipID to be affected to the currently
	// loaded parts, so we have lost any way to re-link trough ids the currently loaded parts and the last loaded confignode.
	[HarmonyPatch(typeof(ShipConstruction))]
	[HarmonyPatch("AssembleForLaunch")]
	[HarmonyPatch(new Type[] { typeof(ShipConstruct), typeof(string), typeof(string), typeof(string), typeof(Game),
		typeof(VesselCrewManifest), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(Orbit), typeof(bool), typeof(bool) })]
	class ShipConstruction_AssembleForLaunch
	{
		static void Postfix(Vessel __result)
		{
			Lib.LogDebug($"Assembling ship for launch: {__result.vesselName}");

			ConfigNode kerbalismDataNode = ShipConstruction.ShipConfig?.nodes[0]?.GetNode(VesselDataBase.NODENAME_VESSEL);

			DB.NewVesselDataFromShipConstruct(__result, kerbalismDataNode, VesselDataShip.Instance);

			VesselDataShip.ShipParts.Clear();
			VesselDataShip.Instance = null;
		}
	}
}
