namespace KERBALISM
{
	public static class ProfileParser
	{
		public const string NODENAME_PROFILE = "KERBALISM_PROFILE";
		public const string NODENAME_RULE = "RULE";
		public const string NODENAME_PROCESS = "PROCESS";
		public const string NODENAME_SUPPLY = "SUPPLY";
		public const string NODENAME_VIRTUAL_RESOURCE = "VIRTUAL_RESOURCE";
		public const string NODENAME_RESOURCE_HVL = "RESOURCE_HVL";
		public const string NODENAME_COMFORT = "COMFORT";

		public static void Parse()
		{
			// for each profile config
			ConfigNode[] profileNodes = Lib.ParseConfigs(NODENAME_PROFILE);
			ConfigNode profileNode;
			if (profileNodes.Length == 1)
			{
				profileNode = profileNodes[0];
			}
			else
			{
				profileNode = new ConfigNode();

				if (profileNodes.Length == 0)
				{
					ErrorManager.AddError(true, $"No profile found.",
					"You likely have forgotten to install KerbalismConfig or an alternative config pack in GameData.");
				}
				else if (profileNodes.Length > 1)
				{
					ErrorManager.AddError(true, $"Muliple profiles found.",
					"You likely have duplicates of KerbalismConfig or of an alternative config pack in GameData.");
				}

				ErrorManager.CheckErrors();
				return;
			}

			// build our resource library
			VesselResHandler.ParseDefinitions();

			ResourceHVLDefinition.ParseDefinitions(profileNode.GetNodes(NODENAME_RESOURCE_HVL));

			SupplyDefinition.ParseDefinitions(profileNode.GetNodes(NODENAME_SUPPLY));

			ProcessDefinition.ParseDefinitions(profileNode.GetNodes(NODENAME_PROCESS));

			ComfortDefinition.ParseDefinitions(profileNode.GetNodes(NODENAME_COMFORT));

			KerbalRuleDefinition.ParseDefinitions(profileNode.GetNodes(NODENAME_RULE)); // must be after processes

			// log profile info
			Lib.Log($"{SupplyDefinition.definitions.Count} {NODENAME_SUPPLY} definitions found :");
			foreach (SupplyDefinition supply in SupplyDefinition.definitions)
				Lib.Log($"- {supply.name}");

			Lib.Log($"{KerbalRuleDefinition.definitions.Count} {NODENAME_RULE} definitions found :");
			foreach (KerbalRuleDefinition rule in KerbalRuleDefinition.definitions)
			{
				Lib.Log($"- {rule.name}");
				Lib.Log($"  {rule.modifiers.Count} modifiers : {string.Join(", ", rule.modifiers)}");
				Lib.Log($"  {rule.effects.Count} effects : {string.Join(", ", rule.effects)}");
			}

			Lib.Log($"{ProcessDefinition.definitions.Count} {NODENAME_PROCESS} definitions found :");
			foreach (ProcessDefinition process in ProcessDefinition.definitions)
			{
				Lib.Log($"- {process.name} (category={process.category.name})");
				Lib.Log($"  {process.inputs.Count} inputs : {string.Join(", ", process.inputs)}");
				Lib.Log($"  {process.outputs.Count} outputs : {string.Join(", ", process.outputs)}");
			}
		}
	}
} // KERBALISM
