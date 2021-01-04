using KSP.Localization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KERBALISM
{
	public static class KerbalismLateLoading
	{
		public static void DoLoading()
		{
			// What we need to do here :
			// 1. Set B9PS modules subtypes description
			// 2. Create auto-generated KsmModuleDefinitions (drives)
			// 3. Do some extra parsing on KsmModuleDefinitions (experiments)
			// 4. Generate the ScienceDB module infos (KsmModuleExperiment, B9 subtypes, stock/mod experiment modules...)
			// 5. Set the AvailablePart.ModuleInfos for KsmPartModules

			// The plan :
			// - We already have everything for 1.
			// - The IPrefabPostCompilation interface is probably the right way to do 2/3
			// - Keep 4 separate. Altough the concept of a generalized system for browsing all definitions is appealing,
			//   we don't really have a use case outside of the science archive. And it would require extending definitions
			//   to be available on foreign modules, wich add a lot of complexity.
			// - 5 is ready, but we probably need to do it last.

			// The editor part tweaks are bothering me quite a bit. The name thing is causing various issues, it's a bad hack,
			// and could just be solved be having some coherent naming scheme 
			// And icon size tweaking isn't that much important.


			foreach (KsmModuleDefinition definition in KsmModuleDefinitionLibrary.Definitions)
			{
				if (definition is IKsmModuleDefinitionLateInit lateInitDefinition)
				{
					lateInitDefinition.OnLateInit();
				}
			}

			foreach (AvailablePart ap in PartLoader.LoadedPartsList)
			{
				foreach (PartModule modulePrefab in ap.partPrefab.Modules)
				{
					if (modulePrefab is KsmPartModule ksmModule)
					{
						// Due to KSP mess with EVA kerbals (it create the additional DLC EVA parts by duplicating the modules), the handlers
						// will have lost their reference. We fix that here, but there might be other side effects, so as a general rule, don't
						// rely on our modules being correctly initialized on the EVA prefabs.
						if (ksmModule.ModuleHandler.PrefabModuleBase == null)
						{
							ksmModule.ModuleHandler.SetModuleReferences(ksmModule, ksmModule);
						}

						if (ksmModule.ModuleHandler is IKsmModuleHandlerLateInit lateInitHandler)
						{
							lateInitHandler.OnLatePrefabInit(ap);
						}
					}
					else if (modulePrefab.moduleName == B9PartSwitch.moduleName)
					{
						SetupB9Subtypes(ap, modulePrefab);
					}

					ScienceModulesUIInfo.AddModuleInfo(modulePrefab);
				}

				SetupModuleInfos(ap);
			}

			foreach (ExperimentInfo expInfo in ScienceDB.ExperimentInfos)
			{
				expInfo.ModulesUIInfo.CompileInfo();
			}
		}

		private static void SetupB9Subtypes(AvailablePart ap, PartModule b9psModule)
		{
			foreach (B9PartSwitch.SubtypeWrapper subtype in B9PartSwitch.GetSubtypes(b9psModule))
			{
				foreach (B9PartSwitch.ModuleModifierWrapper moduleModifier in subtype.ModuleModifiers)
				{
					if (moduleModifier.DataNode == null || !(moduleModifier.PartModule is KsmPartModule switchedModule) || !(switchedModule.ModuleHandler is IB9Switchable switchedHandler))
						continue;

					bool switchModuleEnabled = true;
					moduleModifier.DataNode.TryGetValue(nameof(KsmPartModule.switchModuleEnabled), ref switchModuleEnabled);

					if (switchModuleEnabled)
					{
						KsmModuleDefinition switchedDefinition;
						string definitionName = moduleModifier.DataNode.GetValue(nameof(KsmPartModule.definition));
						if (!string.IsNullOrEmpty(definitionName))
						{
							string defaultDefinition = switchedModule.definition;
							switchedModule.definition = definitionName;
							switchedDefinition = KsmModuleDefinitionLibrary.GetDefinition(switchedModule);
							switchedModule.definition = defaultDefinition;
						}
						else
						{
							switchedDefinition = KsmModuleDefinitionLibrary.GetDefinition(switchedModule);
						}

						string description = switchedHandler.GetSubtypeDescription(switchedDefinition, subtype.TechRequired);

						if (!string.IsNullOrEmpty(description))
						{
							subtype.SetSubTypeDescriptionDetail(description);

							if (switchedDefinition is ExperimentDefinition switchedExperimentDefinition)
							{
								ScienceModulesUIInfo.AddSubtypeModuleInfo(ap.partPrefab, subtype, switchedExperimentDefinition);
							}
						}
					}
				}
			}
		}

		// During prefab compilation, all our KsmPartModule that are supposed to have an editor part toltip widget
		// have set the the widget text to a string with a specific format, ex : "KsmInfoIdx@4@ModuleKsmHabitat"
		// by parsing this string, we find the corresponding module on the prefab and set the real descripation of the module
		// This allow to use things in the description that aren't available at prefab compilation : bodies, science, tech tree...
		private static void SetupModuleInfos(AvailablePart ap)
		{
			for (int i = ap.moduleInfos.Count - 1; i >= 0; i--)
			{
				AvailablePart.ModuleInfo moduleInfo = ap.moduleInfos[i];
				if (ap.moduleInfos[i].info.StartsWith(KsmPartModule.AvailablePartKsmModuleInfo))
				{
					string[] moduleRefInfo = ap.moduleInfos[i].info.Split('@');
					if (moduleRefInfo.Length != 3
						|| !int.TryParse(moduleRefInfo[1], out int moduleIdx)
						|| moduleIdx >= ap.partPrefab.Modules.Count
						|| ap.partPrefab.Modules[moduleIdx].GetType().Name != moduleRefInfo[2])
					{
						Lib.Log($"Error parsing a module part tooltip info `{ap.moduleInfos[i].info}` on AvailablePart {ap.name}", Lib.LogLevel.Warning);
						ap.moduleInfos.RemoveAt(i);
						continue;
					}

					ap.moduleInfos[i].info = ap.partPrefab.Modules[moduleIdx].GetInfo();
					ap.moduleInfos[i].moduleDisplayName = ap.partPrefab.Modules[moduleIdx].GetModuleDisplayName();
					if (string.IsNullOrEmpty(ap.moduleInfos[i].info))
					{
						ap.moduleInfos.RemoveAt(i);
					}
				}
			}
		}

		#region EDITOR PART LIST TWEAKS

		private static Dictionary<string, PartListTweak> GetPartListTweaks()
		{
			Dictionary<string, PartListTweak> tweaks = new Dictionary<string, PartListTweak>();

			tweaks.Add("kerbalism-container-inline-prosemian-full-0625", new PartListTweak(0, 0.6f));
			tweaks.Add("kerbalism-container-inline-prosemian-full-125", new PartListTweak(1, 0.85f));
			tweaks.Add("kerbalism-container-inline-prosemian-full-250", new PartListTweak(2, 1.1f));
			tweaks.Add("kerbalism-container-inline-prosemian-full-375", new PartListTweak(3, 1.33f));

			tweaks.Add("kerbalism-container-inline-prosemian-half-125", new PartListTweak(10, 0.85f));
			tweaks.Add("kerbalism-container-inline-prosemian-half-250", new PartListTweak(11, 1.1f));
			tweaks.Add("kerbalism-container-inline-prosemian-half-375", new PartListTweak(12, 1.33f));

			tweaks.Add("kerbalism-container-radial-box-prosemian-small", new PartListTweak(20, 0.6f));
			tweaks.Add("kerbalism-container-radial-box-prosemian-normal", new PartListTweak(21, 0.85f));
			tweaks.Add("kerbalism-container-radial-box-prosemian-large", new PartListTweak(22, 1.1f));

			tweaks.Add("kerbalism-container-radial-pressurized-prosemian-small", new PartListTweak(30, 0.6f));
			tweaks.Add("kerbalism-container-radial-pressurized-prosemian-medium", new PartListTweak(31, 0.85f));
			tweaks.Add("kerbalism-container-radial-pressurized-prosemian-big", new PartListTweak(32, 1.1f));
			tweaks.Add("kerbalism-container-radial-pressurized-prosemian-huge", new PartListTweak(33, 1.33f));

			tweaks.Add("kerbalism-solenoid-short-small", new PartListTweak(40, 0.85f));
			tweaks.Add("kerbalism-solenoid-long-small", new PartListTweak(41, 0.85f));
			tweaks.Add("kerbalism-solenoid-short-large", new PartListTweak(42, 1.33f));
			tweaks.Add("kerbalism-solenoid-long-large", new PartListTweak(43, 1.33f));

			tweaks.Add("kerbalism-greenhouse", new PartListTweak(50));
			tweaks.Add("kerbalism-gravityring", new PartListTweak(51));
			tweaks.Add("kerbalism-activeshield", new PartListTweak(52));
			tweaks.Add("kerbalism-chemicalplant", new PartListTweak(53));

			tweaks.Add("kerbalism-experiment-beep", new PartListTweak(60));
			tweaks.Add("kerbalism-experiment-ding", new PartListTweak(61));
			tweaks.Add("kerbalism-experiment-tick", new PartListTweak(62));
			tweaks.Add("kerbalism-experiment-wing", new PartListTweak(63));
			tweaks.Add("kerbalism-experiment-curve", new PartListTweak(64));

			return tweaks;
		}

		private class PartListTweak
		{
			private int listOrder;
			private float iconScale;

			public PartListTweak(float iconScale)
			{
				listOrder = -1;
				this.iconScale = iconScale;
			}

			public PartListTweak(int listOrder, float iconScale = 1f)
			{
				this.listOrder = listOrder;
				this.iconScale = iconScale;
			}

			public void Apply(AvailablePart ap)
			{
				if (iconScale != 1f)
				{
					ap.iconPrefab.transform.GetChild(0).localScale *= iconScale;
					ap.iconScale *= iconScale;
				}

				if (listOrder >= 0)
				{
					ap.title = Lib.BuildString("<size=1><color=#00000000>" + listOrder.ToString("000") + "</color></size>", ap.title);
				}
			}
		}

		#endregion


	}
}
