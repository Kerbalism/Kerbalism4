using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Steamworks;
using static KERBALISM.HabitatHandler;

namespace KERBALISM
{
	// Data structure holding the vessel wide habitat state, evaluated from VesselData
	public class VesselHabitat
	{
		/// <summary> volume (m3) of enabled and under breathable conditions habitats</summary>
		public double livingVolume = 0.0;

		/// <summary> livingVolume (m3) per kerbal</summary>
		public double volumePerCrew = 0.0;

		/// <summary> volume (m3) of all habitats</summary>
		public double totalVolume = 0.0;

		/// <summary> surface (m2) of all pressurized habitats, enabled of not. Habitats using the outside air are ignored </summary>
		public double pressurizedSurface = 0.0;

		/// <summary> volume (m3) of all pressurized habitats, enabled of not. Habitats using the outside air are ignored </summary>
		public double pressurizedVolume = 0.0;

		/// <summary> pressure (%) of all pressurized habitats, enabled of not. Habitats using the outside air are ignored </summary>
		public double pressure = 0.0;

		/// <summary> [0.0 ; 1.0] amount of crew members not living with their helmets (pressurized hab / outside air) vs total crew count</summary>
		public double pressureFactor = 0.0;

		public int breathingCrewCount = 0;

		/// <summary> [0.0 ; 1.0] % of CO2 in the air (averaged in case there is a mix of pressurized / outside air habitats)</summary>
		public double poisoningLevel = 0.0;

		/// <summary> surface (m2) of all enabled habitats, excluding depressurized habitats that aren't crewed</summary>
		public double shieldingSurface = 0.0;

		/// <summary> amount of shielding resource (1 unit = 1m2 of 20mm thick pb) for all enabled habitats, excluding depressurized habitats that aren't crewed</summary>
		public double shieldingAmount = 0.0;

		/// <summary> in rad/s, radiation received by all considered habitats : non pressurized unmanned parts are ignored</summary>
		public double radiationRate = 0.0;

		/// <summary> average storm radiation protection, in % </summary>
		public double sunRadiationFactor = 0.0;

		public double radiationAmbiantOcclusion;

		public double radiationEmittersOcclusion;

		public double emittersRadiation = 0.0;

		public double activeRadiationShielding = 0.0;

		/// <summary> average artificial gravity in gees, from the vessel rotation or centrifuge habitats</summary>
		public double artificialGravity = 0.0;

		/// <summary> current gravity, artificial or from the main body if landed</summary>
		public double gravity = 0.0;

		public ComfortInfoBase[] comforts;

		/// <summary> active comforts count</summary>
		public int comfortsActiveCount;

		/// <summary> average of all comforts level</summary>
		public double comfortsAverageLevel;

		/// <summary> sum of all comforts bonuses</summary>
		public double comfortsTotalBonus;

		public List<HabitatHandler> Habitats { get; private set; } = new List<HabitatHandler>();

		public VesselHabitat()
		{
			comforts = new ComfortInfoBase[ComfortDefinition.definitions.Count];
			for (int i = 0; i < ComfortDefinition.definitions.Count; i++)
			{
				comforts[i] = ComfortDefinition.definitions[i].GetComfortInfo();
			}
		}

		public double LivingSpaceFactor(double idealLivingSpace)
		{
			return Math.Min(volumePerCrew / idealLivingSpace, 1.0);
		}

		public double ComfortBonusFactor()
		{
			return Math.Min(comfortsTotalBonus, 1.0);
		}

		public void ResetBeforeModulesUpdate(VesselDataBase vd)
		{
			livingVolume = volumePerCrew = totalVolume
				= pressurizedSurface = pressurizedVolume = pressure
				= pressureFactor = poisoningLevel = shieldingSurface
				= shieldingAmount = comfortsAverageLevel = comfortsTotalBonus = radiationAmbiantOcclusion = radiationEmittersOcclusion
				= radiationRate = sunRadiationFactor = emittersRadiation = activeRadiationShielding = artificialGravity = gravity = 0.0;

			comfortsActiveCount = breathingCrewCount = 0;

			// the list of habitats will iterated over by every radiation emitter/shield, so build the list once.
			Habitats.Clear();
			foreach (HabitatHandler habitat in vd.Parts.AllModulesOfType<HabitatHandler>())
			{
				Habitats.Add(habitat);
			}
		}

