using Unity.Collections;
using Unity.Mathematics;

namespace KERBALISM.SteppedSim
{
	public struct SubstepBody
	{
		public double3 position;
		public Planetarium.CelestialFrame bodyFrame;
		public double radius;
		public double atmosphereDepth;
		public double atmDensityASL;
		public double radiusAtmoFactor;

		public double solarLuminosity;    // For stars
		public double albedo;
		public double bodyCoreThermalFlux;  // Basically solarLuminosity for bodies

		public static bool Occluded(double3 a, double3 b, NativeSlice<SubstepBody> occluders)
		{
			// Given a, b, and a point v, the perpendicular distance from v to ab is:
			//  mag(av) if av dot ab < 0
			//  mag(bv) if bv dot ab > 0
			//  mag(ab cross av) / mag(ab) otherwise
			double3 ab = b - a;
			var abLen2 = math.lengthsq(ab);
			if (Unity.Burst.CompilerServices.Hint.Unlikely(abLen2 < 1))
				return false;
			bool occluded = false;
			for (int i=0; i<occluders.Length; i++)
			{
				double3 v = occluders[i].position;
				var radiusSq = occluders[i].radius * occluders[i].radius;
				double3 av = v - a;
				double3 bv = v - b;
				if (math.dot(av, ab) < 0)
					occluded |= math.lengthsq(av) <= radiusSq;
				else if (math.dot(bv, ab) > 0)
					occluded |= math.lengthsq(bv) <= radiusSq;
				else
					occluded |= math.lengthsq(math.cross(ab, av)) <= radiusSq * abLen2;
			}
			return occluded;
		}
	}
}
