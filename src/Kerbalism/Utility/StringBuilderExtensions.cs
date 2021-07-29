using System.Text;

namespace KERBALISM
{
	static class StringBuilderExtensions
	{
		public static StringBuilder Add(this StringBuilder sb, string s1)
		{
			sb.Append(s1);
			return sb;
		}

		public static StringBuilder Concat(this StringBuilder sb, string s1, string s2)
		{
			sb.Append(s1);
			sb.Append(s2);
			return sb;
		}

		public static StringBuilder Concat(this StringBuilder sb, string s1, string s2, string s3)
		{
			sb.Append(s1);
			sb.Append(s2);
			sb.Append(s3);
			return sb;
		}

		public static StringBuilder Concat(this StringBuilder sb, string s1, string s2, string s3, string s4)
		{
			sb.Append(s1);
			sb.Append(s2);
			sb.Append(s3);
			sb.Append(s4);
			return sb;
		}

		public static StringBuilder Concat(this StringBuilder sb, string s1, string s2, string s3, string s4, string s5)
		{
			sb.Append(s1);
			sb.Append(s2);
			sb.Append(s3);
			sb.Append(s4);
			sb.Append(s5);
			return sb;
		}

		public static StringBuilder Concat(this StringBuilder sb, string s1, string s2, string s3, string s4, string s5, string s6)
		{
			sb.Append(s1);
			sb.Append(s2);
			sb.Append(s3);
			sb.Append(s4);
			sb.Append(s5);
			sb.Append(s6);
			return sb;
		}

		public static StringBuilder Concat(this StringBuilder sb, string s1, string s2, string s3, string s4, string s5, string s6, string s7)
		{
			sb.Append(s1);
			sb.Append(s2);
			sb.Append(s3);
			sb.Append(s4);
			sb.Append(s5);
			sb.Append(s6);
			sb.Append(s7);
			return sb;
		}

		public static StringBuilder Concat(this StringBuilder sb, string s1, string s2, string s3, string s4, string s5, string s6, string s7, string s8)
		{
			sb.Append(s1);
			sb.Append(s2);
			sb.Append(s3);
			sb.Append(s4);
			sb.Append(s5);
			sb.Append(s6);
			sb.Append(s7);
			sb.Append(s8);
			return sb;
		}

		public static StringBuilder Concat(this StringBuilder sb, params string[] strings)
		{
			for (int i = 0; i < strings.Length; i++)
			{
				sb.Append(strings[i]);
			}
			return sb;
		}

		public static StringBuilder Format(this StringBuilder sb, string value, KF f1)
		{
			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);
			return sb;
		}

		public static StringBuilder Format(this StringBuilder sb, string value, KF f1, KF f2)
		{
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			return sb;
		}

		public static StringBuilder Format(this StringBuilder sb, string value, KF f1, KF f2, KF f3)
		{
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			return sb;
		}

		public static StringBuilder Format(this StringBuilder sb, string value, KF f1, KF f2, KF f3, KF f4)
		{
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			return sb;
		}

		public static StringBuilder Format(this StringBuilder sb, string value, KF f1, KF f2, KF f3, KF f4, KF f5)
		{
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			return sb;
		}

		public static StringBuilder Format(this StringBuilder sb, string value, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6)
		{
			f6.OpeningTag(sb);
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			f6.ClosingTag(sb);
			return sb;
		}

		public static StringBuilder Format(this StringBuilder sb, string value, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6, KF f7)
		{
			f7.OpeningTag(sb);
			f6.OpeningTag(sb);
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			f6.ClosingTag(sb);
			f7.ClosingTag(sb);
			return sb;
		}

		public static StringBuilder Format(this StringBuilder sb, string value, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6, KF f7, KF f8)
		{
			f8.OpeningTag(sb);
			f7.OpeningTag(sb);
			f6.OpeningTag(sb);
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			f6.ClosingTag(sb);
			f7.ClosingTag(sb);
			f8.ClosingTag(sb);
			return sb;
		}

		public static StringBuilder Format(this StringBuilder sb, KF f1)
		{
			f1.OpeningTag(sb);
			f1.ClosingTag(sb);
			return sb;
		}

		public static StringBuilder Format(this StringBuilder sb, KF f1, KF f2)
		{
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			return sb;
		}

		public static StringBuilder Format(this StringBuilder sb, KF f1, KF f2, KF f3)
		{
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			return sb;
		}

		public static StringBuilder Format(this StringBuilder sb, KF f1, KF f2, KF f3, KF f4)
		{
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			return sb;
		}

		public static StringBuilder Format(this StringBuilder sb, KF f1, KF f2, KF f3, KF f4, KF f5)
		{
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			return sb;
		}

		public static StringBuilder Format(this StringBuilder sb, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6)
		{
			f6.OpeningTag(sb);
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			f6.ClosingTag(sb);
			return sb;
		}

		public static StringBuilder Format(this StringBuilder sb, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6, KF f7)
		{
			f7.OpeningTag(sb);
			f6.OpeningTag(sb);
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			f6.ClosingTag(sb);
			f7.ClosingTag(sb);
			return sb;
		}

		public static StringBuilder Format(this StringBuilder sb, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6, KF f7, KF f8)
		{
			f8.OpeningTag(sb);
			f7.OpeningTag(sb);
			f6.OpeningTag(sb);
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			f6.ClosingTag(sb);
			f7.ClosingTag(sb);
			f8.ClosingTag(sb);
			return sb;
		}

