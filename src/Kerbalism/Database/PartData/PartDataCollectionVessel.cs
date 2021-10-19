﻿using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public class PartDataCollectionVessel : PartDataCollectionBase
	{
		#region FIELDS

		public static Dictionary<uint, PartData> allFlightPartDatas = new Dictionary<uint, PartData>();
		private Dictionary<uint, PartData> partDictionary = new Dictionary<uint, PartData>();
		private List<PartData> partList = new List<PartData>();
		private VesselDataBase vesselData;

		#endregion

		#region CONSTRUCTORS

		public PartDataCollectionVessel(VesselDataBase vesselData, PartDataCollectionShip shipPartData)
		{
			Lib.LogDebug($"Transferring PartDatas from ship to vessel for launch");
			this.vesselData = vesselData;

			foreach (PartData partData in shipPartData)
			{
				partData.flightId = partData.LoadedPart.flightID;
				partData.vesselData = vesselData;
				Add(partData);
			}
		}

		public PartDataCollectionVessel(VesselDataBase vesselData, List<PartData> partDatas)
		{
			Lib.LogDebug($"Transferring {partDatas.Count} PartDatas from vessel {partDatas[0].vesselData.VesselName} to vessel {vesselData.VesselName}");
			this.vesselData = vesselData;

			foreach (PartData partData in partDatas)
			{
				partData.vesselData = vesselData;
				Add(partData);
			}
		}

		/// <summary>
		/// For new vessels created in flight. Part/modules FirstSetup/Start will be done by the VesselData ctor
		/// </summary>
		public PartDataCollectionVessel(VesselDataBase vesselData, Vessel vessel)
		{
			Lib.LogDebug($"Creating partdatas for new loaded vessel {vessel.vesselName}");
			this.vesselData = vesselData;

			foreach (Part part in vessel.parts)
			{
				PartData partData = Add(part);

				for (int i = 0; i < part.Modules.Count; i++)
				{
					ModuleHandler.GetForLoadedModule(partData, part.Modules[i], i, ModuleHandler.ActivationContext.Loaded);
				}
			}
		}

		public PartDataCollectionVessel(VesselDataBase vesselData, ProtoVessel protoVessel, ConfigNode vesselDataNode)
		{
			Lib.LogDebug($"Loading partdatas for existing vessel {protoVessel.vesselName}");
			this.vesselData = vesselData;

			foreach (ProtoPartSnapshot protopart in protoVessel.protoPartSnapshots)
			{
				// In case a part was removed (a part mod uninstalled), the ProtoPartSnapshot will still be created by KSP
				// (even though it will delete the vessel later). Creating the partdata for it will fail hard, so detect that
				// case by checking if the part prefab is null
				if (ReferenceEquals(protopart.partPrefab, null))
				{
					Lib.LogDebug($"Skipping PartData creation for {protopart.partName} on {vesselData.VesselName}\nVessel will be deleted by KSP, as the part doesn't exist anymore due to a configuration change");
					continue;
				}

				PartData partData = Add(protopart);

				for (int i = 0; i < protopart.modules.Count; i++)
				{
					ModuleHandler.GetForProtoModule(partData, protopart, protopart.modules[i], i, ModuleHandler.ActivationContext.Unloaded);
				}
			}
		}

		public PartDataCollectionVessel(VesselDataBase vesselData, Vessel vessel, ConfigNode vesselDataNode)
		{
			Lib.LogDebug($"Loading partdatas for existing vessel {vessel.vesselName}");
			this.vesselData = vesselData;

			foreach (Part part in vessel.parts)
			{
				PartData partData = Add(part);

				for (int i = 0; i < part.Modules.Count; i++)
				{
					ModuleHandler.GetForLoadedModule(partData, part.Modules[i], i, ModuleHandler.ActivationContext.Loaded);
				}
			}
		}

		#endregion

		#region PERSISTENCE

		public override void Save(ConfigNode VesselDataNode)
		{
			ConfigNode partsNode = new ConfigNode(NODENAME_PARTS);
			foreach (PartData partData in partList)
			{
				bool isPersistent = false;
				ConfigNode partNode = new ConfigNode(partData.flightId.ToString());

				isPersistent |= PartRadiationData.Save(partData, partNode);

				if (isPersistent)
					partsNode.AddNode(partNode);
			}

			if (partsNode.CountNodes > 0)
				VesselDataNode.AddNode(partsNode);
		}

		public override void Load(ConfigNode vesselDataNode)
		{
			ConfigNode partsNode = vesselDataNode.GetNode(NODENAME_PARTS);
			if (partsNode == null)
				return;

			foreach (ConfigNode partNode in partsNode.nodes)
			{
				
				if (!partDictionary.TryGetValue(Lib.Parse.ToUInt(partNode.name), out PartData partData))
				{
					Lib.Log($"PartData with flightId {partNode.name} not found, skipping load", Lib.LogLevel.Warning);
					continue;
				}

				PartRadiationData.Load(partData, partNode);
			}
		}

		#endregion

		#region LIST/DICTIONARY IMPLEMENTATION

		public override List<PartData> Parts => partList;

		// base implementation
		public override PartData this[Part part] => partDictionary[part.flightID];
		public override bool Contains(PartData data) => partDictionary.ContainsKey(data.flightId);
		public override bool Contains(Part part) => partDictionary.ContainsKey(part.flightID);
		public override bool TryGet(Part part, out PartData pd) => partDictionary.TryGetValue(part.flightID, out pd);

		// vessel specific implementation
		public PartData this[uint flightId] => partDictionary[flightId];
		public bool Contains(uint flightId) => partDictionary.ContainsKey(flightId);
		public bool TryGet(uint flightId, out PartData pd) => partDictionary.TryGetValue(flightId, out pd);

		public override void Add(PartData partData)
		{
			if (partDictionary.ContainsKey(partData.flightId))
			{
				Lib.LogDebugStack($"PartData with key {partData.flightId} exists already ({partData.Title})", Lib.LogLevel.Warning);
				return;
			}

			allFlightPartDatas[partData.flightId] = partData;
			partDictionary.Add(partData.flightId, partData);
			partList.Add(partData);
		}

		public PartData Add(Part part)
		{
			uint id = part.flightID;

			if (partDictionary.ContainsKey(id))
			{
				Lib.LogDebugStack($"PartData with key {id} exists already ({part.partInfo.title})", Lib.LogLevel.Warning);
				return null;
			}

			PartData pd = new PartData(vesselData, part);
			allFlightPartDatas[id] = pd;
			partDictionary.Add(id, pd);
			partList.Add(pd);
			return pd;
		}

		public PartData Add(ProtoPartSnapshot protoPart)
		{
			if (partDictionary.ContainsKey(protoPart.flightID))
			{
				Lib.LogDebugStack($"PartData with key {protoPart.flightID} exists already ({protoPart.partInfo.title})", Lib.LogLevel.Warning);
				return null;
			}

			PartData pd = new PartData(vesselData, protoPart);
			allFlightPartDatas[protoPart.flightID] = pd;
			partDictionary.Add(protoPart.flightID, pd);
			partList.Add(pd);
			return pd;
		}

		public override bool Remove(PartData partdata)
		{
			if (partDictionary.TryGetValue(partdata.flightId, out PartData partData))
			{
				partDictionary.Remove(partdata.flightId);
				partList.Remove(partData);
			}

			return allFlightPartDatas.Remove(partdata.flightId);
		}

		public void Remove(uint flightID)
		{
			if (partDictionary.TryGetValue(flightID, out PartData partData))
			{
				partDictionary.Remove(flightID);
				partList.Remove(partData);
			}

			allFlightPartDatas.Remove(flightID);
		}

		public void Clear(bool clearFromFlightDictionary)
		{
			if (clearFromFlightDictionary)
			{
				foreach (PartData partData in partList)
				{
					allFlightPartDatas.Remove(partData.flightId);
				}
			}

			partDictionary.Clear();
			partList.Clear();
		}

		#endregion

		#region VESSEL SPECIFIC LIFECYCLE

		public void TransferFrom(PartDataCollectionVessel other)
		{
			foreach (PartData partData in other)
			{
				partData.vesselData = vesselData;
				Add(partData);
				partData.OnPartWasTransferred(other.vesselData);
			}

			other.Clear(false);
		}

		public void OnPartWillDie(Part part)
		{
			if (partDictionary.TryGetValue(part.flightID, out PartData partData))
			{
				partData.PartWillDie();
				partDictionary.Remove(part.flightID);
				partList.Remove(partData);
			}
		}

		public void OnAllPartsWillDie()
		{
			foreach (PartData partData in partList)
			{
				partData.PartWillDie();
			}

			partDictionary.Clear();
			partList.Clear();
		}

		#endregion

	}

	public static class PartDataCollectionVesselExtensions
	{
		public static bool TryGetFlightModuleDataOfType<T>(this Part part, out T moduleData) where T : ModuleHandler
		{
			if (PartDataCollectionVessel.allFlightPartDatas.TryGetValue(part.flightID, out PartData partData))
			{
				for (int i = 0; i < partData.modules.Count; i++)
				{
					moduleData = partData.modules[i] as T;
					if (moduleData != null)
						return true;
				}
			}

			moduleData = null;
			return false;
		}

		public static bool TryGetModuleDataOfType<T>(this ProtoPartSnapshot part, out T moduleData) where T : ModuleHandler
		{
			if (PartDataCollectionVessel.allFlightPartDatas.TryGetValue(part.flightID, out PartData partData))
			{
				for (int i = 0; i < partData.modules.Count; i++)
				{
					moduleData = partData.modules[i] as T;
					if (moduleData != null)
						return true;
				}
			}

			moduleData = null;
			return false;
		}

		public static bool TryGetModuleDataOfType(this ProtoPartSnapshot part, Type type, out ModuleHandler moduleData)
		{
			if (PartDataCollectionVessel.allFlightPartDatas.TryGetValue(part.flightID, out PartData partData))
			{
				for (int i = 0; i < partData.modules.Count; i++)
				{
					if(type == partData.modules[i].GetType())
					{
						moduleData = partData.modules[i];
						return true;
					}
				}
			}

			moduleData = null;
			return false;
		}

		public static IEnumerable<T> GetFlightModuleDatasOfType<T>(this Part part) where T : ModuleHandler
		{
			if (!PartDataCollectionVessel.allFlightPartDatas.TryGetValue(part.flightID, out PartData partData))
				yield break;

			for (int i = 0; i < partData.modules.Count; i++)
			{
				T moduleData = partData.modules[i] as T;
				if (moduleData != null)
					yield return moduleData;
			}
		}

		public static IEnumerable<T> GetModuleDatasOfType<T>(this ProtoPartSnapshot part) where T : ModuleHandler
		{
			if (!PartDataCollectionVessel.allFlightPartDatas.TryGetValue(part.flightID, out PartData partData))
				yield break;

			for (int i = 0; i < partData.modules.Count; i++)
			{
				T moduleData = partData.modules[i] as T;
				if (moduleData != null)
					yield return moduleData;
			}
		}
	}
}
