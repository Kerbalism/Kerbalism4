using Flee.PublicTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;

namespace KERBALISM
{
    public partial class VesselData : VesselDataBase
	{
		//public List<SimStep> subSteps = new List<SimStep>();
		public readonly List<double> timestamps = new List<double>(4096);


		#region FIELDS/PROPERTIES : CORE STATE AND SUBSYSTEMS

		private ConfigNode dataNode;

		/// <summary>
		/// reference to the KSP Vessel object
		/// </summary>
		public Vessel Vessel { get; private set; }

		/// <summary>
		/// Guid of the vessel, match the KSP affected Guid
		/// </summary>
		public Guid VesselId { get; private set; }

		/// <summary>
		/// convenience property
		/// </summary>
		public override string VesselName => Vessel == null ? "new vessel instance" : Vessel.vesselName;

        /// <summary>
		/// False in the following cases : asteroid, debris, flag, deployed ground part, dead eva, rescue
		/// </summary>
        public bool IsSimulated
		{
			get => isSimulated;
			private set
			{
				if (value) isPersisted = true;
				isSimulated = value;
			}
		}
		private bool isSimulated;

		/// <summary>
		/// True if the vessel once the vessel has been simulated at least once in its lifetime.
		/// Can't become false once has been set true.
		/// </summary>
		public bool IsPersisted
		{
			get => isPersisted;
			private set
			{
				if (!value && isPersisted)
				{
					Lib.LogStack($"Attempting to set IsPersisted to false on a persisted vessel isn't allowed (vessel : {this}", Lib.LogLevel.Warning);
					return;
				}

				isPersisted = value;
			}
		}

		/// <summary>
		/// Never set this directly, use the property
		/// </summary>
		private bool isPersisted;

		// those are the various component of the IsSimulated check
		private bool isVessel;  // true if the vessel is not dead and if the vessel type is right
		private bool isRescue;  // true if this is a not yet loaded rescue mission vessel
		private bool isEvaDead; // true if this is an EVA kerbal that we killed

		public override bool LoadedOrEditor => !ReferenceEquals(Vessel, null) && Vessel.loaded;

		public override bool IsEVA => Vessel.isEVA;

		/// <summary>
		/// Time elapsed since last evaluation
		/// </summary>
		public double lastEvalUT;

		/// <summary>
		/// Resource handler for this vessel. <br/>
		/// This can be null, or not properly initialized while VesselData.IsSimulated is false. <br/>
		/// Do not use it from PartModule.Start() / PartModule.OnStart(), and check VesselData.IsSimulated before using it from Update() / FixedUpdate()
		/// </summary>
		public override VesselResHandler ResHandler => resHandler; VesselResHandler resHandler;

		/// <summary>
		/// Comms handler for this vessel, evaluate and expose data about the vessel antennas and comm link
		/// </summary>
		public CommHandler CommHandler { get; private set; }


		public DriveHandler TransmitBuffer { get; private set; }

		/// <summary>
		/// List/Dictionary of all the vessel PartData, and their ModuleData
		/// </summary>
		public override PartDataCollectionBase Parts => VesselParts;

		/// <summary>
		/// Base class implementation for the PartData list.
		/// Prefer using the Parts property unless you are doing something that must be flight/editor agnostic.
		/// </summary>
		public PartDataCollectionVessel VesselParts { get; private set; }

		/// <summary>
		/// all part modules that have a ResourceUpdate method
		/// </summary>
		public List<ResourceUpdateDelegate> resourceUpdateDelegates = null;

        /// <summary>
        /// List of files being transmitted, or empty if nothing is being transmitted <br/>
        /// Note that the transmit rates stored in the File objects can be unreliable, do not use it apart from UI purposes
        /// </summary>
        public List<DriveFile> filesTransmitted;

		private VesselLogic.VesselRadiation vesselRadiation = new VesselLogic.VesselRadiation();

		#endregion

		#region FIELDS/PROPERTIES : PERSISTED STATE

		// user defined persisted fields
		public bool cfg_ec;           // enable/disable message: ec level
        public bool cfg_supply;       // enable/disable message: supplies level
        public bool cfg_signal;       // enable/disable message: link status
        public bool cfg_malfunction;  // enable/disable message: malfunctions
        public bool cfg_storm;        // enable/disable message: storms
        public bool cfg_script;       // enable/disable message: scripts
        public bool cfg_highlights;   // show/hide malfunction highlights
        public bool cfg_showlink;     // show/hide link line
        public bool cfg_show;         // show/hide vessel in monitor
		public bool cfg_orbit;       // show/hide vessel orbit lines in map view
		public Computer computer;     // store scripts

		// other persisted fields
		public bool IsUIPinned { get; set; }

		public bool msg_signal;       // message flag: link status
        public bool msg_belt;         // message flag: crossing radiation belt
        public StormData stormData;   // store state of current/next solar storm
        public double scienceTransmitted; // how much science points has this vessel earned trough transmission
		

		// persist that so we don't have to do an expensive check every time
		public bool IsSerenityGroundController => isSerenityGroundController; bool isSerenityGroundController;

		#endregion

		#region FIELDS/PROPERTIES : EVALUATED VESSEL ENVIRONMENT

		// Things like vessel situation, sunlight, temperature, radiation, 

		/// <summary>
		/// [environment] true when timewarping faster at 10000x or faster. When true, some fields are updated more frequently
		/// and their evaluation is changed to an analytic, timestep-independant and vessel-position-independant mode.
		/// </summary>
		public bool IsSubstepping => isSubstepping; bool isSubstepping;

		public override CelestialBody MainBody => mainBody; CelestialBody mainBody;

		public override double Altitude => altitude; double altitude;

		public override double Latitude => Vessel.latitude;

		public override double Longitude => Vessel.longitude;

		public override double Gravity => gravity; double gravity;

