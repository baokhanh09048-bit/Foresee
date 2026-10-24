using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace foresee.Scripts;

[HarmonyPatch(typeof(NRelicInventory), "UpdateNavigation")]
public static class NRelicInventory_UpdateNavigation_ForeseeFrozenEye_Patch
{
	public static void Prefix(NRelicInventory __instance)
	{
		ForeseeFrozenEye.SortInventory(__instance);
	}
}
