using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace foresee.Scripts;

[HarmonyPatch(typeof(NRelicInventoryHolder), "OnFocus")]
public static class NRelicInventoryHolder_OnFocus_ForeseeFrozenEye_Patch
{
	public static void Postfix(NRelicInventoryHolder __instance)
	{
		if (ForeseeFrozenEye.IsForeseeHolder(__instance))
		{
			ForeseeFrozenEye.ApplyIcon(__instance);
			ForeseeFrozenEye.ShowHoverTip(__instance);
		}
	}
}
