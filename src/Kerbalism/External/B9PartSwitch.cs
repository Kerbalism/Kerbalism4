using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{

	public static class B9PartSwitch
	{
		private static Version moduleMatcherMinVersion = new Version(2, 16, 0, 0);
		private static bool isV216Plus;

		public const string moduleName = "ModuleB9PartSwitch";
		private static Type moduleType;
		private static FieldInfo moduleSubtypesField;

		private static Type subTypeType;
		private static FieldInfo subtypeNameField;
		private static FieldInfo subtypeDescriptionDetailField;
		private static FieldInfo subtypeUpgradeRequiredField;
		private static FieldInfo subtypeModuleModifierInfosField;

		private static Type moduleModifierInfoType;
		private static FieldInfo moduleModifierIdentifierNodeField;
		private static FieldInfo moduleModifierDataNodeField;
		private static FieldInfo moduleModifierModuleActiveField;

		// 2.16+
		private static Type moduleMatcherType;
		private static ConstructorInfo moduleMatcherCtor;
		private static MethodInfo moduleMatcherFindModuleMethod;

		// 2.15-
		private static MethodInfo moduleModifierFindModuleMethod;

		public static void Init()
		{
			foreach (var a in AssemblyLoader.loadedAssemblies)
			{
				if (a.name == "B9PartSwitch")
				{
					Version loadedVersion = a.assembly.GetName().Version;
					isV216Plus = loadedVersion >= moduleMatcherMinVersion;

					moduleType = a.assembly.GetType("B9PartSwitch.ModuleB9PartSwitch");
					moduleSubtypesField = moduleType.GetField("subtypes");

					subTypeType = a.assembly.GetType("B9PartSwitch.PartSubtype");
					subtypeNameField = subTypeType.GetField("subtypeName");
					subtypeDescriptionDetailField = subTypeType.GetField("descriptionDetail");
					subtypeUpgradeRequiredField = subTypeType.GetField("upgradeRequired");
					subtypeModuleModifierInfosField = subTypeType.GetField("moduleModifierInfos");

					moduleModifierInfoType = a.assembly.GetType("B9PartSwitch.ModuleModifierInfo");
					moduleModifierIdentifierNodeField = moduleModifierInfoType.GetField("identifierNode");
					moduleModifierDataNodeField = moduleModifierInfoType.GetField("dataNode");
					moduleModifierModuleActiveField = moduleModifierInfoType.GetField("moduleActive");

					if (isV216Plus)
					{
						moduleMatcherType = a.assembly.GetType("B9PartSwitch.ModuleMatcher");

						// public ModuleMatcher(ConfigNode identifierNode)
						Type[] moduleMatcherCtorArgs = new Type[] { typeof(ConfigNode) };
						moduleMatcherCtor = moduleMatcherType.GetConstructor(new Type[] { typeof(ConfigNode) });

						// public PartModule FindModule(Part part)
						Type[] findModuleArgs = new Type[] { typeof(Part) };
						moduleMatcherFindModuleMethod = moduleMatcherType.GetMethod("FindModule", findModuleArgs);
					}
					else
					{
						// private PartModule FindModule(Part part, PartModule parentModule, string moduleName)
						Type[] findModuleArgs = new Type[] { typeof(Part), typeof(PartModule), typeof(string) };
						moduleModifierFindModuleMethod = moduleModifierInfoType.GetMethod("FindModule", BindingFlags.Instance | BindingFlags.NonPublic, null, findModuleArgs, null);
					}

					break;
				}
			}
		}

		public static IEnumerable<SubtypeWrapper> GetSubtypes(PartModule moduleB9PartSwitch)
		{
			foreach (object subtype in GetSubTypes(moduleB9PartSwitch))
			{
				yield return new SubtypeWrapper(moduleB9PartSwitch, subtype);
			}
		}

		private static IList GetSubTypes(PartModule moduleB9PartSwitch)
		{
			return (IList)moduleSubtypesField.GetValue(moduleB9PartSwitch);
		}

		public class SubtypeWrapper
		{
			private object instance;
			private PartModule moduleB9PartSwitch;
			public string Name { get; private set; }
			public string TechRequired { get; private set; }

			public SubtypeWrapper(PartModule moduleB9PartSwitch, object subtype)
			{
				this.moduleB9PartSwitch = moduleB9PartSwitch;
				instance = subtype;
				Name = (string)subtypeNameField.GetValue(subtype);

				string upgradeName = (string)subtypeUpgradeRequiredField.GetValue(subtype);
				if (string.IsNullOrEmpty(upgradeName))
				{
					TechRequired = string.Empty;
				}
				else
				{
					PartUpgradeHandler.Upgrade upgrade = PartUpgradeManager.Handler.GetUpgrade(upgradeName);
					if (upgrade != null)
					{
						TechRequired = upgrade.techRequired;
					}
					else
					{
						TechRequired = string.Empty;
					}
				}
			}

			public IEnumerable<ModuleModifierWrapper> ModuleModifiers
			{
				get
				{
					foreach (object modifier in GetModuleModifiers())
					{
						yield return new ModuleModifierWrapper(moduleB9PartSwitch, modifier);
					}
				}
			}

			private IList GetModuleModifiers()
			{
				return (IList)subtypeModuleModifierInfosField.GetValue(instance);
			}

			public void SetSubTypeDescriptionDetail(string descriptionDetail)
			{
				descriptionDetail = descriptionDetail.Trim(); // stop B9PS complaining about trailing "\n"
				subtypeDescriptionDetailField.SetValue(instance, descriptionDetail);
			}
		}

		public class ModuleModifierWrapper
		{
			public PartModule PartModule { get; private set; }
			public bool ModuleActive { get; private set; }
			public ConfigNode DataNode { get; private set; }

			public ModuleModifierWrapper(PartModule moduleB9PartSwitch, object moduleModifier)
			{
				ConfigNode identiferNode = (ConfigNode)moduleModifierIdentifierNodeField.GetValue(moduleModifier);

				if (isV216Plus)
				{
					// public ModuleMatcher(ConfigNode identifierNode)
					object[] moduleMatcherCtorParams = new object[] { identiferNode };
					object moduleMatcher = moduleMatcherCtor.Invoke(moduleMatcherCtorParams);

					// public PartModule FindModule(Part part)
					object[] moduleMatcherFindModuleMethodParams = new object[] { moduleB9PartSwitch.part };
					PartModule = (PartModule)moduleMatcherFindModuleMethod.Invoke(moduleMatcher, moduleMatcherFindModuleMethodParams);
				}
				else
				{
					// private PartModule FindModule(Part part, PartModule parentModule, string moduleName)
					string moduleName = Lib.ConfigValue(identiferNode, "name", string.Empty);
					object[] findModuleParams = new object[] { moduleB9PartSwitch.part, moduleB9PartSwitch, moduleName };
					PartModule = (PartModule)moduleModifierFindModuleMethod.Invoke(moduleModifier, findModuleParams);
				}

				ModuleActive = (bool)moduleModifierModuleActiveField.GetValue(moduleModifier);
				DataNode = (ConfigNode)moduleModifierDataNodeField.GetValue(moduleModifier);
			}
		}
	}
}
