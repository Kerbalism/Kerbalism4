using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class RadiationCoilHandler : KsmModuleHandler<ModuleKsmRadiationCoil, RadiationCoilHandler, RadiationCoilDefinition>
	{
		public class ArrayEffectData
		{
			public int chargeId;
			public VesselResourceAbstract chargeResource;
			public double chargeRate;
			public double charge;
			public double maxRadiation;
			public bool charging;
			public bool discharging;

			public Recipe passiveDischargeRecipe;
			public Recipe dischargeRecipe;
			public Recipe chargeRecipe;

			public double RadiationRemoved => maxRadiation * chargeResource.Level;

			public ArrayEffectData(ModuleKsmRadiationCoil masterModule, List<ModuleKsmRadiationCoil> coilModules)
			{
				chargeResource = masterModule.moduleHandler.VesselData.ResHandler.AddNewAbstractResourceToHandler();
				chargeResource.SetCapacity(masterModule.ecChargeRequired);
				chargeId = chargeResource.id;

				chargeRate = coilModules.Count * masterModule.ecChargeRate;

				// note : the discharging recipe must have a higher priority than charging
				// so this works correctly when both charging and discharging is enabled

				passiveDischargeRecipe = new Recipe("Charge losses", RecipeCategory.RadiationShield);
				passiveDischargeRecipe.AddInput(VesselResHandler.ElectricChargeId, masterModule.chargeLossRate);

				chargeRecipe = new Recipe("Charging", RecipeCategory.RadiationShield);
				chargeRecipe.AddInput(VesselResHandler.ElectricChargeId, chargeRate);
				chargeRecipe.AddOutput(chargeId, chargeRate, false, false);

				dischargeRecipe = new Recipe("Discharging", RecipeCategory.RadiationShield);
				dischargeRecipe.AddInput(chargeId, chargeRate);
				dischargeRecipe.AddOutput(VesselResHandler.ElectricChargeId, chargeRate, false, false);

			}

			public ArrayEffectData(ConfigNode coilDataNode)
			{
				charge = Lib.ConfigValue(coilDataNode, "charge", 0.0);
				chargeRate = Lib.ConfigValue(coilDataNode, "chargeRate", 0.0);
				maxRadiation = Lib.ConfigValue(coilDataNode, "maxRadiation", 0.0);
				charging = Lib.ConfigValue(coilDataNode, "charging", false);
				discharging = Lib.ConfigValue(coilDataNode, "discharging", false);
			}

			public void Save(ConfigNode coilDataNode)
			{
				coilDataNode.AddValue("charge", charge);
				coilDataNode.AddValue("chargeRate", chargeRate);
				coilDataNode.AddValue("maxRadiation", maxRadiation);
				coilDataNode.AddValue("charging", charging);
				coilDataNode.AddValue("discharging", discharging);
			}

			public void ChargeUpdate(RadiationCoilHandler masterCoil, double elapsedSec)
			{
				//if (chargeResource.Level > 0.0)
				//{
				//	masterCoil.VesselData.ResHandler.Consume(charge.Name, masterCoil.modulePrefab.chargeLossRate * charge.Level * elapsedSec, ResourceBroker.CoilArray);
				//}

				//if (discharging)
				//{
				//	dischargeRecipe.Update(masterCoil.VesselData.ResHandler, 1.0);
				//}

				//if (charging)
				//{
				//	chargeRecipe.Update(masterCoil.VesselData.ResHandler, 1.0);
				//}
			}

		}

		public ArrayEffectData effectData;
		public bool isDeployed;

		public void OnRecipeExecuted(Recipe recipe, double elapsedSec)
		{

		}

		public void CreateEffectData(ModuleKsmRadiationCoil masterModule, List<ModuleKsmRadiationCoil> coilModules)
		{
			effectData = new ArrayEffectData(masterModule, coilModules);
		}

		public void UpdateMaxRadiation(double maxRadiation)
		{
			effectData.maxRadiation = maxRadiation;
		}

		public override void OnUpdate(double elapsedSec)
		{
			if (effectData != null)
			{
				effectData.ChargeUpdate(this, elapsedSec);
			}
		}

		public override void OnSave(ConfigNode node)
		{
			node.AddValue(nameof(isDeployed), isDeployed);

			if (effectData != null)
			{
				effectData.Save(node);
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			isDeployed = Lib.ConfigValue(node, nameof(isDeployed), false);

			if (node.HasValue("chargeId"))
			{
				effectData = new ArrayEffectData(node);
			}
		}


	}
}
