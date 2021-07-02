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
		private bool initialized = false;

		public PartRadiationData radiationData;
		public PartVolumeAndSurface.Definition volumeAndSurface;
		public PartResourceCollection resources;
		public PartVirtualResourceCollection virtualResources;
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
			virtualResources = new PartVirtualResourceCollection(this);
			volumeAndSurface = PartVolumeAndSurface.GetDefinition(PartPrefab);
			radiationData = new PartRadiationData(this);
			loadedPartDatas[part.GetInstanceID()] = this;

			if (flightId != 0)
				flightPartDatas.Add(flightId, this);
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
			virtualResources = new PartVirtualResourceCollection(this);
			volumeAndSurface = PartVolumeAndSurface.GetDefinition(PartPrefab);
			radiationData = new PartRadiationData(this);

			if (flightId != 0)
				flightPartDatas.Add(flightId, this);
		}

		public void SetPartReference(Part part)
		{
			IsLoaded = true;
			loadedPart = part;
			loadedPartDatas[part.GetInstanceID()] = this;
		}

		// TODO : Vessel.Load() patch !!!

		/// <summary>
		/// Called by the Vessel.Unload() patch for every part. Set the protopart reference and the protomodule reference, and instantiate unloaded-context handlers
		/// </summary>
		public void SetProtopartReferenceOnVesselUnload(ProtoPartSnapshot protoPart)
		{
			this.protoPart = protoPart;

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

				if ((handlerType.activation & ModuleHandler.ActivationContext.Unloaded) != 0)
				{
					ModuleHandler.NewFlightFromProto(handlerType, protoPart, protoModule, this);
				}
			}
		}

		/// <summary>
		/// Must be called when loadedPart.OnDestroy() is called by unity. Handled through GameEvents.onPartDestroyed.
		/// Note that in the case of a vessel being unloaded, this is called before SetProtopartReferenceOnVesselUnload()
		/// </summary>
		public void OnLoadedDestroy()
		{
			IsLoaded = false;
			loadedPartDatas.Remove(loadedPart.GetInstanceID());

			foreach (PartModule module in loadedPart.Modules)
			{
				int instanceID = module.GetInstanceID();
				ModuleHandler.loadedHandlersByModuleInstanceId.Remove(instanceID);
				ModuleHandler.handlerFlightIdsByModuleInstanceId.Remove(instanceID);
				ModuleHandler.handlerShipIdsByModuleInstanceId.Remove(instanceID);
			}

			loadedPart = null;

			for (int i = 0; i < modules.Count; i++)
			{
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

		//public void PostInstantiateSetup()
		//{
		//	if (initialized)
		//		return;

		//	radiationData.PostInstantiateSetup();

		//	initialized = true;
		//}

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
