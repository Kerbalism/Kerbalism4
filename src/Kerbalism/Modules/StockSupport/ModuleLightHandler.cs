namespace KERBALISM
{
	// Frankly background ec consumption for lights is silly IMO, but I sense removing it would trigger complaints.
	// ModuleLightEva, ModuleColoredLensLight, ModuleMultiPointSurfaceLight and ModuleStockLightColoredLens
	// are ModuleLight derivatives from the "Surface Mounted Lights" mod : https://github.com/ihsoft/SurfaceLights
	public class ModuleLightHandler : TypedModuleHandler<ModuleLight>
	{
		public override ActivationContext Activation => ActivationContext.Unloaded;

		public override string[] ModuleTypeNames => moduleTypeNames;
		private static string[] moduleTypeNames = new string[] { nameof(ModuleLight), "ModuleLightEva", "ModuleColoredLensLight", "ModuleMultiPointSurfaceLight", "ModuleStockLightColoredLens" };

		private Recipe recipe;
		private ProtoModuleValueBool isOn;

		public override void OnStart()
		{
			if (!prefabModule.useResources
			    || prefabModule.resHandler.inputResources.Count == 0
			    || !ProtoModuleValueBool.TryGet(protoModule.moduleValues, nameof(ModuleLight.isOn), out isOn))
			{
				handlerIsEnabled = false;
				return;
			}

			recipe = new Recipe(partData.Title, RecipeCategory.Light);

			foreach (ModuleResource inputResource in prefabModule.resHandler.inputResources)
			{
				recipe.AddInput(inputResource.id, inputResource.rate);
			}
		}

		public override void OnUpdate(double elapsedSec)
		{
			if (isOn.Value)
				recipe.RequestExecution(VesselData.ResHandler);
		}


	}
}
