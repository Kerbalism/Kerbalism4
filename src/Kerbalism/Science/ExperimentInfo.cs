using System;
using System.Collections.Generic;
using UnityEngine;
using System.Text;

namespace KERBALISM
{
	/// <summary>
	/// Stores information about an experiment_id or a subject_id
	/// Beware that subject information will be incomplete until the stock `ScienceSubject` is created in RnD
	/// </summary>
	public sealed class ExperimentInfo
	{
		public static StringBuilder ExpInfoSB = new StringBuilder();

		/// <summary> experiment definition </summary>
		private ScienceExperiment stockDef;

		/// <summary> experiment identifier </summary>
		public string ExperimentId { get; private set; }

		/// <summary> UI friendly name of the experiment </summary>
		public string Title { get; private set; }

		/// <summary> UI friendly name of the experiment </summary>
		public string Description { get; private set; }

		/// <summary> mass of a full sample (in tons) </summary>
		public double SampleMass { get; private set; }

		/// <summary> volume of a full sample (in liters) </summary>
		public double SampleVolume { get; private set; }

		public bool SampleCollecting { get; private set; }

		public Dictionary<AvailablePart, SampleStorageDefinition> sampleStorageParts = new Dictionary<AvailablePart, SampleStorageDefinition>();

		public BodyConditions ExpBodyConditions { get; private set; }

		/// <summary> size of a full file or sample</summary>
		public double DataSize { get; private set; }

		public bool IsSample { get; private set; }

		/// <summary> mass per data size of the sample (in tons) </summary>
		public double MassPerMB { get; private set; }

		/// <summary> volume per data size of the sample (in liters) </summary>
		public double VolumePerMB { get; private set; }

		public double DataScale => stockDef.dataScale;

		/// <summary> situation mask </summary>
		public uint SituationMask { get; private set; }

		/// <summary> stock ScienceExperiment situation mask </summary>
		public uint StockSituationMask => stockDef.situationMask;

		/// <summary> biome mask </summary>
		public uint BiomeMask { get; private set; }

		/// <summary> stock ScienceExperiment biome mask </summary>
		public uint StockBiomeMask => stockDef.biomeMask;

		/// <summary> virtual biomes mask </summary>
		public uint VirtualBiomeMask { get; private set; }

		public List<VirtualBiome> VirtualBiomes { get; private set; } = new List<VirtualBiome>();

		public double ScienceCap => stockDef.scienceCap * HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier;



		/// <summary> If true, subject completion will enable the stock resource map for the corresponding body</summary>
		public bool UnlockResourceSurvey { get; private set; }

		public bool IsROC => ROCDef != null;

		public ROCDefinition ROCDef { get; private set; }

		public bool HasDBSubjects { get; private set; }

		public bool IgnoreBodyRestrictions { get; private set; }

		/// <summary> List of experiments that will be collected automatically alongside this one</summary>
		public List<ExperimentInfo> IncludedExperiments { get; private set; } = new List<ExperimentInfo>();

		/// <summary> List of all different modules for which that experiment is available </summary>
		public ScienceModulesUIInfo ModulesUIInfo { get; private set; }

		private string[] includedExperimentsId;

