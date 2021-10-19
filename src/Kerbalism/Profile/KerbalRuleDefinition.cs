using System;
using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM
{
	public class KerbalRuleDefinition
	{
		public static readonly List<KerbalRuleDefinition> definitions = new List<KerbalRuleDefinition>();
		public static readonly Dictionary<string, KerbalRuleDefinition> definitionsByName = new Dictionary<string, KerbalRuleDefinition>();

		public static void ParseDefinitions(ConfigNode[] rulesNodes)
		{
			foreach (ConfigNode ruleNode in rulesNodes)
			{
				KerbalRuleDefinition definition;
				try
				{
					definition = new KerbalRuleDefinition(ruleNode, definitions.Count);
				}
				catch (Exception e)
				{
					string name = Lib.ConfigValue(ruleNode, nameof(name), string.Empty);
					ErrorManager.AddError(true, $"Error parsing RULE `{name}`", e.Message);
					continue;
				}

				if (definitionsByName.ContainsKey(definition.name))
				{
					ErrorManager.AddError(false, $"Duplicate definition for RULE `{definition.name}`");
					continue;
				}

				definitions.Add(definition);
				definitionsByName.Add(definition.name, definition);
			}
		}

		[CFGValue] public string name;
		[CFGValue] public string title;
		[CFGValue] public string description;

		[CFGValue] public double maxValue = 1.0;
		[CFGValue] public double maxValueVariance = 0.0;
		[CFGValue] public double maxValueStupidityBonus = 0.0;
		[CFGValue] public double maxValueCourageBonus = 0.0;
		[CFGValue] public double maxValueBadassBonus = 0.0;
		[CFGValue] public double maxValueLevelBonus = 0.0;

		// if true, the value is set to maxValue on recovery
		// if false, the value is never reset
		[CFGValue] public bool resetOnRecovery;

		[CFGValue] public double warningThreshold = 0.7;
		[CFGValue] public double dangerThreshold = 0.9;

		[CFGValue] public string warningMessage;
		[CFGValue] public string dangerMessage;
		[CFGValue] public string fatalMessage;
		[CFGValue] public string relaxMessage;

		public Texture2D icon;

		// primary rules are shown in the kerbal summary UI ?
		// non-primary rules are only shown in the kerbal detailed state UI
		[CFGValue] public bool isPrimaryRule;

		public List<KerbalRuleModifierDefinition> modifiers = new List<KerbalRuleModifierDefinition>();
		public List<KerbalRuleEffectDefinition> effects = new List<KerbalRuleEffectDefinition>();

		public readonly int definitionIndex;

		public override string ToString() => name;

		public KerbalRuleDefinition(ConfigNode definitionNode, int definitionIndex)
		{
			this.definitionIndex = definitionIndex;
			CFGValue.Parse(this, definitionNode);

			if (string.IsNullOrEmpty(name))
				throw new Exception($"Rule definition has no name !");

			if (!name.IsValidNodeName(out char invalidChar))
				throw new Exception($"Rule name `{name}` contains the invalid character `{invalidChar}`");

			if (string.IsNullOrEmpty(title))
				title = name;

			string texturePath = string.Empty;
			if (definitionNode.TryGetValue(nameof(icon), ref texturePath))
			{
				icon = Lib.GetTexture(texturePath);
			}

			foreach (ConfigNode childNode in definitionNode.nodes)
			{
				if (childNode.name == KerbalRuleModifierDefinition.NODENAME)
				{
					KerbalRuleModifierDefinition modifierDefinition = KerbalRuleModifierDefinition.Parse(childNode, this);
					if (modifierDefinition != null)
						modifiers.Add(modifierDefinition);
				}
				else if (childNode.name == KerbalRuleEffectDefinition.NODENAME)
				{
					KerbalRuleEffectDefinition effectDefinition = KerbalRuleEffectDefinition.Parse(childNode, name);
					if (effectDefinition != null)
						effects.Add(effectDefinition);
				}
			}

			// sort modifiers by priority
			modifiers.Sort((a, b) => a.modifierPriority.CompareTo(b.modifierPriority));
		}

		public string TooltipText()
		{
			KsmString ks = KsmString.Get;
			ks.Format(title, KF.Center, KF.Bold, KF.KolorYellow).Break();

			if (!string.IsNullOrEmpty(description))
			{
				ks.Add(description).Break();
			}

			ks.Break();

			if (!resetOnRecovery)
			{
				ks.Format("Doesn't reset on recovery", KF.Center, KF.Bold, KF.Italic, KF.KolorOrange).Break().Break();
			}

			foreach (KerbalRuleEffectDefinition effect in effects)
			{
				ks.Format(KF.Concat("Effect", " : ", effect.title), KF.Center, KF.Bold, KF.KolorYellow).Break();

				if (effect.reputationPenalty > 0.0)
				{
					ks.Info("Reputation penality", effect.reputationPenalty.ToString("F0"));
				}

				if (effect.thresholdCurve != null)
				{
					ks.Info("Effect threshold", KF.Concat((effect.thresholdCurve.Evaluate(0f) * 100f).ToString("F0"), " - ", effect.thresholdCurve.Evaluate(1f).ToString("P0")));
				}
				else
				{
					ks.Info("Effect threshold", effect.threshold.ToString("P0"));
				}

				ks.Break();
			}

			return ks.End();

		}
	}
}
