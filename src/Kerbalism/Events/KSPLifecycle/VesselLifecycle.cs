using Harmony;
using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM.Events
{
	[HarmonyPatch(typeof(Vessel))]
	[HarmonyPatch("Unload")]
	class Vessel_Unload
	{
		static void Postfix(Vessel __instance)
		{
			if (!__instance.TryGetVesselData(out VesselData vesselData))
				return;

			for (int i = 0; i < __instance.protoVessel.protoPartSnapshots.Count; i++)
			{
				vesselData.Parts[i].SetProtopartReferenceOnVesselUnload(__instance.protoVessel.protoPartSnapshots[i]);
			}
		}
	}

	// Create a "OnPartAfterDecouple" event that happen after the decoupling is complete, 
	// and where you have access to the old vessel and the new vessel.
	[HarmonyPatch(typeof(Part))]
	[HarmonyPatch("decouple")]
	class Part_decouple
	{
		static bool Prefix(Part __instance, out Vessel __state)
		{
			// get the vessel of the part, before decoupling
			__state = __instance.vessel;
			return true; // continue to Part.decouple()
		}

		static void Postfix(Part __instance, Vessel __state)
		{
			// only fire the event if a new vessel has been created
			if (__instance.vessel != null && __state != null && __instance.vessel != __state)
			{
				VesselLifecycle.OnPartAfterDecouple(__instance, __state, __instance.vessel);
			}
		}
	}

	// Create a "OnPartAfterUndock" event that happen after the undocking is complete, 
	// and where you have access to the old vessel and the new vessel.
	[HarmonyPatch(typeof(Part))]
	[HarmonyPatch("Undock")]
	class Part_Undock
	{
		static bool Prefix(Part __instance, out Vessel __state)
		{
			// get the vessel of the part, before decoupling
			__state = __instance.vessel;
			return true; // continue to Part.decouple()
		}

		static void Postfix(Part __instance, Vessel __state)
		{
			// only fire the event if a new vessel has been created
			if (__instance.vessel != null && __state != null && __instance.vessel != __state)
			{
				VesselLifecycle.OnPartAfterUndock(__instance, __state, __instance.vessel);
			}
		}
	}

	public class VesselLifecycle
	{
		public static VesselLifecycle Instance { get; private set; }

		public VesselLifecycle()
		{
			Instance = this;
		}

		public static void VesselModified(Vessel vessel) => Instance.OnVesselModified(vessel);

		#region VESSEL LIFECYCLE

		public void OnVesselModified(Vessel vessel)
		{
			// Note : the anonymous cache is currently used for a bunch a bunch of things that could probably be either
			// refactored as KsmPartModules or stored elswhere in a more efficient way :
			// - warp drives : should definitiely be moved to CommHandler
			// - caching the modules that are getting background processing : this is made necessary because the current way
			//   of finding protomodules is terrible performance wise. Now that we basically have out own vessel/part/module
			//   data structure, maybe we should just store the prefabs and protomodule references there and get ride of the whole thing.
			//   I doubt the impact on scene change processing time would be noticeable, and that would reduce to zero the perf impact
			//   of the background processing loop. Plus that would allow to implement background processing for non-Kerbalism modules
			//   in a streamlined, unified way, and that simplify the background processing API implementation.
			// - caching the vessel computer devices
			// - caching Lib.HasPart() calls (experiment requirements)
			// - caching the scansat scanners (?)
			// - caching Lib.FindModules() (find protomodules), used in many places : Emitter, Greenhouse, Passive shield, Reliability, etc
			Cache.PurgeVesselCaches(vessel);
		}

		public void VesselCreated(Vessel v)
		{
			if (Serenity.GetModuleGroundExpControl(v) != null)
				v.vesselName = Lib.BuildString(v.mainBody.name, " Site ", Lib.Greek());
		}

		// Hack the stock recovery dialog to show our science results
		public void OnVesselRecoveryProcessingComplete(ProtoVessel pv, MissionRecoveryDialog dialog, float recoveryFactor)
		{
			VesselRecovery_OnVesselRecovered.OnVesselRecoveryProcessingComplete(dialog);
		}

		public void OnVesselStandardModification(Vessel vessel)
		{
			// avoid this being called on vessel launch, when vessel is not yet properly initialized
			if (!vessel.loaded && vessel.protoVessel == null) return;

			OnVesselModified(vessel);
		}

		// note: this is called multiple times when a vessel is recovered
		public void VesselRecovered(ProtoVessel pv, bool b)
		{
			// for each crew member
			foreach (ProtoCrewMember c in pv.GetVesselCrew())
			{
				// avoid creating kerbal data in db again,
				// as this function may be called multiple times
				if (!DB.ContainsKerbal(c.name))
					continue;

				// set roster status of eva dead kerbals
				if (DB.Kerbal(c.name).eva_dead)
				{
					c.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
				}

				// reset kerbal data of recovered kerbals
				DB.RecoverKerbal(c.name);
			}

			// purge the caches
			Cache.PurgeVesselCaches(pv);
		}

		public void VesselTerminated(ProtoVessel pv)
		{
			// forget all kerbals data
			foreach (ProtoCrewMember c in pv.GetVesselCrew())
				DB.KillKerbal(c.name, true);

			// purge the caches
			Cache.PurgeVesselCaches(pv);

			// trigger die event on unloaded vessels only (this is handled trough OnPartWillDie for loaded vessels)
			if (pv.vesselRef != null && !pv.vesselRef.loaded && DB.TryGetVesselData(pv.vesselRef, out VesselData vd))
				vd.OnVesselWillDie();
		}

		public void VesselDestroyed(Vessel v)
		{
			// rescan the damn kerbals
			// - vessel crew is empty at destruction time
			// - we can't even use the flightglobal roster, because sometimes it isn't updated yet at this point
			HashSet<string> kerbals_alive = new HashSet<string>();
			HashSet<string> kerbals_dead = new HashSet<string>();
			foreach (Vessel ov in FlightGlobals.Vessels)
			{
				foreach (ProtoCrewMember c in Lib.CrewList(ov))
					kerbals_alive.Add(c.name);
			}
			foreach (string key in DB.Kerbals.Keys)
			{
				if (!kerbals_alive.Contains(key))
					kerbals_dead.Add(key);
			}
			foreach (string n in kerbals_dead)
			{
				// we don't know if the kerbal really is dead, or if it is just not currently assigned to a mission
				DB.KillKerbal(n, false);
			}

			// purge the caches
			Cache.PurgeVesselCaches(v); // works with loaded and unloaded vessels

			// trigger die event on unloaded vessels only (this is handled trough OnPartWillDie for loaded vessels)
			if (!v.loaded && DB.TryGetVesselData(v, out VesselData vd))
				vd.OnVesselWillDie();
		}

		public void VesselDock(GameEvents.FromToAction<Part, Part> e)
		{
			Cache.PurgeVesselCaches(e.from.vessel);
			// Update docked to vessel
			OnVesselModified(e.to.vessel);
		}

		// Called by the OnPartCouple events, called for docking and KIS added parts
		public void OnPartCouple(GameEvents.FromToAction<Part, Part> data)
		{
			VesselData.OnPartCouple(data);
		}

		// Called by an harmony patch, happens every time a part is decoupled (decouplers, joint failure...)
		// but only if a new vessel has been created in the process
		public static void OnPartAfterUndock(Part part, Vessel oldVessel, Vessel newVessel)
		{
			VesselData.OnDecoupleOrUndock(oldVessel, newVessel);
		}

		// Called by an harmony patch, happens every time a part is undocked
		// but only if a new vessel has been created in the process
		public static void OnPartAfterDecouple(Part part, Vessel oldVessel, Vessel newVessel)
		{
			VesselData.OnDecoupleOrUndock(oldVessel, newVessel);
		}

		#endregion
	}
}