		public static StringBuilder Format(this StringBuilder sb, string value, params KF[] formats)
		{
			for (int i = formats.Length - 1; i >= 0; i--)
			{
				formats[i].OpeningTag(sb);
			}

			sb.Append(value);

			for (int i = 0; i < formats.Length; i++)
			{
				formats[i].ClosingTag(sb);
			}

			return sb;
		}

		public static StringBuilder Format(this StringBuilder sb, params KF[] formats)
		{
			for (int i = formats.Length - 1; i >= 0; i--)
			{
				formats[i].OpeningTag(sb);
			}

			for (int i = 0; i < formats.Length; i++)
			{
				formats[i].ClosingTag(sb);
			}

			return sb;
		}

		private const string KsmInfoPos1 = "<pos=";
		private const string KsmInfoPos2 = "px><b>";
		private const string KsmInfoSpecifics = ": <b>";
		private const string KsmInfoEnd = "</b>\n";

		/// <summary> Format to "label: <b>value</b>\n" (match the format of Specifics)</summary>
		public static StringBuilder Info(this StringBuilder sb, string label, string value, int valuePos = -1)
		{
			sb.Append(label);

			if (valuePos <= 0)
			{
				sb.Append(KsmInfoSpecifics);
				sb.Append(value);
			}
			else
			{
				sb.Append(KsmInfoPos1);
				sb.Append(valuePos);
				sb.Append(KsmInfoPos2);
				sb.Append(value);
			}

			sb.Append(KsmInfoEnd);
			return sb;
		}

		private const string lineBreak = "\n";

		private const string ksmInfoSeparator = ": ";
		private const string ksmInfoPosBegin = "<pos=";
		private const string ksmInfoPosEnd = "px>";

		public static StringBuilder Info(this StringBuilder sb, string label, string value, KF f1, int valuePos = -1)
		{
			sb.Append(label);

			if (valuePos <= 0)
			{
				sb.Append(ksmInfoSeparator);
			}
			else
			{
				sb.Append(ksmInfoPosBegin);
				sb.Append(valuePos);
				sb.Append(ksmInfoPosEnd);
			}

			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);
			sb.Append(lineBreak);
			return sb;
		}

		public static StringBuilder Info(this StringBuilder sb, string label, string value, KF f1, KF f2, int valuePos = -1)
		{
			sb.Append(label);

			if (valuePos <= 0)
			{
				sb.Append(ksmInfoSeparator);
			}
			else
			{
				sb.Append(ksmInfoPosBegin);
				sb.Append(valuePos);
				sb.Append(ksmInfoPosEnd);
			}

			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			sb.Append(lineBreak);
			return sb;
		}

		public static StringBuilder Info(this StringBuilder sb, string label, string value, KF f1, KF f2, KF f3, int valuePos = -1)
		{
			sb.Append(label);

			if (valuePos <= 0)
			{
				sb.Append(ksmInfoSeparator);
			}
			else
			{
				sb.Append(ksmInfoPosBegin);
				sb.Append(valuePos);
				sb.Append(ksmInfoPosEnd);
			}

			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			sb.Append(lineBreak);
			return sb;
		}

		public static StringBuilder Info(this StringBuilder sb, string label, string value, KF f1, KF f2, KF f3, KF f4, int valuePos = -1)
		{
			sb.Append(label);

			if (valuePos <= 0)
			{
				sb.Append(ksmInfoSeparator);
			}
			else
			{
				sb.Append(ksmInfoPosBegin);
				sb.Append(valuePos);
				sb.Append(ksmInfoPosEnd);
			}

			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			sb.Append(lineBreak);
			return sb;
		}

		public static StringBuilder Info(this StringBuilder sb, string label, KF f1, int valuePos = -1)
		{
			sb.Append(label);

			if (valuePos <= 0)
			{
				sb.Append(ksmInfoSeparator);
			}
			else
			{
				sb.Append(ksmInfoPosBegin);
				sb.Append(valuePos);
				sb.Append(ksmInfoPosEnd);
			}

			f1.OpeningTag(sb);
			f1.ClosingTag(sb);
			sb.Append(lineBreak);
			return sb;
		}

		public static StringBuilder Info(this StringBuilder sb, string label, KF f1, KF f2, int valuePos = -1)
		{
			sb.Append(label);

			if (valuePos <= 0)
			{
				sb.Append(ksmInfoSeparator);
			}
			else
			{
				sb.Append(ksmInfoPosBegin);
				sb.Append(valuePos);
				sb.Append(ksmInfoPosEnd);
			}

			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			sb.Append(lineBreak);
			return sb;
		}

		public static StringBuilder Info(this StringBuilder sb, string label, KF f1, KF f2, KF f3, int valuePos = -1)
		{
			sb.Append(label);

			if (valuePos <= 0)
			{
				sb.Append(ksmInfoSeparator);
			}
			else
			{
				sb.Append(ksmInfoPosBegin);
				sb.Append(valuePos);
				sb.Append(ksmInfoPosEnd);
			}

			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			sb.Append(lineBreak);
			return sb;
		}

		public static StringBuilder Info(this StringBuilder sb, string label, KF f1, KF f2, KF f3, KF f4, int valuePos = -1)
		{
			sb.Append(label);

			if (valuePos <= 0)
			{
				sb.Append(ksmInfoSeparator);
			}
			else
			{
				sb.Append(ksmInfoPosBegin);
				sb.Append(valuePos);
				sb.Append(ksmInfoPosEnd);
			}

			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			sb.Append(lineBreak);
			return sb;
		}
	}
}
