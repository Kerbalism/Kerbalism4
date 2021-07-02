using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM.Events
{
	public class PartLifecycle
	{
		public static PartLifecycle Instance { get; private set; }

		public PartLifecycle()
		{
			Instance = this;
		}

		// Called by an harmony patch, exactly the same as the stock OnPartWillDie (that is not available in 1.5/1.6)
		public void OnPartWillDie(Part p)
		{
			// do nothing in the editor
			if (Lib.IsEditor)
				return;

			// remove part from vesseldata
			VesselData.OnPartWillDie(p);

			// update vessel
			VesselLifecycle.VesselModified(p.vessel);
		}

		// Called when Part.OnDestroy() is called by unity, and nowhere else.
		public void OnPartDestroyed(Part part)
		{
			Lib.LogDebug($"Destroying part {part.partInfo.title}");

			if (Lib.IsEditor)
			{
				VesselDataShip.ShipParts.Remove(part);
			}
			else if (PartData.TryGetLoadedPartData(part, out PartData partData))
			{
				partData.OnLoadedDestroy();
			}
		}
	}

	// OnPartDie is not called for the root part
	// OnPartWillDie works but isn't available in 1.5/1.6
	// Until we drop 1.5/1.6 support, we use this patch instead
	[HarmonyPatch(typeof(Part))]
	[HarmonyPatch("Die")]
	class Part_Die
	{
		static void Prefix(Part __instance)
		{
			// replicate OnPartWillDie
			if (__instance.State == PartStates.DEAD)
				return;

			PartLifecycle.Instance.OnPartWillDie(__instance);

			return; // continue to Part.Die()
		}
	}


	// The purpose of this patch is double :
	// 1. In the editor, create the PartData and ModuleData for every KsmPartModule
	//    This is in a Part.Start() Prefix because that will run after ShipConstruct.LoadShip()
	//    So any PartData/Moduledata that was instantiated by loading a shipconstruct is already here
	//    and won't be "erased", but by doing this in Part.Start() we are sure that any other part
	//    instantiation (picked from the list, alt-copy, symmetry...) is catched, and we are sure
	//    that ModuleData will be here when KsmPartModule.OnStart() is called from Part.Start() -> Part.ModulesOnStart()
	// 2. In flight, two cases :
	//   A. A (loaded) vessel has been loaded from an existing VesselData.
	//      From VesselData.OnLoad(), the PartData collection has been populated
	//      If moduledatas were found in the confignode, they have been loaded, otherwise new ones have been created
	//      In any case, flightIds have been populated.
	//      The Part and its PartModule also have their OnLoad called, so the flightIds are there too
	//      What only remains to do is to set the actual cross-references between the KsmPartModule and its ModuleData,
	//      by using the flightIds.
	//   B. A (loaded) vessel has been loaded, but the ModuleData for one or more of its KsmPartModule can't be found.
	//      This can happen if :
	//      - the part configuration has been modified
	//      - the part was instantiated in flight (KIS...)
	//   To manage those two cases, we use a get-or-create operation.
	[HarmonyPatch(typeof(Part))]
	[HarmonyPatch("Start")]
	class Part_Start
	{
		static void Prefix(Part __instance)
		{
			Lib.Log("PART START");

			if (Lib.IsEditor)
			{
				// PartData will already exist if created by ShipConstruct.LoadShip()
				// Also, ShipConstruct.SaveShip() can be called before Part.Start(), if that happen
				// we will already have instantiatied everything from there because we can't afford
				// to skip the moduledata shipId affectation.
				// In all other cases, this is a newly instantiated part, create the partdata/moduledata
				if (!VesselDataShip.ShipParts.TryGet(__instance, out PartData partData))
				{
					partData = new PartData(VesselDataShip.Instance, __instance);
					VesselDataShip.ShipParts.Add(partData);

					// create and link the ModuleData for every KsmPartModule
					for (int i = 0; i < __instance.Modules.Count; i++)
					{
						ModuleHandler.NewEditorLoaded(__instance.Modules[i], i, partData, ModuleHandler.ActivationContext.Editor, false);
					}
				}

				foreach (ModuleHandler handler in partData.modules)
				{
					handler.FirstSetup();
				}

				partData.Start();
			}
			else
			{
				if (!__instance.vessel.TryGetVesselDataTemp(out VesselData vd))
				{
					// flags have an empty Guid, so we never create a VesselData for them
					if (!VesselData.VesselNeedVesselData(__instance.vessel.protoVessel))
						return;

					Lib.LogDebugStack($"VesselData doesn't exists for vessel {__instance.vessel.vesselName}, can't link PartData !", Lib.LogLevel.Error);
					return;
				}

				if (!vd.Parts.TryGet(__instance, out PartData partData))
				{
					Lib.LogDebug($"Instantiating PartData for {__instance.name} on {__instance.vessel.vesselName}");
					partData = vd.VesselParts.Add(__instance);

					for (int i = 0; i < __instance.Modules.Count; i++)
					{
						// if the module is a type we haven't a handler for, continue
						if (!ModuleHandler.TryGetModuleHandlerType(__instance.Modules[i].moduleName, out ModuleHandler.ModuleHandlerType handlerType))
							continue;

						// only instaniate handlers that have the loaded context
						if ((handlerType.activation & ModuleHandler.ActivationContext.Loaded) == 0)
							continue;

						ModuleHandler.NewLoaded(handlerType, __instance.Modules[i], i, partData, true);
					}

					foreach (ModuleHandler handler in partData.modules)
					{
						handler.FirstSetup();
					}

					partData.Start();

				}
				else if (!partData.IsLoaded)
				{
					Lib.LogDebug($"Acquiring PartData references for {__instance.name} on {__instance.vessel.vesselName}");
					partData.SetPartReference(__instance);
				}
			}
		}
	}



}
