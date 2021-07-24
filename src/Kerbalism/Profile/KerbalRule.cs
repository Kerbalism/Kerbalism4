using Experience;
using Flee.PublicTypes;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KERBALISM
{
	public class KerbalRule
	{
		public const string TOPNODENAME = "KERBAL_RULES";
		private static StringBuilder sb = new StringBuilder();

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
			if (!kerbalData.RulesEnabled || kerbalData.stockKerbal.rosterStatus != ProtoCrewMember.RosterStatus.Assigned)
				return;

			ChangeRate = 0.0;
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

	public class KerbalRuleEffect
	{
		public KerbalRuleEffectDefinition definition;
		public double nextThreshold;
		public double cooldown;
		public bool isOnCooldown;
		public KerbalRuleEffectData customData;

		public KerbalRuleEffect(KerbalRuleEffectDefinition effectDefinition)
		{
			definition = effectDefinition;
		}

		public void Initialize()
		{
			nextThreshold = definition.NextThreshold();
			cooldown = 0.0;
			isOnCooldown = false;
			customData = definition.GetCustomData();
		}

		public void Save(ConfigNode ruleNode)
		{
			ConfigNode effectNode = ruleNode.AddNode(definition.name.ToString());
			effectNode.AddValue("nextThreshold", nextThreshold);
			effectNode.AddValue("cooldown", cooldown);
			effectNode.AddValue("isOnCooldown", isOnCooldown);
			customData?.Save(effectNode);
		}

		public void Load(ConfigNode effectNode)
		{
			nextThreshold = Lib.ConfigValue(effectNode, "nextThreshold", definition.NextThreshold());
			cooldown = Lib.ConfigValue(effectNode, "cooldown", 0.0);
			isOnCooldown = Lib.ConfigValue(effectNode, "isOnCooldown", false);
			customData = KerbalRuleEffectData.Load(definition, effectNode);
		}
	}

	public abstract class KerbalRuleEffectData
	{
		public static KerbalRuleEffectData Load(KerbalRuleEffectDefinition definition, ConfigNode effectNode)
		{
			KerbalRuleEffectData effectData = definition.GetCustomData();
			if (effectData != null)
				effectData.OnLoad(effectNode);

			return effectData;
		}

		public void Save(ConfigNode effectNode)
		{
			OnSave(effectNode);
		}

		public abstract void Evaluate(KerbalRule rule, double elapsedSec);

		protected abstract void OnLoad(ConfigNode effectNode);
		protected abstract void OnSave(ConfigNode effectNode);
	}

	public class KerbalRuleModifier
	{
		public KerbalRuleModifierDefinition Definition { get; private set; }
		public double currentRate;

		public KerbalRuleModifier(KerbalRuleModifierDefinition modifierDefinition)
		{
			Definition = modifierDefinition;
			currentRate = Definition.baseRate;
		}

		public void Evaluate(VesselDataBase vesselData)
		{
			if (!Definition.hasModifier)
				return;

			Definition.rateModifier.Owner = vesselData;
			currentRate = Definition.baseRate * Definition.rateModifier.Evaluate();
		}
	}

	public class KerbalRuleDefinition
	{
		public static Dictionary<string, KerbalRuleDefinition> Library { get; private set; } = new Dictionary<string, KerbalRuleDefinition>();

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

		public override string ToString() => name;

		public KerbalRuleDefinition(ConfigNode definitionNode)
		{
			CFGValue.Parse(this, definitionNode);

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

			// put modifiers with affectRateOnly = true at the end of the list
			modifiers.Sort((a, b) => a.cancelRateMode.CompareTo(b.cancelRateMode));
		}

		private static StringBuilder sb = new StringBuilder();

		public string TooltipText()
		{
			sb.Clear();
			sb.AppendAlignement(Lib.Color(title, Lib.Kolor.Yellow, true), TextAlignment.Center);
			sb.AppendKSPNewLine();
			if (!string.IsNullOrEmpty(description))
			{
				sb.Append(description);
				sb.AppendKSPNewLine();
			}

			sb.AppendKSPNewLine();

			if (!resetOnRecovery)
			{
				sb.AppendAlignement(Lib.Color("Doesn't reset on recovery", Lib.Kolor.Orange, true), TextAlignment.Center);
				sb.AppendKSPNewLine();
			}

			foreach (KerbalRuleEffectDefinition effect in effects)
			{
				sb.AppendColor(Lib.BuildString("Effect", " : ", effect.title), Lib.Kolor.Yellow, true);
				sb.AppendKSPNewLine();

				if (effect.reputationPenalty > 0.0)
				{
					sb.AppendInfo("Reputation penality", effect.reputationPenalty.ToString("F0"));
				}

				if (effect.thresholdCurve != null)
				{
					sb.AppendInfo("Effect threshold", Lib.BuildString((effect.thresholdCurve.Evaluate(0f) * 100f).ToString("F0"), " - ", effect.thresholdCurve.Evaluate(1f).ToString("P0")));
				}
				else
				{
					sb.AppendInfo("Effect threshold", effect.threshold.ToString("P0"));
				}
				sb.AppendKSPNewLine();

			}

			return sb.ToString();

		}
	}

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

	public abstract class KerbalRuleEffectDefinition
	{
		public const string NODENAME = "EFFECT";
		public const double EffectFailureCooldown = 60.0 * 60.0 * 6.0;

		public enum EffectType
		{
			undefined,
			inactive,
			fatality,
			lostScience,
			lostResource,
			componentFailure
		}

		[CFGValue] public EffectType name;
		[CFGValue] public string title;
		[CFGValue] public string description;
		[CFGValue] public string message;
		[CFGValue] public string cooldownMessage;

		// [0;1] value / maxValue percentage threshold for that effect to happen.
		[CFGValue] public double threshold = 1.0;

		// Floatcurve to define the threshold as a probability. First key must be 0.0, last key must be 1.0, value are the threshold in the [0,1] range.
		public FloatCurve thresholdCurve;

		// If another effect having the same weight is on cooldown for the KerbalValue, this effect won't be
		// triggered until the cooldown has expired.
		[CFGValue] public int weight = 0;

		// duration of the effect in seconds. Effects of the same weight can't happen again until the end of that cooldown
		// set to 0.0 for the effect to never expire.
		public double cooldown;

		// max random variance on `cooldown` (positive or negative modifier : total variance is twice that)
		public double cooldownVariance;

		[CFGValue] public float reputationPenalty = 0f;

		[CFGValue] protected double ruleRecovery = 0.0;
		[CFGValue] protected double ruleRecoveryVariance = 0.0;

		public bool HasRuleRecovery => ruleRecovery > 0.0;
		public double RuleRecovery => ruleRecoveryVariance > 0.0 ? ruleRecovery + Lib.RandomDouble(-ruleRecoveryVariance, ruleRecoveryVariance) : ruleRecovery;

		public override string ToString() => name.ToString();

		public static KerbalRuleEffectDefinition Parse(ConfigNode effectDefinitionNode, string kerbalValueName)
		{
			EffectType type = Lib.ConfigEnum(effectDefinitionNode, "name", EffectType.undefined);
			KerbalRuleEffectDefinition instance;

			switch (type)
			{
				case EffectType.inactive:
					instance = new KerbalRuleEffectInactive();
					break;
				case EffectType.fatality:
					instance = new KerbalRuleEffectFatality();
					break;
				case EffectType.lostScience:
					if (!Features.Science)
						return null;
					instance = new KerbalRuleEffectLostScience();
					break;
				case EffectType.lostResource:
					instance = new KerbalRuleEffectLostResource();
					break;
				case EffectType.componentFailure:
					if (!Features.Failures)
						return null;
					instance = new KerbalRuleEffectFailure();
					break;
				default:
					ErrorManager.AddError(false, $"Unknown EFFECT name for KERBAL_RULE '{kerbalValueName}'");
					return null;
			}

			instance.name = type; 
			instance.Parse(effectDefinitionNode);
			return instance;
		}

		public void Parse(ConfigNode effectDefinitionNode)
		{
			CFGValue.Parse(this, effectDefinitionNode);

			if (!Lib.ConfigDuration(effectDefinitionNode, nameof(cooldown), false, out cooldown))
			{
				cooldown = 0.0;
			}

			if (!Lib.ConfigDuration(effectDefinitionNode, nameof(cooldownVariance), false, out cooldownVariance))
			{
				cooldownVariance = 0.0;
			}

			if (ruleRecovery > 0.0 && (ruleRecoveryVariance > ruleRecovery || ruleRecovery + ruleRecoveryVariance > 1.0))
			{
				ErrorManager.AddError(false, $"Error in EFFECT '{name}' configuration", $"ruleRecovery ({ruleRecovery}) +/- ruleRecoveryVariance ({ruleRecoveryVariance}) must be in the [0, 1] range");
				ruleRecovery = 0.0;
			}

			if (cooldown > 0.0 && cooldownVariance > cooldown)
			{
				ErrorManager.AddError(false, $"Error in EFFECT '{name}' configuration", $"cooldownVariance ({cooldownVariance}) must be less than cooldown ({cooldown})");
				cooldownVariance = 0.0;
			}

			ConfigNode thresholdCurveNode = effectDefinitionNode.GetNode("THRESHOLD_CURVE");

			if (thresholdCurveNode != null)
			{
				thresholdCurve = new FloatCurve();
				thresholdCurve.Load(thresholdCurveNode);
			}

			OnParse(effectDefinitionNode);
		}

		protected virtual void OnParse(ConfigNode effectDefinitionNode) { }

		public void ApplyEffect(VesselDataBase vd, KerbalData kd, KerbalRuleEffect effect)
		{
			if (!OnApplyEffect(vd, kd, effect, out string message))
			{
				Lib.LogDebug($"Effect {GetType().Name} wasn't applied to {kd} on {vd} : conditions not met. Putting effect on cooldown.");
				effect.cooldown = EffectFailureCooldown;
				return;
			}

			if (cooldown > 0.0)
			{
				effect.isOnCooldown = true;
				effect.cooldown = cooldown;
				if (cooldownVariance > 0.0)
				{
					effect.cooldown += Lib.RandomDouble(-cooldownVariance, cooldownVariance);
				}
			}

			if (reputationPenalty > 0f && HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
			{
				Reputation.Instance.AddReputation(-reputationPenalty, TransactionReasons.Any);
			}

			// TODO : include custom message
			Message.Post($"Effect {GetType().Name} applied to {kd} on {vd}", message);
		}

		protected abstract bool OnApplyEffect(VesselDataBase vd, KerbalData kd, KerbalRuleEffect effect, out string message);

		public void CooldownExpired(VesselDataBase vd, KerbalData kd, KerbalRuleEffect effect)
		{
			effect.isOnCooldown = false;
			OnCooldownExpired(vd, kd, effect);
		}

		protected virtual void OnCooldownExpired(VesselDataBase vd, KerbalData kd, KerbalRuleEffect effect) { }

		public virtual KerbalRuleEffectData GetCustomData() => null;

		public double NextThreshold()
		{
			if (thresholdCurve != null)
			{
				return Lib.Clamp(thresholdCurve.Evaluate(Lib.RandomFloat()), 0f, 1f);
			}
			else
			{
				return threshold;
			}
		}
	}



	public class KerbalRuleEffectInactive : KerbalRuleEffectDefinition
	{
		private class KerbalRuleEffectInactiveData : KerbalRuleEffectData
		{
			public string originalTrait = string.Empty;
			public double remaining = 0.0;

			protected override void OnLoad(ConfigNode effectNode)
			{
				originalTrait = Lib.ConfigValue(effectNode, nameof(originalTrait), string.Empty);
				remaining = Lib.ConfigValue(effectNode, nameof(remaining), 0.0);
			}

			protected override void OnSave(ConfigNode effectNode)
			{
				if (remaining > 0.0)
				{
					effectNode.AddValue(nameof(originalTrait), originalTrait);
					effectNode.AddValue(nameof(remaining), remaining);
				}
			}

			public override void Evaluate(KerbalRule rule, double elapsedSec)
			{
				if (remaining <= 0.0)
					return;

				remaining -= elapsedSec;

				if (remaining <= 0.0)
				{
					remaining = 0.0;
					originalTrait = string.Empty;
					KerbalData kd = rule.KerbalData;

					if (string.IsNullOrEmpty(originalTrait) || !GameDatabase.Instance.ExperienceConfigs.TraitNames.Contains(originalTrait))
					{
						Lib.Log($"Error restoring trait '{originalTrait}' to inactive Kerbal {kd.stockKerbal.name}, the trait doesn't exist !", Lib.LogLevel.Error);
						return;
					}
					if (kd.stockKerbal.type != ProtoCrewMember.KerbalType.Tourist)
					{
						Lib.Log($"Error restoring trait '{originalTrait}' to inactive Kerbal {kd.stockKerbal.name}, the kerbal isn't a tourist anymore !", Lib.LogLevel.Error);
						return;
					}

					kd.stockKerbal.type = ProtoCrewMember.KerbalType.Crew;
					KerbalRoster.SetExperienceTrait(kd.stockKerbal, originalTrait);
				}
			}
		}

		public double duration;
		public double durationVariance;

		protected override void OnParse(ConfigNode effectDefinitionNode)
		{
			if (!Lib.ConfigDuration(effectDefinitionNode, "duration", false, out duration) || duration <= 0.0)
			{
				ErrorManager.AddError(true, $"Error in EFFECT '{name}' configuration", $"invalid duration of {duration}");
				duration = 1.0;
			}

			if (!Lib.ConfigDuration(effectDefinitionNode, "durationVariance", false, out durationVariance))
			{
				durationVariance = 0.0;
			}

			if (durationVariance >= duration)
			{
				ErrorManager.AddError(false, $"Error in EFFECT '{name}' configuration", $"durationVariance of {durationVariance} is equal or larger than duration of {duration}");
				durationVariance = 0.0;
			}
		}

		public override KerbalRuleEffectData GetCustomData()
		{
			return new KerbalRuleEffectInactiveData();
		}

		protected override bool OnApplyEffect(VesselDataBase vd, KerbalData kd, KerbalRuleEffect effect, out string message)
		{
			if (kd.stockKerbal.type != ProtoCrewMember.KerbalType.Crew)
			{
				message = null;
				return false;
			}

			KerbalRuleEffectInactiveData effectData = (KerbalRuleEffectInactiveData)effect.customData;
			if (effectData.remaining > 0.0)
			{
				message = null;
				return false;
			}

			effectData.originalTrait = kd.stockKerbal.trait;
			effectData.remaining = duration;
			if (durationVariance > 0.0)
			{
				effectData.remaining += Lib.RandomDouble(-durationVariance, durationVariance);
			}

			kd.stockKerbal.type = ProtoCrewMember.KerbalType.Crew;
			KerbalRoster.SetExperienceTrait(kd.stockKerbal, KerbalRoster.touristTrait);
			message = $"{kd.stockKerbal.displayName} has become inactive.";
			return true;
		}
	}

	public class KerbalRuleEffectFatality : KerbalRuleEffectDefinition
	{
		protected override bool OnApplyEffect(VesselDataBase vd, KerbalData kd, KerbalRuleEffect effect, out string message)
		{
			kd.Kill();
			message = $"{kd.stockKerbal.displayName} has died.";
			return true;
		}
	}

	public class KerbalRuleEffectLostScience : KerbalRuleEffectDefinition
	{
		protected override bool OnApplyEffect(VesselDataBase vd, KerbalData kd, KerbalRuleEffect effect, out string message)
		{
			if (!Features.Science)
			{
				message = null;
				return false;
			}

			int count;
			foreach (DriveHandler drive in DriveHandler.GetDrives(vd, true))
			{
				count = drive.files.Count;
				if (count > 0)
				{
					int index = Lib.RandomInt(count);
					File removedFile = null;
					foreach (File file in drive.files.Values)
					{
						if (index-- == 0)
						{
							removedFile = file;
							break;
						}
					}
					drive.files.Remove(removedFile.subjectData);
					message = $"{Lib.HumanReadableDataSize(removedFile.size)} of {removedFile.subjectData.FullTitle} data has been lost.";
					return true;
				}

				count = drive.samples.Count;
				if (count > 0)
				{
					int index = Lib.RandomInt(count);
					Sample removedSample = null;
					foreach (Sample sample in drive.samples.Values)
					{
						if (index-- == 0)
						{
							removedSample = sample;
							break;
						}
					}
					drive.files.Remove(removedSample.subjectData);
					message = $"{Lib.HumanReadableMass(removedSample.mass)} of {removedSample.subjectData.FullTitle} sample has been lost.";
					return true;
				}
			}

			message = null;
			return false;
		}
	}

	public class KerbalRuleEffectFailure : KerbalRuleEffectDefinition
	{
		protected override bool OnApplyEffect(VesselDataBase vd, KerbalData kd, KerbalRuleEffect effect, out string message)
		{
			message = string.Empty;
			if (vd is VesselData flightVd)
			{
				Reliability.CauseMalfunction(flightVd.Vessel);
				return true;
			}
			return false;
		}
	}

	public class KerbalRuleEffectLostResource : KerbalRuleEffectDefinition
	{
		private class LostResource
		{
			public enum ResourceSelection
			{
				Any, // any resource
				ProfileSupply, // any resource defined as a supply in the profile
				Single // a specific resource
			}

			[CFGValue] public ResourceSelection selection = ResourceSelection.ProfileSupply; // how to select the resource
			[CFGValue] public bool allowMassless = false;
			[CFGValue] public string name; // name of the resource in case selection is "Single"
			[CFGValue] public int priority = 0; // in case multiple `LOST_RESOURCE` are defined, entries with lower priority will be picked first
			[CFGValue] public double levelLost = 0.2; // amount of resource lost, in proportion of the resource current amount
			[CFGValue] public double levelLostVariance = 0.1; // max random variance on `levelLost` (positive or negative modifier : total variance is twice that)

			public double EvaluateLevelLost()
			{
				double level = levelLost;
				if (levelLostVariance > 0.0)
				{
					level += Lib.RandomDouble(-levelLostVariance, levelLostVariance);
				}
				return Lib.Clamp(level, 0.0, 1.0);
			}

		}

		private struct SelectedResource
		{
			public VesselKSPResource resource;
			public double levelLost;

			public SelectedResource(VesselKSPResource resource, LostResource lostResource)
			{
				this.resource = resource;
				levelLost = lostResource.EvaluateLevelLost();
			}
		}

		private List<LostResource> resources = new List<LostResource>();
		private static List<SelectedResource> selectedResources = new List<SelectedResource>();

		protected override void OnParse(ConfigNode effectDefinitionNode)
		{
			foreach (ConfigNode resourceNode in effectDefinitionNode.GetNodes("LOST_RESOURCE"))
			{
				LostResource resource = new LostResource();
				CFGValue.Parse(resource, resourceNode);

				if (!string.IsNullOrEmpty(resource.name))
				{
					if (!PartResourceLibrary.Instance.resourceDefinitions.Contains(resource.name))
					{
						continue;
					}
					resource.selection = LostResource.ResourceSelection.Single;
				}

				resources.Add(resource);
			}

			if (resources.Count == 0)
			{
				ErrorManager.AddError(true, "Error in 'lostResource' rule EFFECT definition", $"No valid LOST_RESOURCE definition found (does the resource exists ?)");
				return;
			}

			resources.Sort((a, b) => a.priority.CompareTo(b.priority));
		}

		protected override bool OnApplyEffect(VesselDataBase vd, KerbalData kd, KerbalRuleEffect effect, out string message)
		{
			selectedResources.Clear();
			int currentPriority = resources[0].priority;

			foreach (LostResource lostResource in resources)
			{
				if (lostResource.priority > currentPriority)
				{
					if (selectedResources.Count > 0)
						break;
					else
						currentPriority = lostResource.priority;
				}

				switch (lostResource.selection)
				{
					case LostResource.ResourceSelection.Any:
						foreach (VesselResource vesselResource in vd.ResHandler.Resources)
						{
							if (vesselResource.Level > 0.0 &&
								vesselResource is VesselKSPResource kspResourceAny
								&& kspResourceAny.Visible
								&& (lostResource.allowMassless || (!lostResource.allowMassless && kspResourceAny.Density > 0f)))
							{
								selectedResources.Add(new SelectedResource(kspResourceAny, lostResource));
							}
						}
						break;
					case LostResource.ResourceSelection.ProfileSupply:
						foreach (Supply supply in Profile.supplies)
						{
							if (vd.ResHandler.TryGetResource(supply.resource, out VesselKSPResource kspResourceSupply)
								&& kspResourceSupply.Level > 0.0
								&& (lostResource.allowMassless || (!lostResource.allowMassless && kspResourceSupply.Density > 0f)))
							{
								selectedResources.Add(new SelectedResource(kspResourceSupply, lostResource));
							}
						}
						break;
					case LostResource.ResourceSelection.Single:
						if (vd.ResHandler.TryGetResource(lostResource.name, out VesselKSPResource kspResourceSingle)
							&& kspResourceSingle.Level > 0.0
							&& (lostResource.allowMassless || (!lostResource.allowMassless && kspResourceSingle.Density > 0f)))
						{
							selectedResources.Add(new SelectedResource(kspResourceSingle, lostResource));
						}
						break;
				}
			}

			if (selectedResources.Count == 0)
			{
				message = null;
				return false;
			}

			SelectedResource selectedResource = selectedResources[Lib.RandomInt(selectedResources.Count)];
			double amountLost = selectedResource.resource.Amount * selectedResource.levelLost;
			selectedResource.resource.Consume(amountLost);
			message = $"{Lib.HumanReadableAmountCompact(amountLost)} of {selectedResource.resource.Title} has been lost.";
			return true;
		}
	}
}
