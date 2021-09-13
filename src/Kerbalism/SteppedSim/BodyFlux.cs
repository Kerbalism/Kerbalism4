using System;

namespace KERBALISM
{
	public struct BodyFlux : IEquatable<BodyFlux>
	{
		public CelestialBody body;

		public int bodyIndex;

		public Vector3d direction;

		public double distance;

		/// <summary> indirect visible light & thermal irradiance (in W/m²) reflected from all stars and emitted by this body at the vessel position </summary>
		public double albedoFlux;

		/// <summary> indirect thermal irradiance (in W/m²) caused by the stars irradiance on that body, re-emitted at the vessel position</summary>
		public double emissiveFlux;

		/// <summary> thermal irradiance (in W/m²) from this body "core" (induced by the body own intrinsic sources) emitted toward the vessel</summary>
		public double coreFlux;

		public override bool Equals(object obj)
		{
			return obj is BodyFlux other && Equals(other);
		}

		public bool Equals(BodyFlux other)
		{
			return bodyIndex == other.bodyIndex;
		}

		public override int GetHashCode()
		{
			return bodyIndex;
		}

		public static bool operator ==(BodyFlux x, BodyFlux y)
		{
			return x.bodyIndex == y.bodyIndex;
		}
		public static bool operator !=(BodyFlux x, BodyFlux y)
		{
			return x.bodyIndex != y.bodyIndex;
		}
	}

	public struct StarFlux : IEquatable<StarFlux>
	{
		public CelestialBody body;

		public int bodyIndex;

		public Vector3d direction;

		public double distance;

		/// <summary>
		/// return 1.0 when the vessel is in direct sunlight, 0.0 when in shadow
		/// <para/> in analytic evaluation, this is a scalar of representing the fraction of time spent in sunlight
		/// </summary>
		public double sunlightFactor;

		/// <summary> direct solar irradiance at vessel position in W/m², including atmospheric absorption </summary>
		public double directFlux;

		/// <summary> direct solar irradiance at vessel position in W/m², ignoring atmospheric absorption and occlusion</summary>
		public double directRawFlux;

		/// <summary> proportion of this sun flux in the total flux at the vessel position (ignoring atmosphere and occlusion) </summary>
		public double directRawFluxProportion;

		public override bool Equals(object obj)
		{
			return obj is StarFlux other && Equals(other);
		}

		public bool Equals(StarFlux other)
		{
			return bodyIndex == other.bodyIndex;
		}

		public override int GetHashCode()
		{
			return bodyIndex;
		}

		public static bool operator ==(StarFlux x, StarFlux y)
		{
			return x.bodyIndex == y.bodyIndex;
		}
		public static bool operator !=(StarFlux x, StarFlux y)
		{
			return x.bodyIndex != y.bodyIndex;
		}
	}
}
