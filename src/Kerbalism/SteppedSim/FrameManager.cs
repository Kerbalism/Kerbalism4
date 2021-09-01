using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM.SteppedSim
{
	public class FrameManager
	{
		public Dictionary<double, SubstepFrame> Frames = new Dictionary<double, SubstepFrame>();
		private readonly List<double> oldFrameTimes = new List<double>();

		public void ClearExpiredFrames(double ts)
		{
			oldFrameTimes.Clear();
			oldFrameTimes.AddRange(Frames.Keys.Where(x => x < ts));
			foreach (var t in oldFrameTimes)
			{
				var f = Frames[t];
				f.Release();
				Frames.Remove(t);
			}
		}
	}
}
