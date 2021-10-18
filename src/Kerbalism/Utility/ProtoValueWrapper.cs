using System;
using System.Globalization;

namespace KERBALISM
{
	public abstract class ProtoModuleValue<T>
	{
		protected ConfigNode.Value nodeValue;

		public abstract T Value { get; set; }

		protected static bool TryGet(ConfigNode valuesNode, string valueName, out ConfigNode.Value value)
		{
			for (int i = 0; i < valuesNode.CountValues; i++)
			{
				if (valuesNode.values[i].name == valueName)
				{
					value = valuesNode.values[i];
					return true;
				}
			}

			value = null;
			return false;
		}
	}

	public class ProtoModuleValueBool : ProtoModuleValue<bool>
	{
		public static bool TryGet(ConfigNode valuesNode, string valueName, out ProtoModuleValueBool value)
		{
			if (!TryGet(valuesNode, valueName, out ConfigNode.Value nodeValue) || !bool.TryParse(nodeValue.value, out _))
			{
				value = null;
				return false;
			}

			value = new ProtoModuleValueBool();
			value.nodeValue = nodeValue;
			return true;
		}

		public override bool Value
		{
			get => bool.Parse(nodeValue.value);
			set => nodeValue.value = value.ToString(CultureInfo.InvariantCulture);
		}
	}

	public class ProtoModuleValueUInt : ProtoModuleValue<uint>
	{
		public static bool TryGet(ConfigNode valuesNode, string valueName, out ProtoModuleValueUInt value)
		{
			if (!TryGet(valuesNode, valueName, out ConfigNode.Value nodeValue) || !uint.TryParse(nodeValue.value, out _))
			{
				value = null;
				return false;
			}

			value = new ProtoModuleValueUInt();
			value.nodeValue = nodeValue;
			return true;
		}

		public override uint Value
		{
			get => uint.Parse(nodeValue.value);
			set => nodeValue.value = value.ToString(CultureInfo.InvariantCulture);
		}
	}

	public class ProtoModuleValueInt : ProtoModuleValue<int>
	{
		public static bool TryGet(ConfigNode valuesNode, string valueName, out ProtoModuleValueInt value)
		{
			if (!TryGet(valuesNode, valueName, out ConfigNode.Value nodeValue) || !int.TryParse(nodeValue.value, out _))
			{
				value = null;
				return false;
			}

			value = new ProtoModuleValueInt();
			value.nodeValue = nodeValue;
			return true;
		}

		public override int Value
		{
			get => int.Parse(nodeValue.value);
			set => nodeValue.value = value.ToString(CultureInfo.InvariantCulture);
		}
	}

	public class ProtoModuleValueFloat : ProtoModuleValue<float>
	{
		public static bool TryGet(ConfigNode valuesNode, string valueName, out ProtoModuleValueFloat value)
		{
			if (!TryGet(valuesNode, valueName, out ConfigNode.Value nodeValue) || !float.TryParse(nodeValue.value, out _))
			{
				value = null;
				return false;
			}

			value = new ProtoModuleValueFloat();
			value.nodeValue = nodeValue;
			return true;
		}

		public override float Value
		{
			get => float.Parse(nodeValue.value);
			set => nodeValue.value = value.ToString(CultureInfo.InvariantCulture);
		}
	}

	public class ProtoModuleValueDouble : ProtoModuleValue<double>
	{
		public static bool TryGet(ConfigNode valuesNode, string valueName, out ProtoModuleValueDouble value)
		{
			if (!TryGet(valuesNode, valueName, out ConfigNode.Value nodeValue) || !double.TryParse(nodeValue.value, out _))
			{
				value = null;
				return false;
			}

			value = new ProtoModuleValueDouble();
			value.nodeValue = nodeValue;
			return true;
		}

		public override double Value
		{
			get => double.Parse(nodeValue.value);
			set => nodeValue.value = value.ToString(CultureInfo.InvariantCulture);
		}
	}

	public class ProtoModuleValueString : ProtoModuleValue<string>
	{
		public static bool TryGet(ConfigNode valuesNode, string valueName, out ProtoModuleValueString value)
		{
			if (!TryGet(valuesNode, valueName, out ConfigNode.Value nodeValue))
			{
				value = null;
				return false;
			}

			value = new ProtoModuleValueString();
			value.nodeValue = nodeValue;
			return true;
		}

		public override string Value
		{
			get => nodeValue.value;
			set => nodeValue.value = value;
		}
	}

	public class ProtoModuleValueEnum<T> : ProtoModuleValue<T>
	{
		public static bool TryGet(ConfigNode valuesNode, string valueName, out ProtoModuleValueEnum<T> value)
		{
			if (!TryGet(valuesNode, valueName, out ConfigNode.Value nodeValue) || !Enum.IsDefined(typeof(T), nodeValue.value))
			{
				value = null;
				return false;
			}

			value = new ProtoModuleValueEnum<T>();
			value.nodeValue = nodeValue;
			return true;
		}

		public override T Value
		{
			get => (T)Enum.Parse(typeof(T), nodeValue.value);
			set => nodeValue.value = value.ToString();
		}
	}
}
