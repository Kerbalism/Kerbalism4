using Expansions.Serenity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static KERBALISM.ExperimentRequirements;

namespace KERBALISM
{
	/// <summary>
	/// Hold the description for a science module (can be ours, stock or from another mod). For UI / presentation prupose only
	/// </summary>
	public sealed class ScienceModulesUIInfo
	{
		private class KerbalismVariantInfo
		{
			public ExperimentDefinition definition;
			public List<string> availableOnParts = new List<string>();

			public KerbalismVariantInfo(ExperimentDefinition definition)
			{
				this.definition = definition;
			}
		}

		private static StringBuilder sb = new StringBuilder();
		private static StringBuilder sb2 = new StringBuilder();

		public ExperimentInfo ExpInfo { get; private set; }

		/// <summary> List of all module definitions, and the parts on which they are used. Warning : can be very long</summary>
		public string Info { get; private set; }

		/// <summary> List of all tech nodes where the experiment is available</summary>
		private List<string> availableAtTechs = new List<string>(2);

		// temporary cache used for assembling the final Info string
		private List<KerbalismVariantInfo> variantsInfo = new List<KerbalismVariantInfo>(2);
		private List<string> availableOnParts = new List<string>(2);

		public ScienceModulesUIInfo(ExperimentInfo expInfo)
		{
			ExpInfo = expInfo;
		}


		public void CompileInfo()
		{
			sb.Clear();

			// if no variant, there is no kerbalism module for that experiment
			if (variantsInfo.Count == 0)
			{
				// if no info already written, there is no stock/mod module that we know of for that experiment
				if (string.IsNullOrEmpty(Info))
				{
					// asteroid/comet samples are special cases
					if (ExpInfo.ExperimentId == "asteroidSample" || ExpInfo.ExperimentId.StartsWith("cometSample_", StringComparison.Ordinal))
					{
						sb.Append(CompileInfoAsteroidSample());
					}
					// ROCs should have been picked when we parsed the stock robotic arms, but just in case we double check here
					else if (ExpInfo.IsROC)
					{
						sb.Append(CompileInfoROC());
					}
					// otherwise, default to some basic information
					else
					{
						sb.Append(CompileInfoDefault());
					}
				}
				else
				{
					sb.Append(Info);
				}

				// Add the list of parts that have that experiment
				if (availableOnParts.Count > 0)
				{
					sb.AppendKSPNewLine();
					sb.Append(AvailableOnPartsInfo(availableOnParts));
				}
			}
			// if we have variants, there is at least one ModuleKsmExperiment using that experiment
			else
			{
				// sort the variants, putting the most common first
				variantsInfo.Sort((x, y) => x.availableOnParts.Count.CompareTo(y.availableOnParts.Count));

				for (int i = 0; i < variantsInfo.Count; i++)
				{
					// first variant show the full detailed information about the module, and the experiment
					if (i == 0)
					{
						sb.Append(variantsInfo[i].definition.ModuleDescription(true));
					}
					// subsequent variants only show the module-specific information
					else
					{
						sb.AppendKSPNewLine();
						sb.AppendKSPLine(Lib.Color("Variant :", Lib.Kolor.Yellow, true));
						sb.Append(variantsInfo[i].definition.ModuleDescription(false));
					}

					// build the part list
					if (variantsInfo[i].availableOnParts.Count > 0)
					{
						sb.AppendKSPNewLine();
						sb.Append(AvailableOnPartsInfo(variantsInfo[i].availableOnParts));
					}
				}

				// If we also have a stock module using that experiment, add it at the end
				if (!string.IsNullOrEmpty(Info))
				{
					sb.AppendKSPNewLine();
					sb.AppendKSPLine(Lib.Color("Variant :", Lib.Kolor.Yellow, true));
					sb.Append(Info);

					if (availableOnParts.Count > 0)
					{
						sb.AppendKSPNewLine();
						sb.Append(AvailableOnPartsInfo(availableOnParts));
					}
				}
			}

			Info = sb.ToString();

			// Clear the temporary objects
			variantsInfo = null;
			availableOnParts = null;
		}

		/// <summary> return true if <br/>
		/// - a part containing a module having that defintion is unlocked at the provided tech <br/>
		/// - this definition is available at any tech level
		/// </summary>
		public bool IsAvailableAtTech(string techNode)
		{
			if (availableAtTechs.Count == 0)
				return true;

			return availableAtTechs.Contains(techNode);
		}

		public bool IsResearched()
		{
			if (availableAtTechs.Count == 0)
				return true;

			foreach (string tech in availableAtTechs)
			{
				if (ResearchAndDevelopment.GetTechnologyState(tech) == RDTech.State.Available)
				{
					return true;
				}
			}

			return false;
		}

		private static void AddAvailableOnPart(List<string> partTitles, Part part)
		{
			if (part.partInfo.TechHidden || part.partInfo.category == PartCategories.none || string.IsNullOrEmpty(part.partInfo.title))
				return;

			partTitles.Add(Lib.RemoveTags(part.partInfo.title));
		}