		public ExperimentInfo(ScienceExperiment stockDef, ConfigNode kerbalismExperimentNode = null)
		{
			// if we have a custom "KERBALISM_EXPERIMENT" definition for the experiment, load it, else just use an empty node to avoid nullrefs
			if (kerbalismExperimentNode == null) kerbalismExperimentNode = new ConfigNode();

			this.stockDef = stockDef;
			ExperimentId = stockDef.id;
			ModulesUIInfo = new ScienceModulesUIInfo(this);

			// We have some custom handling for breaking ground ROC experiments.
			// ROC experiment id are always formatted with the ROC name after '_'.
			// This behavior is hardcoded in KSP code, so should be safe even in the eventuality of non-stock ROCs

			// temporary, test is this works
			if (Expansions.ExpansionsLoader.IsExpansionInstalled("Serenity") && ROCManager.Instance == null)
			{
				Lib.Log("ROCManager.Instance IS NULL, THIS DOESN'T WORK !!!!", Lib.LogLevel.Error);
			}


			if (ROCManager.Instance != null)
			{
				int rocDefIndex = ExperimentId.IndexOf('_') + 1; // will be 0 if not found
				if (rocDefIndex > 0 && rocDefIndex < ExperimentId.Length)
				{
					string rocType = ExperimentId.Substring(rocDefIndex);
					ROCDef = ROCManager.Instance.rocDefinitions.Find(p => p.type == rocType);
				}
			}

			if (IsROC)
				Title = "ROC: " + ROCDef.displayName; // group ROC together in the science archive (sorted by Title)
			else
				Title = stockDef.experimentTitle;

			Description = Lib.ConfigValue(kerbalismExperimentNode, "Description", string.Empty);

			// A new bool field was added in 1.7 for serenity : applyScienceScale
			// if not specified, the default is `true`, which is the case for all non-serenity science defs
			// serenity ground experiments and ROCs have applyScienceScale = false.
			// for ground experiment, baseValue = science generated per hour
			// for ROC experiments, it doesn't change anything because they are all configured with baseValue = scienceCap

			// Get the science points from the kerbalism node, and override the stock definition if it is set
			// This is primarily for simplification of the experiment definition process
			float sciencePoints = Lib.ConfigValue(kerbalismExperimentNode, "SciencePoints", 0f);
			if (sciencePoints > 0f)
			{
				if (stockDef.applyScienceScale) // non-serenity
					stockDef.baseValue = stockDef.scienceCap = sciencePoints;
				else
					stockDef.scienceCap = sciencePoints;
			}

			// Get the desired data size from the kerbalism node, and adjust data scale if it is set
			DataSize = Lib.ConfigValue(kerbalismExperimentNode, "DataSize", 0.0);
			if(DataSize > 0.0)
			{
				if (stockDef.applyScienceScale)
					stockDef.dataScale = (float)(DataSize / stockDef.baseValue);
				else
					stockDef.dataScale = (float)(DataSize / stockDef.scienceCap);
			}
			else
			{
				if (this.stockDef.applyScienceScale)
					DataSize = stockDef.baseValue * this.stockDef.dataScale;
				else
					DataSize = stockDef.scienceCap * this.stockDef.dataScale;
			}

			// make sure we don't produce NaN values down the line because of odd/wrong configs
			if (DataSize <= 0.0)
			{
				Lib.Log(ExperimentId + " has DataSize=" + DataSize + ", your configuration is broken!", Lib.LogLevel.Warning);
				DataSize = 1.0;
			}

			// load the included experiments ids in a string array, we will populate the list after 
			// all ExperimentInfos are created. (can't do it here as they may not exist yet)
			includedExperimentsId = kerbalismExperimentNode.GetValues("IncludeExperiment");

			UnlockResourceSurvey = Lib.ConfigValue(kerbalismExperimentNode, "UnlockResourceSurvey", false);

			SampleMass = Lib.ConfigValue(kerbalismExperimentNode, "SampleMass", 0.0);
			SampleVolume = Lib.ConfigValue(kerbalismExperimentNode, "SampleVolume", 0.0);
			SampleCollecting = Lib.ConfigValue(kerbalismExperimentNode, "SampleCollecting", false);

			IsSample = SampleMass > 0.0;
			if (IsSample)
			{
				MassPerMB = SampleMass / DataSize;
				VolumePerMB = SampleVolume / DataSize;
			}
			else
			{
				MassPerMB = 0.0;
				VolumePerMB = 0.0;
			}

			// Patch stock science def restrictions as BodyAllowed/BodyNotAllowed restrictions
			if (!(kerbalismExperimentNode.HasValue("BodyAllowed") || kerbalismExperimentNode.HasValue("BodyNotAllowed")))
			{
				if (IsROC)
				{
					// Parse the ROC definition name to find which body it's available on
					// This rely on the ROC definitions having the body name in the ExperimentId
					foreach (CelestialBody body in FlightGlobals.Bodies)
					{
						if (ExperimentId.IndexOf(body.name, StringComparison.OrdinalIgnoreCase) != -1)
						{
							kerbalismExperimentNode.AddValue("BodyAllowed", body.name);
							break;
						}
					}
				}

				// parse the stock atmosphere restrictions into our own
				if (stockDef.requireAtmosphere)
					kerbalismExperimentNode.AddValue("BodyAllowed", "Atmospheric");
				else if (stockDef.requireNoAtmosphere)
					kerbalismExperimentNode.AddValue("BodyNotAllowed", "Atmospheric");
			}

			ExpBodyConditions = new BodyConditions(kerbalismExperimentNode);

			foreach (string virtualBiomeStr in kerbalismExperimentNode.GetValues("VirtualBiome"))
			{
				if (Enum.IsDefined(typeof(VirtualBiome), virtualBiomeStr))
				{
					VirtualBiomes.Add((VirtualBiome)Enum.Parse(typeof(VirtualBiome), virtualBiomeStr));
				}
				else
				{
					Lib.Log("Experiment definition `{0}` has unknown VirtualBiome={1}", Lib.LogLevel.Warning, ExperimentId, virtualBiomeStr);
				}
			}

			IgnoreBodyRestrictions = Lib.ConfigValue(kerbalismExperimentNode, "IgnoreBodyRestrictions", false);

			uint situationMask = 0;
			uint biomeMask = 0;
			uint virtualBiomeMask = 0;
			// if defined, override stock situation / biome mask
			if (kerbalismExperimentNode.HasValue("Situation"))
			{
				foreach (string situation in kerbalismExperimentNode.GetValues("Situation"))
				{
					string[] sitAtBiome = situation.Split(new char[] { '@' }, StringSplitOptions.RemoveEmptyEntries);
					if (sitAtBiome.Length == 0 || sitAtBiome.Length > 2)
						continue;

					ScienceSituation scienceSituation = ScienceSituationUtils.ScienceSituationDeserialize(sitAtBiome[0]);

					if (scienceSituation != ScienceSituation.None)
					{
						situationMask += scienceSituation.BitValue();

						if (sitAtBiome.Length == 2)
						{
							if (sitAtBiome[1].Equals("Biomes", StringComparison.OrdinalIgnoreCase))
							{
								biomeMask += scienceSituation.BitValue();
							}
							else if (sitAtBiome[1].Equals("VirtualBiomes", StringComparison.OrdinalIgnoreCase) && VirtualBiomes.Count > 0)
							{
								virtualBiomeMask += scienceSituation.BitValue();
							}
						}
					}
					else
					{
						Lib.Log("Experiment definition `{0}` has unknown situation : `{1}`", Lib.LogLevel.Warning, ExperimentId, sitAtBiome[0]);
					}
				}
			}
			else
			{
				situationMask = stockDef.situationMask;
				biomeMask = stockDef.biomeMask;
			}

			if (situationMask == 0)
			{
				Lib.Log("Experiment definition `{0}` : `0` situationMask is unsupported, patching to `BodyGlobal`", Lib.LogLevel.Message, ExperimentId);
				situationMask = ScienceSituation.BodyGlobal.BitValue();
				HasDBSubjects = false;
			}
			else
			{
				HasDBSubjects = !Lib.ConfigValue(kerbalismExperimentNode, "IsGeneratingSubjects", false);
			}

			string error;
			uint stockSituationMask;
			uint stockBiomeMask;
			if (!ScienceSituationUtils.ValidateSituationBitMask(ref situationMask, biomeMask, out stockSituationMask, out stockBiomeMask, out error))
			{
				Lib.Log("Experiment definition `{0}` is incorrect :\n{1}", Lib.LogLevel.Error, ExperimentId, error);
			}

			SituationMask = situationMask;
			BiomeMask = biomeMask;
			VirtualBiomeMask = virtualBiomeMask;
			stockDef.situationMask = stockSituationMask;
			stockDef.biomeMask = stockBiomeMask;
		}

