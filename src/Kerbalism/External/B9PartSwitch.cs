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

		public static void Init(Harmony harmony)
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

					Type moduleDataHandlerBasic = a.assembly.GetType("B9PartSwitch.PartSwitch.PartModifiers.ModuleDataHandlerBasic");

					var activate = moduleDataHandlerBasic.GetMethod("Activate", BindingFlags.Instance | BindingFlags.NonPublic);
					var ActivatePostfixPatch = typeof(B9PartSwitch).GetMethod(nameof(ActivatePostfix), BindingFlags.Static | BindingFlags.NonPublic);
					harmony.Patch(activate, null, new HarmonyMethod(ActivatePostfixPatch));

					break;
				}
			}
		}

		// General note :
		// This piece of code capture B9PS module switching events, and abstract what it does in order to consolidate the two separate B9PS calls:
		// - Deactivate() the current subtype and revert the module config to the prefab config
		// - Activate() the new subtype and apply its config to the module
		// This allow to :
		// - Avoid useless deactivation/activation cycles and reconfiguration cycles, especially when instantiating existing (persisted) modules
		// - Identify the exact nature of the switch event so the handler can handle them without accordingly
		// To do this, we rely on the assumption that our module only support two specific switch actions :
		// - switching the KsmModuleDefinition through B9PS changing the KsmPartModule.definition KSPField
		// - switching the module enabled/disabled state through B9PS changing the KsmPartModule.switchModuleEnabled KSPField
		// To detect changes, we rely on :
		// - the actual Definition reference staying untouched, allowing us to compare its name to the KsmPartModule.definition KSPField
		// - a KsmPartModule.switchLastModuleEnabled field that we maintain synchronized with KsmPartModule.switchModuleEnabled

		// Note on module enabling/disabling :
		// B9PS handle its "moduleActive" config field through a separate internal "ModuleDeactivator" modifier object, which is derived
		// from the same "PartModifierBase" class as the "ModuleDataHandlerBasic" we are patching here. Handling the Activate / Deactivate methods
		// of both objects with patches would be quite a mess, as we would need to move the patching done here further up the B9PS call stack to catch
		// both objects Activate/Deactivate calls.
		// Moreover, we don't want to rely on the default module enabling/disabling behaviour of "ModuleDeactivator".
		// Instead, we implement a "switchModuleEnabled" KSPField on the KsmPartModule base class, which can then be changed in the B9PS subtype DATA{}
		// node. This allow to catch both cases (configuration change and enabled state change) in the same atomic operation, and to handle it as we want.
		private static void ActivatePostfix(PartModule ___module)
		{
			if (___module is KsmPartModule ksmPartModule && ksmPartModule.ModuleHandler is IB9Switchable switchableHandler)
			{
				KsmModuleDefinition lastDefinition;
				if (switchableHandler.Definition.DefinitionName != ksmPartModule.definition)
				{
					lastDefinition = switchableHandler.Definition;
					switchableHandler.Definition = KsmModuleDefinitionLibrary.GetDefinition(ksmPartModule);
				}
				else
				{
					lastDefinition = null;
				}

				if (ksmPartModule.switchLastModuleEnabled == true && ksmPartModule.switchModuleEnabled == false)
				{
					ksmPartModule.switchLastModuleEnabled = false;
					ksmPartModule.enabled = ksmPartModule.isEnabled = false;
					ksmPartModule.ModuleHandler.handlerIsEnabled = false;
					switchableHandler.OnSwitchDisable();
				}
				else if (ksmPartModule.switchLastModuleEnabled == false && ksmPartModule.switchModuleEnabled == true)
				{
					ksmPartModule.switchLastModuleEnabled = true;
					ksmPartModule.enabled = ksmPartModule.isEnabled = true;
					ksmPartModule.ModuleHandler.handlerIsEnabled = true;
					ksmPartModule.ModuleHandler.Start();
					switchableHandler.OnSwitchEnable();
				}

				if (lastDefinition != null)
				{
					switchableHandler.OnSwitchChangeDefinition(lastDefinition);
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
				descriptionDetail = descriptionDetail.TrimStart().TrimEnd(); // stop B9PS complaining about trailing "\n"
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
