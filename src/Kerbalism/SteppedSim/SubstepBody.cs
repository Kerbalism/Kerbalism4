using Unity.Collections;
using Unity.Mathematics;

namespace KERBALISM.SteppedSim
{
	public struct SubstepBody
	{
		public double3 position;
		public Planetarium.CelestialFrame bodyFrame;
		public double radius;

		public double solarLuminosity;    // For stars
		public double albedo;

		public static bool Occluded(double3 a, double3 b, NativeArray<SubstepBody> occluders)
		{
			// Given a, b, and a point v, the perpendicular distance from v to ab is:
			//  mag(av) if av dot ab < 0
			//  mag(bv) if bv dot ab > 0
			//  mag(ab cross av) / mag(ab) otherwise
			double3 ab = b - a;
			if (Unity.Burst.CompilerServices.Hint.Unlikely(math.lengthsq(ab) < 1))
				return false;
			bool occluded = false;
			for (int i=0; i<occluders.Length; i++)
			{
				double3 v = occluders[i].position;
				double3 av = v - a;
				double3 bv = v - b;
				double dist;
				if (math.dot(av, ab) < 0)
					dist = math.length(av);
				else if (math.dot(bv, ab) > 0)
					dist = math.length(bv);
				else
					dist = math.length(math.cross(ab, av)) / math.length(ab);
				occluded |= dist <= occluders[i].radius;
			}
			return occluded;
		}
	}
}