		public void ParseIncludedExperiments()
		{
			foreach (string expId in includedExperimentsId)
			{
				ExperimentInfo includedInfo = ScienceDB.GetExperimentInfo(expId);
				if (includedInfo == null)
				{
					Lib.Log($"Experiment `{ExperimentId}` define a IncludedExperiment `{expId}`, but that experiment doesn't exist", Lib.LogLevel.Warning);
					continue;
				}

				// early prevent duplicated entries
				if (includedInfo.ExperimentId == ExperimentId || IncludedExperiments.Contains(includedInfo))
					continue;

				IncludedExperiments.Add(includedInfo);
			}
		}

		public static void CheckIncludedExperimentsRecursion(ExperimentInfo expInfoToCheck, List<ExperimentInfo> chainedExperiments)
		{
			List<ExperimentInfo> loopedExperiments = new List<ExperimentInfo>();
			foreach (ExperimentInfo includedExp in expInfoToCheck.IncludedExperiments)
			{
				if (chainedExperiments.Contains(includedExp))
				{
					loopedExperiments.Add(includedExp);
				}

				chainedExperiments.Add(includedExp);
			}

			foreach (ExperimentInfo loopedExperiment in loopedExperiments)
			{
				expInfoToCheck.IncludedExperiments.Remove(loopedExperiment);
				Lib.Log($"IncludedExperiment `{loopedExperiment.ExperimentId}` in experiment `{expInfoToCheck.ExperimentId}` would result in an infinite loop in the chain and has been removed", Lib.LogLevel.Warning);
			}

			foreach (ExperimentInfo includedExp in expInfoToCheck.IncludedExperiments)
			{
				CheckIncludedExperimentsRecursion(includedExp, chainedExperiments);
			}
		}

