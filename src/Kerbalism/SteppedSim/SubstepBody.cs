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
	public struct SubstepBody
	{
		public double3 position;
		public Planetarium.CelestialFrame bodyFrame;
		public double radius;
	}
}
