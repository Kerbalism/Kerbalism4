namespace KERBALISM
{
	public class ComfortValue
	{
		public readonly ComfortDefinition definition;
		public string Name => definition.name;
		public string Title => definition.title;
		[CFGValue] public double quality;
		[CFGValue] public int seats;

		public static ComfortValue Load(ConfigNode comfortNode)
		{
			string name = Lib.ConfigValue(comfortNode, "name", string.Empty);
			if (!ComfortDefinition.definitionsByName.TryGetValue(name, out ComfortDefinition definition))
			{
				ErrorManager.AddError(false, $"Error parsing COMFORT node, no definition for a `{name}` comfort");
				return null;
			}

			return new ComfortValue(definition, comfortNode); ;
		}

		private ComfortValue(ComfortDefinition definition, ConfigNode comfortNode)
		{
			this.definition = definition;
			CFGValue.Parse(this, comfortNode);
		}
	}
}
