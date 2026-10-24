using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace foresee.Scripts;

[HarmonyPatch(typeof(NMapScreen), "_Input")]
public static class NMapScreen_Input_ForeseeRoute_Patch
{
	public static bool Prefix(NMapScreen __instance, InputEvent inputEvent)
	{
		return MapRoutePlanner.HandleScreenInput(__instance, inputEvent);
	}
}