		public void EvaluateAfterModuleUpdate(VesselDataBase vd)
		{
			double pressurizedPartsAtmoAmount = 0.0; // for calculating pressure level : all pressurized parts, enabled or not
			int pressurizedPartsCrewCount = 0; // crew in all pressurized parts, pressure modifier = pressurizedPartsCrewCount / totalCrewCount
			int wasteConsideredPartsCount = 0;
			double centrifugeVolume = 0;
			double radiationConsideredPartVolume = 0.0;

			foreach (ComfortInfoBase comfort in comforts)
			{
				comfort.Reset();
			}

			for (int i = 0; i < Habitats.Count; i++)
			{
				HabitatHandler habitat = Habitats[i];
				totalVolume += habitat.definition.volume;

				if (habitat.isEnabled)
				{
					switch (habitat.pressureState)
					{
						case PressureState.Breatheable:
							livingVolume += habitat.definition.volume;
							shieldingSurface += habitat.definition.surface;
							shieldingAmount += habitat.shieldingAmount;

							pressurizedPartsCrewCount += habitat.crewCount;
							radiationRate += habitat.partData.radiationData.RadiationRate * habitat.definition.volume;
							sunRadiationFactor += habitat.partData.radiationData.SunRadiationFactor * habitat.definition.volume;
							emittersRadiation += habitat.partData.radiationData.EmittersRadiation * habitat.definition.volume;
							activeRadiationShielding += habitat.partData.radiationData.ActiveShielding * habitat.definition.volume;
							radiationAmbiantOcclusion += habitat.partData.radiationData.AmbiantOcclusion * habitat.definition.volume;
							radiationEmittersOcclusion += habitat.partData.radiationData.EmittersOcclusion * habitat.definition.volume;
							radiationConsideredPartVolume += habitat.definition.volume;

							artificialGravity += habitat.gravity * habitat.definition.volume;
							centrifugeVolume += habitat.definition.volume;

							foreach (ComfortValue comfort in habitat.definition.comforts)
							{
								((ComfortModuleInfo)comforts[comfort.definition.definitionIndex]).AddComfortValue(comfort);
							}

							break;
						case PressureState.Pressurized:
						case PressureState.DepressurizingAboveThreshold:
							breathingCrewCount += habitat.crewCount;
							pressurizedVolume += habitat.definition.volume;
							livingVolume += habitat.definition.volume;
							pressurizedSurface += habitat.definition.surface;
							shieldingSurface += habitat.definition.surface;
							shieldingAmount += habitat.shieldingAmount;

							pressurizedPartsAtmoAmount += habitat.atmoAmount;
							pressurizedPartsCrewCount += habitat.crewCount;

							// waste evaluation
							poisoningLevel += habitat.wasteLevel;
							wasteConsideredPartsCount++;
							radiationRate += habitat.partData.radiationData.RadiationRate * habitat.definition.volume;
							sunRadiationFactor += habitat.partData.radiationData.SunRadiationFactor * habitat.definition.volume;
							emittersRadiation += habitat.partData.radiationData.EmittersRadiation * habitat.definition.volume;
							activeRadiationShielding += habitat.partData.radiationData.ActiveShielding * habitat.definition.volume;
							radiationAmbiantOcclusion += habitat.partData.radiationData.AmbiantOcclusion * habitat.definition.volume;
							radiationEmittersOcclusion += habitat.partData.radiationData.EmittersOcclusion * habitat.definition.volume;
							radiationConsideredPartVolume += habitat.definition.volume;

							artificialGravity += habitat.gravity * habitat.definition.volume;
							centrifugeVolume += habitat.definition.volume ;

							foreach (ComfortValue comfort in habitat.definition.comforts)
							{
								((ComfortModuleInfo)comforts[comfort.definition.definitionIndex]).AddComfortValue(comfort);
							}

							break;
						case PressureState.AlwaysDepressurized:
						case PressureState.Depressurized:
						case PressureState.Pressurizing:
						case PressureState.DepressurizingBelowThreshold:
							breathingCrewCount += habitat.crewCount;
							if (habitat.crewCount > 0)
							{
								shieldingSurface += habitat.definition.surface;
								shieldingAmount += habitat.shieldingAmount;
								poisoningLevel += habitat.wasteLevel;
								wasteConsideredPartsCount++;
								radiationRate += habitat.partData.radiationData.RadiationRate * habitat.definition.volume;
								sunRadiationFactor += habitat.partData.radiationData.SunRadiationFactor * habitat.definition.volume;
								emittersRadiation += habitat.partData.radiationData.EmittersRadiation * habitat.definition.volume;
								activeRadiationShielding += habitat.partData.radiationData.ActiveShielding * habitat.definition.volume;
								radiationAmbiantOcclusion += habitat.partData.radiationData.AmbiantOcclusion * habitat.definition.volume;
								radiationEmittersOcclusion += habitat.partData.radiationData.EmittersOcclusion * habitat.definition.volume;
								radiationConsideredPartVolume += habitat.definition.volume;

								artificialGravity += habitat.gravity * habitat.definition.volume;
								centrifugeVolume += habitat.definition.volume;
							}
							// waste in suits evaluation
							break;
					}
				}
				else
				{
					switch (habitat.pressureState)
					{
						case PressureState.Breatheable:
							// nothing here
							break;
						case PressureState.Pressurized:
						case PressureState.DepressurizingAboveThreshold:
							pressurizedVolume += habitat.definition.volume;
							pressurizedSurface += habitat.definition.surface;
							pressurizedPartsAtmoAmount += habitat.atmoAmount;
							// waste evaluation
							break;
						case PressureState.AlwaysDepressurized:
						case PressureState.Depressurized:
						case PressureState.Pressurizing:
						case PressureState.DepressurizingBelowThreshold:
							// waste in suits evaluation
							break;
					}
				}
			}

			int crewCount = vd.CrewCount;
			volumePerCrew = crewCount > 0 ? livingVolume / crewCount : 0.0;
			pressure = pressurizedVolume > 0.0 ? pressurizedPartsAtmoAmount / pressurizedVolume : 0.0;

			pressureFactor = crewCount > 0 ? ((double)pressurizedPartsCrewCount / (double)crewCount) : 0.0; // 0.0 when pressurized, 1.0 when depressurized

			poisoningLevel = wasteConsideredPartsCount > 0 ? poisoningLevel / wasteConsideredPartsCount : 0.0;



			if (radiationConsideredPartVolume > 0.0)
			{
				radiationRate /= radiationConsideredPartVolume;
				sunRadiationFactor /= radiationConsideredPartVolume;
				emittersRadiation /= radiationConsideredPartVolume;
				activeRadiationShielding /= radiationConsideredPartVolume;
				radiationAmbiantOcclusion /= radiationConsideredPartVolume;
				radiationEmittersOcclusion /= radiationConsideredPartVolume;
			}

			if (centrifugeVolume > 0.0)
			{
				artificialGravity = artificialGravity / centrifugeVolume;
			}

			if (vd.EnvLanded)
				gravity = vd.Gravity;
			else
				gravity = 0.0;

			foreach (ComfortInfoBase comfort in comforts)
			{
				comfort.ComputeLevel(vd);
				if (comfort.Level > 0.0)
				{
					comfortsActiveCount++;
				}

				comfortsAverageLevel += comfort.Level;

				comfortsTotalBonus += comfort.Bonus;
			}

			comfortsAverageLevel /= comforts.Length;
		}
	}
}
