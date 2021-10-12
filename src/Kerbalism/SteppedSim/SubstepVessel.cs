using Unity.Mathematics;

namespace KERBALISM.SteppedSim
{
	public struct SubstepVessel
	{
		public double3 position;
		public double rotation;
		public bool isLanded;
		public double3 LLA;
		public int mainBodyIndex;
		public double atmosphericDensity;
	}
}
