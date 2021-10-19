using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;



namespace KERBALISM
{
    public enum UnlinkedCtrl
    {
        none,     // disable all controls
        limited,  // disable all controls except full/zero throttle and staging
        full      // do not disable controls at all
    }

	public static class Settings
	{
		public const string NODENAME_RESOURCE_HVL = "RESOURCE_HVL";

		private class ModToCheck
		{
			public const string NODENAME = "MOD_CHECK";

			private const string requiredText = "Missing required mod dependency";
			private const string incompatibleText = "Incompatible mod detected";
			private const string warningText = "Mod with limited compatibility detected";

			public enum ModCompatibility { None, Required, Incompatible, Warning, WarningScience }
			public ModCompatibility compatibility;
			public ErrorManager.Error error;
			public string modName;

			public static ModToCheck Get(ConfigNode node)
			{
				ModToCheck mod = new ModToCheck();
				mod.modName = Lib.ConfigValue(node, "name", string.Empty);
				mod.compatibility = Lib.ConfigEnum(node, "modCompatibility", ModCompatibility.None);
				if (mod.modName.Length == 0 || mod.compatibility == ModCompatibility.None)
					return null;

				string comment = Lib.ConfigValue(node, "comment", string.Empty);

				switch (mod.compatibility)
				{
					case ModCompatibility.Required:
						mod.error = new ErrorManager.Error(true, $"{requiredText} : {mod.modName}", comment);
						break;
					case ModCompatibility.Incompatible:
						mod.error = new ErrorManager.Error(true, $"{incompatibleText} : {mod.modName}", comment);
						break;
					case ModCompatibility.Warning:
					case ModCompatibility.WarningScience:
						if (comment.Length == 0)
						{
							comment = "This mod has some issues running alongside Kerbalism, please consult the mod compatibility page on the Github wiki";
						}

						mod.error = new ErrorManager.Error(false, $"{warningText} : {mod.modName}", comment);
						break;
				}

				return mod;
			}
		}

		private static List<ModToCheck> modsRequired = new List<ModToCheck>();
		private static List<ModToCheck> modsIncompatible = new List<ModToCheck>();

		public static void ParseTime()
		{
			var kerbalismConfigNodes = GameDatabase.Instance.GetConfigs("KERBALISM_SETTINGS");
			if (kerbalismConfigNodes.Length < 1) return;
			ConfigNode cfg = kerbalismConfigNodes[0].config;

			// time in configs
			ConfigsHoursInDays = Lib.ConfigValue(cfg, "ConfigsHoursInDays", 6.0);
			ConfigsDaysInYear = Lib.ConfigValue(cfg, "ConfigsDaysInYear", 426.0);

			ConfigsSecondsInDays = ConfigsHoursInDays * 3600.0;
			ConfigsSecondsInYear = ConfigsDaysInYear * ConfigsHoursInDays * 3600.0;

			ConfigsDurationMultiplier = Lib.ConfigValue(cfg, "ConfigsTimeMultiplier", 1.0);
		}

		public static void Parse()
		{
			var kerbalismConfigNodes = GameDatabase.Instance.GetConfigs("KERBALISM_SETTINGS");
			if (kerbalismConfigNodes.Length < 1) return;
			ConfigNode node = kerbalismConfigNodes[0].config;

			CFGValue.ParseStatic(typeof(Settings), node);

			DepressuriationDefaultDurationValue = Lib.ConfigDuration(node, "DepressuriationDefaultDuration", false, "5m");

			// radiation configs are in rad/h, convert to rad/s
			StormRadiation /= 3600f;
			ExternRadiation /= 3600f;

			if (!PartResourceLibrary.Instance.resourceDefinitions.Contains(HabitatAtmoResource))
				ErrorManager.AddError(true, "KERBALISM_SETTINGS parsing error", $"HabitatAtmoResource {HabitatAtmoResource} doesn't exists");
			else
				HabitatAtmoResourceId = PartResourceLibrary.Instance.GetDefinition(HabitatAtmoResource).id;

			if (!PartResourceLibrary.Instance.resourceDefinitions.Contains(HabitatWasteResource))
				ErrorManager.AddError(true, "KERBALISM_SETTINGS parsing error", $"HabitatWasteResource {HabitatWasteResource} doesn't exists");
			else
				HabitatWasteResourceId = PartResourceLibrary.Instance.GetDefinition(HabitatWasteResource).id;

			if (!PartResourceLibrary.Instance.resourceDefinitions.Contains(HabitatBreathableResource))
				ErrorManager.AddError(true, "KERBALISM_SETTINGS parsing error", $"HabitatBreathableResource {HabitatBreathableResource} doesn't exists");
			else
				HabitatBreathableResourceId = PartResourceLibrary.Instance.GetDefinition(HabitatBreathableResource).id;

			foreach (ConfigNode modNode in node.GetNodes(ModToCheck.NODENAME))
			{
				ModToCheck mod = ModToCheck.Get(modNode);
				if (mod != null)
				{
					if (mod.compatibility == ModToCheck.ModCompatibility.Required)
						modsRequired.Add(mod);
					else
						modsIncompatible.Add(mod);
				}
			}
		}

