using System;
using System.Collections.Generic;

namespace KERBALISM
{
	// Notes :
	// - this use the prefab config, so won't reflect changes made using stock upgrades or B9PS module switching
	// - ignore stock temperature mechanics
	// - fully refactored 11/2020

	public class ModuleSpaceObjectDrillHandler : TypedModuleHandler<BaseDrill>
	{
		public override ActivationContext Activation => ActivationContext.Unloaded;

		public override string[] ModuleTypeNames => moduleTypeNames;
		private static string[] moduleTypeNames = new string[] { nameof(ModuleAsteroidDrill), nameof(ModuleCometDrill) };

		public bool canRun;

		private ProtoPartSnapshot asteroidPart;
		private ProtoPartModuleSnapshot asteroidModule;
		private double massThreshold;

		private Recipe recipe;

		private ProtoModuleValueBool IsActivated;
		private ProtoModuleValueDouble lastUpdateTime;
		private ProtoModuleValueDouble currentMass;

		public override void OnStart()
		{
			// we have no way to check if the drill is in contact with the asteroid mesh while unloaded, so
			// we require the drill to be activated when it was last loaded.
			if (!ProtoModuleValueDouble.TryGet(protoModule.moduleValues, "lastUpdateTime", out lastUpdateTime)
				|| !ProtoModuleValueBool.TryGet(protoModule.moduleValues, nameof(BaseDrill.IsActivated), out IsActivated)
				|| !IsActivated.Value)
			{
				canRun = false;
				handlerIsEnabled = false;
				return;
			}

			recipe = new Recipe(partData.Title, RecipeCategory.Harvester, OnRecipeExecuted);

			string spaceObjectInfoModuleName;
			string resourceModuleName;
			if (prefabModule is ModuleAsteroidDrill asteroidDrill)
			{
				recipe.AddInput(VesselResHandler.ElectricChargeId, asteroidDrill.PowerConsumption);
				spaceObjectInfoModuleName = nameof(ModuleAsteroidInfo);
				resourceModuleName = nameof(ModuleAsteroidResource);
			}
			else if (prefabModule is ModuleCometDrill cometDrill)
			{
				recipe.AddInput(VesselResHandler.ElectricChargeId, cometDrill.PowerConsumption);
				spaceObjectInfoModuleName = nameof(ModuleCometInfo);
				resourceModuleName = nameof(ModuleCometResource);
			}
			else
			{
				canRun = false;
				handlerIsEnabled = false;
				return;
			}

			// find the asteroid part and module
			// note : Incorrectly handle a situation where there are multiple asteroids on the vessel : the first found will always be used.
			// note 2 : funny thing, stock does the same thing.
			foreach (ProtoPartSnapshot protoPart in ((VesselData)VesselData).Vessel.protoVessel.protoPartSnapshots)
			{
				foreach (ProtoPartModuleSnapshot protoModule in protoPart.modules)
				{
					if (protoModule.moduleName == spaceObjectInfoModuleName)
					{
						// get the remaining mass of the asteroid
						if (!ProtoModuleValueDouble.TryGet(protoModule.moduleValues, nameof(ModuleSpaceObjectInfo.currentMass), out currentMass))
							continue;

						// get the mass threshold under witch the asteroid resources are considered depleted 
						massThreshold = Lib.Proto.GetDouble(protoModule, nameof(ModuleSpaceObjectInfo.massThreshold));

						// skip that asteroid if it is depleted (but maybe there is another ?)
						if (currentMass.Value <= massThreshold)
							continue;

						asteroidPart = protoPart;
						asteroidModule = protoModule;
						break;
					}
				}
				if (asteroidPart != null)
					break;
			}

			if (asteroidPart == null)
			{
				canRun = false;
				handlerIsEnabled = false;
				return;
			}

			// get each resource module to get the resource name and its abundance
			// abundance is a persistent value stored on the protomodule, 
			// the resource name is an instance field that we need to get from the prefab
			int count = Math.Min(asteroidPart.modules.Count, asteroidPart.partPrefab.Modules.Count);
			for (int i = 0; i < count; i++)
			{
				if (asteroidPart.modules[i].moduleName == resourceModuleName
				&& asteroidPart.partPrefab.Modules[i] is ModuleSpaceObjectResource modulePrefab
				&& VesselResHandler.allKSPResourceIdsByName.TryGetValue(modulePrefab.resourceName, out int resId))
				{
					double abundance = Lib.Proto.GetFloat(asteroidPart.modules[i], nameof(ModuleAsteroidResource.abundance));
					// the 1e-9 threeshold is from the stock module code
					if (abundance <= 1e-9)
						continue;

					recipe.AddOutput(resId, abundance * prefabModule.Efficiency * prefabModule.EfficiencyBonus, true, true);
				}
			}

            if (recipe.outputs.Count == 0)
            {
	            canRun = false;
				handlerIsEnabled = false;
            }
		}

        public override void OnUpdate(double elapsedSec)
        {
	        if (!IsActivated.Value)
		        return;

	        // get crew bonus
			double expBonus = Lib.GetBaseConverterEfficiencyBonus(prefabModule, VesselData);

			// execute and scale recipe with crew bonus
			recipe.RequestExecution(VesselData.ResHandler, expBonus);

			// prevent stock post-facto catchup by forcing BaseConverter.lastUpdateTime to now
			lastUpdateTime.Value = Planetarium.GetUniversalTime();
        }

        public void OnRecipeExecuted(double elapsedSec)
        {
	        // consume asteroid mass
			double mass = currentMass.Value;

	        foreach (RecipeOutput output in recipe.outputs)
		        mass -= output.ExecutedRate * ((VesselResourceKSP) output.vesselResource).Density;

	        // if everything has been mined, stop forever
	        if (mass <= massThreshold)
	        {
		        mass = massThreshold;
		        IsActivated.Value = false;
		        canRun = false;
		        handlerIsEnabled = false;
	        }

			currentMass.Value = mass;
        }
	}
}
