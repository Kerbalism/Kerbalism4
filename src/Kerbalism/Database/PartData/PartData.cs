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
		public List<ModuleData> modules = new List<ModuleData>();

		/// <summary> Localized part title </summary>
		public string Title => partInfo.title;

		/// <summary> part internal name as defined in configs </summary>
		public string Name => partInfo.name;

		public override string ToString() => Title;

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

		public void SetProtoPartReference(ProtoPartSnapshot protoPart)
		{
			this.protoPart = protoPart;

			foreach (ProtoPartModuleSnapshot protoModule in protoPart.modules)
			{
				if (ModuleData.IsKsmPartModule(protoModule))
				{
					int flightId = Lib.Proto.GetInt(protoModule, KsmPartModule.VALUENAME_FLIGHTID, 0);

					if (flightId != 0 && ModuleData.TryGetModuleData(flightId, out ModuleData moduleData))
					{
						moduleData.protoModule = protoModule;
					}
				}
			}
		}

		/// <summary>
		/// Must be called when loadedPart.OnDestroy() is called by unity. Handled through GameEvents.onPartDestroyed.
		/// </summary>
		public void OnDestroy()
		{
			IsLoaded = false;
			loadedPartDatas.Remove(loadedPart.GetInstanceID());
			loadedPart = null;
		}

		public void PostInstantiateSetup()
		{
			if (initialized)
				return;

			radiationData.PostInstantiateSetup();

			initialized = true;
		}

		/// <summary> Must be called if the part is destroyed </summary>
		public void PartWillDie()
		{
			flightPartDatas.Remove(flightId);

			foreach (ModuleData moduleData in modules)
			{
				moduleData.PartWillDie();
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
