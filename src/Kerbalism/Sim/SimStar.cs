using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class SimStar
	{
		public CelestialBody body;
		private double irradianceAtAU;
		private double luminosity;

		public double Luminosity => luminosity;

		public SimStar(CelestialBody body, double solarFluxAtAU)
		{
			this.body = body;
			this.irradianceAtAU = solarFluxAtAU;
		}

		// This must be called after the "stars" list is populated (because it use AU > GetParentStar)
		public void InitSolarFluxTotal()
		{
			luminosity = irradianceAtAU * Sim.AU * Sim.AU * Math.PI * 4.0;
		}

		/// <summary>Irradiance in W/m² at the given distance from this sun/star</summary>
		/// <param name="distanceIsFromSunSurface">set to true if 'distance' is from the surface</param>
		public double Irradiance(double distance, bool distanceIsFromStarSurface = false)
		{
			if (distanceIsFromStarSurface) distance += body.Radius;

			return luminosity / (Math.PI * 4 * distance * distance);
		}
	}
}
