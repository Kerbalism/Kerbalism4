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
			public string chargeId;
			public VesselVirtualPartResource charge;
			public double chargeRate;
			public double maxRadiation;
			public bool charging;
			public bool discharging;

			public double RadiationRemoved => maxRadiation * charge.Level;

			public ArrayEffectData(ModuleKsmRadiationCoil masterModule, List<ModuleKsmRadiationCoil> coilModules)
			{
				charge = masterModule.moduleHandler.VesselData.ResHandler.GetOrCreateVirtualResource<VesselVirtualPartResource>();
				chargeId = charge.Name;

				foreach (ModuleKsmRadiationCoil coilModule in coilModules)
				{
					coilModule.moduleHandler.CreateChargeResource(chargeId);
				}

				chargeRate = coilModules.Count * masterModule.ecChargeRate;
			}

			public ArrayEffectData(ConfigNode coilDataNode)
			{
				chargeId = Lib.ConfigValue(coilDataNode, "chargeId", string.Empty);
				chargeRate = Lib.ConfigValue(coilDataNode, "chargeRate", 0.0);
				maxRadiation = Lib.ConfigValue(coilDataNode, "maxRadiation", 0.0);
				charging = Lib.ConfigValue(coilDataNode, "charging", false);
				discharging = Lib.ConfigValue(coilDataNode, "discharging", false);
			}

			public void Save(ConfigNode coilDataNode)
			{
				coilDataNode.AddValue("chargeId", chargeId);
				coilDataNode.AddValue("chargeRate", chargeRate);
				coilDataNode.AddValue("maxRadiation", maxRadiation);
				coilDataNode.AddValue("charging", charging);
				coilDataNode.AddValue("discharging", discharging);
			}

			public void ChargeUpdate(RadiationCoilHandler masterCoil, double elapsedSec)
			{
				if (charge.Level > 0.0)
				{
					masterCoil.VesselData.ResHandler.Consume(charge.Name, masterCoil.modulePrefab.chargeLossRate * charge.Level * elapsedSec, ResourceBroker.CoilArray);
				}

				// note : the discharging recipe has to be added first for discharging 
				// to have priority over charging when both are enabled
				if (discharging)
				{
					Recipe dischargingRecipe = new Recipe(ResourceBroker.CoilArray);
					dischargingRecipe.AddInput(charge.Name, chargeRate * elapsedSec);
					dischargingRecipe.AddOutput("ElectricCharge", chargeRate * elapsedSec, false);
					masterCoil.VesselData.ResHandler.AddRecipe(dischargingRecipe);
				}

				if (charging)
				{
					Recipe chargingRecipe = new Recipe(ResourceBroker.CoilArray);
					chargingRecipe.AddInput("ElectricCharge", chargeRate * elapsedSec);
					chargingRecipe.AddOutput(charge.Name, chargeRate * elapsedSec, false);
					masterCoil.VesselData.ResHandler.AddRecipe(chargingRecipe);
				}
			}

		}

		public ArrayEffectData effectData;
		public bool isDeployed;

		public void CreateEffectData(ModuleKsmRadiationCoil masterModule, List<ModuleKsmRadiationCoil> coilModules)
		{
			effectData = new ArrayEffectData(masterModule, coilModules);
		}

		public void UpdateMaxRadiation(double maxRadiation)
		{
			effectData.maxRadiation = maxRadiation;
		}

		public override void OnFixedUpdate(double elapsedSec)
		{
			if (effectData != null)
			{
				effectData.ChargeUpdate(this, elapsedSec);
			}
		}

		public override void OnStart()
		{
			if (effectData != null)
			{
				VesselData.ResHandler.TryGetResource(effectData.chargeId, out effectData.charge);
			}
		}

		public void CreateChargeResource(string chargeId)
		{
			partData.virtualResources.AddResource(chargeId, 0.0, loadedModule.ecChargeRequired);
		}

		public void RemoveChargeResource(string chargeId)
		{
			partData.virtualResources.RemoveResource(chargeId);
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
