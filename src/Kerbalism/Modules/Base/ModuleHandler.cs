using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Steamworks;
using UnityEngine;

namespace KERBALISM
{
	public static class PartModuleExtensions
	{
		public static T GetModuleHandler<T>(this PartModule module) where T : ModuleHandler
		{
			if (ModuleHandler.loadedHandlersByModuleInstanceId.TryGetValue(module.GetInstanceID(), out ModuleHandler handler) && handler is T typedHandler)
				return typedHandler;

			return null;
		}
	}



	public abstract class ModuleHandler
	{
		#region Static : type library

		[Flags]
		public enum ActivationContext
		{
			Editor = 1 << 0,
			Loaded = 1 << 1,
			Unloaded = 1 << 2
		}

		public class ModuleHandlerType
		{
			public readonly string handlerTypeName;
			public readonly string[] moduleTypeNames;
			public readonly bool isPersistent;
			public readonly bool isActiveCargo;
			public readonly bool isKsmModule;
			public readonly ActivationContext activation;

			private readonly Func<ModuleHandler> activator;

			public ModuleHandler Instantiate()
			{
				ModuleHandler handler = activator.Invoke();
				handler.handlerType = this;
				return handler;
			}
			

			public ModuleHandlerType(Type type)
			{
				ConstructorInfo ctor = type.GetConstructor(Type.EmptyTypes);
				NewExpression newExp = Expression.New(ctor);
				LambdaExpression lambda = Expression.Lambda(typeof(Func<ModuleHandler>), newExp);
				activator = (Func<ModuleHandler>)lambda.Compile();

				ModuleHandler dummyInstance = Instantiate();

				handlerTypeName = type.Name;
				moduleTypeNames = dummyInstance.ModuleTypeNames;
				isPersistent = dummyInstance is IPersistentModuleHandler;
				isActiveCargo = dummyInstance is IActiveStoredHandler;
				activation = dummyInstance.Activation;
				isKsmModule = dummyInstance.GetType().IsSubclassOf(typeof(KsmModuleHandler));
			}

			public override string ToString() => handlerTypeName;

		}

		/// <summary> for every ModuleData derived class name, the constructor delegate </summary>
		private static Dictionary<string, ModuleHandlerType> handlerTypesByName = new Dictionary<string, ModuleHandlerType>();

		/// <summary> for every KsmPartModule derived class name, the corresponding ModuleData constructor delegate </summary>
		public static Dictionary<string, ModuleHandlerType> handlerTypesByModuleName = new Dictionary<string, ModuleHandlerType>();

		/// <summary> for every KsmPartModule derived class name, the corresponding ModuleData constructor delegate </summary>
		public static HashSet<string> persistentHandlersByModuleName = new HashSet<string>();

		public static HashSet<string> activeCargoHandlersByModuleName = new HashSet<string>();

		public static void RegisterPartModuleHandlerTypes()
		{
			Type handlerBaseType = typeof(ModuleHandler);

			foreach (AssemblyLoader.LoadedAssembly loadedAssembly in AssemblyLoader.loadedAssemblies)
			{
				foreach (Type type in loadedAssembly.assembly.GetTypes())
				{
					// if this type is a non abstract ModuleHandler derivative
					if (type.IsClass && !type.IsAbstract && handlerBaseType.IsAssignableFrom(type))
					{
						ModuleHandlerType handlerType = new ModuleHandlerType(type);
						handlerTypesByName.Add(handlerType.handlerTypeName, handlerType);

						foreach (string moduleTypeName in handlerType.moduleTypeNames)
						{
							handlerTypesByModuleName.Add(moduleTypeName, handlerType);
							if (handlerType.isPersistent)
							{
								persistentHandlersByModuleName.Add(moduleTypeName);
							}

							if (handlerType.isActiveCargo)
							{
								activeCargoHandlersByModuleName.Add(moduleTypeName);
							}
						}

					}
				}
			}
		}

		public static bool TryGetModuleHandlerType(string partModuleType, out ModuleHandlerType moduleHandlerType)
		{
			return handlerTypesByModuleName.TryGetValue(partModuleType, out moduleHandlerType);
		}

		#endregion

		#region Static : loaded modules dictionaries

		public static Dictionary<int, ModuleHandler> loadedHandlersByModuleInstanceId = new Dictionary<int, ModuleHandler>();
		public static Dictionary<ProtoPartModuleSnapshot, ModuleHandler> protoHandlersByProtoModule = new Dictionary<ProtoPartModuleSnapshot, ModuleHandler>();

		#endregion

		#region Static : activators and lifecycle

