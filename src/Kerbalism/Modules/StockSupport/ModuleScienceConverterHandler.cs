using System;

namespace KERBALISM
{
    // Basic support for the stock science converter : consume EC and scale data production by EC availability.
    // Should work reliably as long as you don't start using all the weird features of BaseConverter
    public class ModuleScienceConverterHandler : TypedModuleHandler<ModuleScienceConverter>
    {
		public override ActivationContext Activation => ActivationContext.Unloaded;

		private double lastElapsedSec = 0.0;

        public override void OnStart()
        {
			if (!Lib.Proto.GetBool(protoModule, "IsActivated"))
            {
				handlerIsEnabled = false;
            }
		}

        public override void OnFixedUpdate(double elapsedSec)
        {
			VesselData.ResHandler.ElectricCharge.Consume(prefabModule.powerRequirement * elapsedSec, ResourceBroker.ScienceLab);

			// The strategy here is to increase the last update time when there isn't enough EC,
			// to trick the stock post-facto simulation into producing less data.
            if (VesselData.ResHandler.ElectricCharge.AvailabilityFactor < 1.0)
            {
				double lastUT = Lib.Proto.GetDouble(protoModule, "lastUpdateTime");
				lastUT += lastElapsedSec * (1.0 - VesselData.ResHandler.ElectricCharge.AvailabilityFactor);
				// make sure we don't accidentally set lastUpdateTime in the future (shouldn't happen but better safe than sorry)
				Lib.Proto.Set(protoModule, "lastUpdateTime", Math.Min(lastUT, Planetarium.GetUniversalTime()));
			}

			lastElapsedSec = elapsedSec;
		}
	}
}
