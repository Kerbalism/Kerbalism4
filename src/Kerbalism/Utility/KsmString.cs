using System.Text;
using static KERBALISM.KF;

namespace KERBALISM
{
	public class KsmString
	{
		private static readonly KsmStringObjectPool<KsmString> stringBuildersPool = new KsmStringObjectPool<KsmString>();

		private StringBuilder sb = new StringBuilder();

		public static KsmString Get => stringBuildersPool.Get();

		public int Length => sb.Length;

		public string GetStringAndRelease()
		{
			string result = sb.ToString();
			sb.Clear();
			stringBuildersPool.Return(this);
			return result;
		}

		public string GetStringAndClear()
		{
			string result = sb.ToString();
			sb.Clear();
			return result;
		}

		public void Release()
		{
			sb.Clear();
			stringBuildersPool.Return(this);
		}

		public KsmString Insert(int index, string value)
		{
			sb.Insert(index, value);
			return this;
		}

		/// <summary> Append a line break </summary>
		public KsmString Break()
		{
			sb.Append("\n");
			return this;
		}

		public KsmString AlignLeft()
		{
			sb.Append("<align=left>");
			return this;
		}

		public KsmString AlignCenter()
		{
			sb.Append("<align=center>");
			return this;
		}

		public KsmString AlignRight()
		{
			sb.Append("<align=right>");
			return this;
		}

		public KsmString AlignReset()
		{
			sb.Append("</align>");
			return this;
		}

		public KsmString Bold()
		{
			sb.Append("<b>");
			return this;
		}

		public KsmString BoldReset()
		{
			sb.Append("</b>");
			return this;
		}

		#region Add

		/// <summary> Append a char </summary>
		public KsmString Add(char value)
		{
			sb.Append(value);
			return this;
		}

		/// <summary> Append a string </summary>
		public KsmString Add(string value)
		{
			sb.Append(value);
			return this;
		}

		/// <summary> Append strings </summary>
		public KsmString Add(string s1, string s2)
		{
			sb.Append(s1);
			sb.Append(s2);
			return this;
		}

		/// <summary> Append strings </summary>
		public KsmString Add(string s1, string s2, string s3)
		{
			sb.Append(s1);
			sb.Append(s2);
			sb.Append(s3);
			return this;
		}

		/// <summary> Append strings </summary>
		public KsmString Add(string s1, string s2, string s3, string s4)
		{
			sb.Append(s1);
			sb.Append(s2);
			sb.Append(s3);
			sb.Append(s4);
			return this;
		}

		/// <summary> Append strings </summary>
		public KsmString Add(string s1, string s2, string s3, string s4, string s5)
		{
			sb.Append(s1);
			sb.Append(s2);
			sb.Append(s3);
			sb.Append(s4);
			sb.Append(s5);
			return this;
		}

		/// <summary> Append strings </summary>
		public KsmString Add(string s1, string s2, string s3, string s4, string s5, string s6)
		{
			sb.Append(s1);
			sb.Append(s2);
			sb.Append(s3);
			sb.Append(s4);
			sb.Append(s5);
			sb.Append(s6);
			return this;
		}

		/// <summary> Append strings </summary>
		public KsmString Add(string s1, string s2, string s3, string s4, string s5, string s6, string s7)
		{
			sb.Append(s1);
			sb.Append(s2);
			sb.Append(s3);
			sb.Append(s4);
			sb.Append(s5);
			sb.Append(s6);
			sb.Append(s7);
			return this;
		}

		/// <summary> Append strings </summary>
		public KsmString Add(string s1, string s2, string s3, string s4, string s5, string s6, string s7, string s8)
		{
			sb.Append(s1);
			sb.Append(s2);
			sb.Append(s3);
			sb.Append(s4);
			sb.Append(s5);
			sb.Append(s6);
			sb.Append(s7);
			sb.Append(s8);
			return this;
		}

