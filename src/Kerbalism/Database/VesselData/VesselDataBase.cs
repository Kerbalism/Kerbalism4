using Flee.PublicTypes;
using System;
using System.Collections.Generic;

namespace KERBALISM
{
	// note : this class should ideally be abstract, but we need 
	// an instance of it to compile the Flee modifiers.
	public partial class VesselDataBase
	{
		public const string NODENAME_VESSEL = "KERBALISM_VESSEL";
		public const string NODENAME_MODULE = "MODULES";

		public static VesselDataBase ExpressionBuilderInstance { get; private set; } = new VesselDataBase();

		#region BASE FIELDS/PROPERTIES

		public ExpressionContext ModifierContext { get; private set; }

		public VesselProcesses VesselProcesses => vesselProcesses; VesselProcesses vesselProcesses;

		/// <summary>habitat info</summary>
		public VesselHabitat Habitat => habitatData; VesselHabitat habitatData;

		public List<KerbalData> Crew { get; private set; } = new List<KerbalData>();

		// the following is to provide Flee modifiers access, it isn't functionally needed otherwise
		private PreferencesReliability PrefReliability => PreferencesReliability.Instance;
		private PreferencesScience PrefScience => PreferencesScience.Instance;
		private PreferencesComfort PrefComfort => PreferencesComfort.Instance;
		private PreferencesRadiation PrefRadiation => PreferencesRadiation.Instance;

		public override string ToString() => VesselName;

		#endregion

		#region VIRTUAL PROPERTIES

		public virtual bool LoadedOrEditor => true;

		public virtual bool IsEVA => false;

		public virtual string VesselName => string.Empty;

		public virtual bool IsPersistent => true;

		public virtual PartDataCollectionBase Parts { get; }

		public virtual VesselResHandler ResHandler { get; }

		public virtual IConnectionInfo ConnectionInfo { get; }

		public virtual bool DeviceTransmit { get; set; }

		public virtual CelestialBody MainBody { get; }

		public VesselSituations VesselSituations { get; protected set; }

		public VesselComms vesselComms;

		/// <summary>in meters</summary>
		public virtual double Altitude { get; }

		/// <summary>in degree</summary>
		public virtual double Latitude { get; }

		/// <summary>in degree</summary>
		public virtual double Longitude { get; }

		/// <summary>in gees</summary>
		public virtual double Gravity { get; }

		/// <summary>in rad/s</summary>
		public virtual double AngularVelocity { get; }

		/// <summary>number of crew on the vessel, excluding rescue, dead or otherwise disabled crew members </summary>
		public virtual int RulesEnabledCrewCount { get; }

		/// <summary>number of crew on the vessel</summary>
		public virtual int CrewCount { get; }

		/// <summary>crew capacity of the vessel</summary>
		public virtual int CrewCapacity { get; }

		/// <summary> [environment] true if inside ocean</summary>
		public virtual bool EnvUnderwater { get; }

		/// <summary> [environment] true if on the surface of a body</summary>
		public virtual bool EnvLanded { get; }

		/// <summary> current atmospheric pressure in atm</summary>
		public virtual double EnvStaticPressure { get; }

		/// <summary> Is the vessel inside an atmosphere ?</summary>
		public virtual bool EnvInAtmosphere { get; }

		/// <summary> Is the vessel inside a breatheable atmosphere ?</summary>
		public virtual bool EnvInOxygenAtmosphere { get; }

		/// <summary> Is the vessel inside a breatheable atmosphere and at acceptable pressure conditions ?</summary>
		public virtual bool EnvInBreathableAtmosphere { get; }

		/// <summary> [environment] true if in zero g</summary>
		public virtual bool EnvZeroG { get; }

		/// <summary> [environment] temperature ar vessel position</summary>
		public virtual double EnvTemperature  { get; }

		/// <summary> [environment] difference between environment temperature and survival temperature</summary>// 
		public virtual double EnvTempDiff  { get; }

		/// <summary> [environment] radiation at vessel position</summary>
		public virtual double EnvRadiation  { get; }

		/// <summary> [environment] radiation from the inner belt</summary>
		public virtual double EnvRadiationInnerBelt { get; }

		/// <summary> [environment] radiation from the outer belt</summary>
		public virtual double EnvRadiationOuterBelt { get; }

		/// <summary> [environment] radiation from the magnetopause</summary>
		public virtual double EnvRadiationMagnetopause { get; }

		/// <summary> [environment] radiation from the surface</summary>
		public virtual double EnvRadiationBodies { get; }

		/// <summary> [environment] radiation from the sun(s)</summary>
		public virtual double EnvRadiationSolar { get; }

		/// <summary> [environment] true if vessel is inside a magnetopause (except the heliosphere)</summary>
		public virtual bool EnvMagnetosphere { get; }

		/// <summary> [environment] true if vessel is inside a radiation belt</summary>
		public virtual bool EnvInnerBelt { get; }

		/// <summary> [environment] true if vessel is inside a radiation belt</summary>
		public virtual bool EnvOuterBelt { get; }

