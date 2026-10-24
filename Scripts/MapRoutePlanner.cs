using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace foresee.Scripts;

public static class MapRoutePlanner
{
	public sealed record ForecastTip(string Title, string Description);

	private const string OverlayName = "foresee_route_overlay";

	private static readonly FieldInfo? MapPointDictionaryField = typeof(NMapScreen).GetField("_mapPointDictionary", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo? MapPointScreenField = typeof(NMapPoint).GetField("_screen", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo? MapScreenDrawingInputField = typeof(NMapScreen).GetField("_drawingInput", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo? MapPathsField = typeof(NMapScreen).GetField("_paths", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly Dictionary<string, Dictionary<int, MapCoord>> MarksByMap = new Dictionary<string, Dictionary<int, MapCoord>>();

	private static ulong _lastHandledRightClickEventId;

	private static NMapPoint? _hoveredPoint;

	private static string? _cachedRoutePanelKey;

	private static string? _cachedRoutePanelText;

	public static void SetHoveredPoint(NMapPoint point)
	{
		_hoveredPoint = point;
	}

	public static void ClearHoveredPoint(NMapPoint point)
	{
		if (_hoveredPoint == point)
		{
			_hoveredPoint = null;
		}
	}

	public static bool HandlePointInput(NMapPoint point, InputEvent inputEvent)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Invalid comparison between Unknown and I8
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		InputEventMouseButton val = (InputEventMouseButton)(object)((inputEvent is InputEventMouseButton) ? inputEvent : null);
		if (val == null || !val.Pressed)
		{
			return true;
		}
		RunState val2 = RunManager.Instance.DebugOnlyGetState();
		object? obj = MapPointScreenField?.GetValue(point);
		NMapScreen val3 = (NMapScreen)((obj is NMapScreen) ? obj : null);
		if ((long)val.ButtonIndex != 2)
		{
			return true;
		}
		if (WasRightClickAlreadyHandled(val))
		{
			((Node)point).GetViewport().SetInputAsHandled();
			return false;
		}
		if (((val2 != null) ? val2.Map : null) == null || point.Point == null)
		{
			((Node)point).GetViewport().SetInputAsHandled();
			return false;
		}
		if (val3 == null || !CanMarkFuture(val2, point.Point))
		{
			((Node)point).GetViewport().SetInputAsHandled();
			return false;
		}
		MarkRightClickHandled(val);
		SetMark(val3, point.Point.coord);
		StopMapDrawingInput(val3);
		Redraw(val3, val2);
		((Node)point).GetViewport().SetInputAsHandled();
		return false;
	}

	public static bool HandleMapInput(NMapScreen screen, InputEvent inputEvent)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Invalid comparison between Unknown and I8
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		InputEventMouseButton val = (InputEventMouseButton)(object)((inputEvent is InputEventMouseButton) ? inputEvent : null);
		if (val == null || !val.Pressed || (long)val.ButtonIndex != 2)
		{
			return true;
		}
		if (WasRightClickAlreadyHandled(val))
		{
			((Node)screen).GetViewport().SetInputAsHandled();
			return false;
		}
		RunState val2 = RunManager.Instance.DebugOnlyGetState();
		if (((val2 != null) ? val2.Map : null) == null)
		{
			return false;
		}
		NMapPoint clickablePoint = GetClickablePoint(screen, ((InputEventMouse)val).GlobalPosition, val2);
		if (((clickablePoint != null) ? clickablePoint.Point : null) == null)
		{
			((Node)screen).GetViewport().SetInputAsHandled();
			return false;
		}
		MarkRightClickHandled(val);
		SetMark(screen, clickablePoint.Point.coord);
		StopMapDrawingInput(screen);
		Redraw(screen, val2);
		((Node)screen).GetViewport().SetInputAsHandled();
		return false;
	}

	public static bool HandleScreenInput(NMapScreen screen, InputEvent inputEvent)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Invalid comparison between Unknown and I8
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		InputEventMouseButton val = (InputEventMouseButton)(object)((inputEvent is InputEventMouseButton) ? inputEvent : null);
		if (val == null || !val.Pressed || (long)val.ButtonIndex != 2)
		{
			return true;
		}
		if (WasRightClickAlreadyHandled(val))
		{
			((Node)screen).GetViewport().SetInputAsHandled();
			return false;
		}
		if (!((CanvasItem)screen).IsVisibleInTree())
		{
			return true;
		}
		RunState val2 = RunManager.Instance.DebugOnlyGetState();
		if (((val2 != null) ? val2.Map : null) == null)
		{
			return true;
		}
		NMapPoint clickablePoint = GetClickablePoint(screen, ((InputEventMouse)val).GlobalPosition, val2);
		if (((clickablePoint != null) ? clickablePoint.Point : null) != null && CanMarkFuture(val2, clickablePoint.Point))
		{
			MarkRightClickHandled(val);
			SetMark(screen, clickablePoint.Point.coord);
			Redraw(screen, val2);
		}
		StopMapDrawingInput(screen);
		((Node)screen).GetViewport().SetInputAsHandled();
		return false;
	}

	public static void Redraw(NMapScreen screen)
	{
		RunState val = RunManager.Instance.DebugOnlyGetState();
		if (((val != null) ? val.Map : null) == null)
		{
			ClearOverlay(screen);
			return;
		}
		PruneMarks(val);
		Redraw(screen, val);
	}

	public static void AfterMapSet(NMapScreen screen)
	{
		ClearRoutePanelCache();
		Redraw(screen);
	}

	public static bool TryGetRouteIndex(MapCoord coord, out int index)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		RunState val = RunManager.Instance.DebugOnlyGetState();
		List<MapPoint> list = (List<MapPoint>)((((val != null) ? val.Map : null) == null) ? ((IList)new List<MapPoint>()) : ((IList)BuildRoute(val)));
		index = list.FindIndex((MapPoint point) => point.coord == coord);
		return index >= 0;
	}

	public static bool CanUseCurrentActForecast(NMapPoint point)
	{
		return RunManager.Instance.DebugOnlyGetState() != null;
	}

