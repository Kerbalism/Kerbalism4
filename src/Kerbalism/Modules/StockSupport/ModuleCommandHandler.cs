namespace KERBALISM
{
	// note : this work in conjunction with the ModuleCommand patch and some special handling in VesselData
    public class ModuleCommandHandler : TypedModuleHandler<ModuleCommand>
    {
		public override ActivationContext Activation => ActivationContext.Unloaded;

        public override void OnFixedUpdate(double elapsedSec)
        {
			bool hibernating = Lib.Proto.GetBool(protoModule, nameof(ModuleCommand.hibernation), false);

			if (!hibernating)
				((VesselData)VesselData).hasNonHibernatingCommandModules = true;

			// do not consume if this is a non-probe MC with no crew
			// this make some sense: you left a vessel with some battery and nobody on board, you expect it to not consume EC
			if (prefabModule.minimumCrew == 0 || partData.ProtoPart.protoModuleCrew.Count > 0)
			{
				double ecRate = Lib.Proto.GetDouble(protoModule, nameof(ModuleCommand.hibernationMultiplier), 0.02);

				if (hibernating)
					ecRate *= Settings.HibernatingEcFactor;

				VesselData.ResHandler.ElectricCharge.Consume(ecRate * elapsedSec, ResourceBroker.Command, true);
			}
		}
    }
}
