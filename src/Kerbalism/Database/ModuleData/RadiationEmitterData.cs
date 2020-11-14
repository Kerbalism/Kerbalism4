using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM
{
	public class RadiationEmitterData : ModuleData<ModuleKsmRadiationEmitter, RadiationEmitterData>, IRadiationEmitter
	{
		// IRadiationEmitter implementation
		public double RadiationRate { get; set; }
		public int ModuleId => ID;
		public bool IsActive => true;
		public bool HighEnergy => modulePrefab.highEnergy;
		public PartRadiationData RadiationData => partData.radiationData;
	}
}
