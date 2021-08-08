using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM
{
	public class Supply
	{
		public static Supply GetSupply(string resource)
		{
			SupplyDefinition definition = Profile.supplies.Find(p => p.name == resource);

			if (definition == null)
				return null;

			Supply supply = new Supply {Definition = definition};
			return supply;
		}

		public SupplyDefinition Definition { get; private set; }
		public Texture2D Texture => Definition.icon;
		public SupplyWarningDefinition CurrentWarning { get; private set; }
		public Severity Severity => CurrentWarning?.severity ?? Severity.none;
		public string Message => CurrentWarning?.message;
		public Kolor Kolor => CurrentWarning?.color;

		public void Evaluate(VesselDataBase vd, VesselResource resource)
		{
			SupplyWarningDefinition lastWarning = CurrentWarning;
			CurrentWarning = null;

			if (resource.Capacity == 0.0)
				return;


			foreach (SupplyWarningDefinition warning in Definition.warnings)
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

	public class SupplyDefinition
	{
		[CFGValue] public string name;					// name of resource
		[CFGValue] public double evaCapacity = 0.0;      // how much resource capacity to add on eva
		[CFGValue] public double grantedOnRescue = 0.0;  // how much resource to gift to rescue missions

		public List<SupplyWarningDefinition> warnings = new List<SupplyWarningDefinition>();

		public Texture2D icon;

		public SupplyDefinition(ConfigNode node)
		{
			CFGValue.Parse(this, node);

			if (string.IsNullOrEmpty(name))
			{
				ErrorManager.AddError(true, "Profile supply definition has no resource name");
			}
			if (Lib.GetDefinition(name) == null)
			{
				ErrorManager.AddError(true, $"Profile supply resource '{name}' doesn't exist");
			}

			string texturePath = string.Empty;
			if (node.TryGetValue("iconPath", ref texturePath))
			{
				icon = Lib.GetTexture(texturePath);
			}

			foreach (ConfigNode warningNode in node.GetNodes())
			{
				if (warningNode.name != SupplyWarningDefinition.NODENAME)
					continue;

				warnings.Add(new SupplyWarningDefinition(warningNode));
			}

			warnings.Sort((a, b) => a.checkOrder.CompareTo(b.checkOrder));
		}

		public void SetupEva(Part p)
		{
			// do nothing if no resource on eva
			if (evaCapacity == 0.0) return;

			// create new resource capacity in the eva kerbal
			Lib.AddResource(p, name, 0.0, evaCapacity);
		}
	}

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

				if (checkAvailability && resource.AvailabilityFactor >= availabilityThreshold)
				{
					return true;
				}
			}
			else
			{
				if (checkLevel && resource.Level <= levelThreshold)
				{
					return true;
				}

				if (checkAvailability && resource.AvailabilityFactor <= availabilityThreshold)
				{
					return true;
				}
			}

			return false;
		}
	}

} // KERBALISM
