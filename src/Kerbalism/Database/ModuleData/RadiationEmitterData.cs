using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM
{
	public class RadiationEmitterData : ModuleData<ModuleKsmRadiationEmitter, RadiationEmitterData>, IRadiationEmitter
	{
		// IRadiationEmitter implementation
		public bool EnableInterface => moduleIsEnabled;
		public double RadiationRate { get; set; }
		public bool IsActive => RadiationRate > 0.0;
		public bool HighEnergy => modulePrefab.highEnergy;
		public PartRadiationData RadiationData => partData.radiationData;
		public ModuleData ModuleData => this;

		public override void OnSave(ConfigNode node)
		{
			node.AddValue("rate", RadiationRate);
		}

		public override void OnLoad(ConfigNode node)
		{
			RadiationRate = Lib.ConfigValue(node, "rate", 0.0);
		}
	}
}
