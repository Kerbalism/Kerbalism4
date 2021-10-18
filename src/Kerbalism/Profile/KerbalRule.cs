using System;
using System.Collections.Generic;
using System.Text;

namespace KERBALISM
{
	public class KerbalRule
	{
		public enum WarningState
		{
			none = 0,
			warning = 1,
			danger = 2
		}

		public const string TOPNODENAME = "KERBAL_RULES";

		private KerbalData kerbalData;
		public KerbalRuleDefinition Definition { get; private set; }
		private List<KerbalRuleEffect> effects = new List<KerbalRuleEffect>(); // serialized
		public List<KerbalRuleModifier> Modifiers { get; private set; } = new List<KerbalRuleModifier>(); // not serialized, for UI purpose only

		public KerbalData KerbalData => kerbalData;
		public double Value { get; private set; } // serialized
		public double MaxValue { get; private set; } // not serialized, recalculated on loads and kerbals level up
		public double Level { get; private set; } // convenience property
		public double ChangeRate { get; private set; } = 0.0;
		public double LevelChangeRate { get; private set; } = 0.0;
		public WarningState State { get; private set; } = WarningState.none;
		public double TimeToMaxValue => ChangeRate != 0.0 ? (MaxValue - Value) / ChangeRate : double.PositiveInfinity;
		public List<string[]> MaxValueInfo { get; private set; } = new List<string[]>();

		public override string ToString() => Definition.name + " " + Level.ToString("P2") + " (" + LevelChangeRate.ToString("P2") + ") : " + Value.ToString("F5") + "/" + MaxValue.ToString("F5");

		public KerbalRule(KerbalData kerbalData, KerbalRuleDefinition ruleDefinition, bool initialize)
		{
			this.kerbalData = kerbalData;
			Definition = ruleDefinition;

			foreach (KerbalRuleEffectDefinition effectDefinition in Definition.effects)
			{
				effects.Add(new KerbalRuleEffect(effectDefinition));
			}

			foreach (KerbalRuleModifierDefinition modifierDefinition in Definition.modifiers)
			{
				Modifiers.Add(new KerbalRuleModifier(modifierDefinition));
			}

			if (initialize)
			{
				Initialize();
			}

			UpdateMaxValue();
		}

		private void Initialize()
		{
			Value = 0.0;

			foreach (KerbalRuleEffect effect in effects)
			{
				effect.Initialize();
			}
		}

		public static void Save(KerbalData kerbal, ConfigNode kerbalDataNode)
		{
			ConfigNode rulesNode = kerbalDataNode.AddNode(TOPNODENAME);

			foreach (KerbalRule rule in kerbal.rules)
			{
				ConfigNode ruleNode = rulesNode.AddNode(rule.Definition.name);
				ruleNode.AddValue("value", rule.Value);

				foreach (KerbalRuleEffect effect in rule.effects)
				{
					effect.Save(ruleNode);
				}
			}
		}

		public static void Load(KerbalData kerbal, ConfigNode kerbalDataNode)
		{
			ConfigNode rulesNode = kerbalDataNode.GetNode(TOPNODENAME);
			if (rulesNode == null)
				rulesNode = new ConfigNode();

			foreach (KerbalRule rule in kerbal.rules)
			{
				ConfigNode ruleNode = rulesNode.GetNode(rule.Definition.name);
				if (ruleNode == null)
				{
					rule.Initialize();
				}
				else
				{
					rule.Value = Lib.ConfigValue(ruleNode, "value", 0.0);
					rule.Level = rule.Value / rule.MaxValue;

					foreach (KerbalRuleEffect effect in rule.effects)
					{
						ConfigNode effectNode = ruleNode.GetNode(effect.definition.name.ToString());
						if (effectNode == null)
						{
							effect.Initialize();
						}
						else
						{
							effect.Load(effectNode);
						}
					}
				}
			}
		}

		public void UpdateMaxValue()
		{
			MaxValue = GetMaxValue();
			Level = Value / MaxValue;
			LevelChangeRate = ChangeRate / MaxValue;
			State = GetWarningState();
		}

		private WarningState GetWarningState()
		{
			if (kerbalData.RulesEnabled)
				return WarningState.none;

			if (Level > Definition.dangerThreshold)
				return WarningState.danger;
			else if (Level > Definition.warningThreshold)
				return WarningState.warning;

			return WarningState.none;
		}

