using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace foresee.Scripts;

public static class ForeseeFrozenEye
{
	private const string ToolMetaName = "foresee_frozen_eye";
	private const string IconPath = "res://FrozenEye.png";
	private const string ToolTitle = "冻结之眼";
	public const int InventoryIndex = 0;

	private static readonly MethodInfo? InventoryAddMethod = typeof(NRelicInventory).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly MethodInfo? InventoryUpdateNavigationMethod = typeof(NRelicInventory).GetMethod("UpdateNavigation", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly FieldInfo? InventoryRelicNodesField = typeof(NRelicInventory).GetField("_relicNodes", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly FieldInfo? InventoryPlayerField = typeof(NRelicInventory).GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly PropertyInfo? HoverTipTitleProperty = typeof(HoverTip).GetProperty("Title", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	private static readonly HashSet<RelicModel> ToolRelics = new(ReferenceEqualityComparer.Instance);

	public static void AddToInventory(NRelicInventory inventory, Player? player)
	{
		if (player == null || InventoryAddMethod == null || inventory.RelicNodes.Any(IsForeseeHolder))
		{
			return;
		}

		RelicModel? carrier = CreateCarrierRelic(player);
		if (carrier == null)
		{
			return;
		}

		InventoryAddMethod.Invoke(inventory, new object[] { carrier, true, InventoryIndex });
		if (inventory.RelicNodes.Count == 0)
		{
			return;
		}

		NRelicInventoryHolder holder = inventory.RelicNodes[InventoryIndex];
		ToolRelics.Add(carrier);
		MarkHolder(holder);
		SyncStatus(holder.Relic.Model);
		ApplyIcon(holder);
		SortInventory(inventory);
		InventoryUpdateNavigationMethod?.Invoke(inventory, Array.Empty<object>());
	}

	public static int InventoryIndexForPlayerRelic(int playerRelicIndex)
	{
		return playerRelicIndex + 1;
	}

	public static void SortInventory(NRelicInventory inventory)
	{
		if (InventoryRelicNodesField?.GetValue(inventory) is not List<NRelicInventoryHolder> relicNodes || relicNodes.Count <= 1)
		{
			return;
		}

		NRelicInventoryHolder? toolHolder = relicNodes.FirstOrDefault(IsForeseeHolder);
		if (toolHolder == null)
		{
			return;
		}

		List<NRelicInventoryHolder> ordered = new() { toolHolder };
		if (InventoryPlayerField?.GetValue(inventory) is Player player)
		{
			foreach (RelicModel relic in player.Relics)
			{
				NRelicInventoryHolder? holder = relicNodes.FirstOrDefault(node => !IsForeseeHolder(node) && ReferenceEquals(node.Relic.Model, relic));
				if (holder != null)
				{
					ordered.Add(holder);
				}
			}
		}

		foreach (NRelicInventoryHolder holder in relicNodes)
		{
			if (!ordered.Contains(holder))
			{
				ordered.Add(holder);
			}
		}

		for (int i = 0; i < ordered.Count; i++)
		{
			relicNodes[i] = ordered[i];
			inventory.MoveChild(ordered[i], i);
		}
	}

	public static bool IsForeseeHolder(NRelicInventoryHolder? holder)
	{
		if (holder == null)
		{
			return false;
		}
		return holder.HasMeta(ToolMetaName) || holder.Name.ToString().Contains(ToolMetaName, StringComparison.Ordinal);
	}

	public static bool IsForeseeRelic(RelicModel? relic)
	{
		return relic != null && ToolRelics.Contains(relic);
	}

	public static void SyncStatus(RelicModel? relic)
	{
		if (relic == null)
		{
			return;
		}
		relic.Status = ForeseeSettings.ForecastingEnabled ? RelicStatus.Normal : RelicStatus.Disabled;
	}

	public static void ShowHoverTip(NRelicInventoryHolder holder)
	{
		NHoverTipSet.Remove(holder);
		HoverTip tip = CreateHoverTip();
		NHoverTipSet.CreateAndShow(holder, tip)?.SetAlignmentForRelic(holder.Relic);
	}

	public static string GetHoverDescription()
	{
		string state = ForeseeSettings.ForecastingEnabled ? "开启" : "关闭";
		string action = ForeseeSettings.ForecastingEnabled ? "关闭预见" : "开启预见";
		return $"预见：{state}\n右键：{action}\n左键：查看说明\n关闭预见时，路线规划仍然可用。";
	}

	public static string GetInspectDescription()
	{
		return "右键开关预见。关闭后只保留路线规划；重启游戏默认开启。";
	}

	public static bool TryToggle(NRelicInventoryHolder? holder)
	{
		if (!IsForeseeHolder(holder))
		{
			return false;
		}
		ForeseeSettings.ToggleForecasting();
		SyncStatus(holder!.Relic.Model);
		ApplyIcon(holder);
		ShowHoverTip(holder);
		return true;
	}

	public static void ApplyIcon(NRelicInventoryHolder holder)
	{
		Texture2D? icon = ResourceLoader.Load<Texture2D>(IconPath, null, ResourceLoader.CacheMode.Reuse);
		if (icon == null || holder.Relic == null || !holder.Relic.IsNodeReady())
		{
			return;
		}
		holder.Relic.Icon.Texture = icon;
		holder.Relic.Outline.Texture = icon;
	}

	private static HoverTip CreateHoverTip()
	{
		HoverTip tip = new HoverTip(new LocString("static_hover_tips", "MAP.title"), GetHoverDescription(), null);
		object boxed = tip;
		HoverTipTitleProperty?.SetValue(boxed, ToolTitle);
		return (HoverTip)boxed;
	}

	private static RelicModel? CreateCarrierRelic(Player player)
	{
		RelicModel? source = player.Relics.FirstOrDefault() ?? ModelDb.AllRelics.FirstOrDefault();
		RelicModel? carrier = source?.ClonePreservingMutability() as RelicModel;
		if (carrier == null)
		{
			return null;
		}
		if (!carrier.IsMutable)
		{
			carrier = carrier.ToMutable();
		}
		carrier.Owner = player;
		carrier.FloorAddedToDeck = 1;
		return carrier;
	}

	private static void MarkHolder(NRelicInventoryHolder holder)
	{
		holder.Name = ToolMetaName;
		holder.SetMeta(ToolMetaName, Variant.From(true));
	}
}