		private string AvailableOnPartsInfo(List<string> partTitles)
		{
			if (partTitles.Count == 0)
				return string.Empty;

			sb2.Clear();
			sb2.Append(Lib.Color("Available on part(s) :", Lib.Kolor.Cyan, true));
			foreach (string title in partTitles)
				sb2.Append(Lib.BuildString("\n• ", Lib.Ellipsis(title, 30)));

			return sb2.ToString();
		}

		public void AddAvailableAtTech(string techRequired)
		{
			if (!string.IsNullOrEmpty(techRequired) && !availableAtTechs.Contains(techRequired))
				availableAtTechs.Add(techRequired);
		}

		public static void AddModuleInfo(PartModule modulePrefab)
		{
			if (modulePrefab is ModuleKsmExperiment ksmExperiment && ksmExperiment.Definition.ExpInfo != null)
			{
				ScienceModulesUIInfo uiInfo = ksmExperiment.Definition.ExpInfo.ModulesUIInfo;
				uiInfo.AddAvailableAtTech(ksmExperiment.part.partInfo.TechRequired);
				KerbalismVariantInfo variantInfo = uiInfo.variantsInfo.Find(p => p.definition == ksmExperiment.Definition);

				if (variantInfo != null)
				{
					AddAvailableOnPart(variantInfo.availableOnParts, modulePrefab.part);
				}
				else
				{
					variantInfo = new KerbalismVariantInfo(ksmExperiment.Definition);
					AddAvailableOnPart(variantInfo.availableOnParts, modulePrefab.part);
					uiInfo.variantsInfo.Add(variantInfo);
				}
			}
			else if (modulePrefab is ModuleScienceExperiment stockExpModule)
			{
				ExperimentInfo expInfo = ScienceDB.GetExperimentInfo(stockExpModule.experimentID);
				if (expInfo != null)
				{
					ScienceModulesUIInfo uiInfo = expInfo.ModulesUIInfo;
					uiInfo.AddAvailableAtTech(stockExpModule.part.partInfo.TechRequired);
					AddAvailableOnPart(uiInfo.availableOnParts, modulePrefab.part);
					if (uiInfo.Info == null)
						uiInfo.Info = uiInfo.CompileInfoForModule(stockExpModule);
				}
			}
			else if (modulePrefab is ModuleGroundExperiment groundExpModule)
			{
				ExperimentInfo expInfo = ScienceDB.GetExperimentInfo(groundExpModule.experimentId);
				if (expInfo != null)
				{
					ScienceModulesUIInfo uiInfo = expInfo.ModulesUIInfo;
					uiInfo.AddAvailableAtTech(groundExpModule.part.partInfo.TechRequired);
					AddAvailableOnPart(uiInfo.availableOnParts, modulePrefab.part);
					if (uiInfo.Info == null)
						uiInfo.Info = uiInfo.CompileInfoForModule(groundExpModule);
				}
			}
			else if (modulePrefab is ModuleRobotArmScanner armScanner)
			{
				foreach (ExperimentInfo expInfo in ScienceDB.ExperimentInfos)
				{
					if (!expInfo.IsROC)
						continue;

					ScienceModulesUIInfo uiInfo = expInfo.ModulesUIInfo;
					AddAvailableOnPart(uiInfo.availableOnParts, modulePrefab.part);

					// small ROCS can be taken on EVA at any tech level
					if (!expInfo.ROCDef.smallRoc)
						uiInfo.AddAvailableAtTech(armScanner.part.partInfo.TechRequired);

					if (uiInfo.Info == null)
						uiInfo.Info = uiInfo.CompileInfoROC();
				}
			}
		}

		public static void AddSubtypeModuleInfo(Part part, B9PartSwitch.SubtypeWrapper subtype, ExperimentDefinition moduleDefinition)
		{
			if (moduleDefinition.ExpInfo == null)
				return;

			ScienceModulesUIInfo uiInfo = moduleDefinition.ExpInfo.ModulesUIInfo;

			if (!string.IsNullOrEmpty(subtype.TechRequired))
				uiInfo.AddAvailableAtTech(subtype.TechRequired);
			else
				uiInfo.AddAvailableAtTech(part.partInfo.TechRequired);

			KerbalismVariantInfo variantInfo = uiInfo.variantsInfo.Find(p => p.definition == moduleDefinition);

			if (variantInfo != null)
			{
				AddAvailableOnPart(variantInfo.availableOnParts, part);
			}
			else
			{
				variantInfo = new KerbalismVariantInfo(moduleDefinition);
				AddAvailableOnPart(variantInfo.availableOnParts, part);
				uiInfo.variantsInfo.Add(variantInfo);
			}
		}

