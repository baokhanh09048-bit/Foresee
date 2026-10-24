using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace foresee.Scripts;

[HarmonyPatch(typeof(NRelicInventory), "_Input")]
public static class NRelicInventory_Input_ForeseeFrozenEye_Patch
{
	public static void Postfix(NRelicInventory __instance, InputEvent inputEvent)
	{
		InputEventMouseButton mouse = (InputEventMouseButton)(object)((inputEvent is InputEventMouseButton) ? inputEvent : null);
		if (mouse == null || !mouse.Pressed || mouse.ButtonIndex != MouseButton.Right)
		{
			return;
		}

		foreach (NRelicInventoryHolder holder in __instance.RelicNodes)
		{
			if (!ForeseeFrozenEye.IsForeseeHolder(holder) || !holder.IsVisibleInTree())
			{
				continue;
			}
			if (!holder.GetGlobalRect().HasPoint(mouse.GlobalPosition))
			{
				continue;
			}

			ForeseeFrozenEye.TryToggle(holder);
			holder.GetViewport().SetInputAsHandled();
			return;
		}
	}
}
