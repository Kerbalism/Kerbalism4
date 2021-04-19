using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{
    public static class DB
    {
		private const string VALUENAME_VERSION = "version";
		private const string VALUENAME_UID = "uid";
		private const string NODENAME_VESSELS = "KERBALISMVESSELS";
		private const string NODENAME_KERBALS = "KERBALISMKERBALS";
		private const string NODENAME_STORMS = "KERBALISMSTORMS";
		private const string NODENAME_GUI = "KERBALISMGUI";
		public static readonly Version LAST_SUPPORTED_VERSION = new Version(4, 0);

		// savegame version
		public static Version version;
		// savegame unique id
		private static Guid uid;
		// Store data per-kerbal.
		// Using ProtoCrewMember as keys because that is faster and safer than using the kerbal name as a string.
		private static Dictionary<ProtoCrewMember, KerbalData> kerbals = new Dictionary<ProtoCrewMember, KerbalData>();
		// store data per-vessel
		private static Dictionary<Guid, VesselData> vessels = new Dictionary<Guid, VesselData>();
		// store data per-body
		private static Dictionary<string, StormData> storms;
		// store ui data
		private static UIData uiData;                               

		public static Guid Guid => uid;
		public static UIData UiData => uiData;
		public static Dictionary<Guid, VesselData>.ValueCollection VesselDatas => vessels.Values;
		public static bool VesselExist(Guid guid) => vessels.ContainsKey(guid);

		#region LOAD/SAVE

		public static void Load(ConfigNode node)
        {
            // get version (or use current one for new savegames)
            string versionStr = Lib.ConfigValue(node, VALUENAME_VERSION, Lib.KerbalismVersion.ToString());
            // sanitize old saves (pre 3.1) format (X.X.X.X) to new format (X.X)
            if (versionStr.Split('.').Length > 2) versionStr = versionStr.Split('.')[0] + "." + versionStr.Split('.')[1];
            version = new Version(versionStr);

            // if this is an unsupported version, print warning
            if (version < LAST_SUPPORTED_VERSION)
            {
                Lib.Log($"Loading save from unsupported version " + version, Lib.LogLevel.Warning);
                return;
            }

            // get unique id (or generate one for new savegames)
            uid = Lib.ConfigValue(node, VALUENAME_UID, Guid.NewGuid());

			// load kerbals data
			kerbals.Clear();
			if (HighLogic.CurrentGame.CrewRoster != null)
			{
				ConfigNode kerbalsNode = node.GetNode(NODENAME_KERBALS);
				if (kerbalsNode != null)
				{
					foreach (ConfigNode kerbalNode in kerbalsNode.GetNodes())
					{
						KerbalData kerbalData = KerbalData.Load(HighLogic.CurrentGame.CrewRoster, kerbalNode);
						if (kerbalData != null)
						{
							kerbals.Add(kerbalData.stockKerbal, kerbalData);
						}
					}
				}
			}

			// load the science database, has to be before vessels are loaded
			ScienceDB.Load(node);

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.DB.Load.Vessels");

			// remove all vessels
			vessels.Clear();

			// clear the dictionaries of moduledatas and partdatas
			ModuleHandler.ClearOnLoad();
			PartData.ClearOnLoad();
			// Clear the loaded parts transforms cache
			PartRadiationData.RaycastTask.ClearLoadedPartsCache();

			// flightstate will be null when first creating the game
			if (HighLogic.CurrentGame.flightState != null)
			{
				ConfigNode vesselsNode = node.GetNode(NODENAME_VESSELS);

				// HighLogic.CurrentGame.flightState.protoVessels is what is used by KSP to persist vessels
				// It is always available and synchronized in OnLoad, no matter the scene, excepted on
				// the first OnLoad in a new game.
				foreach (ProtoVessel pv in HighLogic.CurrentGame.flightState.protoVessels)
				{
					if (!VesselData.VesselNeedVesselData(pv))
						continue;

					ConfigNode vesselDataNode = vesselsNode?.GetNode(pv.vesselID.ToString());
					if (vesselDataNode == null)
						continue;

					VesselData vd = new VesselData(pv, vesselDataNode, Lib.IsEditor);
					vessels.Add(pv.vesselID, vd);
					Lib.LogDebug("VesselData loaded for vessel " + pv.vesselName);
				}
			}
			UnityEngine.Profiling.Profiler.EndSample();

			// load bodies data
			storms = new Dictionary<string, StormData>();
            if (node.HasNode(NODENAME_STORMS))
            {
                foreach (var body_node in node.GetNode(NODENAME_STORMS).GetNodes())
                {
                    storms.Add(FromSafeKey(body_node.name), new StormData(body_node));
                }
            }

            // load ui data
            if (node.HasNode(NODENAME_GUI))
            {
                uiData = new UIData(node.GetNode(NODENAME_GUI));
            }
            else
            {
				uiData = new UIData();
            }

			SubStepSim.Load(node);

			// if an old savegame was imported, log some debug info
			if (version != Lib.KerbalismVersion) Lib.Log("savegame converted from version " + version + " to " + Lib.KerbalismVersion);
        }

        public static void Save(ConfigNode node)
        {
            // save version
            node.AddValue(VALUENAME_VERSION, Lib.KerbalismVersion.ToString());

            // save unique id
            node.AddValue(VALUENAME_UID, uid);

			// save kerbals data
			ConfigNode kerbalsNode = node.AddNode(NODENAME_KERBALS);
			foreach (KerbalData kerbal in kerbals.Values)
			{
				kerbal.Save(kerbalsNode);
			}

			// only persist vessels that exists in KSP own vessel persistence
			// this prevent creating junk data without going into the mess of using gameevents
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.DB.Save.Vessels");
			ConfigNode vesselsNode = node.AddNode(NODENAME_VESSELS);
			foreach (ProtoVessel pv in HighLogic.CurrentGame.flightState.protoVessels)
			{
                if (pv.TryGetVesselData(out VesselData vd) && vd.IsPersisted)
				{
					ConfigNode vesselNode = vesselsNode.AddNode(pv.vesselID.ToString());
					vd.Save(vesselNode);
				}
            }
			UnityEngine.Profiling.Profiler.EndSample();

			// save the science database
			ScienceDB.Save(node);

            // save bodies data
            var bodies_node = node.AddNode(NODENAME_STORMS);
            foreach (var p in storms)
            {
                p.Value.Save(bodies_node.AddNode(ToSafeKey(p.Key)));
            }

			// save ui data
			uiData.Save(node.AddNode(NODENAME_GUI));

			SubStepSim.Save(node);
		}

		/// <summary> Avoid leading and trailing spaces from being removed when saving a string to a ConfigNode value</summary>
		public static string ToSafeKey(string key) => key.Replace(" ", "___");

		/// <summary> Retrieve a string that was serialized using ToSafeKey() </summary>
		public static string FromSafeKey(string key) => key.Replace("___", " ");

		#endregion

		#region VESSELDATA METHODS

		public static VesselData NewVesselDataFromShipConstruct(Vessel v, ConfigNode shipNode, VesselDataShip shipVd)
		{
			Lib.LogDebug("Creating VesselData from ShipConstruct for launched vessel " + v.vesselName);
			VesselData vd = new VesselData(v, shipNode, shipVd);
			vessels.Add(v.id, vd);
			Kerbalism.Fetch.lastLaunchedVessel = v;

			return vd;
		}

		public static void AddNewVesselData(VesselData vd)
		{
			if (vessels.ContainsKey(vd.VesselId))
			{
				Lib.LogDebugStack($"Trying to register new VesselData for {vd.VesselName} but that vessel exists already !", Lib.LogLevel.Error);
				return;
			}

			Lib.LogDebug($"Adding new VesselData for {vd.VesselName}");
			vessels.Add(vd.VesselId, vd);
		}

		public static bool TryGetVesselDataTemp(this Vessel vessel, out VesselData vesselData)
		{
			if (!vessels.TryGetValue(vessel.id, out vesselData))
			{
				Lib.LogStack($"Could not get VesselData for vessel {vessel.vesselName}", Lib.LogLevel.Error);
				return false;
			}
			return true;
		}

		/// <summary>
		/// Get the VesselData for this vessel, if it exists. Typically, you will need this in a Foreach on FlightGlobals.Vessels
		/// </summary>
		public static bool TryGetVesselData(this Vessel vessel, out VesselData vesselData)
		{
			if (!vessels.TryGetValue(vessel.id, out vesselData))
				return false;

			return true;
		}

		/// <summary>
		/// Get the VesselData for this vessel. Will return null if that vessel isn't yet created in the DB, which can happen if this is called too early. <br/>
		/// Typically it's safe to use from partmodules FixedUpdate() and OnStart(), but not in Awake() and probably not from Update()<br/>
		/// Also, don't use this in a Foreach on FlightGlobals.Vessels, check the result of TryGetVesselData() instead
		/// </summary>
		public static VesselData GetVesselData(this Vessel vessel)
		{
			if (!vessels.TryGetValue(vessel.id, out VesselData vesselData))
			{
				Lib.LogStack($"Could not get VesselData for vessel {vessel.vesselName}");
				return null;
			}
			return vesselData;
		}

		public static bool TryGetVesselData(this ProtoVessel protoVessel, out VesselData vesselData)
		{
			return vessels.TryGetValue(protoVessel.vesselID, out vesselData);
		}

		#endregion

		#region STORM METHODS

		public static StormData Storm(string name)
		{
			if (!storms.ContainsKey(name))
			{
				storms.Add(name, new StormData(null));
			}
			return storms[name];
		}

		#endregion

		#region KERBALS METHODS

		/// <summary>
		/// Get a KerbalData given a stock ProtoCrewMember reference, but only if that Kerbal is known is the DB
		/// </summary>
		public static bool TryGetKerbalData(ProtoCrewMember stockKerbal, out KerbalData kerbalData)
		{
			return kerbals.TryGetValue(stockKerbal, out kerbalData);
		}

		/// <summary>
		/// Get or create the KerbalData given a stock ProtoCrewMember reference
		/// </summary>
		public static KerbalData GetOrCreateKerbalData(ProtoCrewMember stockKerbal)
		{
			if (!kerbals.TryGetValue(stockKerbal, out KerbalData kerbalData))
			{
				kerbalData = new KerbalData(stockKerbal, true);
				kerbals.Add(stockKerbal, kerbalData);
			}
			return kerbalData;
		}

		/// <summary>
		/// Alternate method for getting a kerbal data by name. When possible, use the faster GetOrCreateKerbalData(ProtoCrewMember stockKerbal) method instead.
		/// </summary>
		public static bool TryGetOrCreateKerbalData(string kerbalName, out KerbalData kerbalData)
		{
			ProtoCrewMember stockKerbal = HighLogic.CurrentGame.CrewRoster[kerbalName];
			if (stockKerbal != null)
			{
				kerbalData = GetOrCreateKerbalData(stockKerbal);
				return true;
			}
			kerbalData = null;
			return false;
		}

		#endregion

	}
} // KERBALISM