		public static void GetIncludedExperimentTitles(ExperimentInfo expinfo, List<string> includedExperiments)
		{
			foreach (ExperimentInfo includedExpinfo in expinfo.IncludedExperiments)
			{
				includedExperiments.Add(includedExpinfo.Title);
				GetIncludedExperimentTitles(includedExpinfo, includedExperiments);
			}
			includedExperiments.Sort((x, y) => x.CompareTo(y));
		}

		/// <summary> UI friendly list of situations available for the experiment</summary>
		public List<string> AvailableSituations()
		{
			List<string> result = new List<string>();

			foreach (ScienceSituation situation in ScienceSituationUtils.validSituations)
			{
				if (situation.IsAvailableForExperiment(this))
				{
					if (situation.IsBodyBiomesRelevantForExperiment(this))
					{
						result.Add(Lib.BuildString(situation.Title(), " ", Local.Situation_biomes));//(biomes)"
					}
					else if (situation.IsVirtualBiomesRelevantForExperiment(this))
					{
						foreach (VirtualBiome biome in VirtualBiomes)
						{
							result.Add(Lib.BuildString(situation.Title(), " (", biome.Title(), ")"));
						}
					}
					else
					{
						result.Add(situation.Title());
					}
				}
			}

			return result;
		}

		public class BodyConditions
		{
			private static string typeNamePlus = typeof(BodyConditions).FullName + "+";

			public bool HasConditions { get; private set; }
			private List<BodyCondition> bodiesAllowed = new List<BodyCondition>();
			private List<BodyCondition> bodiesNotAllowed = new List<BodyCondition>();

			public BodyConditions(ConfigNode node)
			{
				foreach (string allowed in node.GetValues("BodyAllowed"))
				{
					BodyCondition bodyCondition = ParseCondition(allowed);
					if (bodyCondition != null)
						bodiesAllowed.Add(bodyCondition);
				}

				foreach (string notAllowed in node.GetValues("BodyNotAllowed"))
				{
					BodyCondition bodyCondition = ParseCondition(notAllowed);
					if (bodyCondition != null)
						bodiesNotAllowed.Add(bodyCondition);
				}

				HasConditions = bodiesAllowed.Count > 0 || bodiesNotAllowed.Count > 0;
			}

