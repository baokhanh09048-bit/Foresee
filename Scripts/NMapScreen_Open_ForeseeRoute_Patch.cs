using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace foresee.Scripts;

[HarmonyPatch(typeof(NMapScreen), "Open")]
public static class NMapScreen_Open_ForeseeRoute_Patch
{
	public static void Postfix(NMapScreen __instance)
	{
		MapRoutePlanner.Redraw(__instance);
	}
}
