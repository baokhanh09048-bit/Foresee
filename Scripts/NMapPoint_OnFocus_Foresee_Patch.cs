using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace foresee.Scripts;

[HarmonyPatch(typeof(NMapPoint), "OnFocus")]
public static class NMapPoint_OnFocus_Foresee_Patch
{
	public static void Postfix(NMapPoint __instance)
	{
		MapRoutePlanner.SetHoveredPoint(__instance);
		MapForesee.Show(__instance);
	}
}
