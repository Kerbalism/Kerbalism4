using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public static class KsmModuleDefinitionLibrary
	{
		public const string VALUENAME_DEFINITION_NAME = "name";
		public const string VALUENAME_DEFINITION_MODULE_NAME = "moduleName";
		public const string NODENAME_KSM_MODULE_DEFINITION = "KSM_MODULE_DEFINITION";
		public const string DEFAULT_LOCAL_DEFINITION = "localDefault";
		public const string DEFAULT_GLOBAL_DEFINITION = "globalDefault";

		/// <summary>
		/// Dictionary of all module definitions, by definitionId (partName + moduleType + definitionName)
		/// </summary>
		private static Dictionary<string, KsmModuleDefinition> definitions = new Dictionary<string, KsmModuleDefinition>();

		/// <summary>
		/// Dictionary of KsmModuleDefinition constructor delegates for every KsmPartModule, by KsmPartModule type name
		/// </summary>
		private static Dictionary<string, Func<KsmModuleDefinition>> activators = new Dictionary<string, Func<KsmModuleDefinition>>();

		public static IEnumerable<KsmModuleDefinition> Definitions => definitions.Values;

		/// <summary>
		/// Get the module definition for that module/handler.
		/// </summary>
		public static KsmModuleDefinition GetDefinition(KsmPartModule module, string definitionId = null)
		{
			KsmModuleDefinition definition;

			if (!string.IsNullOrEmpty(definitionId) && definitions.TryGetValue(definitionId, out definition))
			{
				return definition;
			}

			definitionId = GetLocalDefinitionId(module);
			if (!definitions.TryGetValue(definitionId, out definition))
			{
				definitionId = GetGlobalDefinitionId(module);
				if (!definitions.TryGetValue(definitionId, out definition))
				{
					definitionId = GetGlobalDefaultDefinitionId(module.moduleName);
					definition = definitions[definitionId];
				}
			}

			return definition;
		}

		public static bool TryGetDefinition(string definitionId, out KsmModuleDefinition definition)
		{
			return definitions.TryGetValue(definitionId, out definition);
		}

		public static void Init(Assembly executingAssembly)
		{
			Type definitionBaseType = typeof(KsmModuleDefinition);
			Type ksmPartModuleBaseType = typeof(KsmPartModule);

			// key : definition type, value : constructor delegate
			Dictionary<string, Func<KsmModuleDefinition>> activatorsByType = new Dictionary<string, Func<KsmModuleDefinition>>();

			// key : module type, value : definition type.
			// Note that in case of inherited modules, we can have the same definition type for different modules type
			Dictionary<string, string> definitionsByModule = new Dictionary<string, string>();

			foreach (Type type in executingAssembly.GetTypes())
			{
				if (type.IsClass && !type.IsAbstract)
				{
					if (definitionBaseType.IsAssignableFrom(type))
					{
						ConstructorInfo ctor = type.GetConstructor(Type.EmptyTypes);
						NewExpression newExp = Expression.New(ctor);
						LambdaExpression lambda = Expression.Lambda(typeof(Func<KsmModuleDefinition>), newExp);
						activatorsByType.Add(type.Name, (Func<KsmModuleDefinition>)lambda.Compile());
					}

					if (ksmPartModuleBaseType.IsAssignableFrom(type))
					{
						// this is the KsmPartModule.Definition property, in the generic base abstract class. 
						// Not using nameof because nameof is a mess to use with generics.
						Type definitionType = type.GetProperty("Definition", BindingFlags.Instance | BindingFlags.Public).PropertyType;
						definitionsByModule.Add(type.Name, definitionType.Name);
					}
				}
			}

			foreach (KeyValuePair<string, string> moduleType in definitionsByModule)
			{
				if (!activatorsByType.TryGetValue(moduleType.Value, out Func<KsmModuleDefinition> activator))
				{
					ErrorManager.AddError(true, $"Activator not found",
						$"KsmModuleDefinition activator not found for KsmPartModule type `{moduleType.Key}`. Make sure the KsmModuleDefinition class isn't abstract");
					continue;
				}
				activators.Add(moduleType.Key, activator);
				// create default definitions for modules that don't use a customized config (ex : habitat is added to every crewable part without any config customization)
				KsmModuleDefinition defaultDefinition = activator.Invoke();
				defaultDefinition.Init(new ConfigNode(), GetGlobalDefaultDefinitionId(moduleType.Key), DEFAULT_GLOBAL_DEFINITION, moduleType.Key);
				definitions.Add(defaultDefinition.DefinitionId, defaultDefinition);
			}
		}

		public static void Parse()
		{
			// Parse top level definitions
			foreach (ConfigNode definitionNode in GameDatabase.Instance.GetConfigNodes(NODENAME_KSM_MODULE_DEFINITION))
			{
				string moduleType = Lib.ConfigValue(definitionNode, VALUENAME_DEFINITION_MODULE_NAME, string.Empty);
				string definitionName = Lib.ConfigValue(definitionNode, VALUENAME_DEFINITION_NAME, string.Empty);

				if (moduleType.Length == 0 || definitionName.Length == 0)
				{
					ErrorManager.AddError(false, $"KsmModuleDefinition config parsing error",
						$"Error parsing global {NODENAME_KSM_MODULE_DEFINITION} `{definitionName}` for module `{moduleType}`, is `{VALUENAME_DEFINITION_MODULE_NAME}` defined ?");
					continue;
				}

				InstantiateDefinition(definitionNode, moduleType, definitionName, string.Empty);
			}

			foreach (ConfigNode partNode in GameDatabase.Instance.GetConfigNodes("PART"))
			{
				foreach (ConfigNode partSubNode in partNode.nodes)
				{
					// parse part level definitions (note : this is a convenience thing for easier MM patching when
					// you have several modules of the same type sharing the same definition on a part. Functionaly, those
					// definitions are the same as the module level ones.
					if (partSubNode.name == NODENAME_KSM_MODULE_DEFINITION)
					{
						string partName = Lib.ConfigPartInternalName(partNode);
						string moduleType = Lib.ConfigValue(partSubNode, VALUENAME_DEFINITION_MODULE_NAME, string.Empty);
						string definitionName = Lib.ConfigValue(partSubNode, VALUENAME_DEFINITION_NAME, DEFAULT_LOCAL_DEFINITION);

						if (string.IsNullOrEmpty(partName) || string.IsNullOrEmpty(moduleType))
						{
							ErrorManager.AddError(false, $"KsmModuleDefinition config parsing error",
								$"Error parsing {NODENAME_KSM_MODULE_DEFINITION} `{definitionName}` on part `{partName}` for module `{moduleType}`");
							continue;
						}

						InstantiateDefinition(partSubNode, moduleType, definitionName, partName);
					}
					// parse module level definitions
					else if (partSubNode.name == "MODULE")
					{
						foreach (ConfigNode moduleSubNode in partSubNode.nodes)
						{
							if (moduleSubNode.name == NODENAME_KSM_MODULE_DEFINITION)
							{
								string partName = Lib.ConfigPartInternalName(partNode);
								string moduleType = Lib.ConfigValue(partSubNode, "name", string.Empty);
								string definitionName = Lib.ConfigValue(moduleSubNode, VALUENAME_DEFINITION_NAME, DEFAULT_LOCAL_DEFINITION);

								if (string.IsNullOrEmpty(partName) || string.IsNullOrEmpty(moduleType))
								{
									ErrorManager.AddError(false, $"KsmModuleDefinition config parsing error",
										$"Error parsing {NODENAME_KSM_MODULE_DEFINITION} `{definitionName}` on part `{partName}` for module `{moduleType}`");
									continue;
								}

								InstantiateDefinition(moduleSubNode, moduleType, definitionName, partName);
							}
						}
					}
				}
			}

			foreach (KsmModuleDefinition definition in definitions.Values)
			{
				LoadDefinition(definition);
			}
		}

		private static void LoadDefinition(KsmModuleDefinition definition, KsmModuleDefinition parentDefinition = null)
		{
			if (parentDefinition == null)
			{
				parentDefinition = definition;
			}

			if (!TryGetParentDefinitionId(parentDefinition, out string parentDefinitionId))
			{
				definition.Load();
				return;
			}

			if (!definitions.TryGetValue(parentDefinitionId, out parentDefinition))
			{
				ErrorManager.AddError(false, $"KsmModuleDefinition config parsing error",
					$"Cant find the parent {NODENAME_KSM_MODULE_DEFINITION} definition named `{definition.parentDefinition}` for the {NODENAME_KSM_MODULE_DEFINITION} " +
					$"named `{definition.DefinitionName}` for module `{definition.ModuleType}`");

				definition.Load();
				return;
			}

			parentDefinition.CopyTo(definition);
			LoadDefinition(definition, parentDefinition);
		}

		private static bool TryGetParentDefinitionId(KsmModuleDefinition definition, out string parentDefinitionId)
		{
			if (string.IsNullOrEmpty(definition.parentDefinition))
			{
				parentDefinitionId = null;
				return false;
			}
			parentDefinitionId = GetDefinitionId(definition.ModuleType, definition.parentDefinition, string.Empty);
			return true;
		}

		private static void InstantiateDefinition(ConfigNode definitionNode, string moduleType, string definitionName, string partName)
		{
			if (!TryCreateDefinitionId(moduleType, definitionName, partName, out string definitionId))
				return;

			// just ignore definitions for which we don't have an activator. If we don't have the activator, 
			// the PartModule type doesn't exists and KSP will not instantiate it anyway.
			if (!activators.TryGetValue(moduleType, out Func<KsmModuleDefinition> activator))
				return;

			Lib.LogDebug($"Added definition : `{definitionName}` for `{moduleType}`{(string.IsNullOrEmpty(partName) ? "" : $" on part `{partName}`")}");

			// Attempt to handle the EVA kerbal parts mess.
			// For some reason, the AvailablePart.name of kerbalEVA is always set to the "vintage" versions, even if the vintage versions doesn't exist...
			if (!string.IsNullOrEmpty(partName))
			{
				if (partName == "kerbalEVA")
				{
					if (TryCreateDefinitionId(moduleType, definitionName, "kerbalEVAVintage", out string vintageDefinitionId))
					{
						KsmModuleDefinition vintageDefinition = activator.Invoke();
						vintageDefinition.Init(definitionNode, vintageDefinitionId, definitionName, moduleType);
						definitions.Add(vintageDefinitionId, vintageDefinition);
					}
				}
				else if (partName == "kerbalEVAfemale")
				{
					if (TryCreateDefinitionId(moduleType, definitionName, "kerbalEVAfemaleVintage", out string vintageDefinitionId))
					{
						KsmModuleDefinition vintageDefinition = activator.Invoke();
						vintageDefinition.Init(definitionNode, vintageDefinitionId, definitionName, moduleType);
						definitions.Add(vintageDefinitionId, vintageDefinition);
					}
				}
			}

			KsmModuleDefinition definition = activator.Invoke();
			definition.Init(definitionNode, definitionId, definitionName, moduleType);
			definitions.Add(definitionId, definition);
		}

		private static bool TryCreateDefinitionId(string moduleType, string definitionName, string partName, out string definitionId)
		{
			definitionId = GetDefinitionId(moduleType, definitionName, partName);
			if (definitions.ContainsKey(definitionId))
			{
				ErrorManager.AddError(false, $"KsmModuleDefinition config parsing error",
					$"There is a duplicate {NODENAME_KSM_MODULE_DEFINITION} `{definitionName}` for `{moduleType}`{(string.IsNullOrEmpty(partName) ? "" : $" on part `{partName}`")}, check your configs.");
				return false;
			}

			return true;
		}

		private static string GetDefinitionId(string moduleType, string definitionName, string partName)
		{
			return partName + moduleType + definitionName;
		}

		private static string GetGlobalDefinitionId(KsmPartModule module)
		{
			return module.moduleName + module.definition;
		}

		// TODO : test if things work as expected with the weird DLC EVA kerbals variants
		private static string GetLocalDefinitionId(KsmPartModule module)
		{
			if (module.part.partInfo == null) // partInfo will be null at prefab compilation. TODO : instantiate a PartData for the prefab and use it in both cases !
			{
				return module.part.name + module.moduleName + module.definition;
			}
			else
			{
				return module.part.partInfo.name + module.moduleName + module.definition;
			}
		}

		private static string GetGlobalDefaultDefinitionId(string moduleName)
		{
			return moduleName + DEFAULT_GLOBAL_DEFINITION;
		}
	}



	public abstract class KsmModuleDefinition
	{
		private string definitionName;
		private string definitionId;
		private string moduleType;
		
		private ConfigNode config;

		public string DefinitionName => definitionName;
		public string DefinitionId => definitionId;
		public string ModuleType => moduleType;

		public string parentDefinition;
		[CFGValue] public virtual string ModuleTitle { get; private set; }

		public void Init(ConfigNode config, string definitionId, string definitionName, string moduleType)
		{
			this.config = config;
			this.definitionId = definitionId;
			this.definitionName = definitionName;
			this.moduleType = moduleType;

			parentDefinition = Lib.ConfigValue(config, nameof(parentDefinition), string.Empty);

			if (moduleType.StartsWith("ModuleKsm"))
				ModuleTitle = moduleType.Remove(0, "ModuleKsm".Length).PrintSpacedStringFromCamelcase();
			else if (moduleType.StartsWith("Module"))
				ModuleTitle = moduleType.Remove(0, "Module".Length).PrintSpacedStringFromCamelcase();
			else
				ModuleTitle = moduleType.PrintSpacedStringFromCamelcase();
		}

		public void CopyTo(KsmModuleDefinition definition)
		{
			config.CopyTo(definition.config, false);
		}

		public void Load()
		{
			CFGValue.Parse(this, config);
			OnLoad(config);
		}

		public override string ToString() => definitionId;

		/// <summary>
		/// Override this to do some extra parsing after the [CFGValue] fields/properties have been loaded.
		/// </summary>
		public virtual void OnLoad(ConfigNode definitionNode) { }

		/// <summary>
		/// Override this to customize the module description. Will be shown :<br/>
		/// - In the editor part tooltip module list, if the ModuleHandler.Context has the InfoContext.EditorPartTooltip flag<br/>
		/// - In the B9PS subtype tooltips, through the IB9Switchable.GetSubtypeDescription() ModuleHandler interface method <br/>
		/// - In our various editor / flight UIs<br/>
		/// The provided modulePrefab instance allow to customize the description according to the prefab fields, if needed.
		/// Note that id ModuleHandler.Context has the InfoContext.EditorPartTooltip flag, this is automatically mapped to the
		/// default KsmPartModule IModuleInfo.GetInfo() stock interface implementation.
		/// </summary>
		public virtual string ModuleDescription<T>(T modulePrefab) where T : KsmPartModule => null;

	}


}
