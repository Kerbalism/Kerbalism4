namespace KERBALISM
{
	// note : this works in conjunction/spaghetti with the ModuleCommand patch and some special handling in VesselData
    public class ModuleCommandHandler : TypedModuleHandler<ModuleCommand>
    {
		public override ActivationContext Activation => ActivationContext.Unloaded;

		public string BrokerTitle => ModuleTitle;

		private ProtoModuleValueBool hibernation;

		private Recipe ecRecipe;

		public override void OnStart()
		{
			if (!ProtoModuleValueBool.TryGet(protoModule.moduleValues, nameof(ModuleCommand.hibernation), out hibernation))
			{
				handlerIsEnabled = false;
				return;
			}

			double ecRate = Lib.Proto.GetDouble(protoModule, nameof(ModuleCommand.hibernationMultiplier), 0.02);
			ecRecipe = new Recipe(partData.Title, RecipeCategory.Command);
			ecRecipe.AddInput(VesselResHandler.ElectricChargeId, ecRate);
		}

		public override void OnUpdate(double elapsedSec)
        {
			bool hibernating = hibernation.Value;

			if (!hibernating)
				((VesselData)VesselData).hasNonHibernatingCommandModules = true;

			// do not consume if this is an uncrewed non-probe module
			if (prefabModule.minimumCrew == 0 || partData.ProtoPart.protoModuleCrew.Count > 0)
			{
				double recipeFactor = hibernating ? Settings.HibernatingEcFactor : 1.0;
				ecRecipe.RequestExecution(VesselData.ResHandler, null, recipeFactor);
			}
		}
    }
}
