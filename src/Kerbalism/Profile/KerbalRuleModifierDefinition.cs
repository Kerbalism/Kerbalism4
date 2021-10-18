using System;
using Flee.PublicTypes;

namespace KERBALISM
{
	public class KerbalRuleModifierDefinition
	{
		public const string NODENAME = "MODIFIER";

		[CFGValue] public string name;
		[CFGValue] public string title;
		[CFGValue] public double baseRate = 1.0;

		// If set to true, the sum of all other modifiers will be evaluated first
		// Then the result of that modifier will be added, but only if the result is
		// nearer from zero. Use this to prevent modifiers from "regenerating" a rule.
		[CFGValue] public bool cancelRateMode = false;
		public bool hasModifier = false;
		public IGenericExpression<double> rateModifier;

		public override string ToString() => name;

		public static KerbalRuleModifierDefinition Parse(ConfigNode modifierDefinitionNode, KerbalRuleDefinition ruleDefinition)
		{
			KerbalRuleModifierDefinition modifierDefinition = new KerbalRuleModifierDefinition();
			CFGValue.Parse(modifierDefinition, modifierDefinitionNode);
			string modifierStr = Lib.ConfigValue(modifierDefinitionNode, nameof(rateModifier), string.Empty);
			if (modifierStr.Length > 0)
			{
				try
				{
					Lib.ParseFleeExpressionResHandlerResourceCall(ref modifierStr);
					Lib.ParseFleeExpressionProcessCall(ref modifierStr);
					Lib.ParseFleeExpressionComfortCall(ref modifierStr);
					modifierDefinition.rateModifier = VesselDataBase.ExpressionBuilderInstance.ModifierContext.CompileGeneric<double>(modifierStr);
					modifierDefinition.hasModifier = true;
				}
				catch (Exception e)
				{
					ErrorManager.AddError(false, $"Can't parse MODIFIER '{modifierDefinition.name}' for KERBAL_RULE '{ruleDefinition.name}'", $"rateModifier: {modifierStr}\n{e.Message}");
					modifierDefinition.hasModifier = false;
				}
			}

			bool useDurationMultiplier = Lib.ConfigValue(modifierDefinitionNode, "useDurationMultiplier", false);
			if (Lib.ConfigDuration(modifierDefinitionNode, "baseRateDuration", useDurationMultiplier, out double baseRateDuration))
			{
				modifierDefinition.baseRate *= ruleDefinition.maxValue / baseRateDuration;
			}

			return modifierDefinition;
		}
	}
}
