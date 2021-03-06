using System.Collections.Generic;
using HarmonyLib;
using Expansions.Serenity.DeployedScience.Runtime;

namespace KERBALISM
{
	[HarmonyPatch(typeof(DeployedScienceExperiment))]
	[HarmonyPatch("SendDataToComms")]
	class DeployedScienceExperiment_SendDataToComms {
		static bool Prefix(DeployedScienceExperiment __instance, ref bool __result) {
			// get private vars
			ScienceSubject subject = Lib.ReflectionValue<ScienceSubject>(__instance, "subject");
			float storedScienceData = Lib.ReflectionValue<float>(__instance, "storedScienceData");
			float transmittedScienceData = Lib.ReflectionValue<float>(__instance, "transmittedScienceData");
			Vessel ControllerVessel = Lib.ReflectionValue<Vessel>(__instance, "ControllerVessel");
			//Lib.Log("SendDataToComms!: " + subject.title);
			if (__instance.Experiment != null && !(__instance.ExperimentVessel == null) && subject != null && !(__instance.Cluster == null) && __instance.sciencePart.Enabled && !(storedScienceData <= 0f) && __instance.ExperimentSituationValid) {
			/*	if (!__instance.TimeToSendStoredData())
				{
					__result = true;
					Lib.Log(Lib.BuildString("BREAKING GROUND bailout 1"));
					return false;
				} */
				
				if(ControllerVessel == null && __instance.Cluster != null)
				{
					Lib.ReflectionCall(__instance, "SetControllerVessel");
					ControllerVessel = Lib.ReflectionValue<Vessel>(__instance, "ControllerVessel");
				}

				/*
				Part control;
				FlightGlobals.FindLoadedPart(__instance.Cluster.ControlModulePartId, out control);
				if(control == null) {
					//Lib.Log("DeployedScienceExperiment: couldn't find control module");
					__result = true;
					Lib.Log(Lib.BuildString("BREAKING GROUND bailout 2"));
					return false;
				}
				*/

				SubjectData subjectData = ScienceDB.GetSubjectDataFromStockId(subject.id);
				double sciencePerMB = subjectData.SciencePerMB;
				if (sciencePerMB == 0.0)
				{
					Lib.Log($"SciencePerMB is 0 for {subjectData.FullTitle} !", Lib.LogLevel.Error);
					__result = false;
					return false;
				}
				float scienceValue = storedScienceData * subject.subjectValue;
				double dataSize = scienceValue / subjectData.SciencePerMB;

				DriveHandler drive = DriveHandler.FindBestDriveForFile(ControllerVessel.GetVesselData(), null, out _);
				if (drive == null)
				{
					__result = true;
					return false;
				}

				ScienceFile file = drive.RecordFile(subjectData, dataSize);
				if (file == null)
				{
					__result = true;
					return false;
				}

				Lib.ReflectionValue<float>(__instance, "transmittedScienceData", transmittedScienceData + scienceValue);
				Lib.ReflectionValue<float>(__instance, "storedScienceData", 0f);
				__result = false;
			}

			return false; // always return false so we don't continue to the original code
		}
	}
}