		/// <summary> must be called in OnLoad(), before any VesselData are loaded</summary>
		public static void ClearOnSceneSwitch()
		{
			loadedHandlersByModuleInstanceId.Clear();
			protoHandlersByProtoModule.Clear();
		}

		// this called by the Part.Start() prefix patch. It's a "catch all" method for all the situations were a
		// part is instantiated in flight. It will ensure that the PartData and ModuleData exists and that the
		// module <> data cross references are set. Common cases :
		// - all loaded vessels after a scene load
		// - previously unloaded vessel entering physics range
		// - KIS created parts

		// TODO : maybe instantiate a PartData for the prefab ?
		public static void NewForPrefab(KsmPartModule prefabModule)
		{
			KsmModuleHandler moduleHandler = (KsmModuleHandler)handlerTypesByModuleName[prefabModule.moduleName].Instantiate();
			moduleHandler.Definition = KsmModuleDefinitionLibrary.GetDefinition(prefabModule, null);
			prefabModule.ModuleHandler = moduleHandler;
			moduleHandler.PrefabModuleBase = prefabModule;
			moduleHandler.LoadedModuleBase = prefabModule;
			moduleHandler.OnPrefabCompilation();
		}

		public static ModuleHandler GetForLoadedModule(PartData partData, PartModule module, int moduleIndex, ActivationContext context)
		{
			if (!handlerTypesByModuleName.TryGetValue(module.moduleName, out ModuleHandlerType handlerType))
				return null;

			return GetForLoadedModule(handlerType, partData, module, moduleIndex, context);
		}

		public static ModuleHandler GetForLoadedModule(ModuleHandlerType handlerType, PartData partData, PartModule module, int moduleIndex, ActivationContext context)
		{
			if ((handlerType.activation & context) == 0)
				return null;

			int instanceId = module.GetInstanceID();
			if (!loadedHandlersByModuleInstanceId.TryGetValue(instanceId, out ModuleHandler handler))
			{
				handler = handlerType.Instantiate();
				loadedHandlersByModuleInstanceId[instanceId] = handler;
			}

			handler.partData = partData;
			handler.moduleIndex = moduleIndex;
			handler.PrefabModuleBase = partData.PartPrefab.Modules[moduleIndex];
			handler.LoadedModuleBase = module;
			partData.modules.Add(handler);

			if (handlerType.isKsmModule)
			{
				KsmPartModule ksmModule = (KsmPartModule) module;
				ksmModule.ModuleHandler = handler;
			}

			Lib.LogDebug($"Added {handler} to part {partData.Title} on {partData.vesselData}");
			return handler;
		}

		public static ModuleHandler GetForProtoModule(PartData partData, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, int protoModuleIndex, ActivationContext context)
		{
			if (!handlerTypesByModuleName.TryGetValue(protoModule.moduleName, out ModuleHandlerType handlerType))
				return null;

			return GetForProtoModule(handlerType, partData, protoPart, protoModule, protoModuleIndex, context);
		}

		public static ModuleHandler GetForProtoModule(ModuleHandlerType handlerType, PartData partData, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, int protoModuleIndex, ActivationContext context)
		{
			if ((handlerType.activation & context) == 0)
				return null;

			if (!protoHandlersByProtoModule.TryGetValue(protoModule, out ModuleHandler handler))
			{
				handler = handlerType.Instantiate();
				protoHandlersByProtoModule[protoModule] = handler;
			}

			if (!Lib.TryFindModulePrefab(protoPart, ref protoModuleIndex, out PartModule modulePrefab))
				return null;

			handler.partData = partData;
			handler.moduleIndex = protoModuleIndex;
			handler.PrefabModuleBase = modulePrefab;
			handler.protoModule = protoModule;
			partData.modules.Add(handler);

			Lib.LogDebug($"Added {handler} to part {partData.Title} on {partData.vesselData}");
			return handler;
		}

		internal void ParseEnabled(ProtoPartModuleSnapshot protoModule, ConfigNode moduleNode = null)
		{
			if (moduleNode == null || !moduleNode.TryGetValue(nameof(handlerIsEnabled), ref handlerIsEnabled))
			{
				if (!protoModule.moduleValues.TryGetValue(nameof(PartModule.isEnabled), ref handlerIsEnabled))
				{
					handlerIsEnabled = false;
				}
			}
		}

		internal void ParseEnabled(PartModule module, ConfigNode moduleNode = null)
		{
			if (moduleNode == null || !moduleNode.TryGetValue(nameof(handlerIsEnabled), ref handlerIsEnabled))
			{
				handlerIsEnabled = module.isEnabled;
			}
		}

