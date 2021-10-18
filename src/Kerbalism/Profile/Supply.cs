using UnityEngine;

namespace KERBALISM
{
	public class Supply
	{
		public static Supply GetSupply(int resourceId)
		{
			if (!SupplyDefinition.definitionsByResourceId.TryGetValue(resourceId, out SupplyDefinition definition))
				return null;

			return new Supply(definition);
		}

		public readonly SupplyDefinition definition;
		public Texture2D Texture => definition.icon;
		public SupplyWarningDefinition CurrentWarning { get; private set; }
		public Severity Severity => CurrentWarning?.severity ?? Severity.none;
		public string Message => CurrentWarning?.message;
		public Kolor Kolor => CurrentWarning?.color;

		private Supply(SupplyDefinition definition)
		{
			this.definition = definition;
		}

		public void Evaluate(VesselDataBase vd, VesselResource resource)
		{
			SupplyWarningDefinition lastWarning = CurrentWarning;
			CurrentWarning = null;

			if (resource.Capacity == 0.0)
				return;


			foreach (SupplyWarningDefinition warning in definition.warnings)
			{
				if (warning.Evaluate(resource))
				{
					CurrentWarning = warning;
					break;
				}
			}

			if (CurrentWarning != null
				&& lastWarning != null
				&& CurrentWarning.checkOrder < lastWarning.checkOrder
				&& CurrentWarning.message != null
			    && (!CurrentWarning.mannedOnly || vd.CrewCount > 0))
			{

;				KERBALISM.Message.Post(
					CurrentWarning.severity,
					$"On {vd.VesselName} :",
					CurrentWarning.message);

				if (CurrentWarning.stopWarp)
				{
					Lib.StopWarp();
				}
			}
		}
	}
} // KERBALISM