		public override double AngularVelocity => Vessel.angularVelocityD.magnitude;

		/// <summary> [environment] true if inside ocean</summary>
		public override bool EnvUnderwater => underwater; bool underwater;

        /// <summary> [environment] true if on the surface of a body</summary>
        public override bool EnvLanded => landed; bool landed;

        /// <summary> current atmospheric pressure in atm</summary>
        public override double EnvStaticPressure => envStaticPressure; double envStaticPressure;

        /// <summary> Is the vessel inside an atmosphere ?</summary>
        public override bool EnvInAtmosphere => inAtmosphere; bool inAtmosphere;

        /// <summary> Is the vessel inside a breatheable atmosphere ?</summary>
        public override bool EnvInOxygenAtmosphere => inOxygenAtmosphere; bool inOxygenAtmosphere;

        /// <summary> Is the vessel inside a breatheable atmosphere and at acceptable pressure conditions ?</summary>
        public override bool EnvInBreathableAtmosphere => inBreathableAtmosphere; bool inBreathableAtmosphere;

        /// <summary> [environment] true if in zero g</summary>
        public override bool EnvZeroG => zeroG; bool zeroG;

        /// <summary> [environment] temperature ar vessel position</summary>
        public override double EnvTemperature => temperature; double temperature;

        /// <summary> [environment] difference between environment temperature and survival temperature</summary>// 
        public override double EnvTempDiff => tempDiff; double tempDiff;

        /// <summary> [environment] total radiation at vessel position</summary>
        public override double EnvRadiation => radiation; public double radiation;

		/// <summary> [environment] radiation from the inner belt, excluding gamma transparency</summary>
		public override double EnvRadiationInnerBelt => radiationInnerBelt; public double radiationInnerBelt;

		/// <summary> [environment] radiation from the outer belt, excluding gamma transparency</summary>
		public override double EnvRadiationOuterBelt => radiationOuterBelt; public double radiationOuterBelt;

		/// <summary> [environment] radiation from the magnetopause, excluding gamma transparency</summary>
		public override double EnvRadiationMagnetopause => radiationMagnetopause; public double radiationMagnetopause;

        /// <summary> [environment] radiation from the nearby bodies</summary>
        public override double EnvRadiationBodies => radiationBodies; public double radiationBodies;

        /// <summary> [environment] radiation from the surface</summary>
        public override double EnvRadiationSolar => radiationSolar; public double radiationSolar;

		/// <summary> [environment] true if vessel is inside a magnetopause (except the heliosphere)</summary>
		public override bool EnvMagnetosphere => magnetosphere; bool magnetosphere;

        /// <summary> [environment] true if vessel is inside a radiation belt</summary>
        public override bool EnvInnerBelt => innerBelt; bool innerBelt;

        /// <summary> [environment] true if vessel is inside a radiation belt</summary>
        public override bool EnvOuterBelt => outerBelt; bool outerBelt;

        /// <summary> [environment] true if vessel is outside sun magnetopause</summary>
        public bool EnvInterstellar => interstellar; bool interstellar;

        /// <summary> [environment] true if the vessel is inside a magnetopause (except the sun) and under storm</summary>
        public bool EnvBlackout => blackout; bool blackout;

        /// <summary> [environment] true if vessel is inside thermosphere</summary>
        public bool EnvThermosphere => thermosphere; bool thermosphere;

        /// <summary> [environment] true if vessel is inside exosphere</summary>
        public bool EnvExosphere => exosphere; bool exosphere;

		/// <summary> [environment] true if vessel currently experienced a solar storm</summary>
		public override double EnvStormRadiation => stormRadiation; public double stormRadiation;

		/// <summary> [environment] proportion of ionizing radiation not blocked by atmosphere</summary>
		public override double EnvGammaTransparency => gammaTransparency; double gammaTransparency;

        /// <summary> [environment] gravitation gauge particles detected (joke)</summary>
        public double EnvGravioli => gravioli; double gravioli;

        /// <summary> [environment] Bodies whose apparent diameter from the vessel POV is greater than ~10 arcmin (~0.003 radians)</summary>
        // real apparent diameters at earth : sun/moon =~ 30 arcmin, Venus =~ 1 arcmin
        public CelestialBody[] VisibleBodies => visibleBodies; CelestialBody[] visibleBodies;

		/// <summary> Angle of the main sun on the body surface over the vessel position</summary>
		public double MainStarBodyAngle => sunBodyAngle; double sunBodyAngle;

        

		#endregion

		#region FIELDS/PROPERTIES : EVALUATED VESSEL STATE

		public override int RulesEnabledCrewCount => rulesEnabledCrewCount; int rulesEnabledCrewCount;

		/// <summary>number of crew on the vessel</summary>
		public override int CrewCount => crewCount; int crewCount;

        /// <summary>crew capacity of the vessel</summary>
        public override int CrewCapacity => crewCapacity; int crewCapacity;

        /// <summary>connection info</summary>
        public ConnectionInfo Connection => connection; ConnectionInfo connection;

		public override IConnectionInfo ConnectionInfo => connection;

        /// <summary>true if all command modules are hibernating (limited control and no transmission)</summary>
        public bool Hibernating { get; private set; }
        public bool hasNonHibernatingCommandModules = false;

        /// <summary>true if vessel is powered</summary>
        public bool Powered => powered; bool powered;

		/// <summary>
		/// Evaluated on loaded vessels based on the data pushed by SolarPanelFixer.<br/>
		/// This doesn't change for unloaded vessel, so the value is persisted<br/>
		/// Negative if there is no solar panel on the vessel
		/// </summary>
		public override double SolarPanelsAverageExposure => solarPanelsAverageExposure; double solarPanelsAverageExposure = -1.0;
        private List<double> solarPanelsExposure = new List<double>(); // values are added by SolarPanelFixer, then cleared by VesselData once solarPanelsAverageExposure has been computed
        public void SaveSolarPanelExposure(double exposure) => solarPanelsExposure.Add(exposure); // meant to be called by SolarPanelFixer

