using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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
			public readonly bool isKsmModule;
			public readonly ActivationContext activation;

			public readonly Func<ModuleHandler> activator;

			public ModuleHandlerType(Type type)
			{
				ConstructorInfo ctor = type.GetConstructor(Type.EmptyTypes);
				NewExpression newExp = Expression.New(ctor);
				LambdaExpression lambda = Expression.Lambda(typeof(Func<ModuleHandler>), newExp);
				activator = (Func<ModuleHandler>)lambda.Compile();

				ModuleHandler dummyInstance = activator.Invoke();

				handlerTypeName = type.Name;
				moduleTypeNames = dummyInstance.ModuleTypeNames;
				isPersistent = dummyInstance is IPersistentModuleHandler;
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

		#region Static : persistence 

		public const string VALUENAME_FLIGHTID = "ksmFlightId";
		public const string VALUENAME_SHIPID = "ksmShipId";

		/// <summary> dictionary of all IPersistentModuleHandlers game-wide, by flightId</summary>
		private static Dictionary<int, IPersistentModuleHandler> persistentFlightModuleHandlers = new Dictionary<int, IPersistentModuleHandler>();

		public static void SavePersistentHandlers(PartDataCollectionBase partDatas, ConfigNode vesselDataNode)
		{
			ConfigNode topNode = vesselDataNode.AddNode(VesselDataBase.NODENAME_MODULE);

			foreach (PartData partData in partDatas)
			{
				foreach (ModuleHandler moduleHandler in partData.modules)
				{
					if (!(moduleHandler is IPersistentModuleHandler persistentHandler))
						continue;

					// note : the customized node name is for save readibility, but it also has a functional use
					// in the EditorCrewChanged GameEvent handling (see Events > Habitat)
					ConfigNode handlerNode = topNode.AddNode(Lib.BuildString(partData.Name, "@", persistentHandler.GetType().Name));

					if (persistentHandler.FlightId != 0)
					{
						handlerNode.AddValue(VALUENAME_FLIGHTID, persistentHandler.FlightId);
					}
					else if (persistentHandler.ShipId != 0)
					{
						handlerNode.AddValue(VALUENAME_SHIPID, persistentHandler.ShipId);
					}
					else
					{
						Lib.Log($"Can't save ModuleHandler, both flightId and shipId aren't defined !", Lib.LogLevel.Warning);
						continue;
					}

					handlerNode.AddValue(nameof(handlerIsEnabled), moduleHandler.handlerIsEnabled);
					persistentHandler.Save(handlerNode);
				}
			}
		}

		#endregion

		#region Static : loaded modules dictionaries

		public static Dictionary<int, ModuleHandler> loadedHandlersByModuleInstanceId = new Dictionary<int, ModuleHandler>();
		public static Dictionary<int, int> handlerShipIdsByModuleInstanceId = new Dictionary<int, int>();
		public static Dictionary<int, int> handlerFlightIdsByModuleInstanceId = new Dictionary<int, int>();

		#endregion

		#region Static : activators and lifecycle

		public static bool ExistsInFlight(int flightId) => persistentFlightModuleHandlers.ContainsKey(flightId);

		public static bool TryGetPersistentFlightHandler<TModule, TData, TDefinition>(int flightId, out TData handler)
			where TModule : KsmPartModule<TModule, TData, TDefinition>
			where TData : KsmModuleHandler<TModule, TData, TDefinition>
			where TDefinition : KsmModuleDefinition
		{
			if (persistentFlightModuleHandlers.TryGetValue(flightId, out IPersistentModuleHandler persistentHandler))
			{
				handler = (TData)persistentHandler;
				return true;
			}

			handler = null;
			return false;
		}

		public static bool TryGetPersistentFlightHandler(int flightId, out ModuleHandler modulehandler)
		{
			if (persistentFlightModuleHandlers.TryGetValue(flightId, out IPersistentModuleHandler persistentHandler))
			{
				modulehandler = (ModuleHandler)persistentHandler;
				return true;
			}
			modulehandler = null;
			return false;
		}

		public static bool TryGetPersistentFlightHandler<TModule, TData, TDefinition>(ProtoPartModuleSnapshot protoModule, out TData moduleHandler)
			where TModule : KsmPartModule<TModule, TData, TDefinition>
			where TData : KsmModuleHandler<TModule, TData, TDefinition>
			where TDefinition : KsmModuleDefinition
		{
			int flightId = Lib.Proto.GetInt(protoModule, VALUENAME_FLIGHTID, 0);

			if (persistentFlightModuleHandlers.TryGetValue(flightId, out IPersistentModuleHandler persistentHandler))
			{
				moduleHandler = (TData)persistentHandler;
				return true;
			}

			moduleHandler = null;
			return false;
		}

		/// <summary> must be called in OnLoad(), before any VesselData are loaded</summary>
		public static void ClearOnLoad()
		{
			persistentFlightModuleHandlers.Clear();
			handlerFlightIdsByModuleInstanceId.Clear();
			handlerShipIdsByModuleInstanceId.Clear();
			loadedHandlersByModuleInstanceId.Clear();
		}

		// this called by the Part.Start() prefix patch. It's a "catch all" method for all the situations were a
		// part is instantiated in flight. It will ensure that the PartData and ModuleData exists and that the
		// module <> data cross references are set. Common cases :
		// - all loaded vessels after a scene load
		// - previously unloaded vessel entering physics range
		// - KIS created parts


		public static void GetPersistedOrNewLoadedFlightHandler(PartData partData, PartModule partModule, int moduleIndex)
		{
			int moduleInstanceId = partModule.GetInstanceID();
			if (handlerFlightIdsByModuleInstanceId.TryGetValue(moduleInstanceId, out int flightId))
			{
				if (persistentFlightModuleHandlers.TryGetValue(flightId, out IPersistentModuleHandler persistentHandler))
				{
					if (partModule is KsmPartModule ksmPartModule)
					{
						ksmPartModule.ModuleHandler = persistentHandler.ModuleHandler;
					}
					persistentHandler.ModuleHandler.SetModuleReferences(partData.PartPrefab.Modules[moduleIndex], partModule);
				}
			}
			else
			{
				NewEditorLoaded(partModule, moduleIndex, partData, ActivationContext.Loaded, true);
			}
		}

		private static int NewFlightId(IPersistentModuleHandler moduleHandler)
		{
			int flightId = 0;
			do
			{
				flightId = Guid.NewGuid().GetHashCode();
			}
			while (persistentFlightModuleHandlers.ContainsKey(flightId) || flightId == 0);

			persistentFlightModuleHandlers.Add(flightId, moduleHandler);

			return flightId;
		}

		public static void AssignNewFlightId(IPersistentModuleHandler moduleHandler)
		{
			int flightId = NewFlightId(moduleHandler);
			moduleHandler.FlightId = flightId;
		}

		// TODO : maybe instantiate a PartData for the prefab ?
		public static void NewForPrefab(KsmPartModule prefabModule)
		{
			KsmModuleHandler moduleHandler = (KsmModuleHandler)handlerTypesByModuleName[prefabModule.moduleName].activator.Invoke();
			moduleHandler.Definition = KsmModuleDefinitionLibrary.GetDefinition(prefabModule, null);
			prefabModule.ModuleHandler = moduleHandler;
			moduleHandler.SetModuleReferences(prefabModule, prefabModule);
			moduleHandler.OnPrefabCompilation();
		}

		public static void NewEditorLoaded(PartModule module, int moduleIndex, PartData partData, ActivationContext context, bool affectFlightId)
		{
			if (!handlerTypesByModuleName.TryGetValue(module.moduleName, out ModuleHandlerType handlerType))
				return;

			if ((handlerType.activation & context) == 0)
				return;

			// This handle a bunch of in-editor case where we try to re-instatiate an already instantiated handler
			// TODO : what about non-ksm handlers ? In the current state of things, we will likely instantiate duplicates...
			if (module is KsmPartModule ksmModule && ksmModule.ModuleHandler != null)
				return;

			NewLoaded(handlerType, module, moduleIndex, partData, affectFlightId);
		}

		public static void NewLoaded(ModuleHandlerType handlerType, PartModule module, int moduleIndex, PartData partData, bool affectFlightId)
		{
			ModuleHandler moduleHandler = handlerType.activator.Invoke();

			loadedHandlersByModuleInstanceId[module.GetInstanceID()] = moduleHandler;

			if (affectFlightId && moduleHandler is IPersistentModuleHandler persistentHandler)
			{
				int flightId = NewFlightId(persistentHandler);
				persistentHandler.FlightId = flightId;
			}

			moduleHandler.partData = partData;
			moduleHandler.SetModuleReferences(partData.PartPrefab.Modules[moduleIndex], module);
			partData.modules.Add(moduleHandler);

			Lib.LogDebug($"Instantiated new {moduleHandler} for part {partData.Title} on {partData.vesselData} - affectFlightId={affectFlightId}");
		}

		public static void NewLoadedFromNode(PartModule module, int moduleIndex, PartData partData, ConfigNode handlerNode, ActivationContext context)
		{
			if (!handlerTypesByModuleName.TryGetValue(module.moduleName, out ModuleHandlerType handlerType))
				return;

			if ((handlerType.activation & context) == 0)
				return;

			NewLoadedFromNode(handlerType, module, moduleIndex, partData, handlerNode);
		}

		public static void NewLoadedFromNode(ModuleHandlerType handlerType, PartModule module, int moduleIndex, PartData partData, ConfigNode handlerNode)
		{
			ModuleHandler moduleHandler = handlerType.activator.Invoke();
			moduleHandler.setupDone = true;

			loadedHandlersByModuleInstanceId[module.GetInstanceID()] = moduleHandler;

			moduleHandler.partData = partData;
			
			moduleHandler.SetModuleReferences(partData.PartPrefab.Modules[moduleIndex], module);
			partData.modules.Add(moduleHandler);

			moduleHandler.handlerIsEnabled = Lib.ConfigValue(handlerNode, nameof(handlerIsEnabled), true);

			IPersistentModuleHandler persistentHandler = (IPersistentModuleHandler)moduleHandler;
			persistentHandler.Load(handlerNode);
			

			// handlerFlightIdsByModuleInstanceId is populated in the PartModule.Load() PostFix harmony patch
			if (handlerFlightIdsByModuleInstanceId.TryGetValue(module.GetInstanceID(), out int flightId))
			{
				persistentHandler.FlightId = flightId;
				persistentFlightModuleHandlers.Add(flightId, persistentHandler);
			}

			Lib.LogDebug($"Instantiated persisted {moduleHandler} for {partData} on {partData.vesselData}");
		}

		public static ModuleHandler NewFlightFromProto(ModuleHandlerType handlerType, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, PartData partData)
		{
			// TODO : optimisation, search the part once and find the prefab in the caller (PartDataCollectionVessel ctor) instead of redoing it for each module
			if (!Lib.TryFindModulePrefab(protoPart, protoModule, out PartModule modulePrefab))
				return null;

			ModuleHandler moduleHandler = handlerType.activator.Invoke();

			if (moduleHandler is IPersistentModuleHandler persistentHandler)
			{
				int flightId = NewFlightId(persistentHandler);
				persistentHandler.FlightId = flightId;
				Lib.Proto.Set(protoModule, VALUENAME_FLIGHTID, flightId);
			}
			else
			{
				moduleHandler.handlerIsEnabled = Lib.Proto.GetBool(protoModule, nameof(PartModule.isEnabled), true);
			}

			moduleHandler.partData = partData;
			moduleHandler.SetModuleReferences(modulePrefab, null);
			moduleHandler.protoModule = protoModule;
			partData.modules.Add(moduleHandler);

			Lib.LogDebug($"Instantiated new {moduleHandler} for {partData} on {partData.vesselData}");

			return moduleHandler;
		}

		public static void NewPersistedFlightFromProto(ModuleHandlerType handlerType, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, PartData partData, ConfigNode handlerNode, int flightId)
		{
			// TODO : optimisation, search the part once and find the prefab in the caller (PartDataCollectionVessel ctor) instead of redoing it for each module
			if (!Lib.TryFindModulePrefab(protoPart, protoModule, out PartModule modulePrefab))
				return;

			ModuleHandler moduleHandler = handlerType.activator.Invoke();
			moduleHandler.setupDone = true;
			IPersistentModuleHandler persistentHandler = (IPersistentModuleHandler)moduleHandler;

			persistentFlightModuleHandlers.Add(flightId, persistentHandler);
			persistentHandler.FlightId = flightId;
			moduleHandler.partData = partData;
			moduleHandler.SetModuleReferences(modulePrefab, null);
			moduleHandler.protoModule = protoModule;
			partData.modules.Add(moduleHandler);

			moduleHandler.handlerIsEnabled = Lib.ConfigValue(handlerNode, nameof(handlerIsEnabled), true);
			persistentHandler.Load(handlerNode);

			Lib.LogDebug($"Instantiated persisted {moduleHandler} for {partData} on {partData.vesselData}");
		}

		#endregion

		#region Abstract implementation : internals

		protected bool setupDone = false;
		protected bool started = false;

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
		public abstract PartModule LoadedModuleBase { get; }

		/// <summary>
		/// Non-generic, untyped reference to the module prefab. You usually want to use the generic typed modulePrefab field instead.
		/// </summary>
		public abstract PartModule PrefabModuleBase { get; }

		/// <summary>
		/// Name of the type of the PartModule the handler will be attached to. Implemented by default in KsmModuleHandler and
		/// TypedModuleHandler. Need to be overriden on a per-type basis for ForeignModuleHandler and APIModuleHandler.
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

		public abstract void SetModuleReferences(PartModule prefabModule, PartModule loadedModule);

		public void VesselDataUpdate()
		{
			if (!handlerIsEnabled)
				return;

			OnVesselDataUpdate();
		}

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

			setupDone = true;
			OnFirstSetup();
		}

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
		/// - On loaded parts, there is no garantee the other parts / other modules will be initialized when this is called
		/// </summary>
		public virtual void OnStart() { }

		public void FlightPartWillDie()
		{
			OnFlightPartWillDie();

			if (this is IPersistentModuleHandler persistentHandler)
			{
				persistentFlightModuleHandlers.Remove(persistentHandler.FlightId);
			}
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
		public virtual void OnFixedUpdate(double elapsedSec) { }

		/// <summary>
		/// Override this to implement "for every module" type code whose result will be further processed by VesselData. <br/>
		/// Typically you will implement said processing in VesselDataBase.ModuleDataUpdate(); <br/>
		/// This is called at every VesselData full update : <br/>
		/// - On loaded vessels : at a variable interval (less frequently than FixedUpdate)<br/>
		/// - On unloaded vessels : every VesselData update (less frequently than FixedUpdate)<br/>
		/// - In the editor : on every planner simulation step <br/>
		/// </summary>
		/// <param name="elapsedSec">elapsed game time since last update</param>
		public virtual void OnVesselDataUpdate() { }

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