		public static void CheckMods()
		{
			List<string> loadedModsAndAssemblies = new List<string>();

			string[] directories = Directory.GetDirectories(KSPUtil.ApplicationRootPath + "GameData");
			for (int i = 0; i < directories.Length; i++)
			{
				loadedModsAndAssemblies.Add(new DirectoryInfo(directories[i]).Name);
			}

			foreach (var a in AssemblyLoader.loadedAssemblies)
			{
				loadedModsAndAssemblies.Add(a.name);
			}

			foreach (ModToCheck mod in modsRequired)
			{
				if (!loadedModsAndAssemblies.Exists(p => string.Equals(p, mod.modName, StringComparison.OrdinalIgnoreCase)))
					ErrorManager.AddError(mod.error);
			}

			foreach (ModToCheck mod in modsIncompatible)
			{
				if (loadedModsAndAssemblies.Exists(p => string.Equals(p, mod.modName, StringComparison.OrdinalIgnoreCase)))
					ErrorManager.AddError(mod.error);
			}
		}

		// time

		/// <summary>used when parsing duration fields in configs. Doesn't affect the "displayed" calendar, only relevant for configs</summary>
		public static double ConfigsHoursInDays = 6.0;
		/// <summary>used when parsing duration fields in configs. Doesn't affect the "displayed" calendar, only relevant for configs</summary>
		public static double ConfigsDaysInYear = 426.0;
		/// <summary>multiplier applied to all config defined duraton fields (experiments, reliability...)</summary>
		public static double ConfigsDurationMultiplier = 1.0;

		/// <summary>
		/// if true, the ingame displayed time will use the calendar as determined by the home body rotation period and it's orbit rotation period.
		/// if false, the values from the "kerbin time" / "earth time" KSP main menu setting will be used.
		/// </summary>
		[CFGValue] public static bool UseHomeBodyCalendar = true;

		// convenience values (not config defined)
		public static double ConfigsSecondsInDays;
		public static double ConfigsSecondsInYear;

		// habitat

		/// <summary>pressure / EVA suit volume in liters, used for determining CO2 poisoning level while kerbals are in a depressurized habitat</summary>
		[CFGValue] public static double PressureSuitVolume = 100.0;
		/// <summary>resource used to manage habitat pressure</summary>
		[CFGValue] public static string HabitatAtmoResource = "KsmAtmosphere";
		/// <summary>resource used to manage habitat CO2 level (poisoning)</summary>
		[CFGValue] public static string HabitatWasteResource = "KsmWasteAtmosphere";
		/// <summary>resource automagically produced when the habitat is under breathable external conditions (Oxygen in the default profile)</summary>
		[CFGValue] public static string HabitatBreathableResource = "Oxygen";
		/// <summary> per second, per kerbal production of the breathable resource. Should match the consumption defined in the breathing rule. Set it to 0 to disable it entirely.</summary>
		[CFGValue] public static double HabitatBreathableResourceRate = 0.00172379825;
		/// <summary>duration / m3 of habitat volume</summary>
		[CFGValue] public static double DepressuriationDefaultDuration = 60.0 * 60.0 * 5.0;

		/// <summary>
		/// below that threshold, the vessel will be considered under non-survivable pressure and kerbals will put their helmets.
		/// also determine the altitude at which non-pressurized habitats can use the external air.
		/// note that while ingame we display hab pressure as % with no unit, 100 % = 1 atm = 101.325 kPa for all internal calculations
		/// </summary>
		[CFGValue] public static double PressureThreshold = 0.3;

