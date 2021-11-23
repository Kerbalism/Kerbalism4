using System;

namespace KERBALISM
{
	// Notes :
	// - this is a "poor man" fallback with many limitations and potential issues. Ideally a Kerbalism game should not have any ModuleResourceHarvester.
	// - this use the prefab config, so is incompatible with a module using stock upgrades or B9PS module switching
	// - ignore stock temperature mechanics
	// - ignore auto shutdown
	// - ignore depletion (stock seem to do the same)
	// - this cancel the post-facto simulation stock behavior by forcing lastUpdateTime to now

	public class ModuleResourceHarvesterHandler : TypedModuleHandler<ModuleResourceHarvester>
	{
		public override ActivationContext Activation => ActivationContext.Unloaded;

		// note : we assume the vessel can't move and that abundance is fixed for its position
		private double abundance;

		private Recipe recipe;
		private ProtoModuleValueBool IsActivated;
		private ProtoModuleValueDouble lastUpdateTime;

		public override void OnStart()
		{
			if (!ProtoModuleValueBool.TryGet(protoModule.moduleValues, nameof(ModuleResourceHarvester.IsActivated), out IsActivated)
			|| !ProtoModuleValueDouble.TryGet(protoModule.moduleValues, "lastUpdateTime", out lastUpdateTime))
			{
				handlerIsEnabled = false;
				return;
			}

			AbundanceRequest request = new AbundanceRequest
			{
				Altitude = VesselData.Altitude,
				BodyId = VesselData.MainBody.flightGlobalsIndex,
				CheckForLock = false,
				Latitude = VesselData.Latitude,
				Longitude = VesselData.Longitude,
				ResourceType = (HarvestTypes)prefabModule.HarvesterType,
				ResourceName = prefabModule.ResourceName
			};

			abundance = ResourceMap.Instance.GetAbundance(request);

			if (abundance < prefabModule.HarvestThreshold)
			{
				handlerIsEnabled = false;
				return;
			}

			recipe = new Recipe(partData.Title, RecipeCategory.Harvester);

			foreach (ResourceRatio ir in prefabModule.inputList)
				recipe.AddInput(ir.ResourceName, ir.Ratio);

			recipe.AddOutput(prefabModule.ResourceName, abundance * prefabModule.Efficiency, false, true);
		}


		public override void OnUpdate(double elapsedSec)
		{
			if (!IsActivated.Value)
				return;

			// get crew bonus
			double expBonus = Lib.GetBaseConverterEfficiencyBonus(prefabModule, VesselData);

			// execute and scale recipe with crew bonus
			recipe.RequestExecution(VesselData.ResHandler, null, expBonus);

			// prevent stock post-facto catchup by forcing BaseConverter.lastUpdateTime to now
			lastUpdateTime.Value = Planetarium.GetUniversalTime();
		}
	}
}
