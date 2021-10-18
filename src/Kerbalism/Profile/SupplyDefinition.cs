using System;
using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM
{
	public class SupplyDefinition
	{
		public static readonly List<SupplyDefinition> definitions = new List<SupplyDefinition>();
		public static readonly Dictionary<int, SupplyDefinition> definitionsByResourceId = new Dictionary<int, SupplyDefinition>();

		public static void ParseDefinitions(ConfigNode[] supplyNodes)
		{
			foreach (ConfigNode supplyNode in supplyNodes)
			{
				SupplyDefinition definition;
				try
				{
					definition = new SupplyDefinition(supplyNode);
				}
				catch (Exception e)
				{
					string name = Lib.ConfigValue(supplyNode, nameof(name), string.Empty);
					ErrorManager.AddError(true, $"Error parsing SUPPLY `{name}`", e.Message);
					continue;
				}

				if (definitionsByResourceId.ContainsKey(definition.resourceId))
				{
					ErrorManager.AddError(false, $"Duplicate definition for SUPPLY `{definition.name}`");
					continue;
				}

				definitions.Add(definition);
				definitionsByResourceId.Add(definition.resourceId, definition);
			}
		}

		public static void SetupEva(Part evaPart)
		{
			foreach (SupplyDefinition supply in definitions)
			{
				supply.SetupSupplyOnEva(evaPart);
			}
		}

		[CFGValue] public readonly string name;					  // name of resource
		[CFGValue] public readonly double evaCapacity = 0.0;      // how much resource capacity to add on eva
		[CFGValue] public readonly double grantedOnRescue = 0.0;  // how much resource to gift to rescue missions

		public readonly int resourceId;
		public readonly PartResourceDefinition resourceDefinition;
		public readonly List<SupplyWarningDefinition> warnings = new List<SupplyWarningDefinition>();
		public readonly Texture2D icon;

		public SupplyDefinition(ConfigNode node)
		{
			CFGValue.Parse(this, node);

			if (string.IsNullOrEmpty(name))
				throw new Exception("Profile supply definition has no resource name");

			resourceDefinition = Lib.GetDefinition(name);

			if (resourceDefinition == null)
				throw new Exception($"Profile supply resource '{name}' doesn't exist");

			resourceId = resourceDefinition.id;

			string texturePath = string.Empty;
			if (node.TryGetValue("iconPath", ref texturePath))
				icon = Lib.GetTexture(texturePath);

			foreach (ConfigNode warningNode in node.GetNodes())
			{
				if (warningNode.name != SupplyWarningDefinition.NODENAME)
					continue;

				warnings.Add(new SupplyWarningDefinition(warningNode));
			}

			warnings.Sort((a, b) => a.checkOrder.CompareTo(b.checkOrder));
		}

		public void SetupSupplyOnEva(Part evaPart)
		{
			// do nothing if no resource on eva
			if (evaCapacity == 0.0) return;

			// create new resource capacity in the eva kerbal
			Lib.AddResource(evaPart, name, 0.0, evaCapacity);
		}
	}
}
