using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KERBALISM
{
	public class PartData
	{
		private static Dictionary<int, PartData> loadedPartDatas = new Dictionary<int, PartData>();
		private static Dictionary<uint, PartData> flightPartDatas = new Dictionary<uint, PartData>();

		public static void ClearOnLoad()
		{
			loadedPartDatas.Clear();
			flightPartDatas.Clear();
		}

		public static bool TryGetPartData(uint flightId, out PartData partData)
		{
			return flightPartDatas.TryGetValue(flightId, out partData);
		}

		public static bool TryGetLoadedPartData(Part part, out PartData partData)
		{
			return loadedPartDatas.TryGetValue(part.GetInstanceID(), out partData);
		}

		public VesselDataBase vesselData;
		public uint flightId;
		private AvailablePart partInfo;
		public Part PartPrefab { get; private set; }

		public bool Started { get; private set; } = false;

		// the loaded part reference is acquired either :
		// - from the "loaded" ctor when a saved vessel/ship is instantiated as a loaded ship, and when a part is created in flight (ex : KIS)
		// - from the "loaded" ctor, through the Part.Start() harmony prefix when a part is created in the editors
		// - from SetLoadedPartReference(), through the Part.Start() harmony prefix when an existing unloaded part is loaded
		public Part LoadedPart => IsLoaded ? loadedPart : null;
		private Part loadedPart;

		// the protopart reference is acquired either :
		// - directly when loading a saved vessel
		// - with an harmony postfix on the ProtoPartSnapshot(Part PartRef, ProtoVessel protoVessel, bool preCreate) ctor when a loaded vessel is being unloaded
		public ProtoPartSnapshot ProtoPart => IsLoaded ? null : protoPart;
		private ProtoPartSnapshot protoPart;

		public bool IsLoaded { get; private set; }

		public PartRadiationData radiationData;
		public PartVolumeAndSurface.Definition volumeAndSurface;
		public PartResourceCollection resources;
		public List<ModuleHandler> modules = new List<ModuleHandler>();

		/// <summary> Localized part title </summary>
		public string Title => partInfo.title;

		/// <summary> part internal name as defined in configs </summary>
		public string Name => partInfo.name;

		public override string ToString() => Name;

		public PartData(VesselDataBase vesselData, Part part)
		{
			this.vesselData = vesselData;

			IsLoaded = true;
			flightId = part.flightID;
			partInfo = part.partInfo;
			PartPrefab = GetPartPrefab(part.partInfo);
			loadedPart = part;
			resources = new PartResourceCollection(this);
			volumeAndSurface = PartVolumeAndSurface.GetDefinition(PartPrefab);
			radiationData = new PartRadiationData(this);
			loadedPartDatas[part.GetInstanceID()] = this;

			if (flightId != 0)
				flightPartDatas[flightId] = this;
		}

		public PartData(VesselDataBase vesselData, ProtoPartSnapshot protopart)
		{
			this.vesselData = vesselData;

			IsLoaded = false;
			flightId = protopart.flightID;
			partInfo = protopart.partInfo;
			PartPrefab = GetPartPrefab(protopart.partInfo);
			this.protoPart = protopart;
			resources = new PartResourceCollection(this);
			volumeAndSurface = PartVolumeAndSurface.GetDefinition(PartPrefab);
			radiationData = new PartRadiationData(this);

			if (flightId != 0)
				flightPartDatas[flightId] = this;
		}

		/// <summary>
		/// Called by the Part.Start() patch. Set the part and partmodule references, instantiate loaded context handlers.
		/// Called when a previously unloaded vessel becomes loaded (this isn't used for the initially loaded vessel on scene start)
		/// </summary>
		public void OnAfterLoad(Part part)
		{
			IsLoaded = true;
			loadedPart = part;
			protoPart = null;
			loadedPartDatas[part.GetInstanceID()] = this;

			for (int i = modules.Count - 1; i >= 0; i--)
			{
				ModuleHandler moduleHandler = modules[i];

				// Remove handlers that don't have the loaded context
				if ((moduleHandler.Activation & ModuleHandler.ActivationContext.Loaded) == 0)
				{
					modules.RemoveAt(i);
					continue;
				}

				// acquire the loaded module reference
				PartModule module = loadedPart.Modules[PartPrefab.Modules.IndexOf(moduleHandler.PrefabModuleBase)];
				ModuleHandler.loadedHandlersByModuleInstanceId[module.GetInstanceID()] = moduleHandler;
				moduleHandler.SetModuleReferences(moduleHandler.PrefabModuleBase, module);

				if (module is KsmPartModule ksmModule)
					ksmModule.ModuleHandler = moduleHandler;

				moduleHandler.OnBecomingLoaded();
			}

			for (var i = 0; i < loadedPart.Modules.Count; i++)
			{
				// if the module is type we haven't a handler for, continue
				if (!ModuleHandler.TryGetModuleHandlerType(loadedPart.Modules[i].moduleName, out ModuleHandler.ModuleHandlerType handlerType))
					continue;

				// instantiate handlers that don't have the unloaded context and have the loaded context
				if ((handlerType.activation & ModuleHandler.ActivationContext.Unloaded) == 0 && (handlerType.activation & ModuleHandler.ActivationContext.Loaded) != 0)
				{
					ModuleHandler.NewLoaded(handlerType, loadedPart.Modules[i], i, this, false);
				}

				// TODO : KsmStart() is supposed to happen after B9PS subtype switching.
				// We are calling this from the Part.Start() prefix, so it won't be the case...
				if (handlerType.isKsmModule)
				{
					KsmPartModule ksmModule = (KsmPartModule)loadedPart.Modules[i];
					ksmModule.KsmStart();
					ksmModule.SetupActions();
				}
			}
		}

		/// <summary>
		/// Called when loadedPart.OnDestroy() is called by unity. Handled through GameEvents.onPartDestroyed.
		/// Responsible for cleaning up all references to the loaded Part and PartModules
		/// </summary>
		public void OnLoadedPartDestroyed()
		{
			for (int i = modules.Count - 1; i >= 0; i--)
			{
				if (modules[i].LoadedModuleBase != null)
				{
					int instanceID = modules[i].LoadedModuleBase.GetInstanceID();
					ModuleHandler.loadedHandlersByModuleInstanceId.Remove(instanceID);
					ModuleHandler.handlerFlightIdsByModuleInstanceId.Remove(instanceID);
					ModuleHandler.handlerShipIdsByModuleInstanceId.Remove(instanceID);
				}
				else if (!ReferenceEquals(modules[i].LoadedModuleBase, null))
				{
					Lib.Log($"Can't clean up loaded module dictionaries, a module exists but is already destroyed !!!", Lib.LogLevel.Error);
				}
			}

			IsLoaded = false;
			loadedPartDatas.Remove(loadedPart.GetInstanceID());
			loadedPart = null;
		}

		/// <summary>
		/// Called by the Vessel.Unload() prefix for every part. Notify modules of the incoming state change, and remove loaded-only module handlers.
		/// </summary>
		public void OnBeforeUnload()
		{
			for (int i = modules.Count - 1; i >= 0; i--)
			{
				modules[i].OnBecomingUnloaded();

				// If the handler doesn't have the unloaded activation context, remove it
				// For non-persistent handlers we have no way to find the corresponding protomodule if the vessel goes from loaded to unloaded.
				// So also remove any non-persistent handlers. In case the handler has both the loaded and unloaded context, it will be recreated
				// in the SetProtopartReferenceOnVesselUnload() call.
				if ((modules[i].Activation & ModuleHandler.ActivationContext.Unloaded) == 0 || !(modules[i] is IPersistentModuleHandler))
				{
					modules.RemoveAt(i);
				}
			}
		}

		/// <summary>
		/// Called by the Vessel.Unload() postfix for every part. Set the protopart reference and the protomodule reference, and instantiate unloaded-context handlers
		/// </summary>
		public void OnAfterUnload(ProtoPartSnapshot protoPart)
		{
			this.protoPart = protoPart;
			IsLoaded = false;

			foreach (ProtoPartModuleSnapshot protoModule in protoPart.modules)
			{
				if (!ModuleHandler.handlerTypesByModuleName.TryGetValue(protoModule.moduleName, out ModuleHandler.ModuleHandlerType handlerType))
					continue;

				if (handlerType.isPersistent)
				{
					int flightId = Lib.Proto.GetInt(protoModule, ModuleHandler.VALUENAME_FLIGHTID, 0);
					if (flightId != 0 && ModuleHandler.TryGetPersistentFlightHandler(flightId, out ModuleHandler moduleData))
					{
						moduleData.protoModule = protoModule;
						continue;
					}
				}
				else if ((handlerType.activation & ModuleHandler.ActivationContext.Unloaded) != 0)
				{
					ModuleHandler unloadedHandler = ModuleHandler.NewFlightFromProto(handlerType, protoPart, protoModule, this);
					unloadedHandler.FirstSetup();
					unloadedHandler.Start();
				}
			}
		}

		public void OnPartWasTransferred(VesselDataBase previousVessel)
		{
			foreach (ModuleHandler moduleHandler in modules)
			{
				moduleHandler.OnPartWasTransferred(previousVessel);
			}
		}

		public void Start()
		{
			if (Started)
				return;

			Started = true;

			radiationData.PostInstantiateSetup(); // TODO : this shouldn't be here

			foreach (ModuleHandler handler in modules)
			{
				handler.Start();
			}
		}

		/// <summary> Must be called if the part is destroyed </summary>
		public void PartWillDie()
		{
			flightPartDatas.Remove(flightId);

			foreach (ModuleHandler moduleData in modules)
			{
				moduleData.FlightPartWillDie();
			}

			if (LoadedPart != null)
			{
				loadedPartDatas.Remove(LoadedPart.GetInstanceID());
				loadedPart = null;
			}
		}

		// The kerbalEVA part variants (vintage/future) prefabs are created in some weird way
		// causing the PartModules from the base KerbalEVA definition to not exist on them, depending
		// on what DLCs are installed (!). The issue with that is that we rely on the prefab modules
		// for ModuleData instantiation, so in those specific cases we return the base kerbalEVA prefab
		private Part GetPartPrefab(AvailablePart partInfo)
		{
			switch (partInfo.name)
			{
				case "kerbalEVAVintage":
				case "kerbalEVAFuture":
					return PartLoader.getPartInfoByName("kerbalEVA").partPrefab;
				case "kerbalEVAfemaleVintage":
				case "kerbalEVAfemaleFuture":
					return PartLoader.getPartInfoByName("kerbalEVAfemale").partPrefab;
				default:
					return partInfo.partPrefab;
			}
		}
	}
}
