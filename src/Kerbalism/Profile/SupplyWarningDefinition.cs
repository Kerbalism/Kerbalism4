namespace KERBALISM
{
	public class SupplyWarningDefinition
	{
		public const string NODENAME = "WARNING";

		public enum WarningMode { OnIncrease, OnDecrease }

		[CFGValue] public readonly string message;
		[CFGValue] public readonly int checkOrder;
		[CFGValue] public readonly WarningMode warningMode = WarningMode.OnDecrease;
		[CFGValue] public readonly Kolor color = Kolor.White;
		[CFGValue] public readonly Severity severity = Severity.none;
		[CFGValue] public readonly double levelThreshold = -1.0;
		[CFGValue] public readonly double availabilityThreshold = -1.0;
		[CFGValue] public readonly bool stopWarp = false;
		[CFGValue] public readonly bool mannedOnly = true;

		private readonly bool checkLevel;
		private readonly bool checkAvailability;

		public SupplyWarningDefinition(ConfigNode node)
		{
			CFGValue.Parse(this, node);
			checkLevel = levelThreshold >= 0.0;
			checkAvailability = availabilityThreshold >= 0.0;
		}

		public bool Evaluate(VesselResource resource)
		{
			if (warningMode == WarningMode.OnIncrease)
			{
				if (checkLevel && resource.Level >= levelThreshold)
				{
					return true;
				}

				//if (checkAvailability && resource.AvailabilityFactor >= availabilityThreshold)
				//{
				//	return true;
				//}
			}
			else
			{
				if (checkLevel && resource.Level <= levelThreshold)
				{
					return true;
				}

				//if (checkAvailability && resource.AvailabilityFactor <= availabilityThreshold)
				//{
				//	return true;
				//}
			}

			return false;
		}
	}
}