		private string CompileInfoForModule(ModuleScienceExperiment module)
		{
			sb2.Clear();
			sb2.AppendInfo("Base value", Lib.HumanReadableScience(ExpInfo.ScienceCap, true, true));
			sb2.AppendInfo(Local.Experimentinfo_Datasize, Lib.HumanReadableDataSize(ExpInfo.DataSize));

			if (module.xmitDataScalar < Science.maxXmitDataScalarForSample)
			{
				sb2.AppendKSPNewLine();
				sb2.AppendKSPLine(Local.Experimentinfo_generatesample);//Will generate a sample.
				sb2.AppendInfo(Local.Experimentinfo_Samplesize, Lib.HumanReadableSampleSize(ExpInfo.DataSize));
			}

			sb2.AppendKSPNewLine();
			sb2.AppendKSPLine(Lib.Color(Local.Module_Experiment_Specifics_Situations, Lib.Kolor.Cyan, true));//"Situations:"

			foreach (string s in ExpInfo.AvailableSituations())
				sb2.AppendList(Lib.Bold(s));

			sb2.AppendKSPNewLine();
			sb2.Append(module.GetInfo());

			return sb2.ToString();
		}

		private string CompileInfoForModule(ModuleGroundExperiment module)
		{
			sb2.Clear();
			sb2.AppendInfo("Base value", Lib.HumanReadableScience(ExpInfo.ScienceCap, true, true));
			sb2.AppendInfo(Local.Experimentinfo_Datasize, Lib.HumanReadableDataSize(ExpInfo.DataSize));
			sb2.AppendKSPNewLine();
			sb2.Append(module.GetInfo());
			return sb2.ToString();
		}

		private string CompileInfoAsteroidSample()
		{
			sb2.Clear();
			sb2.AppendInfo("Base value", Lib.HumanReadableScience(ExpInfo.ScienceCap, true, true));
			sb2.AppendKSPNewLine();
			sb2.AppendKSPLine(Local.Experimentinfo_Asteroid);//"Asteroid samples can be taken by kerbals on EVA"
			sb2.AppendKSPNewLine();
			sb2.AppendInfo(Local.Experimentinfo_Samplesize, Lib.HumanReadableSampleSize(ExpInfo.DataSize));
			sb2.AppendInfo(Local.Experimentinfo_Samplemass, Lib.HumanReadableMass(ExpInfo.DataSize * Settings.AsteroidSampleMassPerMB));
			return sb2.ToString();
		}

		private string CompileInfoROC()
		{
			sb2.Clear();
			sb2.AppendKSPLine(Lib.Color(ExpInfo.ROCDef.displayName, Lib.Kolor.Cyan, true));
			sb2.AppendKSPNewLine();
			sb2.AppendInfo("Base value", Lib.HumanReadableScience(ExpInfo.ScienceCap, true, true));

			sb2.AppendKSPLine("- " + Local.Experimentinfo_scannerarm);//Analyse with a scanner arm
			sb2.AppendInfo("  " + Local.Experimentinfo_Datasize, Lib.HumanReadableDataSize(ExpInfo.DataSize));

			if (ExpInfo.ROCDef.smallRoc)
			{
				sb2.AppendKSPLine("- " + Local.Experimentinfo_smallRoc);//Collectable on EVA as a sample"
				sb2.AppendInfo("  " + Local.Experimentinfo_Samplesize, Lib.HumanReadableSampleSize(ExpInfo.DataSize));
			}
			else
			{
				sb2.AppendKSPLine("- " + Local.Experimentinfo_smallRoc2); //Can't be collected on EVA
			}

			foreach (RocCBDefinition rocBody in ExpInfo.ROCDef.myCelestialBodies)
			{
				CelestialBody body = FlightGlobals.GetBodyByName(rocBody.name);
				sb2.AppendKSPNewLine();
				sb2.AppendKSPLine(Lib.Color(Local.Experimentinfo_smallRoc3.Format(body.displayName), Lib.Kolor.Cyan, true));//"Found on <<1>>'s :"
				foreach (string biome in rocBody.biomes)
				{
					sb2.AppendList(ScienceUtil.GetBiomedisplayName(body, biome));
				}
			}

			return sb2.ToString();
		}

		private string CompileInfoDefault()
		{
			sb2.Clear();
			sb2.AppendInfo("Base value", Lib.HumanReadableScience(ExpInfo.ScienceCap, true, true));
			sb2.AppendInfo(Local.Experimentinfo_Datasize, Lib.HumanReadableDataSize(ExpInfo.DataSize));

			sb2.AppendKSPNewLine();
			sb2.AppendKSPLine(Lib.Color(Local.Module_Experiment_Specifics_Situations, Lib.Kolor.Cyan, true));//"Situations:"

			foreach (string s in ExpInfo.AvailableSituations())
				sb2.AppendList(Lib.Bold(s));

			// evaScience is the 1.11+ new EVA experiment with the kerbal animations. We don't patch it, and it won't be recognized because we ignore
			if (ExpInfo.ExperimentId != "evaScience")
			{
				sb2.AppendKSPNewLine();
				sb2.Append("This experiment is unused or is from a mod that isn't recognized by Kerbalism.");
			}

			return sb2.ToString();
		}
	}
}
