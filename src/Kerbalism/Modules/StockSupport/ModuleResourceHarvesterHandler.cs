using System;

namespace KERBALISM
{
    public class ModuleResourceHarvesterHandler : TypedModuleHandler<ModuleResourceHarvester>
	{
		public override ActivationContext Activation => ActivationContext.Unloaded;

		public override void OnFixedUpdate(double elapsedSec)
		{
			// Notes :
			// - this is a "poor man" fallback with many limitations and potential issues. Ideally a Kerbalism game should not have any ModuleResourceHarvester.
			// - this use the prefab config, so is incompatible with a module using stock upgrades or B9PS module switching
			// - ignore stock temperature mechanics
			// - ignore auto shutdown
			// - ignore depletion (stock seem to do the same)
			// - this cancel the post-facto simulation stock behavior by forcing lastUpdateTime to now

			// At some point it would be nice to refactor this a bit :
			// - it has bad performance (the AbundanceRequest every update especially)
			// - it could take advantage of TypedModuleHandler features to handle upgrades/module switching and avoid being working on the protomodule/prefab.

			// if active
			if (!Lib.Proto.GetBool(protoModule, nameof(ModuleResourceHarvester.IsActivated)))
				return;

			// undo stock behavior by forcing last_update_time to now
			Lib.Proto.Set(protoModule, "lastUpdateTime", Planetarium.GetUniversalTime());

			// do nothing if full
			// note: comparing against previous amount
			// note : this is bad code that doesn't work at high warp and will cause resource sim instabilities
			if (VesselData.ResHandler.GetResource(prefabModule.ResourceName).Level >= prefabModule.FillAmount - double.Epsilon)
				return;

			Vessel v = ((VesselData)VesselData).Vessel;

			// deduce crew bonus
			int exp_level = -1;
			if (prefabModule.UseSpecialistBonus)
			{
				foreach (ProtoCrewMember c in Lib.CrewList(v))
				{
					if (c.experienceTrait.Effects.Find(k => k.Name == prefabModule.ExperienceEffect) != null)
					{
						exp_level = Math.Max(exp_level, c.experienceLevel);
					}
				}
			}
			double exp_bonus = exp_level < 0
				? prefabModule.EfficiencyBonus * prefabModule.SpecialistBonusBase
				: prefabModule.EfficiencyBonus * (prefabModule.SpecialistBonusBase + (prefabModule.SpecialistEfficiencyFactor * (exp_level + 1)));

			// detect amount of ore in the ground
			
			AbundanceRequest request = new AbundanceRequest
			{
				Altitude = v.altitude,
				BodyId = v.mainBody.flightGlobalsIndex,
				CheckForLock = false,
				Latitude = v.latitude,
				Longitude = v.longitude,
				ResourceType = (HarvestTypes)prefabModule.HarvesterType,
				ResourceName = prefabModule.ResourceName
			};
			double abundance = ResourceMap.Instance.GetAbundance(request);

			// if there is actually something (should be if active when unloaded)
			if (abundance > prefabModule.HarvestThreshold)
			{
				// create and commit recipe
				Recipe recipe = new Recipe(ResourceBroker.StockDrill);
				foreach (var ir in prefabModule.inputList)
				{
					recipe.AddInput(ir.ResourceName, ir.Ratio * elapsedSec);
				}
				recipe.AddOutput(prefabModule.ResourceName, abundance * prefabModule.Efficiency * exp_bonus * elapsedSec, true);
				VesselData.ResHandler.AddRecipe(recipe);
			}
		}
	}
}