		/// <summary> Append strings </summary>
		public KsmString Add(params string[] strings)
		{
			for (int i = 0; i < strings.Length; i++)
			{
				sb.Append(strings[i]);
			}
			return this;
		}

		#endregion

		#region Format values

		/// <summary> Append the default string representation of a bool </summary>
		public KsmString Format(bool value)
		{
			sb.Append(value);
			return this;
		}

		/// <summary> Append the string representation of a byte </summary>
		public KsmString Format(byte value, string format = null)
		{
			sb.Append(format == null ? value.ToString() : value.ToString(format));
			return this;
		}

		/// <summary> Append the string representation of a sbyte </summary>
		public KsmString Format(sbyte value, string format = null)
		{
			sb.Append(format == null ? value.ToString() : value.ToString(format));
			return this;
		}

		/// <summary> Append the string representation of a ushort </summary>
		public KsmString Format(ushort value, string format = null)
		{
			sb.Append(format == null ? value.ToString() : value.ToString(format));
			return this;
		}

		/// <summary> Append the string representation of a short </summary>
		public KsmString Format(short value, string format = null)
		{
			sb.Append(format == null ? value.ToString() : value.ToString(format));
			return this;
		}

		/// <summary> Append the string representation of an uint </summary>
		public KsmString Format(uint value, string format = null)
		{
			sb.Append(format == null ? value.ToString() : value.ToString(format));
			return this;
		}

		/// <summary> Append the string representation of an int </summary>
		public KsmString Format(int value, string format = null)
		{
			sb.Append(format == null ? value.ToString() : value.ToString(format));
			return this;
		}

		/// <summary> Append the string representation of a ulong </summary>
		public KsmString Format(ulong value, string format = null)
		{
			sb.Append(format == null ? value.ToString() : value.ToString(format));
			return this;
		}

		/// <summary> Append the string representation of a long </summary>
		public KsmString Format(long value, string format = null)
		{
			sb.Append(format == null ? value.ToString() : value.ToString(format));
			return this;
		}

		/// <summary> Append the string representation of a float </summary>
		public KsmString Format(float value, string format = null)
		{
			sb.Append(format == null ? value.ToString() : value.ToString(format));
			return this;
		}

		/// <summary> Append the string representation of a double </summary>
		public KsmString Format(double value, string format = null)
		{
			sb.Append(format == null ? value.ToString() : value.ToString(format));
			return this;
		}

		#endregion

		#region Format values + KF

