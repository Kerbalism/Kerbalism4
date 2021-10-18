using System;
using System.Collections.Generic;
using System.Text;

namespace KERBALISM
{
	public abstract class ComfortInfoBase
	{
		private static StringBuilder sb = new StringBuilder();

		public ComfortDefinition Definition { get; private set; }
		public string Name => Definition.name;
		public string Title => Definition.title;
		public double Level { get; protected set; }
		public double MaxBonus => Definition.maxBonus;
		public double Bonus => Level * Definition.maxBonus;

		public ComfortInfoBase(ComfortDefinition definition)
		{
			Definition = definition;
		}

		public virtual void Reset() => Level = 0.0;

		public abstract void ComputeLevel(VesselDataBase vd);

		public static string GetComfortsInfo(IEnumerable<ComfortInfoBase> comforts)
		{
			sb.Clear();

			sb.Append(Lib.Color("Comfort", Lib.Kolor.Yellow, true));
			sb.AppendAtPos(Lib.Color("Level", Lib.Kolor.Yellow, true), 100f);
			sb.AppendAtPos(Lib.Color("Bonus", Lib.Kolor.Yellow, true), 150f);
			sb.AppendKSPNewLine();

			foreach (ComfortInfoBase comfort in comforts)
			{
				sb.Append(comfort.Title);
				sb.AppendAtPos(Lib.Color(comfort.Level > 0.0, comfort.Level.ToString("P0"), Lib.Kolor.Green, Lib.Kolor.Orange), 100f);
				sb.AppendAtPos(Lib.BuildString((comfort.Bonus * 100.0).ToString("F0"), " / ", comfort.MaxBonus.ToString("P0")), 150f);
				sb.AppendKSPNewLine();
			}

			return sb.ToString();
		}
	}


	public class ComfortModuleInfo : ComfortInfoBase
	{
		private List<ComfortValue> comforts = new List<ComfortValue>();

		public ComfortModuleInfo(ComfortDefinition definition) : base(definition)
		{
		}

		public override void Reset()
		{
			base.Reset();
			comforts.Clear();
		}

		public void AddComfortValue(ComfortValue comfort)
		{
			comforts.Add(comfort);
		}

		public override void ComputeLevel(VesselDataBase vd)
		{
			int crewCount = vd.CrewCount;

			if (crewCount == 0)
			{
				foreach (ComfortValue comfort in comforts)
				{
					Level = Math.Max(Level, comfort.quality);
				}
				return;
			}

			comforts.Sort((a, b) => a.quality.CompareTo(b.quality));

			foreach (ComfortValue comfort in comforts)
			{
				Level += comfort.quality * Math.Min(crewCount, comfort.seats);
				crewCount -= comfort.seats;

				if (crewCount <= 0)
					break;
			}

			Level /= vd.CrewCount;
		}
	}

	public class ComfortNotAlone : ComfortInfoBase
	{
		public ComfortNotAlone(ComfortDefinition definition) : base(definition) { }

		public override void ComputeLevel(VesselDataBase vd)
		{
			switch (vd.CrewCount)
			{
				case 0:
				case 1:
					Level = 0.0; break;
				case 2:
					Level = 0.5; break;
				case 3:
					Level = 0.8; break;
				default:
					Level = 1.0; break;
			}
		}
	}

	public class ComfortCallHome : ComfortInfoBase
	{
		public ComfortCallHome(ComfortDefinition definition) : base(definition) { }

		public override void ComputeLevel(VesselDataBase vd)
		{
			if (!vd.ConnectionInfo.Linked)
			{
				Level = 0.0;
			}
			else
			{
				// min when connected : 50 %
				// 0.05 MB/s to reach 100 %
				Level = Math.Min((vd.ConnectionInfo.DataRate / 0.1) + 0.5, 1.0); 
			}
		}
	}

	public class ComfortFirmGround : ComfortInfoBase
	{
		public ComfortFirmGround(ComfortDefinition definition) : base(definition) { }

		public override void ComputeLevel(VesselDataBase vd)
		{
			if (!vd.EnvLanded)
			{
				Level = 0.0;
			}
			else
			{
				// min when landed : 75 %
				// 0.25g on body to reach 100 %
				Level = Math.Min(vd.Gravity + 0.75, 1.0);
			}
		}
	}
}
