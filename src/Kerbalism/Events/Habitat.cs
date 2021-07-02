using HarmonyLib;
using KSP.UI;
using KSP.UI.Screens;
using System;
using System.Collections;
using System.Collections.Generic;

namespace KERBALISM
{
	// Helmets state patching
	[HarmonyPatch(typeof(InternalModel))]
	[HarmonyPatch("SpawnCrew")]
	class InternalModel_SpawnCrew
	{
		// force instantiation of the helmets
		static void Prefix(InternalModel __instance)
		{
			foreach (InternalSeat internalSeat in __instance.seats)
			{
				internalSeat.allowCrewHelmet = true;
			}
		}

		// when SpawnCrew is called by KSP or mods, make sure we set the helmet state
		// according to the Habitat current pressure
		static void Postfix(InternalModel __instance)
		{
			if (!__instance.part.TryGetFlightModuleDataOfType(out HabitatHandler habitatData))
				return;

			bool requireSuit = habitatData.RequireHelmet;
			foreach (InternalSeat internalSeat in __instance.seats)
			{
				if (internalSeat.kerbalRef == null)
					continue;

				internalSeat.kerbalRef.ShowHelmet(requireSuit);
			}
		}
	}

	// Helmets state patching
	[HarmonyPatch(typeof(Kerbal))]
	[HarmonyPatch("ShowHelmet")]
	class Kerbal_ShowHelmet
	{
		// Here we completely override the stock method because if the original ShowHelmet(false) 
		// is called it will actually do Object.Destroy(helmetTransform.gameObject), resulting in 
		// any latter call to ShowHelmet(true) to fail to actually show the helmets.
		// To get around that, we used to respawn the whole vessel IVA, but patching ShowHelmet()
		// is a lot cleaner and faster.
		static bool Prefix(Kerbal __instance, bool show)
		{
			if (__instance.helmetTransform != null)
			{
				__instance.helmetTransform.gameObject.SetActive(show);
				return false;
			}
			return true;
		}
	}

	// Don't show the IVA on non-deployed habitats
	[HarmonyPatch(typeof(InternalModel))]
	[HarmonyPatch("SetVisible")]
	class InternalModel_SetVisible
	{
		static void Prefix(InternalModel __instance, ref bool visible)
		{
			// if visible == false, we don't care
			if (!visible)
				return;

			// otherwise, set the visible parameter to the habitat deployed state before continuing to the method
			if (!__instance.part.TryGetFlightModuleDataOfType(out HabitatHandler habitatData))
				return;

			visible = habitatData.IsDeployed;
		}
	}


	public class GameEventsHabitat
	{
		#region CREW TRANSFER

		public static bool disableCrewTransferFailMessage = false;
		private static bool crewAssignementRefreshWasJustFiredFromCrewChanged = false;

		public void CrewTransferSelected(CrewTransfer.CrewTransferData data)
		{
			bool sourceIsPressurized = false;
			if (data.sourcePart != null && data.sourcePart.TryGetFlightModuleDataOfType(out HabitatHandler sourceHabitatData))
			{
				sourceIsPressurized = sourceHabitatData.pressureState == HabitatHandler.PressureState.Pressurized;
			}

			bool targetIsEnabled = false;
			bool targetIsPressurized = false;
			if (data.destPart != null && data.destPart.TryGetFlightModuleDataOfType(out HabitatHandler destHabitatData))
			{
				// if hab isn't enabled, try to enable it. We do that because otherwise you can 
				// brick your vessel by not being able to transfer back people in control parts.
				if (destHabitatData.isEnabled || ModuleKsmHabitat.TryToggleHabitat(destHabitatData.loadedModule, destHabitatData, data.destPart.vessel.loaded))
					targetIsEnabled = true;

				targetIsPressurized = destHabitatData.pressureState == HabitatHandler.PressureState.Pressurized;
			}

			if (!targetIsEnabled)
			{
				if (!disableCrewTransferFailMessage)
					Message.Post($"Can't transfer {Lib.Bold(data.crewMember.displayName)} to {Lib.Bold(data.destPart?.partInfo.title)}", "The habitat is disabled !");

				data.canTransfer = false;
			}
			else if ((sourceIsPressurized && !targetIsPressurized) || (!sourceIsPressurized && targetIsPressurized))
			{
				if (!disableCrewTransferFailMessage)
					Message.Post($"Can't transfer {Lib.Bold(data.crewMember.displayName)} from {Lib.Bold(data.sourcePart?.partInfo.title)}\nto {Lib.Bold(data.destPart?.partInfo.title)}", "One is pressurized and not the other !");

				data.canTransfer = false;
			}
			else
			{
				data.canTransfer = true;
			}

			disableCrewTransferFailMessage = false;
		}