		#endregion

		#region Abstract implementation : internals


		public ModuleHandlerType handlerType;
		public bool setupDone = false;
		public bool started = false;
		public int moduleIndex = -1;

		/// <summary>
		/// TODO : this currently is used inconsistently (ex : FixedUpdate is called but not VesselDataUpdate).
		/// Also, OnFirstInstantate() isn't called if this is false, which seems like a bad idea (what if this is re-enabled later ?)
		/// </summary>
		public bool handlerIsEnabled;

		/// <summary>
		/// Reference to the partData.
		/// Allow accessing references to the stock part-level objects : Part (loaded and prefab), ProtoPartSnapshot and the PartResource wrappers.
		/// </summary>
		public PartData partData;

		/// <summary>
		/// Is the vessel loaded ? Is this to check if you can safely use the loaded module and protoModule references
		/// Note that the loaded state can change at any time, so in case you're doing loaded-dependant initialization code,
		/// you will need to override the 
		/// </summary>
		public bool IsLoaded => partData.IsLoaded;

		public VesselDataBase VesselData => partData?.vesselData;

		/// <summary>
		/// Reference to the ProtoPartModuleSnapshot for that module. Note that when the part is loaded, that reference may or may not exist
		/// and you can't rely on it for anything. Always check partData.IsLoaded first, unless you are sure the vessel is unloaded.
		/// </summary>
		public ProtoPartModuleSnapshot protoModule;

		/// <summary>
		/// Non-generic, untyped reference to the loaded module. You usually want to use the generic typed loadedModule field instead.
		/// </summary>
		public abstract PartModule LoadedModuleBase { get; set; }

		/// <summary>
		/// Non-generic, untyped reference to the module prefab. You usually want to use the generic typed modulePrefab field instead.
		/// </summary>
		public abstract PartModule PrefabModuleBase { get; set; }

		/// <summary>
		/// Name of the type of the PartModule the handler will be attached to. Implemented by default in KsmModuleHandler and
		/// TypedModuleHandler. Need to be overriden on a per-type basis for ForeignModuleHandler. <br/>
		/// Note 1 : if the type has a derived child classes that you want to target too, they need to be specified explicitely.
		/// Note 2 : this must be constant and accessible right after instantiation, you can't attach any logic to this.
		/// </summary>
		public abstract string[] ModuleTypeNames { get; }

		/// <summary>
		/// Determine in what context (editor, loaded, unloaded) this handler type will be instantiated. This is set to all contexts
		/// by default for KsmModuleHandler derivatives. The value must be read-only and available after instantiation.
		/// Just implement it as a read-only property. <br/>
		/// ex : ActivationContext => ModuleHandler.Context.Loaded | ModuleHandler.Context.Unloaded
		/// </summary>
		public abstract ActivationContext Activation { get; }

		public override string ToString() => $"{GetType().Name} - loaded={LoadedModuleBase != null}";

		public virtual void FirstSetup()
		{
			if (setupDone)
			{
				Lib.LogDebug($"Skipping setup for {this} on {partData} in {VesselData}");
				return;
			}

			Lib.LogDebug($"Setup for {this} on {partData} in {VesselData}");
			if (IsLoaded)
				handlerIsEnabled = LoadedModuleBase.isEnabled;
			else
				handlerIsEnabled = Lib.Proto.GetBool(protoModule, "isEnabled", true);

			OnFirstSetup();
			setupDone = true;
		}

		/// <summary>
		/// For persistent handler, this will be called only once in the part life, the first time the handler is instantiatied
		/// For non-persistent handlers, this will be called every time the handler is instantiated
		/// </summary>
		public virtual void OnFirstSetup() { }

		public virtual void Start()
		{
			if (started)
			{
				Lib.LogDebug($"Skipping start for already started {this} on {partData} in {VesselData}");
				return;
			}

			if (handlerIsEnabled)
			{
				Lib.LogDebug($"Starting {this} on {partData} in {VesselData}");
				started = true;
				OnStart();
			}
			else
			{
				Lib.LogDebug($"Skipping start for disabled {this} on {partData} in {VesselData}");
			}
		}


		/// <summary>
		/// This is called every time the ModuleData is instantiated : <br/>
		/// - After Load/OnLoad and after all the Part/Module references have been set <br/>
		/// - After VesselData instantiation and loading, after the first VesselData evaluation <br/>
		/// - On loaded parts, after the PartModule Awake() but before its OnStart() <br/>
		/// - On loaded parts, there is no garantee the other parts / other modules are initialized when this is called
		/// </summary>
		public virtual void OnStart() { }