	public static bool CanShowForecast(NMapPoint mapPoint)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		if (!ForeseeSettings.ForecastingEnabled)
		{
			return false;
		}
		RunState val = RunManager.Instance.DebugOnlyGetState();
		if (((val != null) ? val.Map : null) == null || mapPoint.Point == null)
		{
			return false;
		}
		MapPoint point = mapPoint.Point;
		bool isVisited = IsVisited(val, point.coord);
		bool isCurrentOrPast = val.CurrentMapPoint != null && point.coord.row <= val.CurrentMapPoint.coord.row;
		bool isNextReachable = IsNextReachable(val, point);
		int index;
		bool isOnRoute = TryGetRouteIndex(point.coord, out index) && index > 0;
		return ForeseeHoverRules.ShouldShowForecast(isVisited, isCurrentOrPast, isNextReachable, isOnRoute);
	}

	public static void ClearMarks(NMapScreen screen)
	{
		RunState val = RunManager.Instance.DebugOnlyGetState();
		if (val != null)
		{
			CurrentMarks(val, val.Map).Clear();
		}
		ClearRoutePanelCache();
		ClearOverlay(screen);
	}

	public static bool TryBuildForecast(MapCoord coord, out ForecastTip tip)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		tip = new ForecastTip(string.Empty, string.Empty);
		if (!ForeseeSettings.ForecastingEnabled)
		{
			return false;
		}
		RunState val = RunManager.Instance.DebugOnlyGetState();
		if (((val != null) ? val.Map : null) == null)
		{
			return false;
		}
		List<MapPoint> list = BuildRoute(val);
		int num = list.FindIndex((MapPoint point) => point.coord == coord);
		if (num <= 0)
		{
			return false;
		}
		try
		{
			RunState runState;
			Player player = ClonePlayerInClonedRun(out runState);
			List<ForecastTip> list2 = new List<ForecastTip>();
			for (int num2 = 1; num2 < list.Count; num2++)
			{
				list2.Add(SimulateRouteStep(runState, player, list[num2], num2));
			}
			if (num - 1 < 0 || num - 1 >= list2.Count)
			{
				return false;
			}
			tip = list2[num - 1];
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static string AppendRouteSummary(string description, IReadOnlyList<ForecastTip> forecasts)
	{
		if (forecasts.Count <= 1)
		{
			return description;
		}
		StringBuilder stringBuilder = new StringBuilder(description.TrimEnd());
		stringBuilder.AppendLine();
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("路线总览：");
		for (int i = 0; i < forecasts.Count; i++)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(5, 3, stringBuilder2);
			handler.AppendFormatted(i + 1);
			handler.AppendLiteral(". ");
			handler.AppendFormatted(forecasts[i].Title);
			handler.AppendLiteral(" - ");
			handler.AppendFormatted(SummarizeForecast(forecasts[i]));
			stringBuilder2.AppendLine(ref handler);
		}
		return stringBuilder.ToString().TrimEnd();
	}

	private static string SummarizeForecast(ForecastTip forecast)
	{
		string[] array = (from line in forecast.Description.Split('\n')
			select line.Trim() into line
			where !string.IsNullOrWhiteSpace(line) && !line.StartsWith("规划路线第 ", StringComparison.Ordinal)
			select line).ToArray();
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (text.StartsWith("掉牌预览", StringComparison.Ordinal) || text.StartsWith("商店预览", StringComparison.Ordinal) || text.StartsWith("选项", StringComparison.Ordinal))
			{
				int num2 = Array.IndexOf(array, text);
				if (num2 + 1 >= array.Length)
				{
					return text.TrimEnd('：');
				}
				return array[num2 + 1].TrimStart('-', ' ');
			}
		}
		return array.FirstOrDefault() ?? "无额外信息";
	}

	private static void PruneMarks(RunState runState)
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		Dictionary<int, MapCoord> dictionary = CurrentMarks(runState, runState.Map);
		if (dictionary.Count == 0)
		{
			return;
		}
		MapPoint start = GetRouteStart(runState);
		int[] array = dictionary.Keys.ToArray();
		foreach (int key in array)
		{
			MapPoint point = runState.Map.GetPoint(dictionary[key]);
			if (point != null && !IsVisited(runState, point.coord))
			{
				MapCoord? currentMapCoord = runState.CurrentMapCoord;
				MapCoord coord = point.coord;
				if (!currentMapCoord.HasValue || !(currentMapCoord.GetValueOrDefault() == coord))
				{
					if (FindPath(start, point).Count > 1)
					{
						start = point;
					}
					continue;
				}
			}
			dictionary.Remove(key);
			ClearRoutePanelCache();
		}
	}

	private static ForecastTip SimulateRouteStep(RunState runState, Player player, MapPoint point, int step)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Invalid comparison between Unknown and I4
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected I4, but got Unknown
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		MapPointType pointType = point.PointType;
		RoomType val = ResolveRoomType(runState, pointType);
		if (val == RoomType.Treasure)
		{
			return SimulateTreasureStep(runState, player, point, pointType, step);
		}
		switch (val)
		{
		case RoomType.Monster:
		case RoomType.Elite:
		case RoomType.Boss:
			return SimulateCombatStep(runState, player, point, pointType, val, step);
		case RoomType.Shop:
			return SimulateShopStep(runState, player, point, pointType, step);
		case RoomType.Event:
			return SimulateEventStep(runState, player, point, pointType, step);
		case RoomType.Treasure:
			return SimulateSimpleStep(runState, point, pointType, val, "宝箱", "内容：宝箱", step);
		case RoomType.RestSite:
			return SimulateSimpleStep(runState, point, pointType, val, "火堆", "内容：休息点", step);
		default:
			return SimulateSimpleStep(runState, point, pointType, val, Describe(val), "内容：" + Describe(val), step);
		}
	}

	private static RoomType ResolveRoomType(RunState runState, MapPointType pointType)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Expected I4, but got Unknown
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		switch (pointType)
		{
		case MapPointType.Unknown:
		{
			UnknownMapPointOdds unknownMapPoint = runState.Odds.UnknownMapPoint;
			MapPointHistoryEntry currentMapPointHistoryEntry = runState.CurrentMapPointHistoryEntry;
			MapPoint currentMapPoint = runState.CurrentMapPoint;
			return unknownMapPoint.Roll((IEnumerable<RoomType>)RunManager.BuildRoomTypeBlacklist(currentMapPointHistoryEntry, (IReadOnlyCollection<MapPoint>)(((currentMapPoint != null) ? currentMapPoint.Children : null) ?? new HashSet<MapPoint>())), (IRunState)(object)runState);
		}
		case MapPointType.Shop:
			return RoomType.Shop;
		case MapPointType.Treasure:
			return RoomType.Treasure;
		case MapPointType.RestSite:
			return RoomType.RestSite;
		case MapPointType.Monster:
			return RoomType.Monster;
		case MapPointType.Elite:
			return RoomType.Elite;
		case MapPointType.Boss:
			return RoomType.Boss;
		case MapPointType.Ancient:
			return RoomType.Event;
		default:
			return RoomType.Unassigned;
		}
	}

	private static ForecastTip SimulateCombatStep(RunState runState, Player player, MapPoint point, MapPointType pointType, RoomType roomType, int step)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected O, but got Unknown
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		EncounterModel val = runState.Act.PullNextEncounter(roomType);
		CombatRoom val2 = new CombatRoom(val.ToMutable(), (IRunState)(object)runState);
		EnterPreviewRoom(runState, point, pointType, ((AbstractRoom)val2).RoomType, ((AbstractRoom)val2).ModelId);
		runState.Act.MarkRoomVisited(((AbstractRoom)val2).RoomType);
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(8, 1, stringBuilder2);
		handler.AppendLiteral("规划路线第 ");
		handler.AppendFormatted(step);
		handler.AppendLiteral(" 步");
		stringBuilder3.AppendLine(ref handler);
		if (val.CustomRewardDescription != null)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(5, 1, stringBuilder2);
			handler.AppendLiteral("特殊奖励：");
			handler.AppendFormatted(val.CustomRewardDescription.GetFormattedText());
			stringBuilder4.AppendLine(ref handler);
		}
		AppendCombatRewardPreviewForPlayers(stringBuilder, runState, val2);
		return new ForecastTip(val.Title.GetFormattedText(), stringBuilder.ToString().TrimEnd());
		}

		private static IEnumerable<Reward> FlattenRewards(IEnumerable<Reward> rewards)
		{
			foreach (Reward reward in rewards)
			{
				LinkedRewardSet val = (LinkedRewardSet)(object)((reward is LinkedRewardSet) ? reward : null);
				if (val != null)
				{
					foreach (Reward item in FlattenRewards(val.Rewards))
					{
						yield return item;
					}
				}
				else
				{
					yield return reward;
				}
			}
		}

		private static void AppendExtraRewardPreview(StringBuilder builder, IReadOnlyList<Reward> rewards)
		{
			int[] array = (from reward in rewards.OfType<GoldReward>()
				where reward.Amount >= 0
				select reward.Amount).ToArray();
			if (array.Length != 0)
			{
				StringBuilder stringBuilder = builder;
				StringBuilder stringBuilder2 = stringBuilder;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(3, 1, stringBuilder);
				handler.AppendLiteral("金币：");
				handler.AppendFormatted(string.Join(" / ", array));
				stringBuilder2.AppendLine(ref handler);
			}
			string[] array2 = (from reward in rewards.OfType<PotionReward>()
				select reward.Potion into potion
				where potion != null
				select FormatPotion(potion)).ToArray();
			if (array2.Length != 0)
			{
				StringBuilder stringBuilder = builder;
				StringBuilder stringBuilder3 = stringBuilder;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(3, 1, stringBuilder);
				handler.AppendLiteral("药水：");
				handler.AppendFormatted(string.Join(" / ", array2));
				stringBuilder3.AppendLine(ref handler);
			}
			string[] array3 = (from reward in rewards.OfType<RelicReward>()
				select reward.Relic into relic
				where relic != null
				select FormatRelic(relic)).ToArray();
			if (array3.Length != 0)
			{
				StringBuilder stringBuilder = builder;
				StringBuilder stringBuilder4 = stringBuilder;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(3, 1, stringBuilder);
				handler.AppendLiteral("遗物：");
				handler.AppendFormatted(string.Join(" / ", array3));
				stringBuilder4.AppendLine(ref handler);
			}
		}

		private static void AppendCombatRewardPreviewForPlayers(StringBuilder builder, RunState runState, CombatRoom room)
		{
			Player[] players = GetPreviewPlayers(runState).ToArray();
			bool isMultiplayer = players.Length > 1;
			for (int i = 0; i < players.Length; i++)
			{
				IReadOnlyList<string> extraRewardLines = Array.Empty<string>();
				IReadOnlyList<string> cards = Array.Empty<string>();
				string? fallbackLine = null;
				try
				{
					Reward[] rewards = FlattenRewards(RewardsCmd.GenerateForRoomEnd(players[i], (AbstractRoom)(object)room).GetAwaiter().GetResult()
						.Rewards).ToArray();
					extraRewardLines = FormatExtraRewardLines(rewards);
					CardReward? cardReward = rewards.OfType<CardReward>().FirstOrDefault();
					cards = cardReward?.Cards.Select(FormatCard).ToArray() ?? Array.Empty<string>();
				}
				catch
				{
					fallbackLine = "\u5956\u52b1\u6682\u4e0d\u53ef\u9884\u6d4b";
				}

				foreach (string line in ForeseeFormatting.FormatCombatRewardPreviewLines(
					PlayerLabel(runState, players[i], i),
					extraRewardLines,
					cards,
					isMultiplayer,
					detailIndent: "  ",
					fallbackLine: fallbackLine))
				{
					builder.AppendLine(line);
				}
			}
		}

		private static IReadOnlyList<string> FormatExtraRewardLines(IReadOnlyList<Reward> rewards)
		{
			StringBuilder builder = new StringBuilder();
			AppendExtraRewardPreview(builder, rewards);
			return builder.ToString()
				.Split('\n')
				.Select(line => line.TrimEnd('\r'))
				.Where(line => line.Length != 0)
				.ToArray();
		}

		private static void AppendExtraRewardPreview(StringBuilder builder, IReadOnlyList<Reward> rewards, string prefix)
		{
			if (string.IsNullOrEmpty(prefix))
			{
				AppendExtraRewardPreview(builder, rewards);
				return;
			}

			StringBuilder nested = new StringBuilder();
			AppendExtraRewardPreview(nested, rewards);
			foreach (string line in nested.ToString().Split('\n'))
			{
				string trimmed = line.TrimEnd('\r');
				if (trimmed.Length != 0)
				{
					builder.AppendLine(prefix + trimmed);
				}
			}
		}

		private static ForecastTip SimulateShopStep(RunState runState, Player player, MapPoint point, MapPointType pointType, int step)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_024a: Unknown result type (might be due to invalid IL or missing references)
			//IL_024c: Invalid comparison between Unknown and I4
			EnterPreviewRoom(runState, point, pointType, (RoomType)5, null);
			runState.Act.MarkRoomVisited((RoomType)5);
			MerchantInventory val = MerchantInventory.CreateForNormalMerchant(player);
			StringBuilder stringBuilder = new StringBuilder();
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(8, 1, stringBuilder2);
			handler.AppendLiteral("规划路线第 ");
			handler.AppendFormatted(step);
			handler.AppendLiteral(" 步");
			stringBuilder3.AppendLine(ref handler);
			stringBuilder.AppendLine("商店预览：");
			foreach (MerchantCardEntry item in val.CharacterCardEntries.Concat(val.ColorlessCardEntries))
			{
				if (item.CreationResult != null)
				{
					stringBuilder2 = stringBuilder;
					StringBuilder stringBuilder4 = stringBuilder2;
					handler = new StringBuilder.AppendInterpolatedStringHandler(7, 3, stringBuilder2);
					handler.AppendLiteral("- 卡牌：");
					handler.AppendFormatted(FormatCard(item.CreationResult.Card));
					handler.AppendLiteral(" ");
					handler.AppendFormatted(((MerchantEntry)item).Cost);
					handler.AppendLiteral("金");
					handler.AppendFormatted(item.IsOnSale ? "（半价）" : "");
					stringBuilder4.AppendLine(ref handler);
				}
			}
			foreach (MerchantRelicEntry relicEntry in val.RelicEntries)
			{
				if (relicEntry.Model != null)
				{
					stringBuilder2 = stringBuilder;
					StringBuilder stringBuilder5 = stringBuilder2;
					handler = new StringBuilder.AppendInterpolatedStringHandler(7, 2, stringBuilder2);
					handler.AppendLiteral("- 遗物：");
					handler.AppendFormatted(FormatRelic(relicEntry.Model));
					handler.AppendLiteral(" ");
					handler.AppendFormatted(((MerchantEntry)relicEntry).Cost);
					handler.AppendLiteral("金");
					stringBuilder5.AppendLine(ref handler);
				}
			}
			foreach (MerchantPotionEntry potionEntry in val.PotionEntries)
			{
				if (potionEntry.Model != null)
				{
					stringBuilder2 = stringBuilder;
					StringBuilder stringBuilder6 = stringBuilder2;
					handler = new StringBuilder.AppendInterpolatedStringHandler(7, 2, stringBuilder2);
					handler.AppendLiteral("- 药水：");
					handler.AppendFormatted(FormatPotion(potionEntry.Model));
					handler.AppendLiteral(" ");
					handler.AppendFormatted(((MerchantEntry)potionEntry).Cost);
					handler.AppendLiteral("金");
					stringBuilder6.AppendLine(ref handler);
				}
			}
			return new ForecastTip(((int)pointType == 1) ? "未知房：商店" : "商店", stringBuilder.ToString().TrimEnd());
		}

		private static ForecastTip SimulateEventStep(RunState runState, Player player, MapPoint point, MapPointType pointType, int step)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Invalid comparison between Unknown and I4
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			EventModel val = (((int)pointType == 8) ? runState.Act.PullAncient() : runState.Act.PullNextEvent(runState));
			EnterPreviewRoom(runState, point, pointType, (RoomType)6, ((AbstractModel)val).Id);
			runState.Act.MarkRoomVisited((RoomType)6);
			EventModel mutableEvent = val.ToMutable();
			ForeseeEventCompat.BeginEvent(mutableEvent, player, false);
			try
			{
				StringBuilder stringBuilder = new StringBuilder();
				StringBuilder stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder3 = stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(8, 1, stringBuilder2);
				handler.AppendLiteral("规划路线第 ");
				handler.AppendFormatted(step);
				handler.AppendLiteral(" 步");
				stringBuilder3.AppendLine(ref handler);
				string[] array = (from option in mutableEvent.CurrentOptions
					select FormatEventOption(option, mutableEvent.DynamicVars) into text
					where !string.IsNullOrWhiteSpace(text)
					select text).Take(4).ToArray();
				if (array.Length != 0)
				{
					stringBuilder.AppendLine("选项：");
					string[] array2 = array;
					foreach (string value in array2)
					{
						stringBuilder2 = stringBuilder;
						StringBuilder stringBuilder4 = stringBuilder2;
						handler = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder2);
						handler.AppendLiteral("- ");
						handler.AppendFormatted(value);
						stringBuilder4.AppendLine(ref handler);
					}
				}
				return new ForecastTip(val.Title.GetFormattedText(), stringBuilder.ToString().TrimEnd());
			}
			finally
			{
				mutableEvent.EnsureCleanup();
			}
		}

		private static ForecastTip SimulateSimpleStep(RunState runState, MapPoint point, MapPointType pointType, RoomType roomType, string title, string body, int step)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			EnterPreviewRoom(runState, point, pointType, roomType, null);
			runState.Act.MarkRoomVisited(roomType);
			return new ForecastTip(title, $"规划路线第 {step} 步\n{body}");
		}

		private static ForecastTip SimulateTreasureStep(RunState runState, Player player, MapPoint point, MapPointType pointType, int step)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
			//IL_0161: Unknown result type (might be due to invalid IL or missing references)
			//IL_0163: Invalid comparison between Unknown and I4
			EnterPreviewRoom(runState, point, pointType, (RoomType)4, null);
			runState.Act.MarkRoomVisited((RoomType)4);
			StringBuilder stringBuilder = new StringBuilder();
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(8, 1, stringBuilder2);
			handler.AppendLiteral("规划路线第 ");
			handler.AppendFormatted(step);
			handler.AppendLiteral(" 步");
			stringBuilder3.AppendLine(ref handler);
			if (Hook.ShouldGenerateTreasure((IRunState)(object)runState, player))
			{
				int num = player.PlayerRng.Rewards.NextInt(42, 53);
				if (AscensionHelper.HasAscension((AscensionLevel)3))
				{
					num = (int)((double)num * AscensionHelper.PovertyAscensionGoldMultiplier);
				}
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder4 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(3, 1, stringBuilder2);
				handler.AppendLiteral("金币：");
				handler.AppendFormatted(num);
				stringBuilder4.AppendLine(ref handler);
			}
			List<string> list = new List<string>();
			foreach (Player player2 in runState.Players)
			{
				if (Hook.ShouldGenerateTreasure((IRunState)(object)runState, player2))
				{
					RelicRarity val = RelicFactory.RollRarity(runState.Rng.TreasureRoomRelics);
					RelicModel relic = runState.SharedRelicGrabBag.PullFromFront(val, (IRunState)(object)runState) ?? RelicFactory.FallbackRelic;
					if (player2.NetId == player.NetId)
					{
						list.Add(FormatRelic(relic));
					}
				}
			}
			stringBuilder.AppendLine((list.Count > 0) ? ("遗物：" + string.Join(" / ", list)) : "遗物：无");
			return new ForecastTip(((int)pointType == 1) ? "未知房间：宝箱" : "宝箱", stringBuilder.ToString().TrimEnd());
		}

		private static void EnterPreviewRoom(RunState runState, MapPoint point, MapPointType pointType, RoomType roomType, ModelId? modelId)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			runState.AddVisitedMapCoord(point.coord);
			runState.AppendToMapPointHistory(pointType, roomType, modelId);
		}

		private static bool CanMarkFuture(RunState runState, MapPoint point)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			if (IsVisited(runState, point.coord))
			{
				return false;
			}
			if (runState.CurrentMapCoord.HasValue)
			{
				return point.coord != runState.CurrentMapCoord.Value;
			}
			return true;
		}

		private static bool IsNextReachable(RunState runState, MapPoint point)
		{
			return GetRouteStart(runState).Children.Any((MapPoint child) => child.coord == point.coord);
		}

		private static bool IsVisited(RunState runState, MapCoord coord)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			return runState.VisitedMapCoords.Contains(coord);
		}

		private static NMapPoint? GetHoveredPointFor(NMapScreen screen)
		{
			if (_hoveredPoint == null || _hoveredPoint.Point == null || !((CanvasItem)_hoveredPoint).IsVisibleInTree())
			{
				return null;
			}
			if (MapPointScreenField?.GetValue(_hoveredPoint) != screen)
			{
				return null;
			}
			return _hoveredPoint;
		}

		private static NMapPoint? GetClickablePoint(NMapScreen screen, Vector2 globalPosition, RunState runState)
		{
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			NMapPoint hoveredPointFor = GetHoveredPointFor(screen);
			if (((hoveredPointFor != null) ? hoveredPointFor.Point : null) != null && CanMarkFuture(runState, hoveredPointFor.Point))
			{
				return hoveredPointFor;
			}
			return FindPointAt(screen, globalPosition, (NMapPoint candidate) => candidate.Point != null && CanMarkFuture(runState, candidate.Point));
		}

		private static bool WasRightClickAlreadyHandled(InputEventMouseButton mouseEvent)
		{
			if (_lastHandledRightClickEventId != 0L)
			{
				return _lastHandledRightClickEventId == ((GodotObject)mouseEvent).GetInstanceId();
			}
			return false;
		}

		private static void MarkRightClickHandled(InputEventMouseButton mouseEvent)
		{
			_lastHandledRightClickEventId = ((GodotObject)mouseEvent).GetInstanceId();
		}

		private static void SetMark(NMapScreen screen, MapCoord coord)
		{
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			RunState val = RunManager.Instance.DebugOnlyGetState();
			if (((val != null) ? val.Map : null) != null)
			{
				Dictionary<int, MapCoord> dictionary = CurrentMarks(val, val.Map);
				if (dictionary.TryGetValue(coord.row, out var value) && value == coord)
				{
					dictionary.Remove(coord.row);
					ClearRoutePanelCache();
				}
				else
				{
					dictionary[coord.row] = coord;
					ClearRoutePanelCache();
				}
			}
		}

		private static NMapPoint? FindPointAt(NMapScreen screen, Vector2 globalPosition, Func<NMapPoint, bool>? predicate = null)
		{
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			Dictionary<MapCoord, NMapPoint> pointNodes = GetPointNodes(screen);
			NMapPoint result = null;
			float num = 180f;
			foreach (NMapPoint value in pointNodes.Values)
			{
				if (predicate == null || predicate(value))
				{
					Rect2 globalRect = ((Control)value).GetGlobalRect();
					Rect2 val = globalRect.Grow(24f);
					if (val.HasPoint(globalPosition))
					{
						return value;
					}
					Vector2 center = globalRect.GetCenter();
					float num2 = center.DistanceTo(globalPosition);
					if (num2 < num)
					{
						num = num2;
						result = value;
					}
				}
			}
			return result;
		}

		private static Dictionary<MapCoord, NMapPoint> GetPointNodes(NMapScreen screen)
		{
			return (MapPointDictionaryField?.GetValue(screen) as Dictionary<MapCoord, NMapPoint>) ?? new Dictionary<MapCoord, NMapPoint>();
		}

		private static void Redraw(NMapScreen screen, RunState runState)
		{
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_0087: Unknown result type (might be due to invalid IL or missing references)
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Expected O, but got Unknown
			//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00de: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0106: Unknown result type (might be due to invalid IL or missing references)
			//IL_0116: Unknown result type (might be due to invalid IL or missing references)
			//IL_011d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0136: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
			ClearOverlay(screen);
			List<MapPoint> list = BuildRoute(runState);
			Dictionary<int, MapCoord> dictionary = CurrentMarks(runState, runState.Map);
			if (list.Count <= 1 && dictionary.Count == 0)
			{
				return;
			}
			Control val = (Control)(((Node)screen).GetNodeOrNull<Control>("TheMap") ?? ((object)((Node)screen).GetNodeOrNull<Control>("TheMap/Paths")) ?? ((object)screen));
			Dictionary<MapCoord, NMapPoint> pointNodes = GetPointNodes(screen);
			Control val2 = new Control
			{
				Name = "foresee_route_overlay",
				MouseFilter = Control.MouseFilterEnum.Ignore,
				Position = Vector2.Zero,
				Size = val.Size,
				ZIndex = 500
			};
			val2.SetAnchorsPreset(Control.LayoutPreset.FullRect, false);
			((Node)val).AddChild((Node)(object)val2);
			for (int i = 0; i < list.Count - 1; i++)
			{
				if (pointNodes.TryGetValue(list[i].coord, out var value) && pointNodes.TryGetValue(list[i + 1].coord, out var value2) && !AddPathHighlight(screen, val2, list[i].coord, list[i + 1].coord))
				{
					AddLine(val2, Endpoint(value), Endpoint(value2), new Color(0.1f, 0.85f, 1f, 0.82f), 5f, 500);
				}
			}
			foreach (MapCoord item in from pair in dictionary
				orderby pair.Key
				select pair.Value)
			{
				if (pointNodes.TryGetValue(item, out var value3))
				{
					AddMarker(val2, Endpoint(value3));
				}
			}
		}

		private static void ClearOverlay(NMapScreen screen)
		{
			RemoveOverlayNode(((Node)screen).GetNodeOrNull<Node>("TheMap/foresee_route_overlay"));
			RemoveOverlayNode(((Node)screen).GetNodeOrNull<Node>("TheMap/Paths/foresee_route_overlay"));
			RemoveOverlayNode(((Node)screen).GetNodeOrNull<Node>("foresee_route_overlay"));
		}

		private static void RemoveOverlayNode(Node? existing)
		{
			if (existing != null)
			{
				Node parent = existing.GetParent();
				if (parent != null)
				{
					parent.RemoveChild(existing);
				}
				existing.QueueFree();
			}
		}

		private static void StopMapDrawingInput(NMapScreen screen)
		{
			object? obj = MapScreenDrawingInputField?.GetValue(screen);
			NMapDrawingInput val = (NMapDrawingInput)((obj is NMapDrawingInput) ? obj : null);
			if (val != null)
			{
				val.StopDrawing();
			}
		}

		private static void AddLine(Control parent, Vector2 from, Vector2 to, Color color, float width, int zIndex = 200)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Expected O, but got Unknown
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			Line2D val = new Line2D
			{
				Width = width,
				DefaultColor = color,
				ZIndex = zIndex
			};
			val.AddPoint(from, -1);
			val.AddPoint(to, -1);
			((Node)parent).AddChild((Node)(object)val);
		}

		private static bool AddPathHighlight(NMapScreen screen, Control overlay, MapCoord from, MapCoord to)
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0065: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Expected O, but got Unknown
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_008b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			//IL_0097: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
			if (!(MapPathsField?.GetValue(screen) is Dictionary<(MapCoord, MapCoord), IReadOnlyList<TextureRect>> dictionary) || !dictionary.TryGetValue((from, to), out var value) || value.Count == 0)
			{
				return false;
			}
			Line2D val = new Line2D
			{
				Width = 5f,
				DefaultColor = new Color(0.1f, 0.85f, 1f, 0.82f),
				ZIndex = 500
			};
			foreach (TextureRect item in value)
			{
				val.AddPoint(((Control)item).GlobalPosition - overlay.GlobalPosition + ((Control)item).Size * 0.5f, -1);
			}
			((Node)overlay).AddChild((Node)(object)val);
			return true;
		}

		private static void AddMarker(Control parent, Vector2 center)
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0067: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			Color color = new Color(1f, 0.86f, 0.18f, 0.95f);
			AddLine(parent, center + new Vector2(-15f, -15f), center + new Vector2(15f, 15f), color, 5f, 520);
			AddLine(parent, center + new Vector2(-15f, 15f), center + new Vector2(15f, -15f), color, 5f, 520);
		}

		private static void AddRoutePanel(Control parent, RunState runState, Vector2 position)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Expected O, but got Unknown
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0089: Expected O, but got Unknown
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e6: Expected O, but got Unknown
			//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
			//IL_0107: Unknown result type (might be due to invalid IL or missing references)
			//IL_010f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0119: Expected O, but got Unknown
			//IL_0125: Unknown result type (might be due to invalid IL or missing references)
			string text = BuildRoutePanelText(runState);
			if (!string.IsNullOrWhiteSpace(text))
			{
				PanelContainer val = new PanelContainer
				{
					Position = position,
					CustomMinimumSize = new Vector2(760f, 460f),
					ZIndex = 260,
					MouseFilter = Control.MouseFilterEnum.Stop
				};
				StyleBoxFlat val2 = new StyleBoxFlat
				{
					BgColor = new Color(0.02f, 0.03f, 0.04f, 0.84f),
					BorderColor = new Color(0.1f, 0.85f, 1f, 0.65f)
				};
				val2.SetBorderWidthAll(2);
				val2.SetCornerRadiusAll(6);
				((StyleBox)val2).SetContentMarginAll(14f);
				((Control)val).AddThemeStyleboxOverride("panel", (StyleBox)(object)val2);
				ScrollContainer val3 = new ScrollContainer
				{
					CustomMinimumSize = new Vector2(730f, 430f),
					HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
					VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
					MouseFilter = Control.MouseFilterEnum.Stop
				};
				Label val4 = new Label
				{
					Text = text,
					CustomMinimumSize = new Vector2(700f, 0f),
					AutowrapMode = TextServer.AutowrapMode.WordSmart,
					MouseFilter = Control.MouseFilterEnum.Ignore
				};
				((Control)val4).AddThemeColorOverride("font_color", Colors.White);
				((Control)val4).AddThemeFontSizeOverride("font_size", 22);
				((Node)val3).AddChild((Node)(object)val4);
				((Node)val).AddChild((Node)(object)val3);
				((Node)parent).AddChild((Node)(object)val);
			}
		}

		private static string BuildRoutePanelText(RunState realRunState)
		{
			if (!ForeseeSettings.ForecastingEnabled)
			{
				ClearRoutePanelCache();
				return string.Empty;
			}
			List<MapPoint> list = BuildRoute(realRunState);
			if (list.Count <= 1)
			{
				ClearRoutePanelCache();
				return string.Empty;
			}
			string text = BuildRoutePanelCacheKey(realRunState, list);
			if (_cachedRoutePanelKey == text && _cachedRoutePanelText != null)
			{
				return _cachedRoutePanelText;
			}
			try
			{
				RunState runState;
				Player player = ClonePlayerInClonedRun(out runState);
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.AppendLine("规划路线预测：");
				for (int i = 1; i < list.Count; i++)
				{
					ForecastTip forecastTip = SimulateRouteStep(runState, player, list[i], i);
					StringBuilder stringBuilder2 = stringBuilder;
					StringBuilder stringBuilder3 = stringBuilder2;
					StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(2, 2, stringBuilder2);
					handler.AppendFormatted(i);
					handler.AppendLiteral(". ");
					handler.AppendFormatted(forecastTip.Title);
					stringBuilder3.AppendLine(ref handler);
					foreach (string detailLine in GetDetailLines(forecastTip))
					{
						stringBuilder2 = stringBuilder;
						StringBuilder stringBuilder4 = stringBuilder2;
						handler = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder2);
						handler.AppendLiteral("  ");
						handler.AppendFormatted(detailLine);
						stringBuilder4.AppendLine(ref handler);
					}
				}
				_cachedRoutePanelKey = text;
				_cachedRoutePanelText = stringBuilder.ToString().TrimEnd();
				return _cachedRoutePanelText;
			}
			catch
			{
				_cachedRoutePanelKey = text;
				_cachedRoutePanelText = "规划路线预测：暂时无法计算";
				return _cachedRoutePanelText;
			}
		}

		private static string BuildRoutePanelCacheKey(RunState runState, IReadOnlyList<MapPoint> route)
		{
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			string value = runState.CurrentMapPoint?.coord.ToString()
				?? (runState.CurrentMapCoord.HasValue ? runState.CurrentMapCoord.Value.ToString() : "none");
			string value2 = BuildPlayersStateCacheKey(runState);
			string value3 = string.Join("|", from pair in CurrentMarks(runState, runState.Map)
				orderby pair.Key
				select ((object)pair.Value/*cast due to .constrained prefix*/).ToString());
			string value4 = string.Join(">", route.Select((MapPoint point) => point.coord.ToString()));
			return $"{runState.CurrentActIndex}:{runState.ActFloor}:{value}:{runState.VisitedMapCoords.Count}:{value2}:{value3}:{value4}";
		}

		private static string BuildPlayerStateCacheKey(Player? player)
		{
			if (player == null)
			{
				return "no-player";
			}
			return string.Join("/", player.NetId, player.Gold, player.Deck.Cards.Count, player.Relics.Count, player.PotionSlots.Count, player.Potions.Count());
		}

		private static string BuildPlayersStateCacheKey(RunState runState)
		{
			return string.Join("|", GetPreviewPlayers(runState).Select(BuildPlayerStateCacheKey));
		}

		private static void ClearRoutePanelCache()
		{
			_cachedRoutePanelKey = null;
			_cachedRoutePanelText = null;
		}

		private static Dictionary<int, MapCoord> CurrentMarks(RunState? runState, ActMap? map)
		{
			string key = BuildMarkKey(runState, map);
			if (!MarksByMap.TryGetValue(key, out Dictionary<int, MapCoord> value))
			{
				value = new Dictionary<int, MapCoord>();
				MarksByMap[key] = value;
			}
			return value;
		}

		private static string BuildMarkKey(RunState? runState, ActMap? map)
		{
			if (runState == null)
			{
				runState = RunManager.Instance.DebugOnlyGetState();
			}
			if (map == null)
			{
				map = ((runState != null) ? runState.Map : null);
			}
			string value = ForeseeRunCompat.GetSeedKey(runState?.Rng);
			return $"{value}:{BuildMapFingerprint(map)}";
		}

		private static string BuildMapFingerprint(ActMap? map)
		{
			if (map == null)
			{
				return "no-map";
			}
			return string.Join("|", from point in map.GetAllMapPoints().Concat((IEnumerable<MapPoint>)(object)new MapPoint[2] { map.StartingMapPoint, map.BossMapPoint })
				where point != null
				select $"{point.coord.row},{point.coord.col},{point.PointType}" into value
				orderby value
				select value);
		}

		private static IEnumerable<string> GetDetailLines(ForecastTip forecast)
		{
			return from line in forecast.Description.Split('\n')
				select line.Trim() into line
				where !string.IsNullOrWhiteSpace(line) && !line.StartsWith("规划路线第", StringComparison.Ordinal)
				select line;
		}

		private static Vector2 Endpoint(NMapPoint point)
		{
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			if (!(point is NNormalMapPoint))
			{
				return ((Control)point).Position + ((Control)point).Size * 0.5f;
			}
			return ((Control)point).Position;
		}

		private static List<MapPoint> BuildRoute(RunState runState)
		{
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			MapPoint routeStart = GetRouteStart(runState);
			List<MapPoint> list = new List<MapPoint> { routeStart };
			foreach (MapCoord item in from pair in CurrentMarks(runState, runState.Map)
				orderby pair.Key
				select pair.Value)
			{
				MapPoint point = runState.Map.GetPoint(item);
				if (point == null)
				{
					continue;
				}
				if (point.coord.row > list[list.Count - 1].coord.row)
				{
					List<MapPoint> list2 = FindPath(list[list.Count - 1], point);
					if (list2.Count <= 1)
					{
						break;
					}
					list.AddRange(list2.Skip(1));
				}
			}
			return list;
		}

		private static MapPoint GetRouteStart(RunState runState)
		{
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			if (runState.CurrentMapPoint != null)
			{
				return runState.CurrentMapPoint;
			}
			if (runState.VisitedMapCoords.Count > 0)
			{
				ActMap map = runState.Map;
				IReadOnlyList<MapCoord> visitedMapCoords = runState.VisitedMapCoords;
				MapPoint point = map.GetPoint(visitedMapCoords[visitedMapCoords.Count - 1]);
				if (point != null)
				{
					return point;
				}
			}
			return runState.Map.StartingMapPoint;
		}

		private static List<MapPoint> FindPath(MapPoint start, MapPoint target)
		{
			Queue<MapPoint> queue = new Queue<MapPoint>();
			Dictionary<MapPoint, MapPoint> dictionary = new Dictionary<MapPoint, MapPoint>();
			queue.Enqueue(start);
			dictionary[start] = null;
			while (queue.Count > 0)
			{
				MapPoint val = queue.Dequeue();
				if (val == target)
				{
					break;
				}
				foreach (MapPoint item in val.Children.OrderBy((MapPoint child) => Math.Abs(child.coord.col - target.coord.col)))
				{
					if (item.coord.row <= target.coord.row && !dictionary.ContainsKey(item))
					{
						dictionary[item] = val;
						queue.Enqueue(item);
					}
				}
			}
			if (!dictionary.ContainsKey(target))
			{
				return new List<MapPoint>();
			}
			List<MapPoint> list = new List<MapPoint>();
			for (MapPoint val2 = target; val2 != null; val2 = dictionary[val2])
			{
				list.Add(val2);
			}
			list.Reverse();
			return list;
		}

		private static Player ClonePlayerInClonedRun(out RunState runState)
		{
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_008d: Expected O, but got Unknown
			RunState obj = RunManager.Instance.DebugOnlyGetState();
			Player realPlayer = obj != null ? GetPrimaryPlayer(obj) : null;
			if (realPlayer == null)
			{
				throw new InvalidOperationException("Unable to find local player.");
			}
			SerializableRun val = RunManager.Instance.ToSave((AbstractRoom)null);
			runState = RunState.FromSerializable(val);
			SerializableActMap val2 = ((val.Acts.Count > runState.CurrentActIndex) ? val.Acts[runState.CurrentActIndex].SavedMap : null);
			if (val2 != null)
			{
				runState.Map = (ActMap)new SavedActMap(val2);
			}
			return ((IEnumerable<Player>)runState.Players).FirstOrDefault((Func<Player, bool>)((Player player) => player.NetId == realPlayer.NetId)) ?? throw new InvalidOperationException($"Unable to find cloned player {realPlayer.NetId}.");
		}

		private static Player? GetPrimaryPlayer(RunState runState)
		{
			return LocalContext.GetMe(runState.Players) ?? runState.Players.FirstOrDefault();
		}

		private static IEnumerable<Player> GetPreviewPlayers(RunState runState)
		{
			Player? localPlayer = LocalContext.GetMe(runState.Players);
			if (localPlayer != null)
			{
				yield return localPlayer;
			}

			foreach (Player player in runState.Players)
			{
				if (localPlayer == null || player.NetId != localPlayer.NetId)
				{
					yield return player;
				}
			}
		}

		private static string PlayerLabel(RunState runState, Player player, int index)
		{
			Player? localPlayer = LocalContext.GetMe(runState.Players);
			return localPlayer != null && player.NetId == localPlayer.NetId
				? "\u4f60"
				: $"\u73a9\u5bb6 {index + 1}";
		}

		private static string Describe(RoomType roomType)
		{
			return roomType switch
			{
				RoomType.Monster => "\u666e\u901a\u6218\u6597",
				RoomType.Elite => "\u7cbe\u82f1",
				RoomType.Boss => "Boss",
				RoomType.Treasure => "\u5b9d\u7bb1",
				RoomType.Shop => "\u5546\u5e97",
				RoomType.Event => "\u4e8b\u4ef6",
				RoomType.RestSite => "\u706b\u5806",
				RoomType.Map => "\u5730\u56fe",
				RoomType.Unassigned => "\u672a\u51b3\u5b9a",
				_ => roomType.ToString(),
			};
		}

		private static string NameOf(CardModel card)
		{
			string title = card.Title;
			if (!string.IsNullOrWhiteSpace(title))
			{
				return title;
			}
			return ((object)((AbstractModel)card).Id).ToString();
		}

		private static string NameOf(RelicModel relic)
		{
			string formattedText = relic.Title.GetFormattedText();
			if (!string.IsNullOrWhiteSpace(formattedText))
			{
				return formattedText;
			}
			return ((object)((AbstractModel)relic).Id).ToString();
		}

		private static string NameOf(PotionModel potion)
		{
			string formattedText = potion.Title.GetFormattedText();
			if (!string.IsNullOrWhiteSpace(formattedText))
			{
				return formattedText;
			}
			return ((object)((AbstractModel)potion).Id).ToString();
		}

		private static string FormatCard(CardModel card)
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			return ForeseeFormatting.ColorCardNameForRarity(NameOf(card), card.IsUpgraded, card.Rarity);
		}

		private static string FormatRelic(RelicModel relic)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			return ForeseeFormatting.ColorRelicNameForRarity(NameOf(relic), relic.Rarity);
		}

		private static string FormatPotion(PotionModel potion)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			return ForeseeFormatting.ColorPotionNameForRarity(NameOf(potion), potion.Rarity);
		}

		private static string FormatEventOption(EventOption option, DynamicVarSet? dynamicVars = null)
		{
			AddEventDynamicVars(option.Title, dynamicVars);
			AddEventDynamicVars(option.Description, dynamicVars);
			string text = SanitizeEventText(option.Title.GetFormattedText());
			string text2 = SanitizeEventText(option.Description.GetFormattedText());
			string text3 = (option.IsLocked ? "（锁定）" : string.Empty);
			if (string.IsNullOrWhiteSpace(text2))
			{
				if (!string.IsNullOrWhiteSpace(text))
				{
					return text + text3;
				}
				return string.Empty;
			}
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text + text3 + "：" + text2;
			}
			return text2 + text3;
		}

		private static void AddEventDynamicVars(LocString locString, DynamicVarSet? dynamicVars)
		{
			if (dynamicVars != null)
			{
				ForeseeFormatting.AddEventDynamicVarsTo(locString, dynamicVars);
			}
		}

		private static string SanitizeEventText(string text)
		{
			return ForeseeFormatting.SanitizeEventText(text);
		}
	}
