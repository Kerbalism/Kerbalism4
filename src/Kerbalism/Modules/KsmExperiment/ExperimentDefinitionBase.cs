using System.Collections.Generic;
using System.Text;
using static KERBALISM.ExperimentRequirements;

namespace KERBALISM
{
	public class ExperimentDefinition : KsmModuleDefinition, IKsmModuleDefinitionLateInit
	{
		private ConfigNode definitionNode;

		public ExperimentInfo ExpInfo { get; protected set; }

		/// <summary> Id of the EXPERIMENT_DEFINITION </summary>
		[CFGValue] protected string ExperimentId { get; set; }

		/// <summary> EC requirement (units/second) </summary>
		[CFGValue] public double RequiredEC { get; private set; } = 0.01;

		/// <summary> don't show UI when the experiment is unavailable </summary>
		[CFGValue] public bool HideWhenInvalid { get; private set; } = false;

		/// <summary> if true, the experiment can run when shrouded (in bay or fairing) </summary>
		[CFGValue] public bool AllowShrouded { get; private set; } = true;

		/// <summary> Duration in seconds </summary>
		public double Duration { get; private set; } = 60.0;

		/// <summary> Data rate, automatically calculated from desired duration and experiments data size </summary>
		public double DataRate { get; private set; } // TODO : Require ExperimentInfo first !

		/// <summary> Operator crew. If set, crew has to be on vessel for the experiment to run </summary>
		public CrewSpecs CrewOperate { get; private set; }

		/// <summary> Experiment requirements </summary>
		public ExperimentRequirements Requirements { get; private set; }

		/// <summary> Resource requirements </summary>
		public List<ObjectPair<int, double>> Resources { get; private set; }

		private string description;

		public override string ToString() => $"{base.ToString()} - {ExperimentId}";

		// This happen while the ScienceDB isn't there yet, so we can't parse Requirements yet
		public override void OnLoad(ConfigNode definitionNode)
		{
			this.definitionNode = definitionNode;

			Duration = Lib.ConfigDuration(definitionNode, "Duration", true, "60s");
			CrewOperate = new CrewSpecs(Lib.ConfigValue(definitionNode, "CrewOperate", string.Empty));

			Resources = new List<ObjectPair<int, double>>();
			string resources = Lib.ConfigValue(definitionNode, "Resources", string.Empty);
			foreach (string s in Lib.Tokenize(resources, ','))
			{
				// definitions are Resource@rate
				var p = Lib.Tokenize(s, '@');
				if (p.Count != 2) continue;             // malformed definition
				if (!VesselResHandler.allKSPResourceIdsByName.TryGetValue(p[0], out int resId)) continue;    // unknown resource
				if (!double.TryParse(p[1], out double rate) || rate < double.Epsilon) continue;  // rate <= 0
				Resources.Add(new ObjectPair<int, double>(resId, rate));
			}

			for (int i = Resources.Count - 1; i >= 0; i--)
			{
				
			}
		}

		// Finish parsing the definition, now that the ScienceDB exists
		public virtual void OnLateInit()
		{
			if (string.IsNullOrEmpty(ExperimentId))
				return;

			ExpInfo = ScienceDB.GetExperimentInfo(ExperimentId);
			DataRate = ExpInfo.DataSize / Duration;
			Requirements = new ExperimentRequirements(Lib.ConfigValue(definitionNode, "Requirements", string.Empty));
			description = ModuleDescription(true);
		}

		public override string ModuleDescription<ModuleKsmExperiment>(ModuleKsmExperiment modulePrefab)
		{
			return description;
		}