		/// <summary>true if at least a component has malfunctioned or had a critical failure</summary>
		public bool Malfunction => malfunction; bool malfunction;

		/// <summary>true if at least a component had a critical failure</summary>
		public bool Critical => critical; bool critical;

		private List<ReliabilityInfo> reliabilityStatus;
        public List<ReliabilityInfo> ReliabilityStatus()
        {
            if (reliabilityStatus != null) return reliabilityStatus;
            reliabilityStatus = ReliabilityInfo.BuildList(Vessel);
            return reliabilityStatus;
        }

        public void ResetReliabilityStatus()
        {
            reliabilityStatus = null;
        }

		#endregion

		#region INSTANTIATION AND PERSISTANCE

		/// <summary>
		/// We never create a VesselData for flags, because they have an empty id.
		/// Call this when you must decide if you need to create a VesselData for a vessel,
		/// or when you are checking if not finding a VesselData in the DB is an error or not.
		/// </summary>
		public static bool VesselNeedVesselData(ProtoVessel pv)
		{
			if (pv.vesselRef == null)
			{
				if (pv.vesselID == Guid.Empty)
					return false;
			}
			else
			{
				if (pv.vesselRef.id == Guid.Empty)
					return false;
			}

			return true;
		}

		/// <summary>
		/// This ctor is **only** used to convert a ship into a vessel (ie, launching a new vessel)
		/// </summary>
		public VesselData(Vessel vessel, ConfigNode kerbalismDataNode, VesselDataShip shipVd)
		{
			IsSimulated = true;

			Vessel = vessel;
			VesselId = Vessel.id;

			// this can't be a rescue, but calling CheckRescueStatus is needed to initialize KerbalData.isRescue
			// for Kerbals being launchd for the first time.
			isRescue = CheckRescueStatus(Vessel, out _);

			Synchronizer = new SynchronizerVessel(this);
			resHandler = shipVd.ResHandler;
			resHandler.ConvertShipHandlerToVesselHandler(this);
			VesselParts = new PartDataCollectionVessel(this, (PartDataCollectionShip)shipVd.Parts);

			// note : we don't load parts, they already have been loaded when the ship was instantiated
			Load(kerbalismDataNode, true);

			SetPersistedDefaults(vessel.protoVessel);
			SetInstantiateDefaults(vessel.protoVessel);
		}

		/// <summary>
		/// This ctor is used for all post scene load created vessels :
		/// - "automatically" created unloaded vessels (ex : rescue, asteroids...)
		/// - Vessels resulting from undocking/decoupling, passing the existing parts to be transferred to that vessel
		/// - EVA vessels creation
		/// </summary>
		// TODO : it would be nice to better handle VesselData level user settings when docking/undocking. We could
		// handle that by serializing the VD for the vessel that dock, persist it in the docked to vessel, along with the root part flightId
		// And on undocking, we could restore that saved configNode when instantiating the VD. 
		public VesselData(Vessel vessel, List<PartData> partDatas = null)
		{
			Vessel = vessel;
			VesselId = Vessel.id;

			IsSimulated = partDatas != null || ShouldBeSimulated(out _);

			if (!IsSimulated)
				return;

			Synchronizer = new SynchronizerVessel(this);
			resHandler = new VesselResHandler(this, VesselResHandler.SimulationType.Vessel);

			if (Vessel.loaded)
			{
				if (partDatas == null)
				{
					// will instantiate new PartDatas/ModuleHandlers, for which we will need a FirstSetup()/Start()
					VesselParts = new PartDataCollectionVessel(this, Vessel); 
				}
				else
				{
					// we must NOT call FirstSetup()/Start() for those parts
					VesselParts = new PartDataCollectionVessel(this, partDatas); 
				}
			}
			else
			{
				if (partDatas != null)
				{
					Lib.LogStack($"Transfering parts to an unloaded vessel is unsupported ! (Vessel : {vessel.protoVessel.vesselName})", Lib.LogLevel.Error);
				}

				// vessels can be created unloaded, asteroids for example
				// will instantiate new PartDatas/ModuleHandlers, for which we will need a FirstSetup()/Start()
				VesselParts = new PartDataCollectionVessel(this, Vessel.protoVessel, null); 
			}

			SetPersistedDefaults(vessel.protoVessel);
			SetInstantiateDefaults(vessel.protoVessel);

			Start();

			Lib.LogDebug($"New vessel {this} created. Loaded={Vessel.loaded}, Simulated={IsSimulated} {(partDatas != null ? $" (From undocking/decoupling, part count={Parts.Count})" : "")}");
		}



		#region CTOR / INIT

		/// <summary>
		/// This ctor is for instantating vessels loaded from the DB, on scene load.
		/// Note that it will accept a null ConfigNode as a fallback, because we can't afford to not have a VesselData instance for every vessel.
		/// </summary>
		public VesselData(ProtoVessel protoVessel, ConfigNode topNode, bool IsEditor)
		{
			IsSimulated = false;
			dataNode = topNode?.GetNode(NODENAME_VESSEL);
			VesselId = protoVessel.vesselID;

			if (IsEditor)
			{
				PersistedVesselSetup(true, protoVessel);
			}
		}

