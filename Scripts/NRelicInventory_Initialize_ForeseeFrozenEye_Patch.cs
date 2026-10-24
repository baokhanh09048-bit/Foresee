using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Runs;

namespace foresee.Scripts;

[HarmonyPatch(typeof(NRelicInventory), "Initialize")]
public static class NRelicInventory_Initialize_ForeseeFrozenEye_Patch
{
	public static void Postfix(NRelicInventory __instance, RunState runState)
	{
		ForeseeFrozenEye.AddToInventory(__instance, LocalContext.GetMe(runState));
	}
}
