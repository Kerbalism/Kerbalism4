using HarmonyLib;

namespace KERBALISM
{
	[HarmonyPatch(typeof(PartLoader))]
	[HarmonyPatch("ParsePart")]
	class PartLoader_ParsePart
	{
		static void Postfix(AvailablePart __result)
		{
			if (__result != null)
				PartVolumeAndSurface.EvaluatePrefabAtCompilation(__result);
		}
	}
}
