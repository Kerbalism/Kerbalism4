using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public class ComfortDefinition
	{
		public static readonly List<ComfortDefinition> definitions = new List<ComfortDefinition>();
		public static readonly Dictionary<string, ComfortDefinition> definitionsByName = new Dictionary<string, ComfortDefinition>();

		private const string notAloneName = "notAlone";
		private const string callHomeName = "callHome";
		private const string firmGroundName = "firmGround";
		private const string exerciseName = "exercise";

		public static ComfortDefinition notAlone;
		public static ComfortDefinition callHome;
		public static ComfortDefinition firmGround;
		public static ComfortDefinition exercise;

		public static void ParseDefinitions(ConfigNode[] comfortsNodes)
		{
			foreach (ConfigNode comfortNode in comfortsNodes)
			{
				ComfortDefinition definition;
				try
				{
					definition = new ComfortDefinition(comfortNode, definitions.Count);
				}
				catch (Exception e)
				{
					string name = Lib.ConfigValue(comfortNode, nameof(name), string.Empty);
					ErrorManager.AddError(true, $"Error parsing COMFORT `{name}`", e.Message);
					continue;
				}

				if (definitionsByName.ContainsKey(definition.name))
				{
					ErrorManager.AddError(false, $"Duplicate definition for COMFORT `{definition.name}`");
					continue;
				}

				switch (definition.name)
				{
					case notAloneName: notAlone = definition; break;
					case callHomeName: callHome = definition; break;
					case firmGroundName: firmGround = definition; break;
					case exerciseName: exercise = definition; break;
				}

				definitions.Add(definition);
				definitionsByName.Add(definition.name, definition);
			}
		}

		[CFGValue] public string name;
		[CFGValue] public string title;
		[CFGValue] public double maxBonus = 1.0; // this could be in the rule modifier, but then we would have no way to show it in the UI...

		public readonly int definitionIndex;

		public ComfortDefinition(ConfigNode comfortNode, int definitionIndex)
		{
			this.definitionIndex = definitionIndex;
			CFGValue.Parse(this, comfortNode);

			if (string.IsNullOrEmpty(name))
				throw new Exception($"Comfort definition has no name !");

			if (!name.IsValidNodeName(out char invalidChar))
				throw new Exception($"Comfort name `{name}` contains the invalid character `{invalidChar}`");
		}

		public ComfortInfoBase GetComfortInfo()
		{
			if (this == notAlone)
				return new ComfortNotAlone(this);
			else if (this == callHome)
				return new ComfortCallHome(this);
			else if (this == firmGround)
				return new ComfortFirmGround(this);
			else
				return new ComfortModuleInfo(this);
		}
	}
}
