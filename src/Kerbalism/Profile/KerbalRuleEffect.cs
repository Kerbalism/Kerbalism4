namespace KERBALISM
{
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
}
