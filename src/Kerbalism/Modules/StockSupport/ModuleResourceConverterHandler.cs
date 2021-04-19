using System;

namespace KERBALISM
{
    public class ModuleResourceConverterHandler : TypedModuleHandler<ModuleResourceConverter>
	{
		public override ActivationContext Activation => ActivationContext.Unloaded;

		// Supported derivatives :
		// - ModuleKPBSConverter is a very slightly modified ModuleResourceConverter from KPBS (that we should be patching away in processes anyway), see :
		//   https://github.com/Nils277/KerbalPlanetaryBaseSystems/blob/master/Sources/PlanetarySurfaceStructures/ModuleKPBSConverter.cs
		// - Removed support for the "FissionReactor" derivative from NFE : this hasn't been properly tested in ages and is currently unused.
		//   Moreover, there are just too many extra features that we ignore, hopefully at some point we will introduce proper in-house replacement.
		//   https://github.com/ChrisAdderley/NearFutureElectrical/blob/master/Source/NearFutureElectrical/FissionReactor.cs

		public override string[] ModuleTypeNames => moduleTypeNames;
		private static string[] moduleTypeNames = new string[] { nameof(ModuleResourceConverter), "ModuleKPBSConverter" };


		public override void OnFixedUpdate(double elapsedSec)
		{
			// Notes :
			// - this is a "poor man" fallback with many limitations and potential issues. Ideally a Kerbalism game should not have any ModuleResourceConverter.
			// - this use the prefab config, so is incompatible with a module using stock upgrades or B9PS module switching
			// - ignore stock temperature mechanic
			// - ignore auto shutdown
			// - non-mandatory resources 'dynamically scale the ratios', that is exactly what mandatory resources do too (DERP ALERT)
			// - this cancel the post-facto simulation stock behavior by forcing lastUpdateTime to now

			// At some point it would be nice to refactor this a bit :
			// - it has bad performance
			// - it could take advantage of TypedModuleHandler features to handle upgrades/module switching and avoid being working on the protomodule/prefab.

			// if active
			if (Lib.Proto.GetBool(protoModule, nameof(ModuleResourceConverter.IsActivated)))
			{
				// determine if vessel is full of all output resources
				// note: comparing against previous amount
				// note : this is bad code that doesn't work at high warp and will cause resource sim instabilities
				bool full = true;
				foreach (var or in prefabModule.outputList)
				{
					VesselResource res = VesselData.ResHandler.GetResource(or.ResourceName);
					full &= (res.Level >= prefabModule.FillAmount - double.Epsilon);
				}

				// if not full
				if (!full)
				{
					// deduce crew bonus
					int exp_level = -1;
					if (prefabModule.UseSpecialistBonus)
					{
						Vessel v = ((VesselData)VesselData).Vessel;
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

					// create and commit recipe
					Recipe recipe = new Recipe(ResourceBroker.StockConverter);
					foreach (var ir in prefabModule.inputList)
					{
						recipe.AddInput(ir.ResourceName, ir.Ratio * exp_bonus * elapsedSec);
					}
					foreach (var or in prefabModule.outputList)
					{
						recipe.AddOutput(or.ResourceName, or.Ratio * exp_bonus * elapsedSec, or.DumpExcess);
					}
					VesselData.ResHandler.AddRecipe(recipe);
				}

				// undo stock behavior by forcing BaseConverter.lastUpdateTime to now
				Lib.Proto.Set(protoModule, "lastUpdateTime", Planetarium.GetUniversalTime());
			}
		}
	}
}