		/// <summary>
		/// This is called by Kerbalism.Start(), for all VesselData that were loaded from save for a scene load, using the previous ctor <br/>
		/// Will instantatiate all PartDatas/ModuleHandlers/ResourceWrappers and set all the cross references with the stock objects <br/>
		/// Note that the timing of that call is very constrained : <br/>
		/// - We need to have access to the loaded Vessel/Parts/PartModules, which don't exist yet when we instantiate the VesselData from OnLoad() <br/>
		/// - This **must** be called before the Part.Start() patch (see KSPLifecycleHooks) <br/>
		/// </summary>
		public void SceneLoadVesselSetup(Vessel vessel)
		{
			Vessel = vessel;

			if (!ShouldBeSimulated(out _))
			{
				IsSimulated = false;
				Lib.LogDebug($"Ignoring non-simulated vessel `{this}` : isVessel={isVessel}, isRescue={isRescue}, isEvaDead={isEvaDead}");
			}
			else
			{
				IsSimulated = true;
			}

			if (IsPersisted)
			{
				PersistedVesselSetup(false);
			}
			
			dataNode = null;
		}

		/// <summary>
		/// Common implementation of all the sub-objects instantiation, for the two following cases :
		/// - Vessels loaded from a save on scene load : Kerbalism.Start() -> PersistedVesselSetup()
		/// - Non-simulated unloaded vessels becoming simulated
		/// </summary>
		private void PersistedVesselSetup(bool inEditor, ProtoVessel protoVessel = null)
		{
			Lib.LogDebug($"Doing setup for vessel {this} (loaded={LoadedOrEditor})");

			IsPersisted = true;
			Synchronizer = new SynchronizerVessel(this);
			resHandler = new VesselResHandler(this, VesselResHandler.SimulationType.Vessel);

			if (!inEditor)
			{
				protoVessel = Vessel.protoVessel;
			}

			if (dataNode == null)
			{
				if (inEditor || !Vessel.loaded)
				{
					VesselParts = new PartDataCollectionVessel(this, protoVessel, null);
				}
				else
				{
					VesselParts = new PartDataCollectionVessel(this, Vessel);
				}

				SetPersistedDefaults(protoVessel);
			}
			else
			{
				if (inEditor || !Vessel.loaded)
				{
					VesselParts = new PartDataCollectionVessel(this, protoVessel, dataNode);
				}
				else
				{
					VesselParts = new PartDataCollectionVessel(this, Vessel, dataNode);
				}

				Parts.Load(dataNode);
				Load(dataNode, false);
			}

			SetInstantiateDefaults(protoVessel);
		}

		#endregion

		private void SetPersistedDefaults(ProtoVessel pv)
		{
			msg_signal = false;
			msg_belt = false;
			cfg_ec = PreferencesMessages.Instance.ec;
			cfg_supply = PreferencesMessages.Instance.supply;
			cfg_signal = PreferencesMessages.Instance.signal;
			cfg_malfunction = PreferencesMessages.Instance.malfunction;
			cfg_storm = Features.Radiation && PreferencesMessages.Instance.storm && Lib.CrewCount(pv) > 0;
			cfg_script = PreferencesMessages.Instance.script;
			cfg_highlights = PreferencesReliability.Instance.highlights;
			cfg_showlink = true;
			cfg_show = true;
			cfg_orbit = true;
			DeviceTransmit = true;
			// note : we check that at vessel creation and persist it, as the vesselType can be changed by the player
			isSerenityGroundController = pv.vesselType == VesselType.DeployedScienceController;
			stormData = new StormData(null);
			computer = new Computer(null);
			lastEvalUT = Planetarium.GetUniversalTime();
		}

		private void SetInstantiateDefaults(ProtoVessel protoVessel)
		{
			filesTransmitted = new List<DriveFile>();
			VesselSituations = new VesselSituations(this);
			connection = new ConnectionInfo();
			CommHandler = CommHandler.GetHandler(this, isSerenityGroundController);
			TransmitBuffer = new DriveHandler();
			TransmitBuffer.OnStart();
		}

		public void SetOrbitVisible(bool visible)
		{
			if (!Settings.EnableOrbitLineTweaks)
				return;

			cfg_orbit = visible;

			if (Vessel == null || Vessel.loaded)
				return;

			var or = Vessel.GetComponent<OrbitRenderer>();
			if (or != null && !or.isFocused)
			{
				var m = cfg_orbit ? OrbitRendererBase.DrawMode.REDRAW_AND_RECALCULATE : OrbitRendererBase.DrawMode.OFF;
				or.drawMode = m;
			}
		}

		#region PERSISTENCE

		protected override void OnLoad(ConfigNode node)
		{
			isPersisted = node != null;

			IsUIPinned = Lib.ConfigValue(node, nameof(IsUIPinned), false);

			msg_signal = Lib.ConfigValue(node, "msg_signal", false);
			msg_belt = Lib.ConfigValue(node, "msg_belt", false);
			cfg_ec = Lib.ConfigValue(node, "cfg_ec", PreferencesMessages.Instance.ec);
			cfg_supply = Lib.ConfigValue(node, "cfg_supply", PreferencesMessages.Instance.supply);
			cfg_signal = Lib.ConfigValue(node, "cfg_signal", PreferencesMessages.Instance.signal);
			cfg_malfunction = Lib.ConfigValue(node, "cfg_malfunction", PreferencesMessages.Instance.malfunction);
			cfg_storm = Lib.ConfigValue(node, "cfg_storm", PreferencesMessages.Instance.storm);
			cfg_script = Lib.ConfigValue(node, "cfg_script", PreferencesMessages.Instance.script);
			cfg_highlights = Lib.ConfigValue(node, "cfg_highlights", PreferencesReliability.Instance.highlights);
			cfg_showlink = Lib.ConfigValue(node, "cfg_showlink", true);
			cfg_show = Lib.ConfigValue(node, "cfg_show", true);
			cfg_orbit = Lib.ConfigValue(node, "cfg_orbits", true);

			

			isSerenityGroundController = Lib.ConfigValue(node, "isSerenityGroundController", false);

			solarPanelsAverageExposure = Lib.ConfigValue(node, "solarPanelsAverageExposure", -1.0);
			scienceTransmitted = Lib.ConfigValue(node, "scienceTransmitted", 0.0);

			stormData = new StormData(node.GetNode("StormData"));
			computer = new Computer(node.GetNode("computer"));

			lastEvalUT = Lib.ConfigValue(node, nameof(lastEvalUT), Planetarium.GetUniversalTime());
		}

