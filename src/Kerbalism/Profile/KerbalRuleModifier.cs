namespace KERBALISM
{
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
}
