namespace KERBALISM
{
	public abstract class KsmModuleHandler : ModuleHandler, IPersistentModuleHandler
	{
		private const string VALUENAME_DEFINITION_ID = "definitionId";

		public ModuleHandler ModuleHandler => this;
		public bool ConfigLoaded { get; set; }

		protected string definitionId;

		public void Load(ConfigNode node)
		{
			definitionId = Lib.ConfigValue(node, VALUENAME_DEFINITION_ID, string.Empty);
			if (!string.IsNullOrEmpty(definitionId) && KsmModuleDefinitionLibrary.TryGetDefinition(definitionId, out KsmModuleDefinition definition))
			{
				Definition = definition;
				OnLoad(node);
			}
		}

		public void Save(ConfigNode node)
		{
			if (Definition == null)
			{
				// Definition will be null until the module has been started.
				// When launching a new ship from the editor, just after loading it, KSP will immediately save it to create the "revert to VAB/SPH" ship
				// Since handlers can't be started at this point, we fallback to using the persisted definitionId value, which has just been loaded.
				// In the worst case, if the definitionId isn't found, we will fallback to the default definition when the handler is started.
				if (string.IsNullOrEmpty(definitionId))
				{
					Lib.Log($"Can't save empty definitionId for {this} on {partData} in {VesselData}", Lib.LogLevel.Warning);
				}
				else
				{
					node.AddValue(VALUENAME_DEFINITION_ID, definitionId);
				}
			}
			else
			{
				node.AddValue(VALUENAME_DEFINITION_ID, Definition.DefinitionId);
			}


			OnSave(node);
		}

		public abstract KsmModuleDefinition Definition { get; set; }

		public virtual void OnLoad(ConfigNode node) { }

		public virtual void OnSave(ConfigNode node) { }

		public override ActivationContext Activation => ActivationContext.Editor | ActivationContext.Loaded | ActivationContext.Unloaded;

		/// <summary>
		/// Run at the module prefab compilation, just before the module OnLoad().
		/// Note that other modules of greater index won't be loaded / initialized when this is called.
		/// </summary>
		public virtual void OnPrefabCompilation() { }

	}


	public abstract class KsmModuleHandler<TModule, THandler, TDefinition> : KsmModuleHandler
		where TModule : KsmPartModule<TModule, THandler, TDefinition>
		where THandler : KsmModuleHandler<TModule, THandler, TDefinition>
		where TDefinition : KsmModuleDefinition
	{
		public TDefinition definition;

		public TModule loadedModule;

		public override PartModule LoadedModuleBase
		{
			get => loadedModule;
			set => loadedModule = (TModule)value;
		}

		public TModule modulePrefab;

		public override PartModule PrefabModuleBase
		{
			get => modulePrefab;
			set => modulePrefab = (TModule)value;
		}

		public override string[] ModuleTypeNames { get; } = new string[] {typeof(TModule).Name};

		public override KsmModuleDefinition Definition
		{
			get => definition;
			set => definition = (TDefinition)value;
		}

		public override void FirstSetup()
		{
			if (setupDone)
			{
				Lib.LogDebug($"Skipping setup for {this} on {partData} in {VesselData}");
				return;
			}

			Lib.LogDebug($"Setup for {this} on {partData} in {VesselData}");
			if (!ReferenceEquals(loadedModule, null))
			{
				loadedModule.ModuleHandler = this;
				definition = (TDefinition)KsmModuleDefinitionLibrary.GetDefinition(loadedModule, definitionId);
				handlerIsEnabled = LoadedModuleBase.isEnabled;
			}
			else
			{
				definition = (TDefinition)KsmModuleDefinitionLibrary.GetDefinition(modulePrefab, definitionId);
				handlerIsEnabled = Lib.Proto.GetBool(protoModule, "isEnabled", true);
			}

			OnFirstSetup();
			setupDone = true;
		}

		public override void Start()
		{
			if (started)
			{
				Lib.LogDebug($"Skipping start for already started {this} on {partData} in {VesselData}");
				return;
			}

			if (definition == null)
			{
				if (!ReferenceEquals(loadedModule, null))
				{
					loadedModule.ModuleHandler = this;
					definition = (TDefinition)KsmModuleDefinitionLibrary.GetDefinition(loadedModule, definitionId);
				}
				else
				{
					definition = (TDefinition)KsmModuleDefinitionLibrary.GetDefinition(modulePrefab, definitionId);
				}
			}

			if (handlerIsEnabled)
			{
				Lib.LogDebug($"Starting {this} on {partData} in {VesselData}");
				started = true;

				OnStart();

				if (!ReferenceEquals(loadedModule, null))
				{
					loadedModule.KsmStart();
					//loadedModule.SetupActions();
				}
			}
			else
			{
				Lib.LogDebug($"Skipping start for disabled {this} on {partData} in {VesselData}");
			}
		}

		public override UIContext UIActivation => UIContext.EditorPartTooltip | UIContext.EditorVesselUI | UIContext.FlightVesselUI;

		public override string ModuleTitle => definition.ModuleTitle;

		public override string ModuleDescription => definition.ModuleDescription(modulePrefab);
	}
}