		protected override void OnSave(ConfigNode node)
		{
			node.AddValue(nameof(IsUIPinned), IsUIPinned);

			node.AddValue("msg_signal", msg_signal);
			node.AddValue("msg_belt", msg_belt);
			node.AddValue("cfg_ec", cfg_ec);
			node.AddValue("cfg_supply", cfg_supply);
			node.AddValue("cfg_signal", cfg_signal);
			node.AddValue("cfg_malfunction", cfg_malfunction);
			node.AddValue("cfg_storm", cfg_storm);
			node.AddValue("cfg_script", cfg_script);
			node.AddValue("cfg_highlights", cfg_highlights);
			node.AddValue("cfg_showlink", cfg_showlink);
			node.AddValue("cfg_show", cfg_show);
			node.AddValue("cfg_orbits", cfg_orbit);

			node.AddValue("isSerenityGroundController", isSerenityGroundController);

			node.AddValue("solarPanelsAverageExposure", solarPanelsAverageExposure);
			node.AddValue("scienceTransmitted", scienceTransmitted);

			stormData.Save(node.AddNode("StormData"));
			computer.Save(node.AddNode("computer"));

			node.AddValue(nameof(lastEvalUT), lastEvalUT);

			if (Vessel != null)
				Lib.LogDebug("VesselData saved for vessel " + Vessel.vesselName);
			else
				Lib.LogDebug("VesselData saved for vessel (Vessel is null)");

		}

		#endregion


		/// <summary>
		/// Called :
		/// - From the first Kerbalism.FixedUpdate() (ie, after a non-editor scene load), for all FlightGlobals vessels.
		/// - From here, when a previously non-simulated vessel becomes simulated
		/// Responsible for :
		/// - Calling FirstSetup() for all new module handlers (typically all non-persistent handlers)
		/// - Synchronizing the part resources and the vessel resource simulation
		/// - Calling OnStart() for every part and module handler
		/// - Initializing any other VesselData component
		/// </summary>
		public void Start()
		{
			Lib.LogDebug($"Starting vessel {this} (loaded={LoadedOrEditor})");

			// update the vessel environment
			//EnvironmentUpdate(0.0, null);
			// update crew state
			CrewUpdate();

			// call every module FirstSetup(), if needed.
			// SetupDone will be false :
			// - for all non-persisted handlers
			// - for persisted handlers that are created in flight (rescue, asteroids...)
			// Typically, this should be used to :
			// - parse the configuration and initialize persisted values (on persistent handlers) or long lived instance values (on non-persistent handlers)
			// - add or remove resources to the part.
			// - for non-persistent handlers : check if the handler is valid (should we delete invalid handlers here ?)
			// When this called :
			// - VesselData environment and crew are evaluated and in an useable state
			// - All parts, part resources and modules related objects will be instantiated and correctly cross-referenced
			// - Part resources state will be in a an useable state
			// - Everything else will be in an undetermined state, notably : resource sim, comms, radiation, science...
			foreach (PartData part in Parts)
			{
				foreach (ModuleHandler handler in part.modules)
				{
					handler.FirstSetup();
				}
			}

			// From now on, we assume that nobody will be altering part resources. Synchronize the resource sim state.
			resHandler.ForceHandlerSync();

			// Call OnStart() on every PartData, and every enabled ModuleHandler/KsmPartModule
			foreach (PartData part in Parts)
			{
				part.Start();
			}

			//StateUpdate();

			//if (LoadedOrEditor && Parts[0].LoadedPart == null)
			//	Lib.LogDebug($"Skipping loaded vessel ModuleDataUpdate (part references not set yet) on {VesselName}");
			//else
			//	ModuleDataUpdate();

			// Set orbit visibility based on the saved user setup
			//SetOrbitVisible(cfg_orbit);
		}

		#endregion

		private bool ShouldBeSimulated(out bool rescueJustLoaded)
		{
			// determine if this is a valid vessel
			isVessel = Lib.IsVessel(Vessel);

			// determine if this is a rescue mission vessel
			isRescue = CheckRescueStatus(Vessel, out rescueJustLoaded);

			// dead EVA are not valid vessels
			isEvaDead = Lib.IsEVADead(Vessel);

			return isVessel && !isRescue && !isEvaDead;
		}

		#region EVALUATION

		/// <summary>
		/// Called from Kerbalism.FixedUpdate() for all existing flightglobal vessels
		/// Responsible for starting in-flight 
		/// </summary>
		public bool SimulatedCheck(Vessel vessel)
		{
			// needed in the case of a newly launched vessel (and maybe other cases ?)
			Vessel = vessel;

			// if vessel wasn't simulated previously and now should be, start it.
			// this will happen when an asteroid/comet/rescue vessel enter physics range
			// and also for freshly launched vessels
			if (!IsSimulated && ShouldBeSimulated(out bool rescueJustLoaded))
			{
				bool wasPersisted = IsPersisted;
				IsSimulated = true;

				// simulated once vessels becomes permanently persisted
				if (!wasPersisted)
					PersistedVesselSetup(false);
				
				Start();

				if (rescueJustLoaded)
					OnRescueVesselLoaded();

				Lib.LogDebug($"{Vessel.vesselName} is now simulated");
			}

			return IsSimulated;
		}