		/// <summary>seconds / m3 of habitat volume</summary>
		public static double DepressuriationDefaultDurationValue;
		/// <summary>resource used to manage habitat pressure</summary>
		public static int HabitatAtmoResourceId;
		/// <summary>resource used to manage habitat CO2 level (poisoning)</summary>
		public static int HabitatWasteResourceId;
		/// <summary>resource automagically produced when the habitat is under breathable external conditions (Oxygen in the default profile)</summary>
		public static int HabitatBreathableResourceId;

		// radiation

		/// <summary>Default 0.004 - Constant wall thickness (m) of all parts  used for radiation occlusion</summary>
		[CFGValue] public static double WallThicknessForOcclusion = PartRadiationData.PART_WALL_THICKNESS_OCCLUSION;
		/// <summary>
		/// Default 0.02 - Constant wall thickness (m) used to determine the part structural mass that will be considered for occlusion.
		/// Separate from the occlusion thickness to account for KSP unrealistic part densities, can be set lower on a SMURFF/RO game.
		/// </summary>
		[CFGValue] public static double WallThicknessForMassFraction = PartRadiationData.PART_WALL_THICKNESS_MASSFRACTION;

		// signal

		/// <summary>available control for unlinked vessels: 'none', 'limited' or 'full'</summary>
		[CFGValue] public static UnlinkedCtrl UnlinkedControl = UnlinkedCtrl.none;
		/// <summary>as long as there is a control connection, the science data rate will never go below this</summary>
		[CFGValue] public static double DataRateMinimumBitsPerSecond = 1.0;
		/// <summary>transmission rate for surface experiments (Serenity DLC)</summary>
		[CFGValue] public static float DataRateSurfaceExperiment = 0.3f;
		/// <summary>how much of the configured EC rate is used while transmitter is active</summary>
		[CFGValue] public static double TransmitterActiveEcFactor = 1.5;
		/// <summary>how much of the configured EC rate is used while transmitter is passive</summary>
		[CFGValue] public static double TransmitterPassiveEcFactor = 0.04;
		/// <summary>
		/// Kerbalism will calculate a damping exponent to achieve good data communication rates (see log file, search for DataRateDampingExponent).
		/// If the calculated value is not good for you, you can set your own.
		/// </summary>
		[CFGValue] public static double DampingExponentOverride = 0.0;

		// science

		/// <summary>keep showing the stock science dialog</summary>
		[CFGValue] public static bool ScienceDialog = true;
		/// <summary>
		/// When taking an asteroid sample, mass (in t) per MB of sample (baseValue * dataScale).
		/// default of 0.00002 => 34 Kg in stock
		/// </summary>
		[CFGValue] public static double AsteroidSampleMassPerMB = 0.00002;

		// misc

		/// <summary>use less particles to render the magnetic fields</summary>
		[CFGValue] public static bool LowQualityRendering = false;
		/// <summary>detect and avoid issues at high timewarp in external modules</summary>
		[CFGValue] public static bool EnforceResourceCoherency = true;
		/// <summary>EC/s cost if eva headlamps are on</summary>
		[CFGValue] public static double HeadLampsECCost = 0.002;
		/// <summary>% of ec consumed on hibernating probes (ModuleCommand.hibernationMultiplier is ignored by Kerbalism)</summary>
		[CFGValue] public static double HibernatingEcFactor = 0.001;
		/// <summary></summary>
		[CFGValue] public static bool EnableOrbitLineTweaks = true;

		// presets for save game preferences

		[CFGValue] public static float StormFrequency = 0.4f;
		[CFGValue] public static int StormDurationHours = 2;
		[CFGValue] public static float StormEjectionSpeed = 0.33f;
		/// <summary>solar storm radiation. Config value in rad/h, instance value in rad/s</summary>
		[CFGValue] public static float StormRadiation = 5f;
		/// <summary>background radiation. Config value in rad/h, instance value in rad/s</summary>
		[CFGValue] public static float ExternRadiation = 0.04f;
		/// <summary>use sievert instead of rad</summary>
		[CFGValue] public static bool RadiationInSievert = false;

		// debug / logging

		[CFGValue] public static bool VolumeAndSurfaceLogging = false;
		[CFGValue] public static bool LogProcessesMassConservationInfo = false;
	}

}
