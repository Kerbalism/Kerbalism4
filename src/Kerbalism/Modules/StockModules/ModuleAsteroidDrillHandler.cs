using System;
using System.Collections.Generic;

namespace KERBALISM
{
	// Notes :
	// - this use the prefab config, so won't reflect changes made using stock upgrades or B9PS module switching
	// - ignore stock temperature mechanics
	// - fully refactored 11/2020
	// - TODO : ModuleCometDrill is an exact 1:1 replica of ModuleAsteroidDrill, copypasting this handler is needed (but test it first)

	public class ModuleAsteroidDrillHandler : TypedModuleHandler<ModuleAsteroidDrill>
	{
		public override ActivationContext Activation => ActivationContext.Unloaded;

		private sealed class AsteroidResource
        {
			public readonly VesselKSPResource resource;
			public readonly double abundance;
			public readonly bool dump;
			public double lastOutput;

            public AsteroidResource(VesselKSPResource resource, double abundance, bool dump)
            {
                this.resource = resource;
				this.abundance = abundance;
				this.dump = dump;
				lastOutput = 0.0;
			}
        }

		private sealed class AsteroidResourceDefinition
        {
			public readonly string name;
			public readonly int moduleIndex;
			public readonly bool dump;

            public AsteroidResourceDefinition(string name, int moduleIndex)
            {
                this.name = name;
                this.moduleIndex = moduleIndex;
				dump = name != "Ore"; // TODO : making this player configurable would be nice
            }
        }

		private static List<AsteroidResourceDefinition> asteroidResourceDefinitions;
		private static bool isValid = true;

		private ProtoPartSnapshot asteroidPart;
		private ProtoPartModuleSnapshot asteroidInfo;
		private List<AsteroidResource> resources;
		private double massThreshold;
		private VesselVirtualResource virtualOutput;


		public override void OnStart()
        {
            if (!isValid)
            {
				handlerIsEnabled = false;
				return;
			}

            if (asteroidResourceDefinitions == null)
            {
				asteroidResourceDefinitions = new List<AsteroidResourceDefinition>();
				Part asteroidPrefab = PartLoader.getPartInfoByName("PotatoRoid")?.partPrefab;

                if (asteroidPrefab == null)
                {
					isValid = false;
					handlerIsEnabled = false;
					return;
				}

				for (int i = 0; i < asteroidPrefab.Modules.Count; i++)
				{
					if (asteroidPrefab.Modules[i] is ModuleAsteroidResource asteroidResource)
					{
						asteroidResourceDefinitions.Add(new AsteroidResourceDefinition(asteroidResource.resourceName, i));
					}
				}

                if (asteroidResourceDefinitions.Count == 0)
                {
					isValid = false;
					handlerIsEnabled = false;
					return;
				}
			}

			// IsActivated will be true only if the drill was actively working (all checks done) when last unloaded
			if (!Lib.Proto.GetBool(protoModule, nameof(ModuleAsteroidDrill.IsActivated)))
            {
				handlerIsEnabled = false;
				return;
			}

			// note : we have no way of handling a situation where there are multiple asteroids on the vessel, the first found will always be used.
			foreach (ProtoPartSnapshot protoPart in ((VesselData)VesselData).Vessel.protoVessel.protoPartSnapshots)
			{
                foreach (ProtoPartModuleSnapshot protoModule in protoPart.modules)
                {
                    if (protoModule.moduleName == nameof(ModuleAsteroidInfo))
                    {
						asteroidPart = protoPart;
						asteroidInfo = protoModule;
						massThreshold = Lib.Proto.GetDouble(protoModule, nameof(ModuleAsteroidInfo.massThreshold));
						break;
					}
                }
				if (asteroidPart != null)
					break;
			}

			if (asteroidPart == null)
			{
				handlerIsEnabled = false;
				return;
			}

			resources = new List<AsteroidResource>();

			foreach (AsteroidResourceDefinition resourceDefinition in asteroidResourceDefinitions)
            {
                if (asteroidPart.modules[resourceDefinition.moduleIndex].moduleName != nameof(ModuleAsteroidResource))
                {
					handlerIsEnabled = false;
					return;
				}

				double abundance = Lib.Proto.GetFloat(asteroidPart.modules[resourceDefinition.moduleIndex], nameof(ModuleAsteroidResource.abundance));

				// only add the resource if we have some capacity for it (see further note on outputs)
				// the 1e-9 threeshold is from the stock module code
				if (abundance > 1e-9 && VesselData.ResHandler.TryGetResource(resourceDefinition.name, out VesselKSPResource resource) && resource.Capacity > 0.0)
                {
					resources.Add(new AsteroidResource(resource, abundance, resourceDefinition.dump));
				}
			}

			if (resources.Count == 0)
			{
				handlerIsEnabled = false;
				return;
			}

			virtualOutput = VesselData.ResHandler.GetOrCreateVirtualResource<VesselVirtualResource>();
			virtualOutput.SetAmount(0.0);
			virtualOutput.SetCapacity(1.0);
		}

        public override void OnFixedUpdate(double elapsedSec)
		{
			// prevent stock post-facto simulation by forcing lastUpdateTime to now
			Lib.Proto.Set(protoModule, "lastUpdateTime", Planetarium.GetUniversalTime());

			double mass = Lib.Proto.GetDouble(asteroidInfo, nameof(ModuleAsteroidInfo.currentMass));

			// if everything has been mined, stop forever
			if (mass <= massThreshold)
            {
				handlerIsEnabled = false;
				return;
			}
				
			// deduce crew bonus
			int expLevel = -1;
			if (prefabModule.UseSpecialistBonus)
			{
				foreach (ProtoCrewMember c in Lib.CrewList(((VesselData)VesselData).Vessel))
				{
					if (c.experienceTrait.Effects.Find(k => k.Name == prefabModule.ExperienceEffect) != null)
					{
						expLevel = Math.Max(expLevel, c.experienceLevel);
					}
				}
			}
			double expBonus = expLevel < 0
			? prefabModule.SpecialistBonusBase
			: prefabModule.SpecialistBonusBase + (prefabModule.SpecialistEfficiencyFactor * (expLevel + 1));

			Recipe recipe = new Recipe(ResourceBroker.StockDrill);
			recipe.AddInput("ElectricCharge", prefabModule.PowerConsumption * elapsedSec);
			recipe.AddOutput(virtualOutput.Name, 1.0, false);

			double lastLostMass = 0.0;

			foreach (AsteroidResource asteroidResource in resources)
            {
				lastLostMass += virtualOutput.Amount * asteroidResource.lastOutput * asteroidResource.resource.Density;
				asteroidResource.lastOutput = asteroidResource.abundance * prefabModule.Efficiency * prefabModule.EfficiencyBonus * expBonus * elapsedSec;
				// Note : stock allow dumping outputs if there is some space for at least one output. We have no way of replicating that, but
				// we ensured that we have at least some capacity for every "resources" entry. Ideally we would give the player the option to select 
				// what to dump, but the default "dump everything but ore" is good enough for now.
				recipe.AddOutput(asteroidResource.resource.Name, asteroidResource.lastOutput, asteroidResource.dump); 
			}

			VesselData.ResHandler.AddRecipe(recipe);

			virtualOutput.SetAmount(0.0);

			// consume asteroid mass
			if (lastLostMass > 0.0)
            {
				Lib.Proto.Set(asteroidInfo, nameof(ModuleAsteroidInfo.currentMassVal), Math.Max(mass - lastLostMass, massThreshold));
			}
		}
	}
}
