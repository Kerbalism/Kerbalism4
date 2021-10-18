using System.Collections.Generic;
using System.Text;
using static KERBALISM.ExperimentRequirements;

namespace KERBALISM
{
	public class ExperimentDefinition : KsmModuleDefinition, IKsmModuleDefinitionLateInit
	{
		private static StringBuilder sb = new StringBuilder();

		private ConfigNode definitionNode;

		public ExperimentInfo ExpInfo { get; private set; }

		/// <summary> Id of the EXPERIMENT_DEFINITION </summary>
		[CFGValue] protected string ExperimentId { get; set; }

		/// <summary> EC requirement (units/second) </summary>
		[CFGValue] public double RequiredEC { get; private set; } = 0.01;

		/// <summary> If true, the experiment will generate mass out of nothing (surface samples) </summary>
		[CFGValue] public bool SampleCollecting { get; private set; } = false;

		/// <summary> the amount of samples this unit is shipped with </summary>
		[CFGValue] public double Samples { get; private set; } = 0.0;

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
		}

		// Finish parsing the definition, now that the ScienceDB exists
		public void OnLateInit()
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
			sb.Clear();

			if (completeWithExperimentInfo)
				sb.AppendInfo("Base value", Lib.HumanReadableScience(ExpInfo.ScienceCap, true, true));

			double expSize = ExpInfo.DataSize;
			if (ExpInfo.SampleMass == 0.0)
			{
				sb.AppendInfo(Local.Module_Experiment_Specifics_info1, Lib.HumanReadableDataSize(expSize)); //"Data size"
				if (DataRate > 0)
				{
					sb.AppendInfo(Local.Module_Experiment_Specifics_info2, Lib.HumanReadableDataRate(DataRate));
					sb.AppendInfo(Local.Module_Experiment_Specifics_info3, Lib.HumanReadableDuration(Duration));
				}
			}
			else
			{
				sb.AppendInfo(Local.Module_Experiment_Specifics_info4, Lib.HumanReadableSampleSize(expSize));//"Sample size"
				sb.AppendInfo(Local.Module_Experiment_Specifics_info5, Lib.HumanReadableMass(ExpInfo.SampleMass));//"Sample mass"
				if (ExpInfo.SampleMass > 0.0 && !SampleCollecting)
					sb.AppendInfo(Local.Module_Experiment_Specifics_info6, Lib.BuildString(Samples.ToString("F2"), " (", Lib.HumanReadableMass(ExpInfo.SampleMass * Samples), ")"));//"Samples"
				if (Duration > 0)
					sb.AppendInfo(Local.Module_Experiment_Specifics_info7_sample, Lib.HumanReadableDuration(Duration));
			}

			if (completeWithExperimentInfo && ExpInfo.IncludedExperiments.Count > 0)
			{
				sb.AppendKSPNewLine();
				sb.AppendKSPLine(Lib.Color("Included experiments:", Lib.Kolor.Cyan, true));
				List<string> includedExpInfos = new List<string>();
				ExperimentInfo.GetIncludedExperimentTitles(ExpInfo, includedExpInfos);
				foreach (string includedExp in includedExpInfos)
				{
					sb.AppendList(includedExp);
				}
			}

			if (completeWithExperimentInfo)
			{
				List<string> situations = ExpInfo.AvailableSituations();
				if (situations.Count > 0)
				{
					sb.AppendKSPNewLine();
					sb.AppendKSPLine(Lib.Color(Local.Module_Experiment_Specifics_Situations, Lib.Kolor.Cyan, true));//"Situations:"
					foreach (string s in situations)
						sb.AppendList(Lib.Bold(s));
				}
			}

			if (completeWithExperimentInfo && ExpInfo.ExpBodyConditions.HasConditions)
			{
				sb.AppendKSPNewLine();
				sb.AppendKSPLine(ExpInfo.ExpBodyConditions.ConditionsToString());
			}

			if (RequiredEC > 0 || Resources.Count > 0)
			{
				sb.AppendKSPNewLine();
				sb.AppendKSPLine(Lib.Color(Local.Module_Experiment_Specifics_info8, Lib.Kolor.Cyan, true));//"Needs:"
				if (RequiredEC > 0)
					sb.AppendInfo(PartResourceLibrary.Instance.GetDefinition(PartResourceLibrary.ElectricityHashcode).displayName, Lib.HumanReadableRate(RequiredEC));
				foreach (var p in Resources)
					sb.AppendInfo(PartResourceLibrary.Instance.GetDefinition(p.Key).displayName, Lib.HumanReadableRate(p.Value));
			}

			if (CrewOperate)
			{
				sb.AppendKSPNewLine();
				sb.AppendInfo(Local.Module_Experiment_Specifics_info11, CrewOperate.Info());
			}

			if (Requirements.Requires.Length > 0)
			{
				sb.AppendKSPNewLine();
				sb.AppendKSPLine(Lib.Color(Local.Module_Experiment_Requires, Lib.Kolor.Cyan, true));//"Requires:"
				foreach (RequireDef req in Requirements.Requires)
					sb.AppendInfo(Lib.BuildString("• <b>", ReqName(req.require), "</b>"), ReqValueFormat(req.require, req.value));
			}

			if (!AllowShrouded)
			{
				sb.AppendKSPNewLine();
				sb.AppendKSPLine(Lib.Bold("Unavailable while shrouded"));
			}

			return sb.ToString();
		}
	}
}
