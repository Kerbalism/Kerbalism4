using System;

namespace KERBALISM
{
	public class LocalRecipeDefinition : RecipeDefinition
	{
		public LocalRecipeDefinition(ConfigNode recipeNode, RecipeCategory category) : base(recipeNode)
		{
			foreach (RecipeInputDefinition input in inputs)
			{
				if (input.resourceDef != null && input.resourceDef.id == VesselResHandler.ElectricChargeId && category.ecProducer)
				{
					throw new Exception($"A process in category={category.name} can't have an ElectricCharge input");
				}
			}

			foreach (RecipeOutputDefinition output in outputs)
			{
				if (output.resourceDef != null && output.resourceDef.id == VesselResHandler.ElectricChargeId && !category.ecProducer)
				{
					throw new Exception($"A process in category={category.name} can't have an ElectricCharge output");
				}
			}
		}
	}
}
