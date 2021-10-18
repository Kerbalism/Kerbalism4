using System.Collections;
using ModuleWheels;

// TODO : Refactor the old planner handlers for stock / modded modules
// They need to be converted to TypedModuleHandler / ForeignModuleHandler classes

/*

namespace KERBALISM
{
	static class EditorHandlers
	{
		private static VesselResHandler handler;
		private static VesselDataShip vd;

		private static void ProcessCommand(ModuleCommand command)
		{
			if (command.hibernationMultiplier == 0.0)
				return;

			// do not consume if this is a non-probe MC with no crew
			// this make some sense: you left a vessel with some battery and nobody on board, you expect it to not consume EC
			if (command.minimumCrew == 0 || command.part.protoModuleCrew.Count > 0)
			{
				double ecRate = command.hibernationMultiplier;
				if (command.hibernation)
					ecRate *= Settings.HibernatingEcFactor;

				handler.ElectricCharge.Consume(ecRate, RecipeCategory.Command, true);
			}
		}

		private static void ProcessGenerator(ModuleGenerator generator, Part p)
		{
			Recipe recipe = new Recipe(RecipeCategory.GetOrCreate(p.partInfo.title));
			foreach (ModuleResource res in generator.resHandler.inputResources)
			{
				recipe.AddInput(res.name, res.rate);
			}
			foreach (ModuleResource res in generator.resHandler.outputResources)
			{
				recipe.AddOutput(res.name, res.rate, true);
			}
			handler.AddRecipe(recipe);
		}

		private static void ProcessConverter(ModuleResourceConverter converter)
		{
			// calculate experience bonus
			float exp_bonus = converter.UseSpecialistBonus
			  ? converter.EfficiencyBonus * (converter.SpecialistBonusBase + (converter.SpecialistEfficiencyFactor * (vd.crewEngineerMaxlevel + 1)))
			  : 1.0f;

			// use part name as recipe name
			// - include crew bonus in the recipe name
			string recipe_name = Lib.BuildString(converter.part.partInfo.title, " (efficiency: ", Lib.HumanReadablePerc(exp_bonus), ")");

			// generate recipe
			Recipe recipe = new Recipe(RecipeCategory.GetOrCreate(recipe_name));
			foreach (ResourceRatio res in converter.inputList)
			{
				recipe.AddInput(res.ResourceName, res.Ratio * exp_bonus);
			}
			foreach (ResourceRatio res in converter.outputList)
			{
				recipe.AddOutput(res.ResourceName, res.Ratio * exp_bonus, res.DumpExcess);
			}
			handler.AddRecipe(recipe);
		}

		private static void ProcessStockharvester(ModuleResourceHarvester harvester)
		{
			// calculate experience bonus
			float exp_bonus = harvester.UseSpecialistBonus
			  ? harvester.EfficiencyBonus * (harvester.SpecialistBonusBase + (harvester.SpecialistEfficiencyFactor * (vd.crewEngineerMaxlevel + 1)))
			  : 1.0f;

			// use part name as recipe name
			// - include crew bonus in the recipe name
			string recipe_name = Lib.BuildString(harvester.part.partInfo.title, " (efficiency: ", Lib.HumanReadablePerc(exp_bonus), ")");

			// generate recipe
			Recipe recipe = new Recipe(RecipeCategory.StockDrill);
			foreach (ResourceRatio res in harvester.inputList)
			{
				recipe.AddInput(res.ResourceName, res.Ratio);
			}
			recipe.AddOutput(harvester.ResourceName, harvester.Efficiency * exp_bonus, true);
			handler.AddRecipe(recipe);
		}

		private static void ProcessStocklab(ModuleScienceConverter lab)
		{
			handler.ElectricCharge.Consume(lab.powerRequirement, RecipeCategory.ScienceLab);
		}

		private static void ProcessRadiator(ModuleActiveRadiator radiator)
		{
			// note: IsCooling is not valid in the editor, for deployable radiators,
			// we will have to check if the related deploy module is deployed
			// we use PlannerController instead
			foreach (ModuleResource res in radiator.resHandler.inputResources)
			{
				handler.GetResource(res.name).Consume(res.rate, RecipeCategory.Radiator);
			}
		}

		private static void ProcessWheelMotor(ModuleWheelMotor motor)
		{
			foreach (ModuleResource res in motor.resHandler.inputResources)
			{
				handler.GetResource(res.name).Consume(res.rate, RecipeCategory.Wheel);
			}
		}

		private static void ProcessWheelSteering(ModuleWheelMotorSteering steering)
		{
			foreach (ModuleResource res in steering.resHandler.inputResources)
			{
				handler.GetResource(res.name).Consume(res.rate, RecipeCategory.Wheel);
			}
		}


		private static void ProcessLight(ModuleLight light)
		{
			if (light.useResources && light.isOn)
			{
				handler.ElectricCharge.Consume(light.resourceAmount, RecipeCategory.Light);
			}
		}


		//private static void ProcessScanner(KerbalismScansat m)
		//{
		//	handler.ElectricCharge.Consume(m.ec_rate, ResourceBroker.Scanner);
		//}

		private static void ProcessRTG(Part p, PartModule m)
		{
			double max_rate = Lib.ReflectionValue<float>(m, "BasePower");

			handler.ElectricCharge.Produce(max_rate, RecipeCategory.RTG);
		}

		private static void ProcessCryotank(Part p, PartModule m)
		{
			// is cooling available
			bool available = Lib.ReflectionValue<bool>(m, "CoolingEnabled");

			// get list of fuels, do nothing if no fuels
			IList fuels = Lib.ReflectionValue<IList>(m, "fuels");
			if (fuels == null)
				return;

			// get cooling cost
			double cooling_cost = Lib.ReflectionValue<float>(m, "CoolingCost");

			string fuel_name = "";
			double amount = 0.0;
			double total_cost = 0.0;
			double boiloff_rate = 0.0;

			// calculate EC cost of cooling
			foreach (object fuel in fuels)
			{
				fuel_name = Lib.ReflectionValue<string>(fuel, "fuelName");
				// if fuel_name is null, don't do anything
				if (fuel_name == null)
					continue;

				// get amount in the part
				amount = Lib.Amount(p, fuel_name);

				// if there is some fuel
				if (amount > double.Epsilon)
				{
					// if cooling is enabled
					if (available)
					{
						// calculate ec consumption
						total_cost += cooling_cost * amount * 0.001;
					}
					// if cooling is disabled
					else
					{
						// get boiloff rate per-second
						boiloff_rate = Lib.ReflectionValue<float>(fuel, "boiloffRate") / 360000.0f;

						// let it boil off
						handler.GetResource(fuel_name).Consume(amount * boiloff_rate, RecipeCategory.Cryotank);
					}
				}
			}

			// apply EC consumption
			handler.ElectricCharge.Consume(total_cost, RecipeCategory.Cryotank);
		}

		private static void ProcessEngines(ModuleEngines me)
		{
			// calculate thrust fuel flow
			double thrust_flow = me.maxFuelFlow * 1e3 * me.thrustPercentage;

			// search fuel types
			foreach (Propellant fuel in me.propellants)
			{
				switch (fuel.name)
				{
					case "ElectricCharge":  // mainly used for Ion Engines
						handler.ElectricCharge.Consume(thrust_flow * fuel.ratio, RecipeCategory.Engine);
						break;
					case "LqdHydrogen":     // added for cryotanks and any other supported mod that uses Liquid Hydrogen
						handler.GetResource("LqdHydrogen").Consume(thrust_flow * fuel.ratio, RecipeCategory.Engine);
						break;
				}
			}
		}

		private static void ProcessEnginesFX(ModuleEnginesFX mefx)
		{
			// calculate thrust fuel flow
			double thrust_flow = mefx.maxFuelFlow * 1e3 * mefx.thrustPercentage;

			// search fuel types
			foreach (Propellant fuel in mefx.propellants)
			{
				switch (fuel.name)
				{
					case "ElectricCharge":  // mainly used for Ion Engines
						handler.ElectricCharge.Consume(thrust_flow * fuel.ratio, RecipeCategory.Engine);
						break;
					case "LqdHydrogen":     // added for cryotanks and any other supported mod that uses Liquid Hydrogen
						handler.GetResource("LqdHydrogen").Consume(thrust_flow * fuel.ratio, RecipeCategory.Engine);
						break;
				}
			}
		}

		private static void ProcessRCS(ModuleRCS mr)
		{
			// calculate thrust fuel flow
			double thrust_flow = mr.maxFuelFlow * 1e3 * mr.thrustPercentage * mr.thrusterPower;

			// search fuel types
			foreach (Propellant fuel in mr.propellants)
			{
				switch (fuel.name)
				{
					case "ElectricCharge":  // mainly used for Ion RCS
						handler.ElectricCharge.Consume(thrust_flow * fuel.ratio, RecipeCategory.GetOrCreate("rcs", RecipeCategory.BrokerCategory.VesselSystem, "rcs"));
						break;
					case "LqdHydrogen":     // added for cryotanks and any other supported mod that uses Liquid Hydrogen
						handler.GetResource("LqdHydrogen").Consume(thrust_flow * fuel.ratio, RecipeCategory.GetOrCreate("rcs", RecipeCategory.BrokerCategory.VesselSystem, "rcs"));
						break;
				}
			}
		}

		private static void ProcessRCSFX(ModuleRCSFX mrfx)
		{
			// calculate thrust fuel flow
			double thrust_flow = mrfx.maxFuelFlow * 1e3 * mrfx.thrustPercentage * mrfx.thrusterPower;

			// search fuel types
			foreach (Propellant fuel in mrfx.propellants)
			{
				switch (fuel.name)
				{
					case "ElectricCharge":  // mainly used for Ion RCS
						handler.ElectricCharge.Consume(thrust_flow * fuel.ratio, RecipeCategory.GetOrCreate("rcs", RecipeCategory.BrokerCategory.VesselSystem, "rcs"));
						break;
					case "LqdHydrogen":     // added for cryotanks and any other supported mod that uses Liquid Hydrogen
						handler.GetResource("LqdHydrogen").Consume(thrust_flow * fuel.ratio, RecipeCategory.GetOrCreate("rcs", RecipeCategory.BrokerCategory.VesselSystem, "rcs"));
						break;
				}
			}
		}
	}
}

*/
