using System.Collections.Generic;

namespace KERBALISM
{
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

			// TODO : refactor the lost science effect once science data handling refactor is complete

			//int count;
			//foreach (DriveHandler drive in DriveHandler.GetDrives(vd, true))
			//{
			//	count = drive.files.Count;
			//	if (count > 0)
			//	{
			//		int index = Lib.RandomInt(count);
			//		DriveFile removedFile = null;
			//		foreach (DriveFile file in drive.files.Values)
			//		{
			//			if (index-- == 0)
			//			{
			//				removedFile = file;
			//				break;
			//			}
			//		}
			//		drive.files.Remove(removedFile.subjectData);
			//		message = $"{Lib.HumanReadableDataSize(removedFile.size)} of {removedFile.subjectData.FullTitle} data has been lost.";
			//		return true;
			//	}

			//	count = drive.samples.Count;
			//	if (count > 0)
			//	{
			//		int index = Lib.RandomInt(count);
			//		Sample removedSample = null;
			//		foreach (Sample sample in drive.samples.Values)
			//		{
			//			if (index-- == 0)
			//			{
			//				removedSample = sample;
			//				break;
			//			}
			//		}
			//		drive.files.Remove(removedSample.subjectData);
			//		message = $"{Lib.HumanReadableMass(removedSample.mass)} of {removedSample.subjectData.FullTitle} sample has been lost.";
			//		return true;
			//	}
			//}

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
				//Reliability.CauseMalfunction(flightVd.Vessel);
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
			public VesselResourceKSP resource;
			public double levelLost;

			public SelectedResource(VesselResourceKSP resource, LostResource lostResource)
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
								vesselResource is VesselResourceKSP kspResourceAny
								&& kspResourceAny.Visible
								&& (lostResource.allowMassless || (!lostResource.allowMassless && kspResourceAny.Density > 0f)))
							{
								selectedResources.Add(new SelectedResource(kspResourceAny, lostResource));
							}
						}
						break;
					case LostResource.ResourceSelection.ProfileSupply:
						foreach (SupplyDefinition supply in SupplyDefinition.definitions)
						{
							if (vd.ResHandler.TryGetResource(supply.name, out VesselResourceKSP kspResourceSupply)
								&& kspResourceSupply.Level > 0.0
								&& (lostResource.allowMassless || (!lostResource.allowMassless && kspResourceSupply.Density > 0f)))
							{
								selectedResources.Add(new SelectedResource(kspResourceSupply, lostResource));
							}
						}
						break;
					case LostResource.ResourceSelection.Single:
						if (vd.ResHandler.TryGetResource(lostResource.name, out VesselResourceKSP kspResourceSingle)
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