		public bool EnvStorm => EnvStormRadiation > 0.0;

		public virtual double EnvStormRadiation { get; }

		/// <summary> [environment] proportion of ionizing radiation not blocked by atmosphere</summary>
		public virtual double EnvGammaTransparency  { get; }

		public virtual double SolarPanelsAverageExposure => 0.0;

		/// <summary> total irradiance from all sources (W/m²) at vessel position</summary>
		public double IrradianceTotal => irradianceTotal; protected double irradianceTotal;

		/// <summary> star(s) irradiance (W/m²) reflected by the nearest body (and it's parent planet if it's a moon)</summary>
		public double IrradianceAlbedo => irradianceAlbedo; protected double irradianceAlbedo;

		/// <summary> thermal irradiance (W/m²) from the nearest body (and it's parent planet if it's a moon), induced by the star(s) heating effect on the body </summary>
		public double IrradianceBodiesEmissive => irradianceBodiesEmissive; protected double irradianceBodiesEmissive;

		/// <summary> thermal irradiance (W/m²) from the nearest body (and it's parent planet if it's a moon), induced by the body own intrinsic sources </summary>
		public double IrradianceBodiesCore => irradianceBodiesCore; protected double irradianceBodiesCore;

		/// <summary> direct star(s) irradiance (W/m²) from all stars at vessel position, include atmospheric absorption if inside an atmosphere </summary>
		public double IrradianceStarTotal => irradianceStarTotal; protected double irradianceStarTotal;

		/// <summary> List of all stars/suns and the related data/calculations for the current vessel</summary>
		public StarFlux[] StarsIrradiance => starsIrradiance; protected StarFlux[] starsIrradiance;

		/// <summary> Star that send the highest nominal flux (in W/m²) at the vessel position (ignoring occlusion / atmo absorbtion)</summary>
		public StarFlux MainStar => mainStar; protected StarFlux mainStar;

		/// <summary> Nomalized direction vector to the main star</summary>
		public Vector3d MainStarDirection => MainStar.direction;

		/// <summary> % of time spent in the main star direct light (for the current environment update)</summary>
		public double MainStarSunlightFactor => MainStar.sunlightFactor;

		/// <summary> True if at least half of the current update was spent in the direct light of the main star</summary>
		public bool InSunlight => MainStar.sunlightFactor > 0.45;

		/// <summary> True if less than 10% of the current update was spent in the direct light of the main star</summary>
		public bool InFullShadow => MainStar.sunlightFactor < 0.1;

		

		#endregion

		#region LIFECYCLE

		public VesselDataBase()
		{
			ModifierContext = new ExpressionContext(this);
			ModifierContext.Options.CaseSensitive = true;
			ModifierContext.Options.ParseCulture = System.Globalization.CultureInfo.InvariantCulture;
			ModifierContext.Imports.AddType(typeof(Math));

			vesselProcesses = new VesselProcesses();
			habitatData = new VesselHabitat();
			starsIrradiance = StarFlux.StarArrayFactory();
			vesselComms = new VesselComms();
		}

		// put here the persistence that is common to VesselData and VesselDataShip to have
		// it transfered when creating a vessel from a shipconstruct (ie, from editor to flight)
		public void Load(ConfigNode vesselDataNode, bool isNewVessel)
		{
			VesselProcesses.Load(vesselDataNode);

			DeviceTransmit = Lib.ConfigValue(vesselDataNode, nameof(DeviceTransmit), true);

			if (!isNewVessel)
			{
				OnLoad(vesselDataNode);
			}
		}

		public void Save(ConfigNode node)
		{
			if (!IsPersistent)
				return;

			ConfigNode vesselNode = new ConfigNode(NODENAME_VESSEL);
			OnSave(vesselNode);
			VesselProcesses.Save(vesselNode);
			Parts.Save(vesselNode);

			node.AddValue(nameof(DeviceTransmit), DeviceTransmit);

			node.AddNode(vesselNode);
		}

		// This is overridden in VesselData for vessel <--> vessel persistence.
		// It can't be used in VesselDataShip, as we don't call it when instantiating
		// a vessel from a shipconstruct
		protected virtual void OnLoad(ConfigNode node) { }
		protected virtual void OnSave(ConfigNode node) { }

