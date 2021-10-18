using UnityEngine;

namespace KERBALISM
{
	public class Kolor
	{
		public readonly string name;
		public readonly string hex;
		public readonly Color color;

		public Kolor(string name, string hex, Color color)
		{
			this.name = name;
			this.hex = hex;
			this.color = color;
		}

		public override string ToString()
		{
			return name;
		}

		public static readonly Kolor White     = new Kolor("White",     "#FFFFFF", new Color(1.000f, 1.000f, 1.000f)); // white, use this in the Color() methods if no color tag is to be applied
		public static readonly Kolor Green     = new Kolor("Green",     "#88FF00", new Color(0.533f, 1.000f, 0.000f)); // green whith slightly less red than the ksp ui default (CCFF00), for better contrast with yellow
		public static readonly Kolor Yellow    = new Kolor("Yellow",    "#FFD200", new Color(1.000f, 0.824f, 0.000f)); // ksp ui yellow
		public static readonly Kolor Orange    = new Kolor("Orange",    "#FF8000", new Color(1.000f, 0.502f, 0.000f)); // ksp ui orange
		public static readonly Kolor Red       = new Kolor("Red",       "#FF3333", new Color(1.000f, 0.200f, 0.200f)); // custom red
		public static readonly Kolor Science   = new Kolor("Science",   "#6DCFF6", new Color(0.427f, 0.812f, 0.965f)); // ksp science color
		public static readonly Kolor Cyan      = new Kolor("Cyan",      "#00FFFF", new Color(0.000f, 1.000f, 1.000f)); // cyan
		public static readonly Kolor LightGrey = new Kolor("LightGrey", "#CCCCCC", new Color(0.800f, 0.800f, 0.800f)); // light grey
		public static readonly Kolor DarkGrey  = new Kolor("DarkGrey",  "#999999", new Color(0.600f, 0.600f, 0.600f)); // dark grey
		public static readonly Kolor NearBlack = new Kolor("NearBlack", "#434343", new Color(0.263f, 0.263f, 0.263f)); // very dark grey	
		public static Kolor PosRate => Green;
		public static Kolor NegRate => Orange;

		public static Kolor Parse(string kolorName)
		{
			switch (kolorName)
			{
				case "White":     return White;
				case "Green":     return Green;
				case "Yellow":    return Yellow;
				case "Orange":    return Orange;
				case "Red":       return Red;
				case "Science":   return Science;
				case "Cyan":      return Cyan;
				case "LightGrey": return LightGrey;
				case "DarkGrey":  return DarkGrey;
				case "NearBlack": return NearBlack;
				case "PosRate":   return PosRate;
				case "NegRate":   return NegRate;
				default:          return null;
			}
		}

		public static implicit operator Color(Kolor kolor) => kolor.color;
	}
}
