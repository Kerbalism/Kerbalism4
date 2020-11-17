using System;

namespace KERBALISM
{
	public partial class PartRadiationData
	{
		public const double ALUMINUM_DENSITY = 2.7;

		// Constant wall thickness of all parts in meters used for radiation occlusion.
		// Resulting occlusion will use HVL values derived from aluminium.
		public const double PART_WALL_THICKNESS_OCCLUSION = 0.004;

		// Constant wall thickness used to determine the part structural mass that will be considered for occlusion.
		// The formula will use this to determine the part hull mass (using the part surface and aluminum density)
		// and substract it to the KSP-defined part mass to get the structural mass.
		// The is separate from the occlusion thickness to counteract the fact that KSP part masses are quite a bit too
		// high, especially non-tank parts.
		// TODO : For RO or Smurff that that use more accurate mass fraction, make this configurable in the profile.
		public const double PART_WALL_THICKNESS_MASSFRACTION = 0.02;

		private static class PartWallOcclusion
		{
			private static double highHVLCrossingFactor = Math.Pow(0.5, Settings.WallThicknessForOcclusion * 2.0 / Radiation.aluminiumHVL_Gamma25MeV);
			private static double lowHVLCrossingFactor = Math.Pow(0.5, Settings.WallThicknessForOcclusion * 2.0 / Radiation.aluminiumHVL_Gamma1MeV);
			private static double highHVLNonCrossingFactor = Math.Pow(0.5, Settings.WallThicknessForOcclusion / Radiation.aluminiumHVL_Gamma25MeV);
			private static double lowHVLNonCrossingFactor = Math.Pow(0.5, Settings.WallThicknessForOcclusion / Radiation.aluminiumHVL_Gamma1MeV);

			public static double RadiationFactor(bool highPowerRad, bool crossing)
			{
				if (highPowerRad)
				{
					if (crossing)
					{
						return highHVLCrossingFactor;
					}
					else
					{
						return highHVLNonCrossingFactor;
					}
				}
				else
				{
					if (crossing)
					{
						return lowHVLCrossingFactor;
					}
					else
					{
						return lowHVLNonCrossingFactor;
					}
				}
			}
		}

		private class PartStructuralOcclusion
		{
			// for non wall occluder, this is an "occlusion material volume vs part volume" factor
			protected double occlusionFactor;

			public void Update(double partMass, double partSurface, double partVolume)
			{
				double wallMass = (partVolume - (partSurface * Settings.WallThicknessForMassFraction)) * ALUMINUM_DENSITY;
				double structuralMass = Math.Max(0.0, partMass - wallMass);
				occlusionFactor = (structuralMass / ALUMINUM_DENSITY) / partVolume;
			}

			public double RadiationFactor(double penetration, bool highPowerRad)
			{
				if (occlusionFactor == 0.0)
				{
					return 1.0;
				}

				return Math.Pow(0.5, occlusionFactor * penetration / (highPowerRad ? Radiation.aluminiumHVL_Gamma25MeV : Radiation.aluminiumHVL_Gamma1MeV));
			}
		}

		private class ResourceOcclusion
		{
			private Radiation.ResourceOcclusion occlusionDefinition;
			private double volumePerUnit;
			private int resourceId;
			public bool IsWallOccluder => occlusionDefinition.IsWallResource;

			// for a wall occluder, this is the wall thickness
			// for non wall occluder, this is an "occlusion material volume vs part volume" factor
			private double occlusionFactor;

			public ResourceOcclusion(PartResourceDefinition partResourceDefinition)
			{
				Setup(partResourceDefinition);
			}

			private void Setup(PartResourceDefinition partResourceDefinition)
			{
				occlusionDefinition = Radiation.GetResourceOcclusion(partResourceDefinition);
				volumePerUnit = partResourceDefinition.volume;
				resourceId = partResourceDefinition.id;
			}

			public void UpdateOcclusion(PartResource partResource, double partSurface, double partVolume)
			{
				if (partResource.info.id != resourceId)
				{
					Setup(partResource.info);
				}

				if (partResource.amount <= 0.0)
				{
					occlusionFactor = 0.0;
					return;
				}

				double volume = (partResource.amount * volumePerUnit) / 1000.0;

				if (IsWallOccluder)
				{
					occlusionFactor = volume / partSurface;
				}
				else
				{
					occlusionFactor = volume / partVolume;
				}
			}

			public double VolumeRadiationFactor(double penetration, bool highPowerRad)
			{
				if (occlusionFactor == 0.0)
				{
					return 1.0;
				}

				return Math.Pow(0.5, occlusionFactor * penetration / (highPowerRad ? occlusionDefinition.HighHVL : occlusionDefinition.LowHVL));
			}

			public double WallRadiationFactor(bool highPowerRad, bool crossing)
			{
				if (occlusionFactor <= 0.0)
				{
					return 1.0;
				}

				return Math.Pow(0.5, (crossing ? occlusionFactor * 2.0 : occlusionFactor) / (highPowerRad ? occlusionDefinition.HighHVL : occlusionDefinition.LowHVL));
			}
		}
	}
}
