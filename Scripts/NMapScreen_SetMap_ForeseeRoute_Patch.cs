using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace foresee.Scripts;

[HarmonyPatch(typeof(NMapScreen), "SetMap")]
public static class NMapScreen_SetMap_ForeseeRoute_Patch
{
	public static void Postfix(NMapScreen __instance)
	{
		MapRoutePlanner.AfterMapSet(__instance);
	}
}