		// LoadShipConstruct is a constructor for VesselDataShip, and is responsible for
		// instantiating the PartData/ModuleData objects. This differs a lot from the flight
		// VesselData objects instantiation / loading, so while the data structure is the same
		// the handling is completely different. Ideally, we should use common methods but the
		// hacky nature of forcing our data into the stock ShipConstruct persistence, as well
		// as the difficulty of keeping our editor data synchronized severly limit the options.
		public static void LoadShipConstruct(ShipConstruct ship, ConfigNode vesselDataNode, bool isNewShip)
		{
			ModuleHandler.ActivationContext context = Lib.IsEditor ? ModuleHandler.ActivationContext.Editor : ModuleHandler.ActivationContext.Loaded;

			Lib.LogDebug($"Loading VesselDataShip for shipconstruct {ship.shipName}");

			List<PartData> thisShipParts = new List<PartData>(ship.parts.Count);

			if (vesselDataNode != null)
			{
				// we don't want to overwrite VesselData when loading a subassembly or when merging.
				if (isNewShip)
					VesselDataShip.Instance = new VesselDataShip();

				// we need to instantiate the PartDatas before Load() is called
				foreach (Part part in ship.parts)
				{
					PartData partData = new PartData(VesselDataShip.Instance, part);
					VesselDataShip.ShipParts.Add(partData);
					thisShipParts.Add(partData);
				}

				VesselDataShip.Instance.Parts.Load(vesselDataNode);

				if (isNewShip)
				{
					VesselDataShip.Instance.Load(vesselDataNode, false);
				}
			}
			else
			{
				foreach (Part part in ship.parts)
				{
					PartData partData = new PartData(VesselDataShip.Instance, part);
					VesselDataShip.ShipParts.Add(partData);
					thisShipParts.Add(partData);
				}
			}

			// instantiate all ModuleData for the ship, loading ModuleData if available.
			// Note that we always prevent flightId affection even when this is called to create a new vessel.
			// FlightId affectation will be done when the VesselDataShip is converted to a VesselData through
			// the ShipConstruction.AssembleForLaunch() patch.
			// We can't do it here because we need to check the uniqueness of newly created flightIds, which isn't possible
			// at this point because the existing vessels aren't loaded yet when LoadShipConstruct() is called.
			foreach (PartData partData in thisShipParts)
			{
				for (int i = 0; i < partData.LoadedPart.Modules.Count; i++)
				{
					ModuleHandler.SetupForLoadedModule(partData, partData.LoadedPart.Modules[i], i, context);
				}
			}

			// Firstsetup / start everything, but only in the editor. Flight vessel will be started by the VesselData ctor
			if (Lib.IsEditor)
			{
				if (isNewShip)
				{
					VesselDataShip.Instance.Start(); 
				}
				else
				{
					foreach (PartData partData in thisShipParts)
					{
						partData.FirstSetup();
						partData.Start();
					}
				}
			}
		}

		#endregion

		#region EVALUATION

		public void ModuleDataUpdate()
		{

		}

		public void ProcessSimStep(SimStep step)
		{
			irradianceBodiesCore = step.bodiesCoreIrradiance;

			double directRawFluxTotal = 0.0;
			irradianceStarTotal = 0.0;
			mainStar = starsIrradiance[0];
			irradianceAlbedo = 0.0;
			irradianceBodiesEmissive = 0.0;

			for (int i = 0; i < starsIrradiance.Length; i++)
			{
				StarFlux starFlux = starsIrradiance[i];
				StarFlux stepStarFlux = step.starFluxes[i];

				starFlux.direction = stepStarFlux.direction;
				starFlux.distance = stepStarFlux.distance;
				starFlux.directFlux = stepStarFlux.directFlux;
				starFlux.directRawFlux = stepStarFlux.directRawFlux;
				starFlux.bodiesAlbedoFlux = stepStarFlux.bodiesAlbedoFlux;
				starFlux.bodiesEmissiveFlux = stepStarFlux.bodiesEmissiveFlux;
				starFlux.mainBodyVesselStarAngle = stepStarFlux.mainBodyVesselStarAngle;

				starFlux.mainBodyVesselStarAngle = stepStarFlux.mainBodyVesselStarAngle;
				starFlux.sunAndBodyFaceSkinTemp = stepStarFlux.sunAndBodyFaceSkinTemp;
				starFlux.bodiesFaceSkinTemp = stepStarFlux.bodiesFaceSkinTemp;
				starFlux.sunFaceSkinTemp = stepStarFlux.sunFaceSkinTemp;
				starFlux.darkFaceSkinTemp = stepStarFlux.darkFaceSkinTemp;
				starFlux.skinIrradiance = stepStarFlux.skinIrradiance;
				starFlux.skinRadiosity = stepStarFlux.skinRadiosity;

				irradianceStarTotal += stepStarFlux.directFlux;
				directRawFluxTotal += stepStarFlux.directRawFlux;
				irradianceAlbedo += stepStarFlux.bodiesAlbedoFlux;
				irradianceBodiesEmissive += stepStarFlux.bodiesEmissiveFlux;

				starFlux.sunlightFactor = starFlux.directFlux > 0.0 ? 1.0 : 0.0;

				if (mainStar.directFlux < starFlux.directFlux)
					mainStar = starFlux;
			}

			foreach (StarFlux vesselStarFlux in starsIrradiance)
			{
				vesselStarFlux.directRawFluxProportion = vesselStarFlux.directRawFlux / directRawFluxTotal;
			}

			irradianceTotal = irradianceStarTotal + irradianceAlbedo + irradianceBodiesEmissive + irradianceBodiesCore;
		}

		#endregion
	}
}
