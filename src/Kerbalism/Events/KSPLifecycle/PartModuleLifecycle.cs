using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM.Events
{
	[HarmonyPatch(typeof(PartModule))]
	[HarmonyPatch("Save")]
	class PartModule_Save
	{
		static void Postfix(PartModule __instance, ConfigNode node)
		{
			// Lib.LogDebug($"Saving {__instance.moduleName} on {__instance.part.name}");

			if (Lib.IsEditor)
			{
				// only save known module types
				if (!ModuleHandler.handlerTypesByModuleName.TryGetValue(__instance.moduleName, out ModuleHandler.ModuleHandlerType handlerType))
					return;

				// only save persistant handlers having the editor context
				// the context check is needed because we will instantiate missing modules if needed.
				if (!handlerType.isPersistent || (handlerType.activation & ModuleHandler.ActivationContext.Editor) == 0)
					return;

				int instanceId = __instance.GetInstanceID();

				if (!ModuleHandler.loadedHandlersByModuleInstanceId.TryGetValue(instanceId, out ModuleHandler handler))
				{
					// if we are here, that's because ShipConstruct.SaveShip is called to create the "auto-saved ship" 
					// in the editor just after the first part is instantiated when you pick it up from the part list
					// This happens before Part.Start(), meaning we won't have yet instantiated the PartData and the
					// ModuleHandler

					if (!VesselDataShip.ShipParts.TryGet(__instance.part, out PartData partData))
					{
						partData = new PartData(VesselDataShip.Instance, __instance.part);
						VesselDataShip.ShipParts.Add(partData);
						ShipConstruct_SaveShip.newPartDatas.Add(partData);
					}

					ModuleHandler.NewEditorLoaded(__instance, __instance.part.Modules.IndexOf(__instance), partData, ModuleHandler.ActivationContext.Editor, false);
					handler = ModuleHandler.loadedHandlersByModuleInstanceId[instanceId]; // this can't fail unless something is deeply wrong.
				}

				node.AddValue(ModuleHandler.VALUENAME_SHIPID, instanceId);
				((IPersistentModuleHandler)handler).ShipId = instanceId;
			}
			else
			{
				if (!ModuleHandler.loadedHandlersByModuleInstanceId.TryGetValue(__instance.GetInstanceID(), out ModuleHandler handler))
					return;

				if (!(handler is IPersistentModuleHandler persistentHandler))
					return;

				// note : this check might not be failproof to mods (KCT/Scrapyard...) doing flight vessel to ShipConstruct conversions. Need some testing.
				if (persistentHandler.FlightId != 0)
				{
					node.AddValue(ModuleHandler.VALUENAME_FLIGHTID, persistentHandler.FlightId);
				}
				else
				{
					// There are two (stock) cases where a VesselDataShip is saved in flight :
					// On launching a new vessel, during the AssembleForLaunch() call, KSP will save the "revert to launchpad / revert to editor" shipConstruct
					// from the freshly instantiatied shipConstruct (it does a Save() call on the actual parts, not a simple backup of the shipConstruct).
					// In order for that backup shipConstruct to be loadable again by us, we need to assign every shipId.
					if (persistentHandler.ModuleHandler.VesselData is VesselDataShip)
					{
						int instanceId = __instance.GetInstanceID();
						node.AddValue(ModuleHandler.VALUENAME_SHIPID, instanceId);
						persistentHandler.ShipId = instanceId;
					}
					else
					{
						Lib.Log($"FlightId isn't affected on {__instance.moduleName} for persistent {handler.GetType().Name} on part {__instance.part.name}", Lib.LogLevel.Warning);
					}

				}
			}
		}
	}

	[HarmonyPatch(typeof(PartModule))]
	[HarmonyPatch("Load")]
	class PartModule_Load
	{
		static void Postfix(PartModule __instance, ConfigNode node)
		{
			// Lib.LogDebug($"Loading {__instance.moduleName} on {__instance.part.name}");

			if (!ModuleHandler.persistentHandlersByModuleName.Contains(__instance.moduleName))
				return;

			foreach (ConfigNode.Value value in node.values)
			{
				if (value.name == ModuleHandler.VALUENAME_FLIGHTID && int.TryParse(value.value, out int flightd))
				{
					ModuleHandler.handlerFlightIdsByModuleInstanceId[__instance.GetInstanceID()] = flightd;
					return;
				}
				else if (value.name == ModuleHandler.VALUENAME_SHIPID && int.TryParse(value.value, out int shipId))
				{
					ModuleHandler.handlerShipIdsByModuleInstanceId[__instance.GetInstanceID()] = shipId;
					return;
				}
			}
		}
	}

	public class PartModuleLifecycle
	{

	}
}