		/// <summary> Append the string representation of a value using the specified format (ex : "F1", P0"...) and the specifed KsmFormat formatter </summary>
		public KsmString Format(object value, string format, KF f1)
		{
			f1.OpeningTag(sb);
			sb.AppendFormat(format, value);
			f1.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a value using the specified format (ex : "F1", P0"...) and the specifed KsmFormat formatters </summary>
		public KsmString Format(object value, string format, KF f1, KF f2)
		{
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.AppendFormat(format, value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a value using the specified format (ex : "F1", P0"...) and the specifed KsmFormat formatters </summary>
		public KsmString Format(object value, string format, KF f1, KF f2, KF f3)
		{
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.AppendFormat(format, value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a value using the specified format (ex : "F1", P0"...) and the specifed KsmFormat formatters </summary>
		public KsmString Format(object value, string format, KF f1, KF f2, KF f3, KF f4)
		{
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.AppendFormat(format, value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a value using the specified format (ex : "F1", P0"...) and the specifed KsmFormat formatters </summary>
		public KsmString Format(object value, string format, KF f1, KF f2, KF f3, KF f4, KF f5)
		{
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.AppendFormat(format, value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a value using the specified format (ex : "F1", P0"...) and the specifed KsmFormat formatters </summary>
		public KsmString Format(object value, string format, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6)
		{
			f6.OpeningTag(sb);
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.AppendFormat(format, value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			f6.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a value using the specified format (ex : "F1", P0"...) and the specifed KsmFormat formatters </summary>
		public KsmString Format(object value, string format, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6, KF f7)
		{
			f7.OpeningTag(sb);
			f6.OpeningTag(sb);
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.AppendFormat(format, value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			f6.ClosingTag(sb);
			f7.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a value using the specified format (ex : "F1", P0"...) and the specifed KsmFormat formatters </summary>
		public KsmString Format(object value, string format, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6, KF f7, KF f8)
		{
			f8.OpeningTag(sb);
			f7.OpeningTag(sb);
			f6.OpeningTag(sb);
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.AppendFormat(format, value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			f6.ClosingTag(sb);
			f7.ClosingTag(sb);
			f8.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a value using the specified format (ex : "F1", P0"...) and the specifed KsmFormat formatters </summary>
		public KsmString Format(object value, string format, params KF[] formats)
		{
			for (int i = formats.Length - 1; i >= 0; i--)
			{
				formats[i].OpeningTag(sb);
			}

			sb.AppendFormat(format, value);

			for (int i = 0; i < formats.Length; i++)
			{
				formats[i].ClosingTag(sb);
			}

			return this;
		}

		#endregion

		#region Format int + KF

		/// <summary> Append the default string representation of an int and the specifed KsmFormat formatters </summary>
		public KsmString Format(int value, KF f1)
		{
			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the default string representation of an int and the specifed KsmFormat formatters </summary>
		public KsmString Format(int value, KF f1, KF f2)
		{
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the default string representation of an int and the specifed KsmFormat formatters </summary>
		public KsmString Format(int value, KF f1, KF f2, KF f3)
		{
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the default string representation of an int and the specifed KsmFormat formatters </summary>
		public KsmString Format(int value, KF f1, KF f2, KF f3, KF f4)
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
			return this;
		}

		/// <summary> Append the default string representation of an int and the specifed KsmFormat formatters </summary>
		public KsmString Format(int value, KF f1, KF f2, KF f3, KF f4, KF f5)
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
			return this;
		}

		/// <summary> Append the default string representation of an int and the specifed KsmFormat formatters </summary>
		public KsmString Format(int value, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6)
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
			return this;
		}

		/// <summary> Append the default string representation of an int and the specifed KsmFormat formatters </summary>
		public KsmString Format(int value, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6, KF f7)
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
			return this;
		}

		/// <summary> Append the default string representation of an int and the specifed KsmFormat formatters </summary>
		public KsmString Format(int value, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6, KF f7, KF f8)
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
			return this;
		}

		/// <summary> Append the default string representation of an int and the specifed KsmFormat formatters </summary>
		public KsmString Format(int value, params KF[] formats)
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

			return this;
		}

		#endregion

		#region Format int + format + KF

		/// <summary> Append the string representation of an int and the specifed KsmFormat formatters </summary>
		public KsmString Format(int value, string format, KF f1)
		{
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of an int and the specifed KsmFormat formatters </summary>
		public KsmString Format(int value, string format, KF f1, KF f2)
		{
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of an int and the specifed KsmFormat formatters </summary>
		public KsmString Format(int value, string format, KF f1, KF f2, KF f3)
		{
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of an int and the specifed KsmFormat formatters </summary>
		public KsmString Format(int value, string format, KF f1, KF f2, KF f3, KF f4)
		{
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of an int and the specifed KsmFormat formatters </summary>
		public KsmString Format(int value, string format, KF f1, KF f2, KF f3, KF f4, KF f5)
		{
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of an int and the specifed KsmFormat formatters </summary>
		public KsmString Format(int value, string format, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6)
		{
			f6.OpeningTag(sb);
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			f6.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of an int and the specifed KsmFormat formatters </summary>
		public KsmString Format(int value, string format, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6, KF f7)
		{
			f7.OpeningTag(sb);
			f6.OpeningTag(sb);
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			f6.ClosingTag(sb);
			f7.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of an int and the specifed KsmFormat formatters </summary>
		public KsmString Format(int value, string format, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6, KF f7, KF f8)
		{
			f8.OpeningTag(sb);
			f7.OpeningTag(sb);
			f6.OpeningTag(sb);
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			f6.ClosingTag(sb);
			f7.ClosingTag(sb);
			f8.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of an int and the specifed KsmFormat formatters </summary>
		public KsmString Format(int value, string format, params KF[] formats)
		{
			for (int i = formats.Length - 1; i >= 0; i--)
			{
				formats[i].OpeningTag(sb);
			}

			sb.Append(value.ToString(format));

			for (int i = 0; i < formats.Length; i++)
			{
				formats[i].ClosingTag(sb);
			}

			return this;
		}

		#endregion

		#region Format float + KF

		/// <summary> Append the string representation of a float and the specifed KsmFormat formatters </summary>
		public KsmString Format(float value, string format, KF f1)
		{
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a float and the specifed KsmFormat formatters </summary>
		public KsmString Format(float value, string format, KF f1, KF f2)
		{
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a float and the specifed KsmFormat formatters </summary>
		public KsmString Format(float value, string format, KF f1, KF f2, KF f3)
		{
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a float and the specifed KsmFormat formatters </summary>
		public KsmString Format(float value, string format, KF f1, KF f2, KF f3, KF f4)
		{
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a float and the specifed KsmFormat formatters </summary>
		public KsmString Format(float value, string format, KF f1, KF f2, KF f3, KF f4, KF f5)
		{
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a float and the specifed KsmFormat formatters </summary>
		public KsmString Format(float value, string format, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6)
		{
			f6.OpeningTag(sb);
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			f6.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a float and the specifed KsmFormat formatters </summary>
		public KsmString Format(float value, string format, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6, KF f7)
		{
			f7.OpeningTag(sb);
			f6.OpeningTag(sb);
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			f6.ClosingTag(sb);
			f7.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a float and the specifed KsmFormat formatters </summary>
		public KsmString Format(float value, string format, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6, KF f7, KF f8)
		{
			f8.OpeningTag(sb);
			f7.OpeningTag(sb);
			f6.OpeningTag(sb);
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			f6.ClosingTag(sb);
			f7.ClosingTag(sb);
			f8.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a float and the specifed KsmFormat formatters </summary>
		public KsmString Format(float value, string format, params KF[] formats)
		{
			for (int i = formats.Length - 1; i >= 0; i--)
			{
				formats[i].OpeningTag(sb);
			}

			sb.Append(value.ToString(format));

			for (int i = 0; i < formats.Length; i++)
			{
				formats[i].ClosingTag(sb);
			}

			return this;
		}

		#endregion

		#region Format double + KF

		/// <summary> Append the string representation of a double and the specifed KsmFormat formatters </summary>
		public KsmString Format(double value, string format, KF f1)
		{
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a double and the specifed KsmFormat formatters </summary>
		public KsmString Format(double value, string format, KF f1, KF f2)
		{
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a double and the specifed KsmFormat formatters </summary>
		public KsmString Format(double value, string format, KF f1, KF f2, KF f3)
		{
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a double and the specifed KsmFormat formatters </summary>
		public KsmString Format(double value, string format, KF f1, KF f2, KF f3, KF f4)
		{
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a double and the specifed KsmFormat formatters </summary>
		public KsmString Format(double value, string format, KF f1, KF f2, KF f3, KF f4, KF f5)
		{
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a double and the specifed KsmFormat formatters </summary>
		public KsmString Format(double value, string format, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6)
		{
			f6.OpeningTag(sb);
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			f6.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a double and the specifed KsmFormat formatters </summary>
		public KsmString Format(double value, string format, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6, KF f7)
		{
			f7.OpeningTag(sb);
			f6.OpeningTag(sb);
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			f6.ClosingTag(sb);
			f7.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a double and the specifed KsmFormat formatters </summary>
		public KsmString Format(double value, string format, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6, KF f7, KF f8)
		{
			f8.OpeningTag(sb);
			f7.OpeningTag(sb);
			f6.OpeningTag(sb);
			f5.OpeningTag(sb);
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value.ToString(format));
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			f5.ClosingTag(sb);
			f6.ClosingTag(sb);
			f7.ClosingTag(sb);
			f8.ClosingTag(sb);
			return this;
		}

		/// <summary> Append the string representation of a double and the specifed KsmFormat formatters </summary>
		public KsmString Format(double value, string format, params KF[] formats)
		{
			for (int i = formats.Length - 1; i >= 0; i--)
			{
				formats[i].OpeningTag(sb);
			}

			sb.Append(value.ToString(format));

			for (int i = 0; i < formats.Length; i++)
			{
				formats[i].ClosingTag(sb);
			}

			return this;
		}

		#endregion

		#region Format string + KF

		/// <summary> Append a string using specifed KsmFormat formatters </summary>
		public KsmString Format(string value, KF f1)
		{
			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);
			return this;
		}

		/// <summary> Append a string using specifed KsmFormat formatters </summary>
		public KsmString Format(string value, KF f1, KF f2)
		{
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			return this;
		}

		/// <summary> Append a string using specifed KsmFormat formatters </summary>
		public KsmString Format(string value, KF f1, KF f2, KF f3)
		{
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			return this;
		}

		/// <summary> Append a string using specifed KsmFormat formatters </summary>
		public KsmString Format(string value, KF f1, KF f2, KF f3, KF f4)
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
			return this;
		}

		/// <summary> Append a string using specifed KsmFormat formatters </summary>
		public KsmString Format(string value, KF f1, KF f2, KF f3, KF f4, KF f5)
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
			return this;
		}

		/// <summary> Append a string using specifed KsmFormat formatters </summary>
		public KsmString Format(string value, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6)
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
			return this;
		}

		/// <summary> Append a string using specifed KsmFormat formatters </summary>
		public KsmString Format(string value, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6, KF f7)
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
			return this;
		}

		/// <summary> Append a string using specifed KsmFormat formatters </summary>
		public KsmString Format(string value, KF f1, KF f2, KF f3, KF f4, KF f5, KF f6, KF f7, KF f8)
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
			return this;
		}

		/// <summary> Append a string using specifed KsmFormat formatters </summary>
		public KsmString Format(string value, params KF[] formats)
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

			return this;
		}

		#endregion

		#region Format KF

		/// <summary> Append KsmFormat formatters </summary>
		public KsmString Format(KF f1)
		{
			f1.OpeningTag(sb);
			f1.ClosingTag(sb);
			return this;
		}

		/// <summary> Append KsmFormat formatters </summary>
		public KsmString Format(KF f1, KF f2)
		{
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			return this;
		}

		/// <summary> Append KsmFormat formatters </summary>
		public KsmString Format(KF f1, KF f2, KF f3)
		{
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			return this;
		}

		/// <summary> Append KsmFormat formatters </summary>
		public KsmString Format(KF f1, KF f2, KF f3, KF f4)
		{
			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);
			return this;
		}

		/// <summary> Append KsmFormat formatters </summary>
		public KsmString Format(KF f1, KF f2, KF f3, KF f4, KF f5)
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
			return this;
		}

		/// <summary> Append KsmFormat formatters </summary>
		public KsmString Format(KF f1, KF f2, KF f3, KF f4, KF f5, KF f6)
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
			return this;
		}

		/// <summary> Append KsmFormat formatters </summary>
		public KsmString Format(KF f1, KF f2, KF f3, KF f4, KF f5, KF f6, KF f7)
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
			return this;
		}

		/// <summary> Append KsmFormat formatters </summary>
		public KsmString Format(KF f1, KF f2, KF f3, KF f4, KF f5, KF f6, KF f7, KF f8)
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
			return this;
		}

		/// <summary> Append KsmFormat formatters </summary>
		public KsmString Format(params KF[] formats)
		{
			for (int i = formats.Length - 1; i >= 0; i--)
			{
				formats[i].OpeningTag(sb);
			}

			for (int i = 0; i < formats.Length; i++)
			{
				formats[i].ClosingTag(sb);
			}

			return this;
		}

		#endregion

		public KsmString Position(int position)
		{
			sb.Append(KsmFormatPosition.closingTagBegin);
			sb.Append(position);
			sb.Append(KsmFormatPosition.closingTagEnd);
			return this;
		}

		public KsmString ReadableRate(double rate, bool showSign = true, string unit = "")
		{
			KsmFormatReadableRate.Append(sb, rate, showSign, unit);
			return this;
		}

		public KsmString ReadableDuration(double d, Precision precision = Precision.Compact, ulong yearsMax = 99)
		{
			KsmFormatReadableDuration.Append(sb, d, precision, yearsMax);
			return this;
		}

		public KsmString ReadableCountdown(double d, Precision precision = Precision.Compact, ulong yearsMax = 99)
		{
			sb.Append("T-");
			KsmFormatReadableDuration.Append(sb, d, precision, yearsMax);
			return this;
		}

		public KsmString ReadableDistance(double distance)
		{
			KsmFormatReadableDistance.Append(sb, distance);
			return this;
		}

		public KsmString ReadableSpeed(double speed)
		{
			KsmFormatReadableDistance.Append(sb, speed);
			sb.Append("/s");
			return this;
		}

		public KsmString ReadableTemperature(double temp)
		{
			KsmFormatReadableTemperature.Append(sb, temp);
			return this;
		}

		public KsmString ReadableAngle(double angle)
		{
			KsmFormatReadableAngle.Append(sb, angle);
			return this;
		}

		public KsmString ReadableIrradiance(double irradiance)
		{
			KsmFormatReadableIrradiance.Append(sb, irradiance);
			return this;
		}

		public KsmString ThermalFlux(double flux)
		{
			KsmFormatReadableThermalFlux.Append(sb, flux);
			return this;
		}

		public KsmString ReadableField(double strength)
		{
			KsmFormatReadableField.Append(sb, strength);
			return this;
		}

		public KsmString ReadableRadiation(double radiation, bool dangerColor = true)
		{
			KsmFormatReadableRadiation.Append(sb, radiation, dangerColor);
			return this;
		}

		public KsmString ReadablePressure(double pressure)
		{
			KsmFormatReadablePressure.Append(sb, pressure);
			return this;
		}

		public KsmString ReadableVolume(double volume)
		{
			KsmFormatReadableVolume.Append(sb, volume);
			return this;
		}

		public KsmString ReadableSurface(double surface)
		{
			KsmFormatReadableSurface.Append(sb, surface);
			return this;
		}

		public KsmString ReadableMass(double mass)
		{
			KsmFormatReadableMass.Append(sb, mass);
			return this;
		}

		public KsmString ReadableStorage(double amount, double capacity)
		{
			KsmFormatReadableStorage.Append(sb, amount, capacity);
			return this;
		}

		public KsmString ReadableAmountCompact(double amount, string unit = null)
		{
			KsmFormatReadableAmountCompact.Append(sb, amount, unit);
			return this;
		}

		public KsmString ReadableScience(double amount)
		{
			KsmFormatReadableScience.Append(sb, amount);
			return this;
		}

		public KsmString ReadableDataSize(double size)
		{
			KsmFormatReadableDataSize.Append(sb, size);
			return this;
		}

		public KsmString ReadableDataSizeCompared(double size, double capacity)
		{
			KsmFormatReadableDataSizeCompared.Append(sb, size, capacity);
			return this;
		}

		public KsmString ReadableDataRate(double rate)
		{
			KsmFormatReadableDataRate.Append(sb, rate);
			return this;
		}

		public KsmString ReadableDataRateCompared(double rate, double maxRate)
		{
			KsmFormatReadableDataRateCompared.Append(sb, rate, maxRate);
			return this;
		}

		#region Info

		private const string KsmInfoPos1 = "<pos=";
		private const string KsmInfoPos2 = "px><b>";
		private const string KsmInfoSpecifics = ": <b>";
		private const string KsmInfoEnd = "</b>";

		/// <summary> Format to "label <b>value</b>\n" with the value position at valuePos or "label: <b>value</b>\n" if valuePos is negative</summary>
		public KsmString Info(string label, string value, int valuePos = -1, bool endLine = true)
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
			if (endLine)
				sb.Append(lineBreak);
			return this;
		}

		private const string lineBreak = "\n";

		private const string ksmInfoSeparator = ": ";
		private const string ksmInfoPosBegin = "<pos=";
		private const string ksmInfoPosEnd = "px>";

		/// <summary> Format to "label value\n" with the value position at valuePos or "label: value\n" if valuePos is negative, using KsmFormat formatters around the value</summary>
		public KsmString Info(string label, string value, KF f1, int valuePos = -1, bool endLine = true)
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
			if (endLine)
				sb.Append(lineBreak);
			return this;
		}

		/// <summary> Format to "label value\n" with the value position at valuePos or "label: value\n" if valuePos is negative, using KsmFormat formatters around the value</summary>
		public KsmString Info(string label, string value, KF f1, KF f2, int valuePos = -1, bool endLine = true)
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
			if (endLine)
				sb.Append(lineBreak);
			return this;
		}

		/// <summary> Format to "label value\n" with the value position at valuePos or "label: value\n" if valuePos is negative, using KsmFormat formatters around the value</summary>
		public KsmString Info(string label, string value, KF f1, KF f2, KF f3, int valuePos = -1, bool endLine = true)
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
			if (endLine)
				sb.Append(lineBreak);
			return this;
		}

		/// <summary> Format to "label value\n" with the value position at valuePos or "label: value\n" if valuePos is negative, using KsmFormat formatters around the value</summary>
		public KsmString Info(string label, string value, KF f1, KF f2, KF f3, KF f4, int valuePos = -1, bool endLine = true)
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
			if (endLine)
				sb.Append(lineBreak);
			return this;
		}

		/// <summary> Format to "label [KF formatters] \n" with the formatters position at valuePos or "label: [KF formatters]\n" if valuePos is negative</summary>
		public KsmString Info(string label, KF f1, int valuePos = -1, bool endLine = true)
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
			if (endLine)
				sb.Append(lineBreak);
			return this;
		}

		/// <summary> Format to "label [KF formatters] \n" with the formatters position at valuePos or "label: [KF formatters]\n" if valuePos is negative</summary>
		public KsmString Info(string label, KF f1, KF f2, int valuePos = -1, bool endLine = true)
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
			if (endLine)
				sb.Append(lineBreak);
			return this;
		}

		/// <summary> Format to "label [KF formatters] \n" with the formatters position at valuePos or "label: [KF formatters]\n" if valuePos is negative</summary>
		public KsmString Info(string label, KF f1, KF f2, KF f3, int valuePos = -1, bool endLine = true)
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
			if (endLine)
				sb.Append(lineBreak);
			return this;
		}

		/// <summary> Format to "label [KF formatters] \n" with the formatters position at valuePos or "label: [KF formatters]\n" if valuePos is negative</summary>
		public KsmString Info(string label, KF f1, KF f2, KF f3, KF f4, int valuePos = -1, bool endLine = true)
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
			if (endLine)
				sb.Append(lineBreak);
			return this;
		}

		#endregion

		private const string alignLeft = "<align=left>";
		private const string alignRight = "<align=right>";
		private const string overlappingNewLine = "<line-height=0.00001>\n";
		private const string resetLineHeight = "<line-height=1>";

		/// <summary> Format to "label value" with a left-aligned label and right-aligned value. The text component must have its lineSpacing set to 0</summary>
		public KsmString InfoRight(string label, string value)
		{
			sb.Append(alignLeft);
			sb.Append(label);
			sb.Append(overlappingNewLine);
			sb.Append(alignRight);
			sb.Append(value);
			sb.Append(resetLineHeight);

			return this;
		}

		/// <summary> Format to "label value" with a left-aligned label and right-aligned value. The text component must have its lineSpacing set to 0</summary>
		public KsmString InfoRight(string label, string value, KF f1)
		{
			sb.Append(alignLeft);
			sb.Append(label);
			sb.Append(overlappingNewLine);
			sb.Append(alignRight);

			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);

			sb.Append(resetLineHeight);

			return this;
		}

		/// <summary> Format to "label value" with a left-aligned label and right-aligned value. The text component must have its lineSpacing set to 0</summary>
		public KsmString InfoRight(string label, string value, KF f1, KF f2)
		{
			sb.Append(alignLeft);
			sb.Append(label);
			sb.Append(overlappingNewLine);
			sb.Append(alignRight);

			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);

			sb.Append(resetLineHeight);

			return this;
		}

		/// <summary> Format to "label value" with a left-aligned label and right-aligned value. The text component must have its lineSpacing set to 0</summary>
		public KsmString InfoRight(string label, string value, KF f1, KF f2, KF f3)
		{
			sb.Append(alignLeft);
			sb.Append(label);
			sb.Append(overlappingNewLine);
			sb.Append(alignRight);

			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);

			sb.Append(resetLineHeight);

			return this;
		}

		/// <summary> Format to "label value" with a left-aligned label and right-aligned value. The text component must have its lineSpacing set to 0</summary>
		public KsmString InfoRight(string label, string value, KF f1, KF f2, KF f3, KF f4)
		{
			sb.Append(alignLeft);
			sb.Append(label);
			sb.Append(overlappingNewLine);
			sb.Append(alignRight);

			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			sb.Append(value);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);

			sb.Append(resetLineHeight);

			return this;
		}

		/// <summary> Format to "label [KF formatters]" with a left-aligned label and right-aligned formatters. The text component must have its lineSpacing set to 0</summary>
		public KsmString InfoRight(string label, KF f1)
		{
			sb.Append(alignLeft);
			sb.Append(label);
			sb.Append(overlappingNewLine);
			sb.Append(alignRight);

			f1.OpeningTag(sb);
			f1.ClosingTag(sb);

			sb.Append(resetLineHeight);

			return this;
		}

		/// <summary> Format to "label [KF formatters]" with a left-aligned label and right-aligned formatters. The text component must have its lineSpacing set to 0</summary>
		public KsmString InfoRight(string label, KF f1, KF f2)
		{
			sb.Append(alignLeft);
			sb.Append(label);
			sb.Append(overlappingNewLine);
			sb.Append(alignRight);

			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);

			sb.Append(resetLineHeight);

			return this;
		}

		/// <summary> Format to "label [KF formatters]" with a left-aligned label and right-aligned formatters. The text component must have its lineSpacing set to 0</summary>
		public KsmString InfoRight(string label, KF f1, KF f2, KF f3)
		{
			sb.Append(alignLeft);
			sb.Append(label);
			sb.Append(overlappingNewLine);
			sb.Append(alignRight);

			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);

			sb.Append(resetLineHeight);

			return this;
		}

		/// <summary> Format to "label [KF formatters]" with a left-aligned label and right-aligned formatters. The text component must have its lineSpacing set to 0</summary>
		public KsmString InfoRight(string label, KF f1, KF f2, KF f3, KF f4)
		{
			sb.Append(alignLeft);
			sb.Append(label);
			sb.Append(overlappingNewLine);
			sb.Append(alignRight);

			f4.OpeningTag(sb);
			f3.OpeningTag(sb);
			f2.OpeningTag(sb);
			f1.OpeningTag(sb);
			f1.ClosingTag(sb);
			f2.ClosingTag(sb);
			f3.ClosingTag(sb);
			f4.ClosingTag(sb);

			sb.Append(resetLineHeight);

			return this;
		}
	}
}