		public string ModuleDescription(bool completeWithExperimentInfo)
		{
			KsmString ks = KsmString.Get;

			if (completeWithExperimentInfo)
				ks.Info("Base value", KF.ReadableScience(ExpInfo.ScienceCap));

			double expSize = ExpInfo.DataSize;
			if (this is SampleExperimentDefinition sampleDefinition)
			{
				ks.Info("Sample collecting", ExpInfo.SampleCollecting ? Local.Generic_YES : Local.Generic_NO);
				if (ExpInfo.SampleCollecting)
				{
					ks.Info("Part", sampleDefinition.SampleCollectingCargoPart.title);
					double amount = ExpInfo.sampleStorageParts[sampleDefinition.SampleCollectingCargoPart].SampleAmount;
					ks.Info(Local.Module_Experiment_Specifics_info6, amount.ToString("F2")); //"Samples"
					ks.Info(Local.Module_Experiment_Specifics_info4, (amount * ExpInfo.SampleVolume).ToString("0.0 L")); //"Sample size"
					ks.Info(Local.Module_Experiment_Specifics_info5, KF.ReadableMass(amount * ExpInfo.SampleMass)); //"Sample mass"
				}
				else
				{
					ks.Info(Local.Module_Experiment_Specifics_info4, ExpInfo.SampleVolume.ToString("0.0 L")); //"Sample size"
					ks.Info(Local.Module_Experiment_Specifics_info5, KF.ReadableMass(ExpInfo.SampleMass)); //"Sample mass"
				}

				if (Duration > 0)
					ks.Info(Local.Module_Experiment_Specifics_info3, KF.ReadableDuration(Duration));
			}
			else
			{
				ks.Info(Local.Module_Experiment_Specifics_info1, KF.ReadableDataSize(expSize)); //"Data size"
				if (DataRate > 0)
				{
					ks.Info(Local.Module_Experiment_Specifics_info2, KF.ReadableDataRate(DataRate));
					ks.Info(Local.Module_Experiment_Specifics_info3, KF.ReadableDuration(Duration));
				}
			}

			if (completeWithExperimentInfo && ExpInfo.IncludedExperiments.Count > 0)
			{
				ks.Break();
				ks.Format("Included experiments:", KF.KolorCyan, KF.Bold).Break();
				List<string> includedExpInfos = new List<string>();
				ExperimentInfo.GetIncludedExperimentTitles(ExpInfo, includedExpInfos);
				foreach (string includedExp in includedExpInfos)
					ks.Format(includedExp, KF.List);
			}

			if (completeWithExperimentInfo)
			{
				List<string> situations = ExpInfo.AvailableSituations();
				if (situations.Count > 0)
				{
					ks.Break();
					ks.Format(Local.Module_Experiment_Specifics_Situations, KF.KolorCyan, KF.Bold).Break(); //"Situations:"
					foreach (string s in situations)
						ks.Format(s, KF.Bold, KF.List);
				}

				if (ExpInfo.ExpBodyConditions.HasConditions)
				{
					ks.Break();
					ks.Add(ExpInfo.ExpBodyConditions.ConditionsToString()).Break();
				}
			}

			if (RequiredEC > 0 || Resources.Count > 0)
			{
				ks.Break();
				ks.Format(Local.Module_Experiment_Specifics_info8, KF.KolorCyan, KF.Bold).Break();//"Needs:"
				if (RequiredEC > 0)
					ks.Info(PartResourceLibrary.Instance.GetDefinition(PartResourceLibrary.ElectricityHashcode).displayName, KF.ReadableRate(RequiredEC));
				foreach (var p in Resources)
					ks.Info(PartResourceLibrary.Instance.GetDefinition(p.Key).displayName, KF.ReadableRate(p.Value));
			}

			if (CrewOperate)
			{
				ks.Break();
				ks.Info(Local.Module_Experiment_Specifics_info11, CrewOperate.Info());
			}

			if (Requirements.Requires.Length > 0)
			{
				ks.Break();
				ks.Format(Local.Module_Experiment_Requires, KF.KolorCyan, KF.Bold).Break();//"Requires:"
				foreach (RequireDef req in Requirements.Requires)
					ks.Info(ReqName(req.require), ReqValueFormat(req.require, req.value));
			}

			if (!AllowShrouded)
			{
				ks.Break();
				ks.Format("Unavailable while shrouded", KF.Bold).Break();
			}

			return ks.GetStringAndRelease();
		}
	}
}
