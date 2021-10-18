using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public class RecipeCategory : IComparable<RecipeCategory>
	{
		private static Dictionary<string, RecipeCategory> categoriesByName = new Dictionary<string, RecipeCategory>();
		private static List<RecipeCategory> categories = new List<RecipeCategory>();

		// EC generation
		public static RecipeCategory SolarPanel = new RecipeCategory(nameof(SolarPanel), Local.Brokers_SolarPanel, false, true);
		public static RecipeCategory FuelCell = new RecipeCategory(nameof(FuelCell), "Fuel cells", false, true);
		public static RecipeCategory RTG = new RecipeCategory(nameof(RTG), Local.Brokers_RTG, false, true);
		public static RecipeCategory NuclearGenerator = new RecipeCategory(nameof(NuclearGenerator), "Nuclear generators", false, true);
		public static RecipeCategory ECGenerator = new RecipeCategory(nameof(ECGenerator), "Electric generators", false, true);

		// Main vessel systems
		public static RecipeCategory Command = new RecipeCategory(nameof(Command), Local.Brokers_Command, false);
		public static RecipeCategory Comms = new RecipeCategory(nameof(Comms), "Communications", true);
		public static RecipeCategory LifeSupport = new RecipeCategory(nameof(LifeSupport), "Life support", false);
		public static RecipeCategory Pressure = new RecipeCategory(nameof(Pressure), "Pressure", false);
		public static RecipeCategory Comfort = new RecipeCategory(nameof(Comfort), "Comfort", false);
		public static RecipeCategory Centrifuge = new RecipeCategory(nameof(Centrifuge), "Centrifuges", false);
		public static RecipeCategory RadiationShield = new RecipeCategory(nameof(RadiationShield), "Radiation shields", false);

		// Science
		public static RecipeCategory Instrument = new RecipeCategory(nameof(Instrument), "Instruments", true);
		public static RecipeCategory ScienceData = new RecipeCategory(nameof(ScienceData), "Science data", false);
		public static RecipeCategory ScienceSample = new RecipeCategory(nameof(ScienceSample), "Science samples", false);
		public static RecipeCategory ScienceLab = new RecipeCategory(nameof(ScienceLab), Local.Brokers_ScienceLab, false);

		// ISRU and processes
		public static RecipeCategory Recycler = new RecipeCategory(nameof(Recycler), "Recycler", true);
		public static RecipeCategory FoodProduction = new RecipeCategory(nameof(FoodProduction), "Food production", false);
		public static RecipeCategory Harvester = new RecipeCategory(nameof(Harvester), Local.Brokers_Harvester, true);
		public static RecipeCategory Converter = new RecipeCategory(nameof(Converter), Local.Brokers_StockConverter, true);
		public static RecipeCategory ChemicalProcess = new RecipeCategory(nameof(ChemicalProcess), "Chemical process", true);
		public static RecipeCategory PropellantProduction = new RecipeCategory(nameof(PropellantProduction), "Propellant production", true);
		public static RecipeCategory Liquefaction = new RecipeCategory(nameof(Liquefaction), "Liquefaction", true);

		// Thermal management
		public static RecipeCategory ThermalControl = new RecipeCategory(nameof(ThermalControl), "Thermal control", true);
		public static RecipeCategory Radiators = new RecipeCategory(nameof(Radiators), "Radiators", false);
		public static RecipeCategory Boiloff = new RecipeCategory(nameof(Boiloff), Local.Brokers_Boiloff, false);

		// Other vessel systems
		public static RecipeCategory Wheels = new RecipeCategory(nameof(Wheels), "Wheels", false);
		public static RecipeCategory AttitudeControl = new RecipeCategory(nameof(AttitudeControl), "Attitude control", false);
		public static RecipeCategory Propulsion = new RecipeCategory(nameof(Propulsion), "Propulsion", false);
		public static RecipeCategory Light = new RecipeCategory(nameof(Light), Local.Brokers_Light, false);

		// generic categories
		public static RecipeCategory Unknown = new RecipeCategory(nameof(Unknown), "Unknown", false);
		public static RecipeCategory Environment = new RecipeCategory(nameof(Environment), "Environment", true);
		public static RecipeCategory VesselComponent = new RecipeCategory(nameof(VesselComponent), "Vessel components", true);
		public static RecipeCategory Deployement = new RecipeCategory(nameof(Deployement), "Deployement", true);
		public static RecipeCategory Others = new RecipeCategory(nameof(Others), "Others", true);

		public readonly string name;
		public readonly string title;
		public readonly bool ecProducer;
		public readonly bool expandByDefault;
		private readonly int sortOrder;

		private RecipeCategory(string name, string title = null, bool expandByDefault = false, bool isECProducer = false)
		{
			this.name = name;

			if (string.IsNullOrEmpty(title))
				title = name;

			this.title = title;
			this.ecProducer = isECProducer;
			this.expandByDefault = expandByDefault;

			sortOrder = categories.Count;

			categoriesByName.Add(name, this);
			categories.Add(this);
		}

		public static bool TryGet(string name, out RecipeCategory rc) => categoriesByName.TryGetValue(name, out rc);

		public static RecipeCategory GetOrCreate(string name)
		{
			if (categoriesByName.TryGetValue(name, out RecipeCategory rc))
				return rc;

			return new RecipeCategory(name);
		}

		public static RecipeCategory GetOrCreate(string name, string title = null, bool expandByDefault = false)
		{
			if (categoriesByName.TryGetValue(name, out RecipeCategory rc))
				return rc;

			return new RecipeCategory(name, title, expandByDefault);
		}

		public static IEnumerator<RecipeCategory> List()
		{
			return categories.GetEnumerator();
		}

		public int CompareTo(RecipeCategory other)
		{
			return sortOrder.CompareTo(other.sortOrder);
		}
	}
}
