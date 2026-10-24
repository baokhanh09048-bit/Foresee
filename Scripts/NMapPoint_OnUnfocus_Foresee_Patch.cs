using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace foresee.Scripts;

[HarmonyPatch(typeof(NMapPoint), "OnUnfocus")]
public static class NMapPoint_OnUnfocus_Foresee_Patch
{
	public static void Postfix(NMapPoint __instance)
	{
		MapRoutePlanner.ClearHoveredPoint(__instance);
		MapForesee.Hide(__instance);
	}
}