		/// <summary>
		/// Called from Kerbalism.FixedUpdate() for
		/// - all loaded vessels
		/// - one unloaded vessel per FixedUpdate()
		/// </summary>
		// TODO : improve the unloaded handling currently implemented in Kerbalism.FixedUpdate() :
		// - ignore non-simulated vessels (a game can have a lot (100+) of asteroids/comets/debris)
		// - move the "last update" inside VesselData, and persist it so we don't skip time on scene loads
		// - use a stopwatch to monitor the average time taken for updating every unloaded vessel and allow 
		//   updating several "fast" vessels in the same FU. Could also take into account the loaded vessels
        //   to do some "load balancing" and not update unloaded vessels in the same FU as the "full" loaded
		//   vessels update.
		public void Evaluate(double elapsedSeconds, SteppedSim.SubStepSim sim)
		{
			if (timestamps.Count == 0)
			{
				Lib.LogDebug("Can't update vessel : environemment not evaluated yet", Lib.LogLevel.Warning);
				return;
			}

			// synchronize :
			// - resource wrappers with the stock part resource objects
			// - vessel wide caches of radiation emitters and shields
			Synchronizer.Synchronize();

			// get crew data / count / capacity
			CrewUpdate();

			EnvironmentUpdate(sim);
			StateUpdate();

			if (LoadedOrEditor && Parts.Count > 0 && Parts[0].LoadedPart == null)
				Lib.LogDebug($"Skipping loaded vessel ModuleDataUpdate (part references not set yet) on {VesselName}");
			else
				ModuleDataUpdate();

			FixedUpdate(elapsedSeconds);

			lastEvalUT = timestamps[timestamps.Count - 1];
			timestamps.Clear();
		}

		private void FixedUpdate(double elapsedSec)
		{
			vesselRadiation.FixedUpdate(Parts, LoadedOrEditor, elapsedSec);

			foreach (PartData pd in Parts)
			{
				foreach (ModuleHandler module in pd.modules)
				{
					if (!module.handlerIsEnabled)
						continue;

					module.OnFixedUpdate(elapsedSec);
				}
			}

			foreach (KerbalData kerbal in Crew)
			{
				kerbal.OnFixedUpdate(this, elapsedSec);
			}
		}

		private void CrewUpdate()
		{
			// TODO : move all the synchronization things to the Synchronizer.Synchronize() method
			Crew.Clear();
			rulesEnabledCrewCount = 0;
			crewCount = 0;
			foreach (ProtoCrewMember stockCrew in Vessel.GetVesselCrew())
			{
				KerbalData kd = DB.GetOrCreateKerbalData(stockCrew);
				Crew.Add(kd);
				crewCount++;
				if (kd.RulesEnabled)
				{
					rulesEnabledCrewCount++;
				}
			}

			// TODO : this currently use the prefab crewCapacity on unloaded vessels
			// There are mods dynamically changing crew capacities, and we also want to
			// be able to do that for deployable habitats. Add a persisted crewCapacity
			// field to PartData for that purpose.
			crewCapacity = Lib.CrewCapacity(Vessel);
		}
		
		private void StateUpdate()
        {
            UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.StateUpdate");
            // determine if there is enough EC for a powered state
            powered = Lib.IsPowered(Vessel, ResHandler.ElectricCharge);

            // malfunction stuff
            malfunction = Reliability.HasMalfunction(Vessel);
            critical = Reliability.HasCriticalFailure(Vessel);

            // communications info
            CommHandler.UpdateConnection(connection);

            // check ModuleCommand hibernation
            if (isSerenityGroundController)
                hasNonHibernatingCommandModules = true;

            if (Hibernating != !hasNonHibernatingCommandModules)
            {
                Hibernating = !hasNonHibernatingCommandModules;
                if (!Hibernating)
                    DeviceTransmit = true;
            }

            // this flag will be set by the ModuleCommand harmony patches / background update
            hasNonHibernatingCommandModules = false;

            if (Hibernating)
                DeviceTransmit = false;

            // solar panels data
            if (Vessel.loaded)
            {
                solarPanelsAverageExposure = SolarPanelFixer.GetSolarPanelsAverageExposure(solarPanelsExposure);
                solarPanelsExposure.Clear();
            }
            UnityEngine.Profiling.Profiler.EndSample();
        }

