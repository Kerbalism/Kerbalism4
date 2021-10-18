using System;

namespace KERBALISM
{
    // Basic support for the stock science converter : consume EC and scale data production by EC availability.
    // Should work reliably as long as you don't start using all the weird features of BaseConverter
    public class ModuleScienceConverterHandler : TypedModuleHandler<ModuleScienceConverter>
	{
		public override ActivationContext Activation => ActivationContext.Unloaded;

		private double lastElapsedSec = 0.0;
		private ProtoModuleValueDouble lastUpdateTime;

		private Recipe recipe;

		public override void OnStart()
        {
			if (!Lib.Proto.GetBool(protoModule, nameof(ModuleScienceConverter.IsActivated))
			|| !ProtoModuleValueDouble.TryGet(protoModule.moduleValues, "lastUpdateTime", out lastUpdateTime))
            {
				handlerIsEnabled = false;
				return;
            }

			recipe = new Recipe(partData.Title, RecipeCategory.ScienceLab, OnRecipeExecuted);
			recipe.AddInput(VesselResHandler.ElectricChargeId, prefabModule.powerRequirement);
        }

        public override void OnUpdate(double elapsedSec)
        {
	        recipe.RequestExecution(VesselData.ResHandler);
	        lastElapsedSec = elapsedSec;
        }

        public void OnRecipeExecuted(double elapsedSec)
        {
	        if (recipe.ExecutedFactor < 1.0)
	        {
				double lastUT = lastUpdateTime.Value;

				// The strategy here is to increase the last update time when there isn't enough EC,
				// to trick the stock post-facto simulation into producing less data.
				// We make sure we don't accidentally set lastUpdateTime in the future (shouldn't happen but better safe than sorry)
				lastUT = Math.Min(lastUT + (lastElapsedSec * (1.0 - recipe.ExecutedFactor)), Planetarium.GetUniversalTime());
				lastUpdateTime.Value = lastUT;
	        }
        }
	}
}
