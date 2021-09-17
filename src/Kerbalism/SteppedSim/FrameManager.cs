using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM.SteppedSim
{
	public class FrameManager
	{
		public LinkedList<SubstepFrame> Frames = new LinkedList<SubstepFrame>();
		private readonly List<SubstepFrame> oldFrames = new List<SubstepFrame>();

		public void Add(SubstepFrame f)
		{
			Frames.AddLast(f);
		}

		public void ClearExpiredFrames(double ts)
		{
			oldFrames.Clear();
			oldFrames.AddRange(Frames.Where(x => x.timestamp < ts));
			foreach (var f in oldFrames)
			{
				Frames.Remove(f);
				f.Release();
			}
		}
	}
}