		private void EnvironmentUpdate(SteppedSim.SubStepSim sim)
        {
            UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.EnvironmentUpdate");
			isSubstepping = timestamps.Count > 1; // TODO : is that always right ?

			Vector3d vesselPosition = Lib.VesselPosition(Vessel);
			landed = Lib.Landed(Vessel);
			altitude = Vessel.altitude;
			mainBody = Vessel.mainBody;

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.ProcessStep");

			// TODO : these lists can be made static
			List<int> starIndexes = new List<int>();
			List<int> bodyIndexes = new List<int>();
			foreach (CelestialBody body in FlightGlobals.Bodies)
			{
				if (Sim.IsStar(body))
					starIndexes.Add(body.flightGlobalsIndex);
				else
					bodyIndexes.Add(body.flightGlobalsIndex);
			}

			// TODO : these arrays should be made static and cleared
			int starCount = starIndexes.Count;
			int bodyCount = bodyIndexes.Count;
			double[] starsRawIrradianceTemp = new double[starCount];
			double[] starsIrradianceTemp = new double[starCount];
			double[] starsSunlightFactorTemp = new double[starCount];
			double[] bodiesAlbedoTemp = new double[bodyCount];
			double[] bodiesEmissiveTemp = new double[bodyCount];
			double[] bodiesCoreTemp = new double[bodyCount];


			// TODO: Weight averages by duration of each timestamp, since they can vary
			foreach (double timestamp in timestamps)
			{
				if (!sim.frameManager.Frames.TryGetValue(timestamp, out SteppedSim.SubstepFrame frame)
				    || !frame.guidVesselMap.TryGetValue(VesselId, out int index))
					continue;

				int baseIrradianceIndex = index * frame.bodies.Length;

				for (int i = 0; i < starIndexes.Count; i++)
				{
					SteppedSim.VesselBodyIrradiance irradiance = frame.irradiances[baseIrradianceIndex + starIndexes[i]];
					starsRawIrradianceTemp[i] += irradiance.solarRaw;
					starsIrradianceTemp[i] += irradiance.solar;
					if (irradiance.solar > 0.0)
						starsSunlightFactorTemp[i] += 1.0;
				}

				for (int i = 0; i < bodyIndexes.Count; i++)
				{
					SteppedSim.VesselBodyIrradiance irradiance = frame.irradiances[baseIrradianceIndex + bodyIndexes[i]];
					bodiesAlbedoTemp[i] += irradiance.albedo;
					bodiesEmissiveTemp[i] += irradiance.emissive;
					bodiesCoreTemp[i] += irradiance.core;
				}
			}

			double subStepCountD = timestamps.Count;
			double maxStarDirectRawFlux = 0.0;
			double totalStarDirectRawFlux = 0.0;

			starsIrradiance = 0.0;
			bodiesIrradianceAlbedo = 0.0;
			bodiesIrradianceEmissive = 0.0;
			bodiesIrradianceCore = 0.0;
			bodyFluxes.Clear();
			starFluxes.Clear();

			for (int i = 0; i < starIndexes.Count; i++)
			{
				CelestialBody body = FlightGlobals.Bodies[starIndexes[i]];
				StarFlux starFlux = new StarFlux();
				starFlux.body = body;
				starFlux.bodyIndex = body.flightGlobalsIndex;
				starFlux.direction = body.position - vesselPosition;
				starFlux.distance = starFlux.direction.magnitude;
				starFlux.direction /= starFlux.distance;

				starFlux.directFlux = starsIrradianceTemp[i] / subStepCountD;
				starFlux.directRawFlux = starsRawIrradianceTemp[i] / subStepCountD;
				starFlux.sunlightFactor = starsSunlightFactorTemp[i] / subStepCountD;

				starsIrradiance += starFlux.directFlux;
				totalStarDirectRawFlux += starFlux.directRawFlux;
				if (starFlux.directRawFlux > maxStarDirectRawFlux)
				{
					maxStarDirectRawFlux = starFlux.directRawFlux;
					mainStar = starFlux;
				}

				starFluxes.Add(starFlux);
			}

			for (int i = 0; i < starFluxes.Count; i++)
			{
				StarFlux starFlux = starFluxes[i];
				starFlux.directRawFluxProportion = starFlux.directRawFlux / totalStarDirectRawFlux;
				starFluxes[i] = starFlux;
			}

			for (int i = 0; i < bodyIndexes.Count; i++)
			{
				CelestialBody body = FlightGlobals.Bodies[bodyIndexes[i]];
				BodyFlux bodyFlux = new BodyFlux();
				bodyFlux.body = FlightGlobals.Bodies[i];
				bodyFlux.bodyIndex = body.flightGlobalsIndex;
				bodyFlux.direction = body.position - vesselPosition;
				bodyFlux.distance = bodyFlux.direction.magnitude;
				bodyFlux.direction /= bodyFlux.distance;

				bodyFlux.albedoFlux = bodiesAlbedoTemp[i] / subStepCountD;
				bodyFlux.emissiveFlux = bodiesEmissiveTemp[i] / subStepCountD;
				bodyFlux.coreFlux = bodiesCoreTemp[i] / subStepCountD;

				bodiesIrradianceAlbedo += bodyFlux.albedoFlux;
				bodiesIrradianceEmissive += bodyFlux.emissiveFlux;
				bodiesIrradianceCore += bodyFlux.coreFlux;

				bodyFluxes.Add(bodyFlux);
			}

			irradianceTotal = starsIrradiance + bodiesIrradianceAlbedo + bodiesIrradianceEmissive + bodiesIrradianceCore;

			UnityEngine.Profiling.Profiler.EndSample();

			// situation
			gravity = Math.Abs(FlightGlobals.getGeeForceAtPosition(vesselPosition, mainBody).magnitude * PhysicsGlobals.GraviticForceMultiplier) / PhysicsGlobals.GravitationalAcceleration;

			underwater = Sim.Underwater(Vessel);
            envStaticPressure = Sim.StaticPressureAtm(Vessel);
            inAtmosphere = Vessel.mainBody.atmosphere && Vessel.altitude < Vessel.mainBody.atmosphereDepth;
            inOxygenAtmosphere = Sim.InBreathableAtmosphere(Vessel, inAtmosphere, underwater);
            inBreathableAtmosphere = inOxygenAtmosphere && envStaticPressure > Settings.PressureThreshold;

            zeroG = !EnvLanded && !inAtmosphere;

			visibleBodies = Sim.GetLargeBodies(vesselPosition).ToArray();
            sunBodyAngle = Sim.SunBodyAngle(Vessel, vesselPosition, mainStar.body);

            // temperature at vessel position
            UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.EnvTemperature");
            temperature = Sim.VesselTemperature(irradianceTotal);
            tempDiff = Sim.TempDiff(temperature, Vessel.mainBody, EnvLanded);
            UnityEngine.Profiling.Profiler.EndSample();

            // radiation
            UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.EnvRadiation");
            gammaTransparency = Sim.GammaTransparency(Vessel.mainBody, Vessel.altitude);

            radiation = Radiation.Compute(this, vesselPosition, EnvGammaTransparency, mainStar.sunlightFactor,
	            out blackout, out bool new_magnetosphere, out bool new_innerBelt, out bool new_outerBelt, out interstellar);

            if (new_innerBelt != innerBelt || new_outerBelt != outerBelt || new_magnetosphere != magnetosphere)
            {
                innerBelt = new_innerBelt;
                outerBelt = new_outerBelt;
                magnetosphere = new_magnetosphere;
                if (IsSimulated) API.OnRadiationFieldChanged.Notify(Vessel, innerBelt, outerBelt, magnetosphere);
            }

            thermosphere = Sim.InsideThermosphere(Vessel);
            exosphere = Sim.InsideExosphere(Vessel);
            if (Storm.InProgress(this))
			{
				double sunActivity = Radiation.Info(mainStar.body).SolarActivity(false) / 2.0;
				stormRadiation = PreferencesRadiation.Instance.StormRadiation * mainStar.sunlightFactor * (sunActivity + 0.5);
			}
			else
			{
				stormRadiation = 0.0;
			}

			UnityEngine.Profiling.Profiler.EndSample();

			VesselSituations.Update();

            // other stuff
            gravioli = Sim.Graviolis(Vessel);
            UnityEngine.Profiling.Profiler.EndSample();
        }

