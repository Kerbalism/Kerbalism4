using Harmony;
using KSP.UI.Screens;
using KSP.UI.TooltipTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM.Events
{
	// patch the kerbal tooltip in the administration building lists
	[HarmonyPatch(typeof(TooltipController_CrewAC))]
	[HarmonyPatch("SetTooltip")]
	public class TooltipController_CrewAC_SetTooltip
	{
		static StringBuilder sb = new StringBuilder(256);

		static void Postfix(TooltipController_CrewAC __instance, ProtoCrewMember pcm)
		{
			KerbalData kd = DB.GetOrCreateKerbalData(pcm);
			sb.Length = 0;

			foreach (KerbalRule rule in kd.rules)
			{
				if (rule.Definition.resetOnRecovery)
					continue;

				sb.Append(Lib.BuildString("<b>Career ", rule.Definition.title, "</b>: ", Lib.HumanReadablePerc(rule.Level), "\n"));
			}

			if (sb.Length > 0)
			{
				__instance.descriptionString += Lib.BuildString("\n\n", sb.ToString());
			}
		}
	}

	public class GameEventsUI
	{
		public static GameEventsUI Instance { get; private set; }
		public static bool UIVisible => Instance.uiVisible;
		public bool uiVisible;

		public GameEventsUI()
		{
			Instance = this;
		}

		public void AddEditorCategory()
		{
			if (PartLoader.LoadedPartsList.Find(k => k.tags.IndexOf("_kerbalism", StringComparison.Ordinal) >= 0) != null)
			{
				RUI.Icons.Selectable.Icon icon = new RUI.Icons.Selectable.Icon("Kerbalism", Textures.category_normal, Textures.category_selected);
				PartCategorizer.Category category = PartCategorizer.Instance.filters.Find(k => string.Equals(k.button.categoryName, "filter by function", StringComparison.OrdinalIgnoreCase));
				PartCategorizer.AddCustomSubcategoryFilter(category, "Kerbalism", "Kerbalism", icon, k => k.tags.IndexOf("_kerbalism", StringComparison.Ordinal) >= 0);
			}
		}

	}
}
