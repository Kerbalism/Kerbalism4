using HarmonyLib;
using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KERBALISM.Events
{
	// Create a OnAttemptBoard event that allow to prevent boardinga vessel from EVA
	// Called before any check has been done so the boarding can still fail due to stock restrictions
	// Called before anything (experiments/inventory...) has been transfered to the boarded vessel
	[HarmonyPatch(typeof(KerbalEVA))]
	[HarmonyPatch("BoardPart")]
	class KerbalEVA_BoardPart
	{
		static bool Prefix(KerbalEVA __instance, Part p)
		{
			// continue to BoardPart() if AttemptBoard return true 
			return GameEventsCrew.AttemptBoard(__instance, p);
		}
	}


	public class GameEventsCrew
	{
		public void OnKerbalLevelUp(ProtoCrewMember crew)
		{
			if (DB.TryGetKerbalData(crew, out KerbalData kerbalData))
			{
				foreach (KerbalRule rule in kerbalData.rules)
				{
					rule.UpdateMaxValue();
				}
			}
		}

		#region EVA EVENTS

		private static bool ignoreNextBoardAttemptDriveCheck = false;

		public static bool AttemptBoard(KerbalEVA instance, Part targetPart)
		{
			bool canBoard = false;
			if (targetPart != null && targetPart.TryGetFlightModuleDataOfType(out HabitatHandler habitatData))
			{
				canBoard =
					habitatData.pressureState == HabitatHandler.PressureState.Depressurized
					|| habitatData.pressureState == HabitatHandler.PressureState.AlwaysDepressurized
					|| habitatData.pressureState == HabitatHandler.PressureState.Breatheable;
			}

			if (!canBoard)
			{
				Message.Post($"Can't board {Lib.Bold(targetPart.partInfo.title)}", "Depressurize it first !");
				ignoreNextBoardAttemptDriveCheck = false;
				return canBoard;
			}

			if (!ignoreNextBoardAttemptDriveCheck)
			{
				double filesSize = 0.0;
				double fileCapacity = 0.0;
				int samplesSize = 0;
				int samplesCapacity = 0;

				if (instance.vessel.TryGetVesselData(out VesselData evaVessel))
				{
					foreach (DriveHandler drive in DriveHandler.GetDrives(evaVessel))
					{
						filesSize += drive.FilesSize();
						samplesSize += drive.SamplesSize();
					}
				}

				if (targetPart.vessel.TryGetVesselData(out VesselData boardedVessel))
				{
					foreach (DriveHandler drive in DriveHandler.GetDrives(boardedVessel))
					{
						fileCapacity += drive.FileCapacityAvailable();
						samplesCapacity += (int)drive.SampleCapacityAvailable();
					}
				}

				if (filesSize > fileCapacity || samplesSize > samplesCapacity)
				{
					DialogGUIButton cancel = new DialogGUIButton("#autoLOC_116009", delegate { }); // autoLOC_116009 : cancel
					Callback proceedCallback = delegate { ignoreNextBoardAttemptDriveCheck = true; instance.BoardPart(targetPart); }; // ignore this check on the method next call
					DialogGUIButton proceed = new DialogGUIButton("#autoLOC_116008", proceedCallback); // autoLOC_116008 : Board Anyway\n(Dump Experiments)

					string message = Lib.BuildString(
						string.Format("The vessel {0} doesn't have enough space to store all the experiments carried by {1}", targetPart.vessel.vesselName, instance.vessel.vesselName),
						"\n\n",
						"Files on EVA", " : ", Lib.HumanReadableDataSize(filesSize), " - ", "Storage capacity", " : ", Lib.HumanReadableDataSize(fileCapacity), "\n",
						"Samples on EVA", " : ", Lib.HumanReadableSampleSize(samplesSize), " - ", "Storage capacity", " : ", Lib.HumanReadableSampleSize(samplesCapacity), "\n\n",
						"If you proceed, some experiment results will be lost");

					PopupDialog.SpawnPopupDialog(
						new Vector2(0.5f, 0.5f),
						new Vector2(0.5f, 0.5f),
						new MultiOptionDialog("StoreExperimentsIssue", message, Localizer.Format("#autoLOC_116007"), HighLogic.UISkin, proceed, cancel), // autoLOC_116007 : Cannot store Experiments
						false,
						HighLogic.UISkin);

					return false;
				}
			}

			ignoreNextBoardAttemptDriveCheck = false;
			return true;
		}

		public void AttemptEVA(ProtoCrewMember crew, Part sourcePart, Transform hatchTransform)
		{
			FlightEVA.fetch.overrideEVA = true;
			if (sourcePart != null && sourcePart.TryGetFlightModuleDataOfType(out HabitatHandler habitatData))
			{
				FlightEVA.fetch.overrideEVA =
					!(habitatData.pressureState == HabitatHandler.PressureState.Depressurized
					|| habitatData.pressureState == HabitatHandler.PressureState.AlwaysDepressurized
					|| habitatData.pressureState == HabitatHandler.PressureState.Breatheable);
			}

			if (FlightEVA.fetch.overrideEVA)
			{
				Message.Post($"Can't go on EVA from {Lib.Bold(sourcePart.partInfo.title)}", "Depressurize it first !");
			}
		}

		public void ToEVA(GameEvents.FromToAction<Part, Part> data)
		{
			// setup supply resources capacity in the eva kerbal
			// This has to be before the vesseldata creation, so the reshandler is 
			// initialized with the correct capacities
			Profile.SetupEva(data.to);

			// get vessel data
			if (!data.to.vessel.TryGetVesselData(out VesselData evaVD))
			{
				Lib.LogDebug($"Creating VesselData for EVA Kerbal : {data.to.vessel.vesselName}");
				evaVD = new VesselData(data.to.vessel);
				DB.AddNewVesselData(evaVD);
			}

			VesselData vesselVD = data.from.vessel.GetVesselData();

			// remaining crew on the origin vessel plus the EVAing kerbal
			double totalCrew = Lib.CrewCount(data.from.vessel) + 1.0;

			string evaPropellant = Lib.EvaPropellantName();

			// for each resource in the kerbal
			foreach (PartResource partRes in data.to.Resources)
			{
				// get the resource
				VesselResource evaRes = evaVD.ResHandler.GetResource(partRes.resourceName);
				VesselResource vesselRes = vesselVD.ResHandler.GetResource(partRes.resourceName);

				// clamp request by how much is available
				double amountOnVessel = Math.Max(vesselRes.Amount + vesselRes.Deferred, 0.0);
				double amountRequested = Math.Min(evaRes.Capacity, amountOnVessel);

				// special handling for EVA propellant
				if (evaRes.Name == evaPropellant)
				{
					if (amountRequested < 0.5 && !vesselVD.EnvLanded)
					{
						// "There isn't any <<1>> in the EVA suit", "Don't let the ladder go!"
						Message.Post(Severity.danger,
							Local.CallBackMsg_EvaNoMP.Format("<b>" + evaPropellant + "</b>"), Local.CallBackMsg_EvaNoMP2);
					}
				}
				// for all ressources but propellant, only take this kerbal "share" if there isn't enough for everyone
				else if (amountRequested * totalCrew > amountOnVessel)
				{
					amountRequested = amountOnVessel / totalCrew;
				}

				// remove resource from vessel
				vesselRes.Consume(amountRequested);

				// add resource to eva kerbal
				evaRes.Produce(amountRequested);
			}

			// turn off headlamp light, to avoid stock bug that show them for a split second when going on eva
			//KerbalEVA kerbal = data.to.FindModuleImplementing<KerbalEVA>();
			//EVA.HeadLamps(kerbal, false);

			// execute script
			evaVD.computer.Execute(data.from.vessel, ScriptType.eva_out);

			VesselLifecycle.VesselModified(data.from.vessel);
		}


		public void FromEVA(GameEvents.FromToAction<Part, Part> data)
		{
			// contract configurator calls this event with both parts being the same when it adds a passenger
			if (data.from == data.to)
				return;

			VesselData evaVD = data.from.vessel.GetVesselData();
			VesselData vesselVD = data.to.vessel.GetVesselData();

			// for each resource in the eva kerbal, add leftovers to the vessel
			foreach (PartResource partRes in data.from.Resources)
			{
				vesselVD.ResHandler.GetResource(partRes.resourceName).Produce(partRes.amount);
			}

			// merge drives data
			DriveHandler.Transfer(evaVD, vesselVD, true);

			// forget EVA vessel data
			Cache.PurgeVesselCaches(data.from.vessel);

			// update boarded vessel
			VesselLifecycle.VesselModified(data.to.vessel);

			// execute script
			vesselVD.computer.Execute(data.to.vessel, ScriptType.eva_in);
		}

		#endregion
	}
}