		public void CrewTransferred(GameEvents.HostedFromToAction<ProtoCrewMember, Part> data)
		{
			if (data.from == data.to)
				return;

			double wasteTransferred = 0.0;

			if (data.from != null && data.from.TryGetFlightModuleDataOfType(out HabitatHandler fromHabitatData))
			{

				int newCrewCount = Lib.CrewCount(data.from);
				if (fromHabitatData.crewCount - newCrewCount != 1)
				{
					Lib.LogStack($"From part {data.from.partInfo.title} : crew count old={fromHabitatData.crewCount}, new={newCrewCount}, HabitatData is desynchronized !", Lib.LogLevel.Error);
				}

				switch (fromHabitatData.pressureState)
				{
					case HabitatHandler.PressureState.AlwaysDepressurized:
					case HabitatHandler.PressureState.Depressurized:
					case HabitatHandler.PressureState.Pressurizing:
					case HabitatHandler.PressureState.DepressurizingBelowThreshold:

						PartResourceWrapper wasteRes = fromHabitatData.WasteRes;
						wasteTransferred = fromHabitatData.crewCount > 0 ? wasteRes.Amount / fromHabitatData.crewCount : 0.0;
						wasteRes.Amount = newCrewCount > 0 ? wasteRes.Amount - wasteTransferred : 0.0;
						wasteRes.Capacity = newCrewCount * Settings.PressureSuitVolume;
						break;
				}

				fromHabitatData.crewCount = newCrewCount;
			}


			if (data.to != null && data.to.TryGetFlightModuleDataOfType(out HabitatHandler toHabitatData))
			{
				toHabitatData.crewCount = Lib.CrewCount(data.to);

				PartResource wasteRes;
				if (!data.to.Resources.Contains(Settings.HabitatWasteResource))
					wasteRes = Lib.AddResource(data.to, Settings.HabitatWasteResource, 0.0, HabitatLib.M3ToL(toHabitatData.definition.volume));
				else
					wasteRes = data.to.Resources[Settings.HabitatWasteResource];

				// note : this is called when going from a vessel to EVA, but the EVA modules OnStart() isn't yet called.
				// So the waste resource capacity won't be set yet, and neither the wrapper. And we can't create it here because
				// that will be overriden anyway in the module 
				// So for now you magically get ride of some CO2 by going on EVA
				if (wasteRes != null)
				{
					switch (toHabitatData.pressureState)
					{
						case HabitatHandler.PressureState.AlwaysDepressurized:
						case HabitatHandler.PressureState.Depressurized:
						case HabitatHandler.PressureState.Pressurizing:
						case HabitatHandler.PressureState.DepressurizingBelowThreshold:

							wasteRes.maxAmount = toHabitatData.crewCount * Settings.PressureSuitVolume;
							break;
					}

					if (wasteTransferred > 0.0)
					{
						wasteRes.amount = Math.Min(wasteRes.amount + wasteTransferred, wasteRes.maxAmount);
					}
				}


			}
		}

		#endregion

		#region EDITOR EVENTS

