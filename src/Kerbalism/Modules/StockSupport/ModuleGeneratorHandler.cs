using System;
using System.Collections.Generic;

namespace KERBALISM
{
    public class ModuleGeneratorHandler : TypedModuleHandler<ModuleGenerator>
    {
	    public override ActivationContext Activation => ActivationContext.Unloaded;

	    public RecipeCategory Category => category;
	    private RecipeCategory category = RecipeCategory.Converter;

		private Recipe recipe;
	    private ProtoModuleValueBool generatorIsActive;

		public override void OnStart()
		{
			if (!ProtoModuleValueBool.TryGet(protoModule.moduleValues, nameof(ModuleGenerator.generatorIsActive), out generatorIsActive))
			{
				handlerIsEnabled = false;
				return;
			}

			recipe = new Recipe(partData.Title, RecipeCategory.Converter);

			foreach (ModuleResource input in prefabModule.resHandler.inputResources)
			{
				recipe.AddInput(input.id, input.rate);
			}

			foreach (ModuleResource output in prefabModule.resHandler.outputResources)
			{
				recipe.AddOutput(output.id, output.rate, false, true);

				if (output.id == VesselResHandler.ElectricChargeId)
					recipe.category = RecipeCategory.ECGenerator;
			}
		}

		public override void OnUpdate(double elapsedSec)
        {
	        if (generatorIsActive.Value)
	        {
		        recipe.RequestExecution(VesselData.ResHandler);
	        }
		}
    }
}
