using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KERBALISM
{
	public abstract class KF
	{
		public abstract void OpeningTag(StringBuilder sb);
		public abstract void ClosingTag(StringBuilder sb);

		public const string WhiteSpace = " ";

		#region Append/Concatenate strings

		protected static ObjectPool<KsmFormatString> factoryKsmFormatString = new ObjectPool<KsmFormatString>();

		protected static ObjectPool<KsmFormatConcatStrings> factoryKKsmFormatConcatStrings = new ObjectPool<KsmFormatConcatStrings>();

		/// <summary> Append a single string </summary>
		public static KsmFormatString String(string str1)
		{
			KsmFormatString formatter = factoryKsmFormatString.Get();
			formatter.str = str1;
			return formatter;
		}

		/// <summary> Concatenate strings </summary>
		public static KsmFormatConcatStrings Concat(string str1, string str2)
		{
			KsmFormatConcatStrings formatter = factoryKKsmFormatConcatStrings.Get();
			formatter.strings[0] = str1;
			formatter.strings[1] = str2;
			formatter.stringsCount = 2;
			return formatter;
		}

		/// <summary> Concatenate strings </summary>
		public static KsmFormatConcatStrings Concat(string str1, string str2, string str3)
		{
			KsmFormatConcatStrings formatter = factoryKKsmFormatConcatStrings.Get();
			formatter.strings[0] = str1;
			formatter.strings[1] = str2;
			formatter.strings[2] = str3;
			formatter.stringsCount = 3;
			return formatter;
		}

		/// <summary> Concatenate strings </summary>
		public static KsmFormatConcatStrings Concat(string str1, string str2, string str3, string str4)
		{
			KsmFormatConcatStrings formatter = factoryKKsmFormatConcatStrings.Get();
			formatter.strings[0] = str1;
			formatter.strings[1] = str2;
			formatter.strings[2] = str3;
			formatter.strings[3] = str4;
			formatter.stringsCount = 4;
			return formatter;
		}

		/// <summary> Concatenate strings </summary>
		public static KsmFormatConcatStrings Concat(string str1, string str2, string str3, string str4, string str5)
		{
			KsmFormatConcatStrings formatter = factoryKKsmFormatConcatStrings.Get();
			formatter.strings[0] = str1;
			formatter.strings[1] = str2;
			formatter.strings[2] = str3;
			formatter.strings[3] = str4;
			formatter.strings[4] = str5;
			formatter.stringsCount = 5;
			return formatter;
		}

		/// <summary> Concatenate strings </summary>
		public static KsmFormatConcatStrings Concat(string str1, string str2, string str3, string str4, string str5, string str6)
		{
			KsmFormatConcatStrings formatter = factoryKKsmFormatConcatStrings.Get();
			formatter.strings[0] = str1;
			formatter.strings[1] = str2;
			formatter.strings[2] = str3;
			formatter.strings[3] = str4;
			formatter.strings[4] = str5;
			formatter.strings[5] = str6;
			formatter.stringsCount = 6;
			return formatter;
		}

		/// <summary> Concatenate strings </summary>
		public static KsmFormatConcatStrings Concat(params string[] strings)
		{
			KsmFormatConcatStrings formatter = factoryKKsmFormatConcatStrings.Get();
			formatter.stringsCount = strings.Length;
			for (int i = 0; i < strings.Length; i++)
			{
				formatter.strings[i] = strings[i];
			}
			return formatter;
		}

		#endregion

		#region Style formatting

		/// <summary> Insert a line break "\n" after the constructed string</summary>
		public static KsmFormatLineBreakAfter BreakAfter { get; private set; } = new KsmFormatLineBreakAfter();

		/// <summary> Insert a line break "\n" before the constructed string</summary>
		public static KsmFormatLineBreakBefore BreakBefore { get; private set; } = new KsmFormatLineBreakBefore();

		/// <summary> Surround with <b>bold</b> tags</summary>
		public static KsmFormatBold Bold { get; private set; } = new KsmFormatBold();

		/// <summary> Surround with <i>italics</i> tags</summary>
		public static KsmFormatItalic Italic { get; private set; } = new KsmFormatItalic();

		/// <summary> Surround with a left alignement tag</summary>
		public static KsmFormatAlignLeft Left { get; private set; } = new KsmFormatAlignLeft();

		/// <summary> Surround with a center alignement tag</summary>
		public static KsmFormatAlignCenter Center { get; private set; } = new KsmFormatAlignCenter();

		/// <summary> Surround with a right alignement tag</summary>
		public static KsmFormatAlignRight Right { get; private set; } = new KsmFormatAlignRight();

		/// <summary> Surround with "• value /n" </summary>
		public static KsmFormatList List { get; private set; } = new KsmFormatList();

		/// <summary> Surround with a white color tag </summary>
		public static KsmFormatKolorNone KolorWhite { get; private set; } = new KsmFormatKolorNone();

		/// <summary> Surround with a green color tag </summary>
		public static KsmFormatKolorGreen KolorGreen { get; private set; } = new KsmFormatKolorGreen();

		/// <summary> Surround with a yellow color tag </summary>
		public static KsmFormatKolorYellow KolorYellow { get; private set; } = new KsmFormatKolorYellow();

		/// <summary> Surround with a orange color tag </summary>
		public static KsmFormatKolorOrange KolorOrange { get; private set; } = new KsmFormatKolorOrange();

		/// <summary> Surround with a red color tag </summary>
		public static KsmFormatKolorRed KolorRed { get; private set; } = new KsmFormatKolorRed();

		/// <summary> Surround with a KSP science color tag </summary>
		public static KsmFormatKolorScience KolorScience { get; private set; } = new KsmFormatKolorScience();

		/// <summary> Surround with a cyan color tag </summary>
		public static KsmFormatKolorCyan KolorCyan { get; private set; } = new KsmFormatKolorCyan();

		/// <summary> Surround with a light grey color tag </summary>
		public static KsmFormatKolorLightGrey KolorLightGrey { get; private set; } = new KsmFormatKolorLightGrey();

		/// <summary> Surround with a dark grey color tag </summary>
		public static KsmFormatKolorDarkGrey KolorDarkGrey { get; private set; } = new KsmFormatKolorDarkGrey();

		/// <summary> Surround with a green color tag </summary>
		public static KsmFormatKolorGreen KolorPosRate => KolorGreen;

		/// <summary> Surround with an orange color tag </summary>
		public static KsmFormatKolorOrange KolorNegRate => KolorOrange;

		protected static ObjectPool<KsmFormatFontSize> factoryKsmFormatFontSize = new ObjectPool<KsmFormatFontSize>();
		protected static ObjectPool<KsmFormatPosition> factoryKsmFormatPosition = new ObjectPool<KsmFormatPosition>();
		protected static ObjectPool<KsmFormatColor> factoryKsmFormatColor = new ObjectPool<KsmFormatColor>();

		/// <summary> Surround with a font size (in pixels) tag </summary>
		public static KsmFormatFontSize Size(int fontSize)
		{
			KsmFormatFontSize formatter = factoryKsmFormatFontSize.Get();
			formatter.fontSize = fontSize;
			return formatter;
		}

		/// <summary> Surround with a color tag </summary>
		public static KsmFormatColor Color(Kolor color)
		{
			KsmFormatColor formatter = factoryKsmFormatColor.Get();
			formatter.color = color;
			return formatter;
		}

		/// <summary> Surround with a color tag, depending on a condition </summary>
		public static KsmFormatColor Color(bool condition, Kolor colorIfTrue, Kolor colorIfFalse = null)
		{
			KsmFormatColor formatter = factoryKsmFormatColor.Get();
			formatter.color = condition ? colorIfTrue : colorIfFalse ?? Kolor.White;
			return formatter;
		}

		/// <summary> Insert an horizontal position tag (in pixels) before the constructed string</summary>
		public static KsmFormatPosition Position(int position)
		{
			KsmFormatPosition formatter = factoryKsmFormatPosition.Get();
			formatter.position = position;
			return formatter;
		}

		#endregion

		#region Human readable

		protected static ObjectPool<KsmFormatReadableRate> factoryKsmFormatReadableRate = new ObjectPool<KsmFormatReadableRate>();
		protected static ObjectPool<KsmFormatReadableDuration> factoryKsmFormatReadableDuration = new ObjectPool<KsmFormatReadableDuration>();
		protected static ObjectPool<KsmFormatReadableCountdown> factoryKsmFormatReadableCountdown = new ObjectPool<KsmFormatReadableCountdown>();
		protected static ObjectPool<KsmFormatReadableDistance> factoryKsmFormatReadableDistance = new ObjectPool<KsmFormatReadableDistance>();
		protected static ObjectPool<KsmFormatReadableSpeed> factoryKsmFormatReadableSpeed = new ObjectPool<KsmFormatReadableSpeed>();
		protected static ObjectPool<KsmFormatReadableTemperature> factoryKsmFormatReadableTemperature = new ObjectPool<KsmFormatReadableTemperature>();
		protected static ObjectPool<KsmFormatReadableAngle> factoryKsmFormatReadableAngle = new ObjectPool<KsmFormatReadableAngle>();
		protected static ObjectPool<KsmFormatReadableIrradiance> factoryKsmFormatReadableIrradiance = new ObjectPool<KsmFormatReadableIrradiance>();
		protected static ObjectPool<KsmFormatReadableThermalFlux> factoryKsmFormatReadableThermalFlux = new ObjectPool<KsmFormatReadableThermalFlux>();
		protected static ObjectPool<KsmFormatReadableField> factoryKsmFormatReadableField = new ObjectPool<KsmFormatReadableField>();
		protected static ObjectPool<KsmFormatReadableRadiation> factoryKsmFormatReadableRadiation = new ObjectPool<KsmFormatReadableRadiation>();

		/// <summary> Pretty-print a per second rate </summary>
		public static KsmFormatReadableRate ReadableRate(double rate, string format = "F3", string unit = "", bool showSign = false)
		{
			KsmFormatReadableRate formatter = factoryKsmFormatReadableRate.Get();
			formatter.rate = rate;
			formatter.format = format;
			formatter.unit = unit;
			formatter.showSign = showSign;
			return formatter;
		}

		/// <summary> Pretty-print a duration in seconds </summary>
		public static KsmFormatReadableDuration ReadableDuration(double duration, bool fullprecison = false)
		{
			KsmFormatReadableDuration formatter = factoryKsmFormatReadableDuration.Get();
			formatter.duration = duration;
			formatter.fullprecison = fullprecison;
			return formatter;
		}

		/// <summary> Pretty-print a duration in seconds as a countdown (T-xxxx) </summary>
		public static KsmFormatReadableCountdown ReadableCountdown(double duration, bool compact = false)
		{
			KsmFormatReadableCountdown formatter = factoryKsmFormatReadableCountdown.Get();
			formatter.duration = duration;
			formatter.compact = compact;
			return formatter;
		}

		/// <summary> Pretty-print a distance in meters </summary>
		public static KsmFormatReadableDistance ReadableDistance(double distance)
		{
			KsmFormatReadableDistance formatter = factoryKsmFormatReadableDistance.Get();
			formatter.distance = distance;
			return formatter;
		}

		/// <summary> Pretty-print a speed in meters/s </summary>
		public static KsmFormatReadableSpeed ReadableSpeed(double speed)
		{
			KsmFormatReadableSpeed formatter = factoryKsmFormatReadableSpeed.Get();
			formatter.speed = speed;
			return formatter;
		}

		/// <summary> Pretty-print a temperature in Kelvin </summary>
		public static KsmFormatReadableTemperature ReadableTemperature(double temperature)
		{
			KsmFormatReadableTemperature formatter = factoryKsmFormatReadableTemperature.Get();
			formatter.temp = temperature;
			return formatter;
		}

		/// <summary> Pretty-print an angle in ° </summary>
		public static KsmFormatReadableAngle ReadableAngle(double angle)
		{
			KsmFormatReadableAngle formatter = factoryKsmFormatReadableAngle.Get();
			formatter.angle = angle;
			return formatter;
		}

		/// <summary> Pretty-print irrandiance in W/m² </summary>
		public static KsmFormatReadableIrradiance ReadableIrradiance(double irradiance)
		{
			KsmFormatReadableIrradiance formatter = factoryKsmFormatReadableIrradiance.Get();
			formatter.irradiance = irradiance;
			return formatter;
		}

		/// <summary> Pretty-print thermal flux in kWth </summary>
		public static KsmFormatReadableThermalFlux ReadableThermalFlux(double flux)
		{
			KsmFormatReadableThermalFlux formatter = factoryKsmFormatReadableThermalFlux.Get();
			formatter.flux = flux;
			return formatter;
		}

		/// <summary> Pretty-print magnetic strength in μT </summary>
		public static KsmFormatReadableField ReadableFieldStrength(double strength)
		{
			KsmFormatReadableField formatter = factoryKsmFormatReadableField.Get();
			formatter.strength = strength;
			return formatter;
		}

		/// <summary> Pretty-print radiation rate in rad/s </summary>
		public static KsmFormatReadableRadiation ReadableRadiation(double radiation)
		{
			KsmFormatReadableRadiation formatter = factoryKsmFormatReadableRadiation.Get();
			formatter.radiation = radiation;
			return formatter;
		}

		#endregion
	}

	#region String concatenation

	public class KsmFormatString : KF
	{
		public string str;

		public override void OpeningTag(StringBuilder sb) { }

		public override void ClosingTag(StringBuilder sb)
		{
			sb.Append(str);
			factoryKsmFormatString.Return(this);
		}
	}

	public class KsmFormatConcatStrings : KF
	{
		public string[] strings = new string[32]; // Max 32 strings in the same method call, more than enough
		public int stringsCount;

		public override void OpeningTag(StringBuilder sb) { }

		public override void ClosingTag(StringBuilder sb)
		{
			for (int i = 0; i < stringsCount; i++)
			{
				sb.Append(strings[i]);
			}

			factoryKKsmFormatConcatStrings.Return(this);
		}
	}

	#endregion

	#region Style formatters

	public class KsmFormatLineBreakAfter : KF
	{
		private const string closingTag = "\n";

		public override void OpeningTag(StringBuilder sb) { }
		public override void ClosingTag(StringBuilder sb) => sb.Append(closingTag);
	}

	public class KsmFormatLineBreakBefore : KF
	{
		private const string openingTag = "\n";

		public override void OpeningTag(StringBuilder sb) => sb.Append(openingTag);
		public override void ClosingTag(StringBuilder sb) {}
	}

	public class KsmFormatBold : KF
	{
		private const string openingTag = "<b>";
		private const string closingTag = "</b>";

		public override void OpeningTag(StringBuilder sb) => sb.Append(openingTag);
		public override void ClosingTag(StringBuilder sb) => sb.Append(closingTag);
	}

	public class KsmFormatItalic : KF
	{
		private const string openingTag = "<i>";
		private const string closingTag = "</i>";

		public override void OpeningTag(StringBuilder sb) => sb.Append(openingTag);
		public override void ClosingTag(StringBuilder sb) => sb.Append(closingTag);
	}

	public class KsmFormatAlignLeft : KF
	{
		private const string openingTag = "<align=left>";
		private const string closingTag = "</align>";

		public override void OpeningTag(StringBuilder sb) => sb.Append(openingTag);
		public override void ClosingTag(StringBuilder sb) => sb.Append(closingTag);
	}

	public class KsmFormatAlignCenter : KF
	{
		private const string openingTag = "<align=center>";
		private const string closingTag = "</align>";

		public override void OpeningTag(StringBuilder sb) => sb.Append(openingTag);
		public override void ClosingTag(StringBuilder sb) => sb.Append(closingTag);
	}

	public class KsmFormatAlignRight : KF
	{
		private const string openingTag = "<align=right>";
		private const string closingTag = "</align>";

		public override void OpeningTag(StringBuilder sb) => sb.Append(openingTag);
		public override void ClosingTag(StringBuilder sb) => sb.Append(closingTag);
	}

	public class KsmFormatList : KF
	{
		private const string openingTag = "<b>•</b> ";
		private const string closingTag = "\n";

		public override void OpeningTag(StringBuilder sb) => sb.Append(openingTag);
		public override void ClosingTag(StringBuilder sb) => sb.Append(closingTag);
	}

	public class KsmFormatKolorNone : KF
	{
		private static readonly string openingTag = "<color=" + Kolor.White.hex + ">";
		private const string closingTag = "</color>";

		public override void OpeningTag(StringBuilder sb) => sb.Append(openingTag);
		public override void ClosingTag(StringBuilder sb) => sb.Append(closingTag);
	}

	public class KsmFormatKolorGreen : KF
	{
		private static readonly string openingTag = "<color=" + Kolor.Green.hex + ">";
		private const string closingTag = "</color>";

		public override void OpeningTag(StringBuilder sb) => sb.Append(openingTag);
		public override void ClosingTag(StringBuilder sb) => sb.Append(closingTag);
	}

	public class KsmFormatKolorYellow : KF
	{
		private static readonly string openingTag = "<color=" + Kolor.Yellow.hex + ">";
		private const string closingTag = "</color>";

		public override void OpeningTag(StringBuilder sb) => sb.Append(openingTag);
		public override void ClosingTag(StringBuilder sb) => sb.Append(closingTag);
	}

	public class KsmFormatKolorOrange : KF
	{
		private static readonly string openingTag = "<color=" + Kolor.Orange.hex + ">";
		private const string closingTag = "</color>";

		public override void OpeningTag(StringBuilder sb) => sb.Append(openingTag);
		public override void ClosingTag(StringBuilder sb) => sb.Append(closingTag);
	}

	public class KsmFormatKolorRed : KF
	{
		private static readonly string openingTag = "<color=" + Kolor.Red.hex + ">";
		private const string closingTag = "</color>";

		public override void OpeningTag(StringBuilder sb) => sb.Append(openingTag);
		public override void ClosingTag(StringBuilder sb) => sb.Append(closingTag);
	}

	public class KsmFormatKolorScience : KF
	{
		private static readonly string openingTag = "<color=" + Kolor.Science.hex + ">";
		private const string closingTag = "</color>";

		public override void OpeningTag(StringBuilder sb) => sb.Append(openingTag);
		public override void ClosingTag(StringBuilder sb) => sb.Append(closingTag);
	}

	public class KsmFormatKolorCyan : KF
	{
		private static readonly string openingTag = "<color=" + Kolor.Cyan.hex + ">";
		private const string closingTag = "</color>";

		public override void OpeningTag(StringBuilder sb) => sb.Append(openingTag);
		public override void ClosingTag(StringBuilder sb) => sb.Append(closingTag);
	}

	public class KsmFormatKolorLightGrey : KF
	{
		private static readonly string openingTag = "<color=" + Kolor.LightGrey.hex + ">";
		private const string closingTag = "</color>";

		public override void OpeningTag(StringBuilder sb) => sb.Append(openingTag);
		public override void ClosingTag(StringBuilder sb) => sb.Append(closingTag);
	}

	public class KsmFormatKolorDarkGrey : KF
	{
		private static readonly string openingTag = "<color=" + Kolor.DarkGrey.hex + ">";
		private const string closingTag = "</color>";

		public override void OpeningTag(StringBuilder sb) => sb.Append(openingTag);
		public override void ClosingTag(StringBuilder sb) => sb.Append(closingTag);
	}

	public class KsmFormatFontSize : KF
	{
		private const string openingTagBegin = "<size=";
		private const string openingTagEnd = "px>";
		private const string closingTag = "</size>";

		public int fontSize;

		public override void OpeningTag(StringBuilder sb)
		{
			sb.Append(openingTagBegin);
			sb.Append(fontSize);
			sb.Append(openingTagEnd);
		}

		public override void ClosingTag(StringBuilder sb)
		{
			sb.Append(closingTag);
			factoryKsmFormatFontSize.Return(this);
		}
	}

	public class KsmFormatColor : KF
	{
		private const string openingTagBegin = "<color=";
		private const string openingTagEnd = ">";
		private const string closingTag = "</color>";

		public Kolor color;

		public override void OpeningTag(StringBuilder sb)
		{
			sb.Append(openingTagBegin);
			sb.Append(color.hex);
			sb.Append(openingTagEnd);
		}

		public override void ClosingTag(StringBuilder sb)
		{
			sb.Append(closingTag);
			factoryKsmFormatColor.Return(this);
		}
	}

	public class KsmFormatPosition : KF
	{
		private const string closingTagBegin = "<pos=";
		private const string closingTagEnd = "px>";

		public int position;

		public override void OpeningTag(StringBuilder sb)
		{
			sb.Append(closingTagBegin);
			sb.Append(position);
			sb.Append(closingTagEnd);
			factoryKsmFormatPosition.Return(this);
		}

		public override void ClosingTag(StringBuilder sb)
		{

		}
	}

	#endregion

	#region Human readable formatters

	public class KsmFormatReadableRate : KF
	{
		public double rate;
		public string format;
		public string unit;
		public bool showSign;

		public override void OpeningTag(StringBuilder sb) { }

		public override void ClosingTag(StringBuilder sb)
		{
			Append(sb, rate, format, unit, showSign);
			factoryKsmFormatReadableRate.Return(this);
		}

		public static void Append(StringBuilder sb, double rate, string format = "F3", string unit = "", bool showSign = false)
		{
			if (rate == 0.0)
			{
				sb.Append(Local.Generic_NONE);//"none"
				return;
			}

			if (showSign)
				sb.Append(rate >= 0.0 ? "+" : "-");

			rate = Math.Abs(rate);

			if (Input.GetKey(KeyCode.LeftAlt))
			{
				int exponent = rate == 0.0 ? 0 : (int)Math.Floor(Math.Log10(rate));
				switch (exponent)
				{
					case 11: rate *= 1e-9; format = "0.0e+9"; break;
					case 10: rate *= 1e-9; format = "0.00e+9"; break;
					case 9: rate *= 1e-9; format = "0.000e+9"; break;
					case 8: rate *= 1e-6; format = "0.0e+6"; break;
					case 7: rate *= 1e-6; format = "0.00e+6"; break;
					case 6: rate *= 1e-6; format = "0.000e+6"; break;
					case 5: rate *= 1e-3; format = "0.0e+3"; break;
					case 4: rate *= 1e-3; format = "0.00e+3"; break;
					case 3: rate *= 1e-3; format = "0.000e+3"; break;
					case 2: format = "0.0"; break;
					case 1: format = "0.00"; break;
					case 0: format = "0.000"; break;
					case -1: rate *= 1e3; format = "0.0e-3"; break;
					case -2: rate *= 1e3; format = "0.00e-3"; break;
					case -3: rate *= 1e3; format = "0.000e-3"; break;
					case -4: rate *= 1e6; format = "0.0e-6"; break;
					case -5: rate *= 1e6; format = "0.00e-6"; break;
					case -6: rate *= 1e6; format = "0.000e-6"; break;
					case -7: rate *= 1e9; format = "0.0e-9"; break;
					case -8: rate *= 1e9; format = "0.00e-9"; break;
					case -9: rate *= 1e9; format = "0.000e-9"; break;
					case -10: rate *= 1e12; format = "0.0e-12"; break;
					case -11: rate *= 1e12; format = "0.00e-12"; break;
					case -12: rate *= 1e12; format = "0.000e-12"; break;
					default: format = "0.000e+0"; break;
				}

				sb.Append(rate.ToString(format));

				if (!string.IsNullOrEmpty(unit))
				{
					sb.Append(WhiteSpace);
					sb.Append(unit);
				}

				sb.Append(Local.Generic_perSecond);
				return;
			}

			void BuildString(string timeUnit)
			{
				sb.Append(rate.ToString(format));

				if (!string.IsNullOrEmpty(unit))
				{
					sb.Append(WhiteSpace);
					sb.Append(unit);
				}

				sb.Append(timeUnit);
			}

			if (rate >= 0.01)
			{
				BuildString(Local.Generic_perSecond);
				return;
			}

			rate *= 60.0; // per-minute
			if (rate >= 0.01)
			{
				BuildString(Local.Generic_perMinute);
				return;
			}

			rate *= 60.0; // per-hour
			if (rate >= 0.01)
			{
				BuildString(Local.Generic_perHour);
				return;
			}

			rate *= Lib.HoursInDayExact; // per-day
			if (rate >= 0.01)
			{
				BuildString(Local.Generic_perDay);
				return;
			}

			rate *= Lib.DaysInYearExact; // per year
			BuildString(Local.Generic_perYear);
		}
	}

	public class KsmFormatReadableDuration : KF
	{
		public double duration;
		public bool fullprecison;

		public override void OpeningTag(StringBuilder sb) { }

		public override void ClosingTag(StringBuilder sb)
		{
			Append(sb, duration, fullprecison);
			factoryKsmFormatReadableDuration.Return(this);
		}

		public static void Append(StringBuilder sb, double d, bool fullprecison = false)
		{
			if (double.IsInfinity(d) || double.IsNaN(d))
			{
				sb.Append(Local.Generic_PERPETUAL);//"perpetual"
				return;
			}

			d = Math.Round(d);

			if (d <= 0.0)
			{
				sb.Append(Local.Generic_NONE);//"none"
				return;
			}

			if (!fullprecison)
			{
				ulong durationLong = (ulong)d;

				// seconds
				if (d < 60.0)
				{
					ulong seconds = durationLong % 60ul;
					sb.Append(seconds);
					sb.Append("s");
					return;
				}
				// minutes + seconds
				if (d < 3600.0)
				{
					ulong seconds = durationLong % 60ul;
					ulong minutes = (durationLong / 60ul) % 60ul;

					sb.Append(minutes);
					sb.Append("m ");
					sb.Append(seconds.ToString("00"));
					sb.Append("s");
					return;
				}
				// hours + minutes
				if (d < Lib.SecondsInDayFloored)
				{
					ulong minutes = (durationLong / 60ul) % 60ul;
					ulong hours = (durationLong / 3600ul) % Lib.HoursInDayLong;

					sb.Append(hours);
					sb.Append("h ");
					sb.Append(minutes.ToString("00"));
					sb.Append("m");
					return;
				}
				ulong days = (durationLong / Lib.SecondsInDayLong) % Lib.DaysInYearLong;
				// days + hours
				if (d < Lib.SecondsInYearFloored)
				{
					ulong hours = (durationLong / 3600ul) % Lib.HoursInDayLong;

					sb.Append(days);
					sb.Append("d ");
					sb.Append(hours);
					sb.Append("h");
					return;
				}
				// years + days
				ulong years = durationLong / Lib.SecondsInYearLong;

				sb.Append(years);
				sb.Append("y ");
				sb.Append(days);
				sb.Append("d");
			}
			else
			{
				double hours_in_day = Lib.HoursInDayFloored;
				double days_in_year = Lib.DaysInYearFloored;

				long duration = (long)d;
				long seconds = duration % 60;
				duration /= 60;
				long minutes = duration % 60;
				duration /= 60;
				long hours = duration % (long)hours_in_day;
				duration /= (long)hours_in_day;
				long days = duration % (long)days_in_year;
				long years = duration / (long)days_in_year;

				string result = string.Empty;
				if (years > 0)
				{
					sb.Append(years);
					sb.Append("y ");
				}
				if (years > 0 || days > 0)
				{
					sb.Append(days);
					sb.Append("d ");
				}
				if (years > 0 || days > 0 || hours > 0)
				{
					sb.Append(hours.ToString("D2"));
					sb.Append(":");
				}
				if (years > 0 || days > 0 || hours > 0 || minutes > 0)
				{
					sb.Append(minutes.ToString("D2"));
					sb.Append(":");
				}
				sb.Append(seconds.ToString("D2"));
			}
		}
	}

	public class KsmFormatReadableCountdown : KF
	{
		public double duration;
		public bool compact;

		public override void OpeningTag(StringBuilder sb) { }

		public override void ClosingTag(StringBuilder sb)
		{
			sb.Append("T-");
			KsmFormatReadableDuration.Append(sb, duration, !compact);
			factoryKsmFormatReadableCountdown.Return(this);
		}
	}

	public class KsmFormatReadableDistance : KF
	{
		public double distance;

		public override void OpeningTag(StringBuilder sb) { }

		public override void ClosingTag(StringBuilder sb)
		{
			Append(sb, distance);
			factoryKsmFormatReadableDistance.Return(this);
		}

		public static void Append(StringBuilder sb, double distance)
		{
			if (distance == 0.0)
			{
				sb.Append(Local.Generic_NONE);
				return;
			}

			if (distance < 0.0)
			{
				sb.Append("-");
				Append(sb, -distance);
				return;
			}

			if (distance < 1000.0)
			{
				sb.Append(distance.ToString("F1"));
				sb.Append(" m");
				return;
			}

			distance /= 1000.0;
			if (distance < 1000.0)
			{
				sb.Append(distance.ToString("F1"));
				sb.Append(" Km");
				return;
			}

			distance /= 1000.0;
			if (distance < 1000.0)
			{
				sb.Append(distance.ToString("F1"));
				sb.Append(" Mm");
				return;
			}

			distance /= 1000.0;
			if (distance < 1000.0)
			{
				sb.Append(distance.ToString("F1"));
				sb.Append(" Gm");
				return;
			}

			distance /= 1000.0;
			if (distance < 1000.0)
			{
				sb.Append(distance.ToString("F1"));
				sb.Append(" Tm");
				return;
			}

			distance /= 1000.0;
			if (distance < 1000.0)
			{
				sb.Append(distance.ToString("F1"));
				sb.Append(" Pm");
				return;
			}

			distance /= 1000.0;

			sb.Append(distance.ToString("F1"));
			sb.Append(" Em");
		}
	}

	public class KsmFormatReadableSpeed : KF
	{
		public double speed;

		public override void OpeningTag(StringBuilder sb) { }

		public override void ClosingTag(StringBuilder sb)
		{
			Append(sb, speed);
			factoryKsmFormatReadableSpeed.Return(this);
		}

		public static void Append(StringBuilder sb, double speed)
		{
			KsmFormatReadableDistance.Append(sb, speed);
			sb.Append("/s");
		}
	}

	public class KsmFormatReadableTemperature : KF
	{
		public double temp;

		public override void OpeningTag(StringBuilder sb) { }

		public override void ClosingTag(StringBuilder sb)
		{
			Append(sb, temp);
			factoryKsmFormatReadableTemperature.Return(this);
		}

		public static void Append(StringBuilder sb, double temperature)
		{
			sb.Append(temperature.ToString("F1"));
			sb.Append("/s");
		}
	}

	public class KsmFormatReadableAngle : KF
	{
		public double angle;

		public override void OpeningTag(StringBuilder sb) { }

		public override void ClosingTag(StringBuilder sb)
		{
			Append(sb, angle);
			factoryKsmFormatReadableAngle.Return(this);
		}

		public static void Append(StringBuilder sb, double angle)
		{
			if (angle > 0.0001 || angle < -0.0001)
				sb.Append(angle.ToString("F1"));
			else
				sb.Append("0");

			sb.Append(" °");
		}
	}

	public class KsmFormatReadableIrradiance : KF
	{
		public double irradiance;

		public override void OpeningTag(StringBuilder sb) { }

		public override void ClosingTag(StringBuilder sb)
		{
			Append(sb, irradiance);
			factoryKsmFormatReadableIrradiance.Return(this);
		}

		public static void Append(StringBuilder sb, double irradiance)
		{
			if (irradiance >= 0.1 || irradiance == 0.0)
				sb.Append(irradiance.ToString("0.0 W/m²"));
			else
				sb.Append(irradiance.ToString("0.0E+0 W/m²"));
		}
	}

	public class KsmFormatReadableThermalFlux : KF
	{
		public double flux;

		public override void OpeningTag(StringBuilder sb) { }

		public override void ClosingTag(StringBuilder sb)
		{
			Append(sb, flux);
			factoryKsmFormatReadableThermalFlux.Return(this);
		}

		public static void Append(StringBuilder sb, double flux)
		{
			if (flux <= -0.001 || flux >= 0.001 || flux == 0.0)
				sb.Append(flux.ToString("0.000 kWth"));
			else
				sb.Append(flux.ToString("0.0E+0 kWth"));
		}
	}

	public class KsmFormatReadableField : KF
	{
		public double strength;

		public override void OpeningTag(StringBuilder sb) { }

		public override void ClosingTag(StringBuilder sb)
		{
			Append(sb, strength);
			factoryKsmFormatReadableField.Return(this);
		}

		public static void Append(StringBuilder sb, double strength)
		{
			sb.Append(strength.ToString("F1"));
			sb.Append(" μT");
		}
	}

	public class KsmFormatReadableRadiation : KF
	{
		public double radiation;
		public bool nominal;
		public bool dangerColor;

		public override void OpeningTag(StringBuilder sb) { }

		public override void ClosingTag(StringBuilder sb)
		{
			Append(sb, radiation, nominal, dangerColor);
			factoryKsmFormatReadableRadiation.Return(this);
		}

		public static void Append(StringBuilder sb, double radiation, bool nominal = true, bool dangerColor = true)
		{
			if (nominal && radiation <= Radiation.Nominal)
			{
				if (dangerColor)
					sb.Format(Local.Generic_NOMINAL, KolorGreen);
				else
					sb.Append(Local.Generic_NOMINAL);

				return;
			}

			radiation *= 3600.0;

			if (Settings.RadiationInSievert)
			{
				radiation /= 100.0;
				sb.Append(radiation.ToString("F3"));
				sb.Append(" Sv/h");
				return;
			}

			string unit;
			KF color;

			if (radiation < 0.00001)
			{
				unit = " μrad/h";
				radiation *= 1000000.0;
				color = KolorGreen;
			}
			else if (radiation < 0.01)
			{
				unit = " mrad/h";
				radiation *= 1000.0;
				color = KolorGreen;
			}
			else
			{
				unit = " rad/h";
				if (radiation < 0.25)
					color = KolorYellow;
				else if (radiation < 1.0)
					color = KolorOrange;
				else
					color = KolorRed;
			}

			if (dangerColor)
				sb.Format(radiation.ToString("F3"), color);
			else
				sb.Append(radiation.ToString("F3"));

			sb.Append(unit);
		}
	}

	#endregion

	public class ObjectPool<T> where T : new()
	{
		private readonly Queue<T> pool = new Queue<T>();

		public T Get()
		{
			if (pool.Count == 0)
			{
				return new T();
			}
			else
			{
				return pool.Dequeue();
			}
		}

		public void Return(T value)
		{
			pool.Enqueue(value);
		}
	}
}
