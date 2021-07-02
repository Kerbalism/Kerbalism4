using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace KERBALISM
{
	/// <summary>
	/// When applied on a public/non-public field/property, the value will parsed from a provided ConfigNode by calling CFGValue.Parse().<br/>
	/// See the Utility/Serialization class for supported types. Can also be applied to a generic List of supported types.<br/>
	/// The ConfigNode value(s) name will be the same as the member name. If the value isn't found in the node, the instance member stays untouched.<br/>
	/// Note : vastly slower and garbagey than manual deserialization, use this only for one time config parsing and not for game load/save cycles.<br/>
	/// Note 2 : if called on a child class, private members of the base class won't be returned. They need to be public or protected.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
	public sealed class CFGValue : Attribute
	{
		/// <summary>
		/// Deserialize all the instance fields/properties that have the [CFGValue] attribute and have a corresponding value
		/// in the provided ConfigNode. If the value isn't defined in the ConfigNode, the instance field/property is untouched.
		/// </summary>
		public static void Parse(object instance, ConfigNode node)
		{
			Type instanceType = instance.GetType();

			foreach (FieldInfo field in instanceType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (!IsDefined(field, typeof(CFGValue)))
					continue;

				if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>)) && node.HasValue(field.Name))
				{
					Type itemType = field.FieldType.GetGenericArguments()[0];
					Type listType = typeof(List<>).MakeGenericType(itemType);
					IList list = (IList)Activator.CreateInstance(listType);
					field.SetValue(instance, list);

					foreach (string itemStr in node.GetValues(field.Name))
					{
						if (string.IsNullOrEmpty(itemStr))
							continue;

						if (Serialization.TryDeserialize(itemStr, itemType, out object item))
							list.Add(item);
					}
				}
				else
				{
					string valueStr = node.GetValue(field.Name);

					if (string.IsNullOrEmpty(valueStr))
						continue;

					if (Serialization.TryDeserialize(valueStr, field.FieldType, out object value))
						field.SetValue(instance, value);
				}
			}

			foreach (PropertyInfo property in instanceType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (!property.CanWrite)
					continue;

				if (!IsDefined(property, typeof(CFGValue)))
					continue;

				if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>)) && node.HasValue(property.Name))
				{
					Type itemType = property.PropertyType.GetGenericArguments()[0];
					Type listType = typeof(List<>).MakeGenericType(itemType);
					IList list = (IList)Activator.CreateInstance(listType);
					property.SetValue(instance, list);

					foreach (string itemStr in node.GetValues(property.Name))
					{
						if (string.IsNullOrEmpty(itemStr))
							continue;

						if (Serialization.TryDeserialize(itemStr, itemType, out object item))
							list.Add(item);
					}
				}
				else
				{
					string valueStr = node.GetValue(property.Name);

					if (string.IsNullOrEmpty(valueStr))
						continue;

					if (Serialization.TryDeserialize(valueStr, property.PropertyType, out object value))
						property.SetValue(instance, value);
				}
			}
		}
	}
}