		// in editor crew assignement through the assignement dialog
		// prevent being able to put crew on disabled habitats
		public void EditorCrewChanged(VesselCrewManifest crewManifest)
		{
			if (crewManifest == null || crewAssignementRefreshWasJustFiredFromCrewChanged)
			{
				crewAssignementRefreshWasJustFiredFromCrewChanged = false;
				return;
			}

			bool fromVesselSpawnDialog = VesselSpawnDialog.Instance != null;
			HashSet<uint> enabledHabsPartId = new HashSet<uint>();

			// This handle the case where this is called from the launch pad / runway launch UI.
			// Note : the check won't work on imported crafts, as our data will not be serialized here.
			// Note2 : I'm not sure this works anymore
			if (fromVesselSpawnDialog)
			{
				try
				{
					object vesselDataItem = typeof(VesselSpawnDialog).GetField("selectedDataItem", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(VesselSpawnDialog.Instance);
					ConfigNode vesselNode = (ConfigNode)vesselDataItem.GetType().GetProperty("configNode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).GetValue(vesselDataItem);
					ConfigNode kerbalismDataNode = vesselNode.nodes[0].GetNode(VesselDataBase.NODENAME_VESSEL);

					HashSet<int> enabledHabsShipIds = new HashSet<int>();
					foreach (ConfigNode moduleDataNode in kerbalismDataNode.GetNode(VesselDataBase.NODENAME_MODULE).nodes)
					{
						if (moduleDataNode.name.Split('@')[1] == nameof(HabitatHandler) && Lib.ConfigValue(moduleDataNode, "habitatEnabled", false))
						{
							enabledHabsShipIds.Add(Lib.ConfigValue(moduleDataNode, ModuleHandler.VALUENAME_SHIPID, 0));
						}
					}

					foreach (ConfigNode partNode in vesselNode.nodes)
					{
						ConfigNode habitatModuleNode = partNode.GetNode("MODULE", "name", nameof(ModuleKsmHabitat));
						if (habitatModuleNode == null)
							continue;

						int dataShipId = Lib.ConfigValue(habitatModuleNode, ModuleHandler.VALUENAME_SHIPID, 0);
						if (dataShipId != 0 && enabledHabsShipIds.Contains(dataShipId))
						{
							uint partCraftId = uint.Parse(partNode.GetValue("part").Split('_')[1]);
							enabledHabsPartId.Add(partCraftId);
						}
					}
				}
				catch (Exception)
				{
					return;
				}
			}

			bool needRefresh = false;
			foreach (PartCrewManifest partManifest in crewManifest.PartManifests)
			{
				if (partManifest.NoSeats())
					continue;

				if (fromVesselSpawnDialog)
				{
					if (!enabledHabsPartId.Contains(partManifest.PartID))
					{
						foreach (ProtoCrewMember crew in partManifest.GetPartCrew())
							if (crew != null)
								Message.Post($"Can't put {Lib.Bold(crew.displayName)} in {Lib.Bold(partManifest.PartInfo.title)}", "Habitat is disabled !");

						for (int i = 0; i < partManifest.partCrew.Length; i++)
						{
							if (!string.IsNullOrEmpty(partManifest.partCrew[i]))
							{
								partManifest.RemoveCrewFromSeat(i);

								needRefresh |= true;
							}
						}
					}
				}
				else
				{
					ModuleKsmHabitat habitat = EditorLogic.fetch.ship.parts.Find(p => p.craftID == partManifest.PartID)?.FindModuleImplementing<ModuleKsmHabitat>();

					if (habitat == null || habitat.moduleHandler == null)
						continue;

					HabitatHandler habData = habitat.moduleHandler;

					if (!habData.isEnabled)
					{
						habData.crewCount = 0;

						foreach (ProtoCrewMember crew in partManifest.GetPartCrew())
							if (crew != null)
								Message.Post($"Can't put {Lib.Bold(crew.displayName)} in {Lib.Bold(habitat.part.partInfo.title)}", "Habitat is disabled !");

						for (int i = 0; i < partManifest.partCrew.Length; i++)
						{
							if (!string.IsNullOrEmpty(partManifest.partCrew[i]))
							{
								partManifest.RemoveCrewFromSeat(i);

								needRefresh |= true;
							}
						}

						if (needRefresh && habitat.part.PartActionWindow != null)
						{
							habitat.part.PartActionWindow.displayDirty = true;
							habitat.part.PartActionWindow.UpdateWindow();
						}
					}
					else
					{
						int crewCount = 0;
						for (int i = 0; i < partManifest.partCrew.Length; i++)
						{
							if (!string.IsNullOrEmpty(partManifest.partCrew[i]))
							{
								crewCount++;
							}
						}
						habData.crewCount = crewCount;
					}
				}
			}

			if (needRefresh)
			{
				// delay the RefreshCrewLists() call so other listeners to the onCrewDialogChange events don't get confused
				CrewAssignmentDialog.Instance.StartCoroutine(RefreshEditorCrewList(crewManifest));
			}
		}

		private IEnumerator RefreshEditorCrewList(VesselCrewManifest crewManifest)
		{
			yield return null;

			// RefreshCrewLists() will call EditorCrewChanged() by firing the onCrewDialogChange event again, so avoid an useless recursion.
			crewAssignementRefreshWasJustFiredFromCrewChanged = true;
			CrewAssignmentDialog.Instance.RefreshCrewLists(crewManifest, false, true);
			GameEvents.onEditorShipCrewModified.Fire(crewManifest);
		}

		internal void blah()
		{
			throw new NotImplementedException();
		}

		#endregion
	}

}
