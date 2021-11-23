using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public class ExperimentHandlerUtils
	{
		public enum ExpStatus { Stopped, Running, Forced, Waiting, Issue }
		public enum RunningState { Stopped, Running, Forced }

		public static string RunningStateInfo(RunningState state)
		{
			switch (state)
			{
				case RunningState.Stopped: return Lib.Color(Local.Module_Experiment_runningstate1, Lib.Kolor.Yellow);//"stopped"
				case RunningState.Running: return Lib.Color(Local.Module_Experiment_runningstate2, Lib.Kolor.Green);//"started"
				case RunningState.Forced: return Lib.Color(Local.Module_Experiment_runningstate3, Lib.Kolor.Red);//"forced run"
				default: return string.Empty;
			}

		}

		public static string StatusInfo(ExpStatus status, double currentDataRate, double maxDataRate)
		{
			KsmString ks = KsmString.Get;

			switch (status)
			{
				case ExpStatus.Stopped: ks.Format(Local.Module_Experiment_runningstate1, KF.KolorYellow); break;//"stopped"
				case ExpStatus.Running: ks.Format(Local.Module_Experiment_runningstate5, KF.KolorGreen); break;//"running"
				case ExpStatus.Forced: ks.Format(Local.Module_Experiment_runningstate3, KF.KolorRed); break;//"forced run"
				case ExpStatus.Waiting: ks.Format(Local.Module_Experiment_runningstate6, KF.KolorScience); break;//"waiting"
				case ExpStatus.Issue: ks.Format(Local.Module_Experiment_issue_title, KF.KolorOrange); break;//"issue"
			}

			if (currentDataRate > 0.0)
			{
				ks.Add(" (");
				if (currentDataRate < maxDataRate)
					ks.ReadableDataRateCompared(currentDataRate, maxDataRate);
				else
					ks.ReadableDataRate(currentDataRate);
				ks.Add(")");
			}

			return ks.GetStringAndRelease();
		}

		public static string RunningCountdown(ExperimentDefinition definition, SubjectData subjectData, double currentDataRate, bool compact = true)
		{
			double count;

			if (currentDataRate == 0.0)
				currentDataRate = definition.DataRate;

			if (subjectData != null)
				count = Math.Max(1.0 - subjectData.PercentCollectedTotal, 0.0) * (definition.ExpInfo.DataSize / currentDataRate);
			else
				count = definition.ExpInfo.DataSize / currentDataRate;

			return Lib.HumanReadableCountdown(count, compact);
		}

		public static string ScienceValue(SubjectData subjectData)
		{
			if (subjectData != null)
				return Lib.BuildString(Lib.HumanReadableScience(subjectData.ScienceCollectedTotal), " / ", Lib.HumanReadableScience(subjectData.ScienceMaxValue));
			else
				return Lib.Color(Local.Module_Experiment_ScienceValuenone, Lib.Kolor.Science, true);//"none"
		}

		private static HashSet<ExperimentInfo> editorRunningExperiments = new HashSet<ExperimentInfo>();

		public static void CheckEditorExperimentMultipleRun()
		{
			foreach (PartData partData in VesselDataShip.ShipParts.AllLoadedParts)
			{
				for (int i = 0; i < partData.modules.Count; i++)
				{
					if (partData.modules[i] is IExperimentHandler expHandler && expHandler.HandlerIsEnabled && expHandler.IsRunningRequested)
					{
						if (editorRunningExperiments.Contains(expHandler.ExperimentInfo))
						{
							expHandler.Toggle();
						}
						else
						{
							editorRunningExperiments.Add(expHandler.ExperimentInfo);
						}
					}
				}
			}

			editorRunningExperiments.Clear();
		}
	}
}
