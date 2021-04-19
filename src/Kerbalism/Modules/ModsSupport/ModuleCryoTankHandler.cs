using System;
using System.Collections;
using System.Collections.Generic;

namespace KERBALISM
{
	// Support for CryoTanks boiloff : https://github.com/post-kerbin-mining-corporation/CryoTanks
	// - We don't support the LongwaveFluxAffectsBoiloff / ShortwaveFluxAffectsBoiloff config options
	//   Could eventually be supported as we have our background bodyFlux / sunflux computations, but
	//   as of 11/2020, a github search returns absolutely nobody using that feature, including CryoTanks itself.
	// - We don't support the `OUTPUT_RESOURCE` config option. Could be supported, but same deal : in all of github
	//   I can only find one tiny patch from the probably abandoned "Simplified Real Fuels" mod using that feature :
	//   https://github.com/judicator/SimplifiedRealFuels/blob/a2ef19660f42135a3947d1c717a59c6ecb222606/GameData/SimplifiedRealFuels/Patches/UniversalStorage2/Wedges/OxygenWedge.cfg#L98-L113

	// TODO : FULLY UNTESTED, VERY LIKELY HAS BUGS !!!

	public class ModuleCryoTankHandler : ForeignModuleHandler
    {
		public override string[] ModuleTypeNames => moduleTypeNames;
		private static string[] moduleTypeNames = new string[] { "ModuleCryoTank" };

		public override ActivationContext Activation => ActivationContext.Unloaded;

		private VesselResource electricCharge;
		private bool coolingEnabled;
		private double baseCoolingCost;
		private List<FuelBoiloff> fuels;

		private class FuelBoiloff
        {
			public PartResourceWrapper fuel;
			public double boiloffRate;
			public double coolingCost;

            public FuelBoiloff(PartResourceWrapper fuel, double boiloffRate, double coolingCost)
            {
                this.fuel = fuel;
                this.boiloffRate = boiloffRate;
                this.coolingCost = coolingCost;
            }
        }

        public override void OnStart()
        {
			coolingEnabled = Lib.Proto.GetBool(protoModule, "CoolingEnabled");
            if (!coolingEnabled)
            {
				handlerIsEnabled = false;
				return;
			}

            try
            {
				// get cooling EC cost in EC/s/1000 units, convert to EC/s/unit
				baseCoolingCost = Lib.ReflectionValue<float>(prefabModule, "CoolingCost");
				baseCoolingCost /= 1000.0;

				IList prefabFuels = Lib.ReflectionValue<IList>(prefabModule, "fuels");
				if (prefabFuels == null)
                {
					handlerIsEnabled = false;
					return;
				}

				foreach (object prefabFuel in prefabFuels)
				{
					string resName = Lib.ReflectionValue<string>(prefabFuel, "fuelName");
					PartResourceWrapper resource = partData.resources.Find(p => p.ResName == resName);
					if (resource == null)
						continue;

					// get boiloff %/H, convert it to a per second factor (/100/3600)
					double boiloffRate = Lib.ReflectionValue<float>(prefabFuel, "boiloffRate") / 100.0 / 3600.0;

					// get additional cooling EC cost in EC/s/1000 units, convert to EC/s/unit
					double coolingCost = Lib.ReflectionValue<float>(prefabFuel, "coolingCost") / 1000.0;

					if (fuels == null)
						fuels = new List<FuelBoiloff>();

					fuels.Add(new FuelBoiloff(resource, boiloffRate, coolingCost));
				}
			}
            catch (Exception e)
            {
				Lib.Log($"Error instantiating background processing handler for ModuleCryoFuel on {partData} on {VesselData}\n{e}", Lib.LogLevel.Warning);
				handlerIsEnabled = false;
				return;
			}

			electricCharge = VesselData.ResHandler.ElectricCharge;
		}

        public override void OnFixedUpdate(double elapsedSec)
        {
			double ecConsumed = 0.0;

			foreach (FuelBoiloff boiloff in this.fuels)
            {
				double fuelAmount = boiloff.fuel.Amount;

				if (fuelAmount == 0.0)
					continue;

				ecConsumed += fuelAmount * (baseCoolingCost + boiloff.coolingCost);

				// scale boiloff by available ec
				double boiloffRate = boiloff.boiloffRate * (1.0 - electricCharge.AvailabilityFactor);

				// Note that we bypass the resource sim to get a part-local effect. This should be relatively fine
				// as this is a slow rate consumption, and the resource chains involving cryogenic things are rare.
                if (boiloffRate > 0.0)
                {
					double amountToBoil = fuelAmount * (1.0 - Math.Pow(1.0 - boiloffRate, elapsedSec));
					boiloff.fuel.Amount = Math.Max(fuelAmount - amountToBoil, 0.0);
				}
			}

            if (ecConsumed > 0.0)
            {
				electricCharge.Consume(ecConsumed * elapsedSec, ResourceBroker.Cryotank);
			}

			// prevent the module post-facto catchup from doing its thing on next load by resetting the last loaded UT every update.
			Lib.Proto.Set(protoModule, "LastUpdateTime", Planetarium.GetUniversalTime());
		}
	}
}
