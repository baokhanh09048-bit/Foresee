using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace foresee.Scripts;

[HarmonyPatch(typeof(NClickableControl), "_GuiInput")]
public static class NClickableControl_GuiInput_ForeseeRoute_Patch
{
	public static bool Prefix(NClickableControl __instance, InputEvent inputEvent)
	{
		NRelicInventoryHolder relicHolder = (NRelicInventoryHolder)(object)((__instance is NRelicInventoryHolder) ? __instance : null);
		if (relicHolder != null && ForeseeFrozenEye.IsForeseeHolder(relicHolder))
		{
			return true;
		}
		NMapPoint val = (NMapPoint)(object)((__instance is NMapPoint) ? __instance : null);
		if (val != null)
		{
			return MapRoutePlanner.HandlePointInput(val, inputEvent);
		}
		return true;
	}
}
