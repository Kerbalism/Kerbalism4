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

        public override void OnFixedUpdate(double elapsedSec)
		{
			if (prefabModule.useResources && Lib.Proto.GetBool(protoModule, nameof(ModuleLight.isOn)))
			{
				VesselData.ResHandler.ElectricCharge.Consume(prefabModule.resourceAmount * elapsedSec, ResourceBroker.Light);
			}
		}
	}
}
