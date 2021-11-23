using System;
using Experience;

namespace KERBALISM
{
	// Notes :
	// - this is a "poor man" fallback with many limitations and potential issues. Ideally a Kerbalism game should not have any ModuleResourceConverter.
	// - this use the prefab config, so is incompatible with a module using stock upgrades or B9PS module switching
	// - ignore temperature/heat management
	// - ignore auto shutdown
	// - ignore FillAmount/TakeAmount
	// - this cancel the post-facto simulation stock behavior by forcing lastUpdateTime to now
	// Supported derivatives :
	// - ModuleKPBSConverter is a very slightly modified ModuleResourceConverter from KPBS (that we should be patching away in processes anyway), see :
	//   https://github.com/Nils277/KerbalPlanetaryBaseSystems/blob/master/Sources/PlanetarySurfaceStructures/ModuleKPBSConverter.cs
	// - Removed support for the "FissionReactor" derivative from NFE : this hasn't been properly tested in ages and is currently unused.
	//   Moreover, there are just too many extra features that we ignore, hopefully at some point we will introduce proper in-house replacement.
	//   https://github.com/ChrisAdderley/NearFutureElectrical/blob/master/Source/NearFutureElectrical/FissionReactor.cs

	public class ModuleResourceConverterHandler : TypedModuleHandler<ModuleResourceConverter>
	{
		public override ActivationContext Activation => ActivationContext.Unloaded;

		public override string[] ModuleTypeNames => moduleTypeNames;
		private static string[] moduleTypeNames = new string[] { nameof(ModuleResourceConverter), "ModuleKPBSConverter" };

		private Recipe recipe;
		private ProtoModuleValueBool IsActivated;
		private ProtoModuleValueDouble lastUpdateTime;

		public override void OnStart()
		{
			if (!ProtoModuleValueBool.TryGet(protoModule.moduleValues, nameof(ModuleResourceConverter.IsActivated), out IsActivated)
			|| !ProtoModuleValueDouble.TryGet(protoModule.moduleValues, "lastUpdateTime", out lastUpdateTime))
			{
				handlerIsEnabled = false;
				return;
			}

			recipe = new Recipe(partData.Title, RecipeCategory.Converter);

			foreach (ResourceRatio input in prefabModule.inputList)
			{
				recipe.AddInput(input.ResourceName, input.Ratio);
			}

			foreach (ResourceRatio output in prefabModule.outputList)
			{
				recipe.AddOutput(output.ResourceName, output.Ratio, output.DumpExcess, true);

				if (output.ResourceName == VesselResHandler.ElectricChargeDefinition.name)
					recipe.category = RecipeCategory.ECGenerator;
			}
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
