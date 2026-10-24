using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace foresee.Scripts;

[HarmonyPatch(typeof(NMapScreen), "_GuiInput")]
public static class NMapScreen_GuiInput_ForeseeRoute_Patch
{
	public static bool Prefix(NMapScreen __instance, InputEvent inputEvent)
	{
		return MapRoutePlanner.HandleMapInput(__instance, inputEvent);
	}
}
