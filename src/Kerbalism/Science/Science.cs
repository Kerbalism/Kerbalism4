using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{

	public static class Science
	{
		public static bool GameHasRnD
		{
			get
			{
				switch (HighLogic.CurrentGame.Mode)
				{
					case Game.Modes.CAREER:
					case Game.Modes.SCIENCE_SANDBOX:
						return true;
					default:
						return false;
				}
			}
		}

		/// <summary> When converting stock experiments / data, if the XmitDataScalar is less than this, the data will be converted as a sample.</summary>
		public const float maxXmitDataScalarForSample = 0.001f;

		// science points from transmission won't be credited until they reach this amount
		public const double minCreditBuffer = 0.1;

		// a subject will be completed (gamevent fired and popup shown) when there is less than this value to retrieve in RnD
		// this is needed because of floating point imprecisions in the in-flight science count (due to a gazillion adds of very small values)
		public const double scienceLeftForSubjectCompleted = 0.01;


		// pseudo-ctor
		public static void Init()
		{
			if (!Features.Science)
				return;

			// Add our hijacker to the science dialog prefab
			GameObject prefab = AssetBase.GetPrefab("ScienceResultsDialog");
			if (Settings.ScienceDialog)
				prefab.gameObject.AddOrGetComponent<Hijacker>();
			else
				prefab.gameObject.AddOrGetComponent<MiniHijacker>();
		}

		// return module acting as container of an experiment
		public static IScienceDataContainer Container(Part p, string experiment_id)
		{
			// first try to get a stock experiment module with the right experiment id
			// - this support parts with multiple experiment modules, like eva kerbal
			foreach (ModuleScienceExperiment exp in p.FindModulesImplementing<ModuleScienceExperiment>())
			{
				if (exp.experimentID == experiment_id) return exp;
			}

			// if none was found, default to the first module implementing the science data container interface
			// - this support third-party modules that implement IScienceDataContainer, but don't derive from ModuleScienceExperiment
			return p.FindModuleImplementing<IScienceDataContainer>();
		}

		/// <summary>
		/// Return the result description (Experiment definition RESULTS node) for the subject_id.
		/// Same as the stock ResearchAndDevelopment.GetResults(subject_id) but can be forced to return a non-randomized result
		/// </summary>
		/// <param name="randomized">If true the result can be different each this is called</param>
		/// <param name="useGenericIfNotFound">If true, a generic text will be returned if no RESULTS{} definition exists</param>
		public static string SubjectResultDescription(string subject_id, bool useGenericIfNotFound = true)
		{
			string result = ResearchAndDevelopment.GetResults(subject_id);
			if (result == null) result = string.Empty;
			if (result == string.Empty && useGenericIfNotFound)
			{
				result = Lib.TextVariant(
					  Local.SciencresultText1,//"Our researchers will jump on it right now"
					  Local.SciencresultText2,//"This cause some excitement"
					  Local.SciencresultText3,//"These results are causing a brouhaha in R&D"
					  Local.SciencresultText4,//"Our scientists look very confused"
					  Local.SciencresultText5);//"The scientists won't believe these readings"
			}
			return result;
		}
	}

} // KERBALISM