		private double GetMaxValue()
		{
			double baseValue = Definition.maxValue;
			double variance = 0.0;
			// variance is deterministic, using a seed derived from the kerbal name and the rule name
			if (Definition.maxValueVariance != 0.0)
				variance = Lib.RandomDeterministic(kerbalData.stockKerbal.name + Definition.name, Definition.maxValueVariance * -0.5, Definition.maxValueVariance * 0.5);

			double stupidityBonus = kerbalData.stockKerbal.stupidity * Definition.maxValueStupidityBonus;
			double courageBonus = kerbalData.stockKerbal.courage * Definition.maxValueCourageBonus;
			double badassBonus = kerbalData.stockKerbal.isBadass ? Definition.maxValueBadassBonus : 0.0;
			double levelBonus = kerbalData.stockKerbal.experienceLevel * Definition.maxValueLevelBonus;

			double maxValue = baseValue + variance + stupidityBonus + courageBonus + badassBonus + levelBonus;

			MaxValueInfo.Clear();

			if (variance != 0.0)
			{
				MaxValueInfo.Add(new []{"Base", (variance / baseValue).ToString("+0.0 %;-0.0 %") });
			}

			if (stupidityBonus != 0.0)
			{
				MaxValueInfo.Add(new[] { "Stupidity", (stupidityBonus / baseValue).ToString("+0.0 %;-0.0 %") });
			}

			if (courageBonus != 0.0)
			{
				MaxValueInfo.Add(new[] { "Courage", (courageBonus / baseValue).ToString("+0.0 %;-0.0 %") });
			}

			if (badassBonus != 0.0)
			{
				MaxValueInfo.Add(new[] { "Badass", (badassBonus / baseValue).ToString("+0.0 %;-0.0 %") });
			}

			if (levelBonus != 0.0)
			{
				MaxValueInfo.Add(new[] { "Level", (levelBonus / baseValue).ToString("+0.0 %;-0.0 %") });
			}

			double totalBonus = (maxValue / baseValue) - baseValue;
			if (totalBonus != 0.0)
			{
				MaxValueInfo.Add(new[] { "Total bonus", totalBonus.ToString("P1") });
			}

			return maxValue;
		}

		public void OnFixedUpdate(VesselDataBase vesselData, double elapsedSec)
		{
			if (!kerbalData.RulesEnabled
			    || kerbalData.stockKerbal.rosterStatus == ProtoCrewMember.RosterStatus.Dead
			    || kerbalData.stockKerbal.rosterStatus == ProtoCrewMember.RosterStatus.Missing)
				return;

			ChangeRate = 0.0;
			// TODO : evaluating the modifier for each kerbal is redundant and inefficient
			foreach (KerbalRuleModifier modifier in Modifiers)
			{
				modifier.Evaluate(vesselData);

				if (modifier.currentRate != 0.0 && modifier.Definition.cancelRateMode)
				{
					if (ChangeRate > 0.0)
					{
						if (modifier.currentRate < 0.0)
						{
							modifier.currentRate = Math.Max(modifier.currentRate, -ChangeRate);
						}
						else
						{
							modifier.currentRate = 0.0;
						}
					}
					else if (ChangeRate < 0.0)
					{
						if (modifier.currentRate < 0.0)
						{
							modifier.currentRate = 0.0;
						}
						else
						{
							modifier.currentRate = Math.Min(modifier.currentRate, -ChangeRate);
						}
					}
					else
					{
						modifier.currentRate = 0.0;
					}
				}

				ChangeRate += modifier.currentRate;
			}

			Value = Lib.Clamp(Value + (ChangeRate * elapsedSec), 0.0, MaxValue);
			Level = Value / MaxValue;
			LevelChangeRate = ChangeRate / MaxValue;
			State = GetWarningState();

			foreach (KerbalRuleEffect effect in effects)
			{
				if (effect.cooldown > 0.0)
				{
					effect.cooldown -= elapsedSec;
					if (effect.cooldown <= 0.0)
					{
						effect.cooldown = 0.0;
						if (effect.isOnCooldown)
						{
							effect.definition.CooldownExpired(vesselData, kerbalData, effect);
						}
					}
				}

				effect.customData?.Evaluate(this, elapsedSec);

				if (effect.cooldown > 0.0 || Level < effect.nextThreshold)
					continue;

				bool waitForOtherEffectCooldown = false;
				if (effects.Count > 1)
				{
					foreach (KerbalRuleEffect otherEffect in effects)
					{
						if (otherEffect.isOnCooldown && otherEffect != effect && otherEffect.definition.weight == effect.definition.weight)
						{
							waitForOtherEffectCooldown = true;
							break;
						}
					}
				}

				if (!waitForOtherEffectCooldown)
				{
					effect.definition.ApplyEffect(vesselData, kerbalData, effect);
					if (effect.definition.HasRuleRecovery)
					{
						
						Value = Math.Max(Value * (1.0 - effect.definition.RuleRecovery), 0.0);
						Level = Value / MaxValue;
						LevelChangeRate = ChangeRate / MaxValue;
					}
				}
			}
		}

		public void OnVesselRecovered()
		{
			if (Definition.resetOnRecovery)
			{
				Value = 0.0;
				Level = 0.0;
			}
		}
	}
}
