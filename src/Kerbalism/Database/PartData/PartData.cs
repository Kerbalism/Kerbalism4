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
		public Part LoadedPart { get; private set; }
		private bool initialized = false;

		public PartRadiationData radiationData;
		public PartVolumeAndSurface.Definition volumeAndSurface;
		public PartResourceDataCollection virtualResources;
		public List<ModuleData> modules = new List<ModuleData>();

		/// <summary> Localized part title </summary>
		public string Title => partInfo.title;

		/// <summary> part internal name as defined in configs </summary>
		public string Name => partInfo.name;

		public override string ToString() => Title;

		public PartData(VesselDataBase vesselData, Part part)
		{
			this.vesselData = vesselData;
			
			flightId = part.flightID;
			partInfo = part.partInfo;
			PartPrefab = GetPartPrefab(part.partInfo);
			LoadedPart = part;
			virtualResources = new PartResourceDataCollection();
			volumeAndSurface = PartVolumeAndSurface.GetDefinition(PartPrefab);
			radiationData = new PartRadiationData(this);
			loadedPartDatas[part.GetInstanceID()] = this;

			if (flightId != 0)
				flightPartDatas.Add(flightId, this);
		}

		public PartData(VesselDataBase vesselData, ProtoPartSnapshot protopart)
		{
			this.vesselData = vesselData;
			flightId = protopart.flightID;
			partInfo = protopart.partInfo;
			PartPrefab = GetPartPrefab(protopart.partInfo);
			virtualResources = new PartResourceDataCollection();
			volumeAndSurface = PartVolumeAndSurface.GetDefinition(PartPrefab);
			radiationData = new PartRadiationData(this);

			if (flightId != 0)
				flightPartDatas.Add(flightId, this);
		}

		public void SetLoadedPartReference(Part part)
		{
			LoadedPart = part;
			loadedPartDatas[part.GetInstanceID()] = this;
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
				LoadedPart = null;
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
