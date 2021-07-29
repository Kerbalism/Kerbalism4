using UnityEngine;

namespace KERBALISM
{
	public class Kolor
	{
		public readonly string hex;
		public readonly Color color;

		public Kolor(string hex, Color color)
		{
			this.hex = hex;
			this.color = color;
		}

		public static readonly Kolor White     = new Kolor("#FFFFFF", new Color(1.000f, 1.000f, 1.000f)); // white, use this in the Color() methods if no color tag is to be applied
		public static readonly Kolor Green     = new Kolor("#88FF00", new Color(0.533f, 1.000f, 0.000f)); // green whith slightly less red than the ksp ui default (CCFF00), for better contrast with yellow
		public static readonly Kolor Yellow    = new Kolor("#FFD200", new Color(1.000f, 0.824f, 0.000f)); // ksp ui yellow
		public static readonly Kolor Orange    = new Kolor("#FF8000", new Color(1.000f, 0.502f, 0.000f)); // ksp ui orange
		public static readonly Kolor Red       = new Kolor("#FF3333", new Color(1.000f, 0.200f, 0.200f)); // custom red
		public static readonly Kolor Science   = new Kolor("#6DCFF6", new Color(0.427f, 0.812f, 0.965f)); // ksp science color
		public static readonly Kolor Cyan      = new Kolor("#00FFFF", new Color(0.000f, 1.000f, 1.000f)); // cyan
		public static readonly Kolor LightGrey = new Kolor("#CCCCCC", new Color(0.800f, 0.800f, 0.800f)); // light grey
		public static readonly Kolor DarkGrey  = new Kolor("#999999", new Color(0.600f, 0.600f, 0.600f)); // dark grey	
		public static Kolor PosRate => Green;
		public static Kolor NegRate => Orange;
	}
}