			private BodyCondition ParseCondition(string condition)
			{
				Type type = Type.GetType(typeNamePlus + condition);
				if (type != null)
				{
					return (BodyCondition)Activator.CreateInstance(type);
				}
				else
				{
					foreach (CelestialBody body in FlightGlobals.Bodies)
						if (body.name.Equals(condition, StringComparison.OrdinalIgnoreCase))
							return new SpecificBody(body.name);
				}
				Lib.Log("Invalid BodyCondition : '" + condition + "' defined in KERBALISM_EXPERIMENT node.");
				return null;
			}

			public bool IsBodyAllowed(CelestialBody body)
			{
				bool isAllowed;

				if (bodiesAllowed.Count > 0)
				{
					isAllowed = false;
					foreach (BodyCondition bodyCondition in bodiesAllowed)
						isAllowed |= bodyCondition.TestCondition(body);
				}
				else
				{
					isAllowed = true;
				}

				foreach (BodyCondition bodyCondition in bodiesNotAllowed)
					isAllowed &= !bodyCondition.TestCondition(body);

				return isAllowed;
			}

			public string ConditionsToString()
			{
				ExpInfoSB.Length = 0;

				if (bodiesAllowed.Count > 0)
				{
					ExpInfoSB.Append(Lib.Color(Local.Experimentinfo_Bodiesallowed + "\n", Lib.Kolor.Cyan, true));//Bodies allowed:
					for (int i = bodiesAllowed.Count - 1; i >= 0; i--)
					{
						ExpInfoSB.Append(bodiesAllowed[i].Title);
						if (i > 0) ExpInfoSB.Append(", ");
					}

					if (bodiesNotAllowed.Count > 0)
						ExpInfoSB.Append("\n");
				}

				if (bodiesNotAllowed.Count > 0)
				{
					ExpInfoSB.Append(Lib.Color(Local.Experimentinfo_Bodiesnotallowed + "\n", Lib.Kolor.Cyan, true));//Bodies not allowed:
					for (int i = bodiesNotAllowed.Count - 1; i >= 0; i--)
					{
						ExpInfoSB.Append(bodiesNotAllowed[i].Title);
						if (i > 0) ExpInfoSB.Append(", ");
					}
				}

				return ExpInfoSB.ToString();
			}

			private abstract class BodyCondition
			{
				public abstract bool TestCondition(CelestialBody body);
				public abstract string Title { get; }
			}

			private class Atmospheric : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => body.atmosphere;
				public override string Title => Local.Experimentinfo_BodyCondition1;//"atmospheric"
			}

			private class NonAtmospheric : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => !body.atmosphere;
				public override string Title => Local.Experimentinfo_BodyCondition2;//"non-atmospheric"
			}

			private class Gaseous : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => body.hasSolidSurface;
				public override string Title => Local.Experimentinfo_BodyCondition3;//"gaseous"
			}

			private class Solid : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => !body.hasSolidSurface;
				public override string Title => Local.Experimentinfo_BodyCondition4;//"solid"
			}

			private class Oceanic : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => body.ocean;
				public override string Title => Local.Experimentinfo_BodyCondition5;//"oceanic"
			}

			private class HomeBody : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => body.isHomeWorld;
				public override string Title => Local.Experimentinfo_BodyCondition6;//"home body"
			}

			private class HomeBodyAndMoons : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => body.isHomeWorld || body.referenceBody.isHomeWorld;
				public override string Title => Local.Experimentinfo_BodyCondition7;//"home body and its moons"
			}

			private class Planets : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => !Sim.IsStar(body) && Sim.IsStar(body.referenceBody);
				public override string Title => Local.Experimentinfo_BodyCondition8;//"planets"
			}

			private class Moons : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => !Sim.IsStar(body) && !Sim.IsStar(body.referenceBody);
				public override string Title => Local.Experimentinfo_BodyCondition9;//"moons"
			}

			private class Suns : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => Sim.IsStar(body);
				public override string Title => Local.Experimentinfo_BodyCondition10;//"suns"
			}

			private class SpecificBody : BodyCondition
			{
				private string bodyName;
				public override bool TestCondition(CelestialBody body) => body.name == bodyName;
				public override string Title => string.Empty;
				public SpecificBody(string bodyName) { this.bodyName = bodyName; }
			}
		}
	}



} // KERBALISM