		#endregion

		#region EVENTS

		private static List<PartData> transferredParts = new List<PartData>();

		/// <summary>
		/// Called from Callbacks, just after a part has been decoupled (undocking) or detached (usually a joint failure)
		/// At this point, the new Vessel object has been created by KSP and should be fully initialized.
		/// </summary>
		public static void OnDecoupleOrUndock(Vessel oldVessel, Vessel newVessel)
		{
			Lib.LogDebug("Decoupling vessel '{0}' from vessel '{1}'", Lib.LogLevel.Message, newVessel.vesselName, oldVessel.vesselName);

			if (!oldVessel.TryGetVesselDataTemp(out VesselData oldVD))
				return;

			if (newVessel.TryGetVesselData(out VesselData newVD))
			{
				Lib.LogDebugStack($"Decoupled/Undocked vessel {newVessel.vesselName} exists already, can't transfer partdatas !", Lib.LogLevel.Error);
				return;
			}

			transferredParts.Clear();
			foreach (Part part in newVessel.Parts)
			{
				// for all parts in the new vessel, move the corresponding partdata from the old vessel to the new vessel
				if (oldVD.VesselParts.TryGet(part.flightID, out PartData pd))
				{
					transferredParts.Add(pd);
					oldVD.VesselParts.Remove(pd);
				}
			}

			oldVD.OnVesselWasModified();

			newVD = new VesselData(newVessel, transferredParts);
			transferredParts.Clear();

			DB.AddNewVesselData(newVD);

			Lib.LogDebug($"Decoupling complete for new vessel, vd.partcount={newVD.Parts.Count}, v.partcount={newVessel.parts.Count} ({newVessel.vesselName})");
			Lib.LogDebug($"Decoupling complete for old vessel, vd.partcount={oldVD.Parts.Count}, v.partcount={oldVessel.parts.Count} ({oldVessel.vesselName})");
		}

		/// <summary>
		/// Called from Callbacks, just after a part has been coupled (docking, KIS attached part...)
		/// </summary>
		public static void OnPartCouple(GameEvents.FromToAction<Part, Part> data)
		{
			Lib.LogDebug($"Coupling part '{data.from.partInfo.title}' from vessel '{data.from.vessel.vesselName}' to vessel '{data.to.vessel.vesselName}'");

			Vessel fromVessel = data.from.vessel;
			Vessel toVessel = data.to.vessel;

			fromVessel.TryGetVesselDataTemp(out VesselData fromVD);
			toVessel.TryGetVesselDataTemp(out VesselData toVD);

			// GameEvents.onPartCouple may be fired by mods (KIS) that add new parts to an existing vessel
			// In the case of KIS, the part vessel is already set to the destination vessel when the event is fired
			// so we just add the part.
			if (fromVessel == toVessel)
			{
				if (!toVD.VesselParts.Contains(data.from.flightID))
				{
					toVD.VesselParts.Add(data.from);
					Lib.LogDebug("VesselData : newly created part '{0}' added to vessel '{1}'", Lib.LogLevel.Message, data.from.partInfo.title, data.to.vessel.vesselName);
				}
				return;
			}

			// transfer all partdata of the docking vessel to the docked to vessel
			toVD.VesselParts.TransferFrom(fromVD.VesselParts);

			// reset a few things on the docked to vessel
			toVD.OnVesselWasModified();

			Lib.LogDebug($"Coupling complete to   vessel, vd.partcount={toVD.Parts.Count}, v.partcount={toVessel.parts.Count} ({toVessel.vesselName})");
			Lib.LogDebug($"Coupling complete from vessel, vd.partcount={fromVD.Parts.Count}, v.partcount={fromVessel.parts.Count} ({fromVessel.vesselName})");
		}


		public static void OnPartWillDie(Part part)
		{
			if (!part.vessel.TryGetVesselDataTemp(out VesselData vd))
				return;

			vd.OnPartWillDie(part.flightID);

			vd.OnVesselWasModified();
			Lib.LogDebug($"Removing dead part, vd.partcount={vd.Parts.Count}, v.partcount={part.vessel.parts.Count} (part '{part.partInfo.title}' in vessel '{part.vessel.vesselName}')");
		}

		private void OnPartWillDie(uint flightId)
		{
			VesselParts[flightId].PartWillDie();
			VesselParts.Remove(flightId);
			OnVesselWasModified();
		}

		// note : we currently have no way of detecting 100% of cases 
		// where an unloaded vessel is destroyed,
		public void OnVesselWillDie()
		{
			if (!IsPersisted)
				return;

			resourceUpdateDelegates = null;
			VesselParts.OnAllPartsWillDie();
			CommHandler.ResetPartTransmitters();
		}

		public void OnVesselWasModified()
		{
			if (!IsSimulated)
				return;

			resourceUpdateDelegates = null;
			Synchronizer.Synchronize();
			CommHandler.ResetPartTransmitters();
			ResetReliabilityStatus();
			StateUpdate();

			Lib.LogDebug("VesselData updated on vessel modified event ({0})", Lib.LogLevel.Message, Vessel.vesselName);
		}

		#endregion
	}
} // KERBALISM
