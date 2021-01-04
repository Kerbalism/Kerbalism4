namespace KERBALISM
{
    public class ModuleGeneratorHandler : TypedModuleHandler<ModuleGenerator>
    {
		public override ActivationContext Activation => ActivationContext.Unloaded;

		public override void OnFixedUpdate(double elapsedSec)
        {
			// if active
			if (Lib.Proto.GetBool(protoModule, nameof(ModuleGenerator.generatorIsActive)))
			{
				// create and commit recipe
				Recipe recipe = new Recipe(ResourceBroker.StockConverter);
				foreach (ModuleResource ir in prefabModule.resHandler.inputResources)
				{
					recipe.AddInput(ir.name, ir.rate * elapsedSec);
				}
				foreach (ModuleResource or in prefabModule.resHandler.outputResources)
				{
					recipe.AddOutput(or.name, or.rate * elapsedSec, true);
				}
				VesselData.ResHandler.AddRecipe(recipe);
			}
		}
	}
}
