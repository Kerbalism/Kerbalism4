using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace KERBALISM.SteppedSim
{
	public struct SubstepVessel
	{
		public double3 position;
		public double rotation;
		public bool isLanded;
		public double3 LLA;
		public double directIrradiance;
		public double bodyAlbedoIrradiance;
		public double bodyEmissiveIrradiance;
		public int mainBodyIndex;
	}

	/* Things to do:
	 * For each vessel, get vector to all bodies
	 * For each body:
	 *	Test occlusion
	 *	Compute flux (sun direct flux plus per-body reflected flux)
	 *	Include body core flux (body-generated outbound radiation)


	* For each body:
	*	Compute core flux baseline (falls off with distance^2)
	*	Calculate albedo flux (can't re-emit sun if it can't see the sun)
	*	Can probably skip the 3 microWatts/m^2 of the CMB...
	*	*/
}
