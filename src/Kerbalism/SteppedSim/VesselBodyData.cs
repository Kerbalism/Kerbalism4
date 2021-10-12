using System;

namespace KERBALISM
{
	public class VesselBodyData
	{
		public readonly CelestialBody body;

		public readonly int bodyIndex;

		public readonly bool isStar;

		public Vector3d direction;

		public double distance;

		public double visibility;

		public readonly FluxData fluxData;

		public VesselBodyData(int bodyIndex)
		{
			this.bodyIndex = bodyIndex;
			body = FlightGlobals.Bodies[bodyIndex];
			isStar = Sim.IsStar(body);

			if (body.isStar)
				fluxData = new StarFlux(this);
			else
				fluxData = new NonStarFlux(this);
		}

		public void UpdateVesselPosition(Vector3d vesselPosition)
		{
			direction = body.position - vesselPosition;
			distance = direction.magnitude;
			direction /= distance;
		}

		public static void Factory(out VesselBodyData[] bodyVesselDataArray, out StarFlux[] starfluxArray, out NonStarFlux[] nonStarFluxArray)
		{
			bodyVesselDataArray = new VesselBodyData[FlightGlobals.Bodies.Count];
			starfluxArray = new StarFlux[Sim.stars.Count];
			nonStarFluxArray = new NonStarFlux[FlightGlobals.Bodies.Count - Sim.stars.Count];
			int starCount = 0;
			int nonStarCount = 0;
			for (int i = 0; i < bodyVesselDataArray.Length; i++)
			{
				VesselBodyData body = new VesselBodyData(i);
				if (body.isStar)
				{
					starfluxArray[starCount] = (StarFlux)body.fluxData;
					starCount++;
				}
				else
				{
					nonStarFluxArray[nonStarCount] = (NonStarFlux)body.fluxData;
					nonStarCount++;
				}

				bodyVesselDataArray[i] = body;
			}
		}

		public static void Clear(VesselBodyData[] bodyVesselDataArray)
		{
			for (int i = 0; i < bodyVesselDataArray.Length; i++)
			{
				VesselBodyData body = bodyVesselDataArray[i];
				body.visibility = 0.0;
				body.fluxData.Clear();
			}
		}
	}

	public abstract class FluxData
	{
		public readonly VesselBodyData bodyData;

		public FluxData(VesselBodyData body)
		{
			bodyData = body;
		}

		public abstract void Clear();
	}

	public class StarFlux : FluxData
	{
		public StarFlux(VesselBodyData body) : base(body) { }

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

		public override void Clear()
		{
			sunlightFactor = 0.0;
			directFlux = 0.0;
			directRawFlux = 0.0;
			directRawFluxProportion = 0.0;
		}
	}

	public class NonStarFlux : FluxData
	{
		public NonStarFlux(VesselBodyData body) : base(body) { }

		/// <summary> indirect visible light & thermal irradiance (in W/m²) reflected from all stars and emitted by this body at the vessel position </summary>
		public double albedoFlux;

		/// <summary> indirect thermal irradiance (in W/m²) caused by the stars irradiance on that body, re-emitted at the vessel position</summary>
		public double emissiveFlux;

		/// <summary> thermal irradiance (in W/m²) from this body "core" (induced by the body own intrinsic sources) emitted toward the vessel</summary>
		public double coreFlux;

		public override void Clear()
		{
			albedoFlux = 0.0;
			emissiveFlux = 0.0;
			coreFlux = 0.0;
		}
	}
}
