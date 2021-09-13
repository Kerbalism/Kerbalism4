using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class TempStarData
	{
		public static TempStarData[] array;

		public readonly int index;
		public double rawIrrandiance;
		public double irradiance;
		public double sunlightFactor;

		public TempStarData(int starIndex)
		{
			index = starIndex;
		}

		public static void Init()
		{
			array = new TempStarData[Sim.starIndexes.Count];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = new TempStarData(Sim.starIndexes[i]);
			}
		}

		public static void Clear()
		{
			for (int i = array.Length - 1; i >= 0; i--)
			{
				TempStarData data = array[i];
				data.irradiance = 0.0;
				data.rawIrrandiance = 0.0;
				data.sunlightFactor = 0.0;
			}
		}
	}

	public class TempBodyData
	{
		public static TempBodyData[] array;

		public readonly int index;
		public double albedo;
		public double emissive;
		public double core;

		public TempBodyData(int bodyIndex)
		{
			index = bodyIndex;
		}

		public static void Init()
		{
			array = new TempBodyData[Sim.nonStarIndexes.Count];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = new TempBodyData(Sim.nonStarIndexes[i]);
			}
		}

		public static void Clear()
		{
			for (int i = array.Length - 1; i >= 0; i--)
			{
				TempBodyData data = array[i];
				data.albedo = 0.0;
				data.emissive = 0.0;
				data.core = 0.0;
			}
		}
	}
}