		public void FlightPartWillDie()
		{
			OnFlightPartWillDie();
		}

		/// <summary>
		/// Override this to implement unloaded/loaded/editor agnostic update code. This is called :<br/>
		/// - After the core VesselData update <br/>
		/// - On loaded vessels : every FixedUpdate <br/>
		/// - On unloaded vessels : every VesselData update (less frequently than FixedUpdate) <br/>
		/// - (TODO: CURRENTLY NOT IMPLEMENTED) In the editor : on every planner simulation step (?) <br/>
		/// This is intended to entirely replace the PartModule.FixedUpdate() method, ideally the PartModule shouldn't have one.
		/// </summary>
		/// <param name="elapsedSec"></param>
		public virtual void OnUpdate(double elapsedSec) { }

		/// <summary>
		/// Called only once for the life of the part, when the part is about to be definitely removed from the game.
		/// </summary>
		public virtual void OnFlightPartWillDie() { }

		/// <summary>
		/// Called when a previously unloaded handler is becoming loaded. This is always followed by the PartModule.OnStart() call
		/// </summary>
		public virtual void OnBecomingLoaded() { }

		/// <summary>
		/// Called when a previously loaded handler will become unloaded
		/// </summary>
		public virtual void OnBecomingUnloaded() { }

		/// <summary>
		/// Called when the part now belongs to a different vessel.
		/// </summary>
		public virtual void OnPartWasTransferred(VesselDataBase previousVessel) { }

		#endregion

		#region Abstract implementation : UI info

		/// <summary>
		/// The UI title for the module. Keep it as short as possible, and try to map it to a cached field instance when possible.
		/// </summary>
		public virtual string ModuleTitle => PrefabModuleBase is IModuleInfo moduleInfo ? moduleInfo.GetModuleTitle() : PrefabModuleBase.GUIName;

		/// <summary>
		/// Determine the UI visibility of the module. See the InfoContext enum annotations for details.
		/// This value should stay constant for each ModuleHandler type, conditional code won't work.
		/// </summary>
		public virtual UIContext UIActivation => UIContext.None;

		/// <summary>
		/// Full description of the module, should return null if not implemented. <br/>
		/// Default implementation will return the IModuleInfo.GetInfo() for stock/foreign modules. <br/>
		/// On KsmPartModule handlers, by default, this will return the ModuleDefinition.ModuleDescription
		/// value. This also provide a default implementation for the stock IModuleInfo.GetInfo()<br/>
		/// You don't usually need to override this, the implementation should be in ModuleDefinition.ModuleDescription
		/// </summary>
		public virtual string ModuleDescription
		{
			get
			{
				if (IsLoaded)
				{
					if (LoadedModuleBase is IModuleInfo moduleInfo)
					{
						return moduleInfo.GetInfo();
					}
				}
				else
				{
					if (PrefabModuleBase is IModuleInfo moduleInfo)
					{
						return moduleInfo.GetInfo();
					}
				}

				return null;
			}
		}

		/// <summary>
		/// Override this to show real-time information about the module state.
		/// Can be multiline, will be in the editor/flight vessel control UI tooltips, and in the default module info popup
		/// </summary>
		public virtual string ModuleFullState => null;

		/// <summary>
		/// Override this to show real-time information about the module state. 
		/// Must be a short single line (~25 chars max), will be truncated if too long.
		/// Visible in the editor/flight vessel control UI, next to the module title.
		/// </summary>
		public virtual string ModuleShortState => null;

		public virtual Texture2D ModuleIcon => null;

		public List<ModuleAction> Actions { get; private set; } = new List<ModuleAction>(2);

		#endregion
	}

	[Flags]
	public enum UIContext
	{
		/// <summary>
		/// Default value. If set, the module won't get any UI visibility.
		/// </summary>
		None = 0,

		/// <summary>
		/// If set in a KsmModuleHandler implementation, the module will be added in the
		/// modules panel of the editor part list tooltip, showing the string returned by
		/// ModuleHandler.ModuleDescription, if that string isn't null<br/>
		/// </summary>
		EditorPartTooltip = 1 << 0,

		/// <summary>
		/// If set in the ModuleHandler implementation, the module will get 
		/// an entry in the editor vessel control UI
		/// </summary>
		EditorVesselUI = 1 << 1,

		/// <summary>
		/// If set in the ModuleHandler implementation, the module will get 
		/// an entry in the flight vessel control UI
		/// </summary>
		FlightVesselUI = 1 << 2
	}
}
