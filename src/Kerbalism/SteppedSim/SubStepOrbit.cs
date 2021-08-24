using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;

namespace KERBALISM.SteppedSim
{
	public struct SubStepOrbit
	{
		public int refBodyIndex;
		public double3 defaultPosition;

		public double inclination;
		public double eccentricity;
		public double semiMajorAxis;
		public double LAN;
		public double argumentOfPeriapsis;
		public double meanAnomalyAtEpoch;

		public double epoch;
		public Planetarium.CelestialFrame OrbitFrame;
		public Planetarium.CelestialFrame PlanetariumZup;
		//public double3 an;
		//public double3 eccVec;
		public double3 h;
		public double meanMotion;
		//public double meanAnomaly;
		public double ObT;
		public double ObTAtEpoch;
		public double period;
		//public double orbitPercent;
		//public double refBodyGravParam;
		public double semiLatusRectum;
		public SubStepOrbit(Orbit stockOrbit, CelestialBody refBody, in Dictionary<CelestialBody, int> refBodies)
		{
			refBodyIndex = -1;
			refBodies.TryGetValue(refBody, out refBodyIndex);
			defaultPosition = double3.zero;
			inclination = stockOrbit.inclination;
			eccentricity = stockOrbit.eccentricity;
			semiMajorAxis = stockOrbit.semiMajorAxis;
			LAN = stockOrbit.LAN;
			argumentOfPeriapsis = stockOrbit.argumentOfPeriapsis;
			meanAnomalyAtEpoch = stockOrbit.meanAnomalyAtEpoch;

			epoch = stockOrbit.epoch;

			// Orbit.Init()
			OrbitFrame = stockOrbit.OrbitFrame;
			/*
			an = new double3(stockOrbit.an.x, stockOrbit.an.y, stockOrbit.an.z);
			eccVec = new double3(stockOrbit.eccVec.x, stockOrbit.eccVec.y, stockOrbit.eccVec.z);
			*/
			h = new double3(stockOrbit.h.x, stockOrbit.h.y, stockOrbit.h.z);
			meanMotion = stockOrbit.meanMotion;
			//meanAnomaly = stockOrbit.meanAnomaly;
			
			ObT = stockOrbit.ObT;
			ObTAtEpoch = stockOrbit.ObTAtEpoch;
			period = stockOrbit.period;
			//orbitPercent = stockOrbit.orbitPercent;
			semiLatusRectum = math.lengthsq(h) / refBody.gravParameter;
			PlanetariumZup = Planetarium.Zup;
		}
		public double3 getRelativePositionAtUT(double UT)
		{
			var obt = GetObtAtUT(UT);
			return getRelativePositionAtT(obt);
		}

		public double GetObtAtUT(double UT)
		{
			double obt;
			if (double.IsInfinity(UT))
				return eccentricity < 1 ? double.NaN : UT;
			if (this.eccentricity < 1)
			{
				obt = (UT - this.epoch + this.ObTAtEpoch) % this.period;
				if (obt > this.period / 2)
					obt -= this.period;
			}
			else
				obt = this.ObTAtEpoch + (UT - this.epoch);
			return obt;
		}

		public double3 getRelativePositionAtT(double T) => this.getRelativePositionFromTrueAnomaly(this.GetTrueAnomaly(this.solveEccentricAnomaly(T * this.meanMotion, this.eccentricity)));
		public double3 getRelativePositionFromTrueAnomaly(double tA) => this.getPositionFromTrueAnomaly(tA, true);

		public double3 getPositionFromTrueAnomaly(double tA, bool worldToLocal)
		{
			math.sincos(tA, out double sintA, out double costA);
			Vector3d r = this.semiLatusRectum / (1.0 + this.eccentricity * costA) * (this.OrbitFrame.X * costA + this.OrbitFrame.Y * sintA);
			if (worldToLocal)
				r = PlanetariumZup.WorldToLocal(r);
			return new double3(r.x, r.y, r.z);
		}

		public double GetTrueAnomaly(double E)
		{
			double num1;
			if (this.eccentricity < 1.0)
			{
				math.sincos(E / 2, out double sinE, out double cosE);
				num1 = 2 * Math.Atan2(math.sqrt(1 + this.eccentricity) * sinE, math.sqrt(1 - this.eccentricity) * cosE);
			}
			else if (double.IsPositiveInfinity(E))
				num1 = Math.Acos(-1.0 / this.eccentricity);
			else if (double.IsNegativeInfinity(E))
				num1 = -Math.Acos(-1.0 / this.eccentricity);
			else
			{
				double num2 = math.sinh(E / 2);
				double num3 = math.cosh(E / 2);
				num1 = 2.0 * Math.Atan2(math.sqrt(this.eccentricity + 1.0) * num2, math.sqrt(this.eccentricity - 1.0) * num3);
			}
			return num1;
		}


		public double solveEccentricAnomaly(double M, double ecc, double maxError = 1E-07, int maxIterations = 8)
		{
			if (this.eccentricity >= 1.0)
				return solveEccentricAnomalyHyp(M, eccentricity, maxError);
			else if (this.eccentricity < 0.8)
				return this.solveEccentricAnomalyStd(M, this.eccentricity, maxError);
			else
				return this.solveEccentricAnomalyExtremeEcc(M, this.eccentricity, maxIterations);
		}

		private double solveEccentricAnomalyStd(double M, double ecc, double maxError = 1E-07)
		{
			double error = 1;
			double eccAnomaly = M + ecc * math.sin(M) + 0.5 * ecc * ecc * math.sin(2 * M);
			while (math.abs(error) > maxError)
			{
				double num3 = eccAnomaly - ecc * math.sin(eccAnomaly);
				error = (M - num3) / (1.0 - ecc * math.cos(eccAnomaly));
				eccAnomaly += error;
			}
			return eccAnomaly;
		}

		private double solveEccentricAnomalyExtremeEcc(double M, double ecc, int iterations = 8)
		{
			double num1 = M + 0.85 * this.eccentricity * math.sign(math.sin(M));
			for (int index = 0; index < iterations; ++index)
			{
				double num2 = ecc * math.sin(num1);
				double num3 = ecc * math.cos(num1);
				double num4 = num1 - num2 - M;
				double num5 = 1 - num3;
				double num6 = num2;
				double denom = (num5 + math.sign(num5) * math.sqrt(math.abs(16 * num5 * num5 - 20 * num4 * num6)));
				if (denom == 0)
					return double.NaN;
				num1 += -5 * num4 / denom;
			}
			return num1;
		}

		private double solveEccentricAnomalyHyp(double M, double ecc, double maxError = 1E-07)
		{
			if (double.IsInfinity(M))
			{
				return M;
			}
			else
			{
				double num1 = 1.0;
				double num2 = 2.0 * M / ecc;
				double num3 = math.log(math.sqrt(num2 * num2 + 1.0) + num2);
				while (math.abs(num1) > maxError)
				{
					num1 = (this.eccentricity * math.sinh(num3) - num3 - M) / (this.eccentricity * math.cosh(num3) - 1.0);
					num3 -= num1;
				}
				return num3;
			}
		}

	}
}
