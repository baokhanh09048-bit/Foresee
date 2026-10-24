using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace foresee.Scripts;

[HarmonyPatch(typeof(NMapScreen), "OnClearMapDrawingButtonPressed")]
public static class NMapScreen_ClearMapDrawingButton_ForeseeRoute_Patch
{
	public static bool Prefix(NMapScreen __instance)
	{
		MapRoutePlanner.ClearMarks(__instance);
		return false;
	}
}
