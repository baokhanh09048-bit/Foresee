using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace foresee.Scripts;

public static class MapForesee
{
	private sealed record ForeseeTip(string Title, string Description);

	private sealed record UnknownRoomPreview(RoomType RoomType, EncounterModel? Encounter, EventModel? Event)
	{
		[CompilerGenerated]
		public void Deconstruct(out RoomType RoomType, out EncounterModel? Encounter, out EventModel? Event)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Expected I4, but got Unknown
			RoomType = (RoomType)(int)this.RoomType;
			Encounter = this.Encounter;
			Event = this.Event;
		}
	}

	private static readonly LocString FallbackTitle = new LocString("static_hover_tips", "MAP.title");

	private static readonly PropertyInfo? HoverTipTitleProperty = typeof(HoverTip).GetProperty("Title", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

	private static readonly CardType[] ShopCardTypes;

	public static void Show(NMapPoint mapPoint)
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		if (!ForeseeSettings.ForecastingEnabled)
		{
			Hide(mapPoint);
			return;
		}
		if (mapPoint.Point == null || RunManager.Instance.DebugOnlyGetState() == null)
		{
			return;
		}
		ForeseeTip? foreseeTip;
		try
		{
			foreseeTip = BuildTip(mapPoint);
		}
		catch
		{
			foreseeTip = BuildFailureFallback(mapPoint);
		}
		if (!(foreseeTip == null) && !string.IsNullOrWhiteSpace(foreseeTip.Description))
		{
			NHoverTipSet.Remove((Control)(object)mapPoint);
			HoverTip val = CreateHoverTip(foreseeTip.Title, foreseeTip.Description);
			val.Id = $"foresee:{mapPoint.Point.coord.col}:{mapPoint.Point.coord.row}";
			val.IsInstanced = true;
			NHoverTipSet obj = NHoverTipSet.CreateAndShow((Control)(object)mapPoint, (IHoverTip)(object)val, HoverTip.GetHoverTipAlignment((Control)(object)mapPoint, 0.75f));
			if (obj != null)
			{
				obj.SetExtraFollowOffset(new Vector2(0f, 20f));
			}
		}
	}

	public static void Hide(NMapPoint mapPoint)
	{
		NHoverTipSet.Remove((Control)(object)mapPoint);
	}

	private static HoverTip CreateHoverTip(string title, string description)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		HoverTip val = new HoverTip(FallbackTitle, description, (Texture2D)null);
		if (string.IsNullOrWhiteSpace(title) || HoverTipTitleProperty == null)
		{
			return val;
		}
		object obj = val;
		HoverTipTitleProperty.SetValue(obj, title);
		return (HoverTip)obj;
	}

	public static bool TryBuildForecastSafely(Func<MapRoutePlanner.ForecastTip?> buildForecast, out MapRoutePlanner.ForecastTip tip)
	{
		try
		{
			MapRoutePlanner.ForecastTip? builtTip = buildForecast();
			if (builtTip != null)
			{
				tip = builtTip;
				return true;
			}
		}
		catch
		{
		}

		tip = new MapRoutePlanner.ForecastTip(string.Empty, string.Empty);
		return false;
	}

	public static bool TryBuildRouteFailureFallback(MapPointType pointType, int routeIndex, bool isOnRoute, out string title, out string description)
	{
		return TryBuildForecastFailureFallback(pointType, routeIndex, isOnRoute, isForecastEligible: false, out title, out description);
	}

	public static bool TryBuildForecastFailureFallback(MapPointType pointType, int routeIndex, bool isOnRoute, bool isForecastEligible, out string title, out string description)
	{
		if (ForeseeHoverRules.ShouldShowRouteFallback(isOnRoute, routeForecastBuilt: false, hasDirectRoomPreview: false))
		{
			title = RouteFallbackTitle(pointType);
			description = RouteFallbackDescription(routeIndex);
			return true;
		}

		if (isForecastEligible)
		{
			title = Describe(MapPointTypeToRoomType(pointType));
			description = "\u9884\u6d4b\u6682\u4e0d\u53ef\u7528\u3002";
			return true;
		}

		title = string.Empty;
		description = string.Empty;
		return false;
	}

	private static ForeseeTip? BuildFailureFallback(NMapPoint mapPoint)
	{
		if (mapPoint.Point == null)
		{
			return null;
		}

		RunState? runState = RunManager.Instance.DebugOnlyGetState();
		bool isOnRoute = MapRoutePlanner.TryGetRouteIndex(mapPoint.Point.coord, out int routeIndex) && routeIndex > 0;
		bool isForecastEligible = runState != null && IsFailureFallbackEligible(runState, mapPoint.Point);
		return TryBuildForecastFailureFallback(mapPoint.Point.PointType, routeIndex, isOnRoute, isForecastEligible, out string title, out string description)
			? new ForeseeTip(title, description)
			: null;
	}

	private static bool IsFailureFallbackEligible(RunState runState, MapPoint point)
	{
		if (IsVisited(runState, point.coord))
		{
			return false;
		}

		if (runState.CurrentMapPoint != null && point.coord.row <= runState.CurrentMapPoint.coord.row)
		{
			return false;
		}

		return runState.CurrentMapPoint == null || runState.CurrentMapPoint.Children.Any(child => child.coord == point.coord);
	}

	private static RoomType MapPointTypeToRoomType(MapPointType pointType)
	{
		return pointType switch
		{
			MapPointType.Monster => RoomType.Monster,
			MapPointType.Elite => RoomType.Elite,
			MapPointType.Boss => RoomType.Boss,
			MapPointType.Treasure => RoomType.Treasure,
			MapPointType.Shop => RoomType.Shop,
			MapPointType.Ancient => RoomType.Event,
			MapPointType.RestSite => RoomType.RestSite,
			MapPointType.Unknown => RoomType.Unassigned,
			_ => RoomType.Unassigned,
		};
	}

	private static ForeseeTip? BuildTip(NMapPoint mapPoint)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Expected I4, but got Unknown
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		RunState val = RunManager.Instance.DebugOnlyGetState();
		if (val == null)
		{
			return null;
		}
		MapPoint point = mapPoint.Point;
		StringBuilder stringBuilder = new StringBuilder();
		if (IsVisited(val, point.coord))
		{
			return null;
		}
		if (val.CurrentMapPoint != null && point.coord.row <= val.CurrentMapPoint.coord.row)
		{
			return null;
		}
		if (!MapRoutePlanner.CanShowForecast(mapPoint))
		{
			return null;
		}
		Player val2 = GetPrimaryPlayer(val);
		if (val2 == null)
		{
			return null;
		}
		bool flag = MapRoutePlanner.CanUseCurrentActForecast(mapPoint);
		bool isOnRoute = MapRoutePlanner.TryGetRouteIndex(point.coord, out var routeIndex) && routeIndex > 0;
		MapRoutePlanner.ForecastTip? BuildRouteForecast()
		{
			return MapRoutePlanner.TryBuildForecast(point.coord, out MapRoutePlanner.ForecastTip routeTip)
				? routeTip
				: null;
		}
		if (flag && TryBuildForecastSafely(BuildRouteForecast, out MapRoutePlanner.ForecastTip tip))
		{
			return new ForeseeTip(tip.Title, tip.Description);
		}
		MapPointType pointType = point.PointType;
		string title;
		switch ((int)pointType)
		{
		case 5:
		{
			EncounterModel val5 = val.Act.PullNextEncounter((RoomType)1);
			title = val5.Title.GetFormattedText();
			AppendEncounter(stringBuilder, val5);
			AppendCardRewardPreview(stringBuilder, val, point.PointType, RoomType.Monster);
			break;
		}
		case 6:
		{
			EncounterModel val4 = val.Act.PullNextEncounter((RoomType)2);
			title = val4.Title.GetFormattedText();
			AppendEncounter(stringBuilder, val4);
			AppendCardRewardPreview(stringBuilder, val, point.PointType, RoomType.Elite);
			break;
		}
		case 7:
		{
			EncounterModel val3 = ((point == val.Map.SecondBossMapPoint && val.Act.SecondBossEncounter != null) ? val.Act.SecondBossEncounter : val.Act.BossEncounter);
			title = val3.Title.GetFormattedText();
			AppendEncounter(stringBuilder, val3);
			AppendCardRewardPreview(stringBuilder, val, point.PointType, RoomType.Boss);
			break;
		}
		case 2:
			title = "商店";
			AppendShopPreview(stringBuilder, val2, point.PointType);
			break;
		case 1:
			title = AppendUnknownPreview(stringBuilder, val, val2);
			break;
		case 8:
		{
			EventModel eventModel = val.Act.PullAncient();
			title = NameOf(eventModel);
			AppendEventOptions(stringBuilder, eventModel, val2, point.PointType);
			break;
		}
		case 0:
		case 3:
		case 4:
			if (ForeseeHoverRules.ShouldShowRouteFallback(isOnRoute, routeForecastBuilt: false, hasDirectRoomPreview: false))
			{
				return new ForeseeTip(RouteFallbackTitle(pointType), RouteFallbackDescription(routeIndex));
			}
			return null;
		default:
			if (ForeseeHoverRules.ShouldShowRouteFallback(isOnRoute, routeForecastBuilt: false, hasDirectRoomPreview: false))
			{
				return new ForeseeTip(RouteFallbackTitle(pointType), RouteFallbackDescription(routeIndex));
			}
			return null;
		}
		if (flag && MapRoutePlanner.TryGetRouteIndex(point.coord, out var index) && index > 0)
		{
			stringBuilder.Insert(0, $"规划路线第 {index} 步\n");
		}
		return new ForeseeTip(title, stringBuilder.ToString().TrimEnd());
	}

	private static string RouteFallbackTitle(MapPointType pointType)
	{
		return pointType switch
		{
			MapPointType.Treasure => "\u5b9d\u7bb1",
			MapPointType.RestSite => "\u706b\u5806",
			_ => "\u8def\u7ebf\u623f\u95f4",
		};
	}

	private static string RouteFallbackDescription(int routeIndex)
	{
		return $"\u89c4\u5212\u8def\u7ebf\u7b2c {routeIndex} \u6b65\n\u8def\u7ebf\u9884\u6d4b\u6682\u4e0d\u53ef\u7528\u3002";
	}

	private static bool IsVisited(RunState runState, MapCoord coord)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		return runState.VisitedMapCoords.Contains(coord);
	}

	private static string AppendUnknownPreview(StringBuilder builder, RunState runState, Player player)
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Expected I4, but got Unknown
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		UnknownRoomPreview unknownRoomPreview = TryPreviewUnknownRoom(player);
		if (unknownRoomPreview == null)
		{
			builder.AppendLine("未知房：进房时才决定具体类型。");
			return "未知房";
		}
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(6, 1, builder);
		handler.AppendLiteral("未知房预测：");
		handler.AppendFormatted(Describe(unknownRoomPreview.RoomType));
		builder.AppendLine(ref handler);
		switch (unknownRoomPreview.RoomType)
		{
		case RoomType.Monster:
		case RoomType.Elite:
		case RoomType.Boss:
		{
			string result = "未知房：" + Describe(unknownRoomPreview.RoomType);
			if (unknownRoomPreview.Encounter != null)
			{
				AppendEncounter(builder, unknownRoomPreview.Encounter);
				result = unknownRoomPreview.Encounter.Title.GetFormattedText();
			}
			AppendCardRewardPreview(builder, runState, MapPointType.Unknown, unknownRoomPreview.RoomType);
			return result;
		}
		case RoomType.Shop:
			AppendShopPreview(builder, player, (MapPointType)1);
			return "未知房：商店";
		case RoomType.Event:
			if (unknownRoomPreview.Event != null)
			{
				AppendEventOptions(builder, unknownRoomPreview.Event, player, (MapPointType)1);
				return unknownRoomPreview.Event.Title.GetFormattedText();
			}
			break;
		case RoomType.Treasure:
			builder.AppendLine("内容：宝箱");
			return "未知房：宝箱";
		}
		return "未知房：" + Describe(unknownRoomPreview.RoomType);
	}

	private static UnknownRoomPreview? TryPreviewUnknownRoom(Player player)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Invalid comparison between Unknown and I4
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Invalid comparison between Unknown and I4
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			ClonePlayerInClonedRun(player, out RunState runState);
			MapPointHistoryEntry currentMapPointHistoryEntry = runState.CurrentMapPointHistoryEntry;
			MapPoint currentMapPoint = runState.CurrentMapPoint;
			HashSet<RoomType> hashSet = RunManager.BuildRoomTypeBlacklist(currentMapPointHistoryEntry, (IReadOnlyCollection<MapPoint>)(((currentMapPoint != null) ? currentMapPoint.Children : null) ?? new HashSet<MapPoint>()));
			RoomType val = runState.Odds.UnknownMapPoint.Roll((IEnumerable<RoomType>)hashSet, (IRunState)(object)runState);
			return val is RoomType.Monster or RoomType.Elite or RoomType.Boss
				? new UnknownRoomPreview(val, runState.Act.PullNextEncounter(val), null)
				: (val == RoomType.Event ? new UnknownRoomPreview(val, null, runState.Act.PullNextEvent(runState)) : new UnknownRoomPreview(val, null, null));
		}
		catch
		{
			return null;
		}
	}

	private static void AppendHistory(StringBuilder builder, MapPointHistoryEntry history)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		builder.AppendLine("已进入过：");
		foreach (MapPointRoomHistoryEntry room in history.Rooms)
		{
			string text = Describe(room.RoomType);
			if (room.ModelId != (ModelId)null)
			{
				text += $" - {room.ModelId}";
			}
			StringBuilder stringBuilder = builder;
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder);
			handler.AppendLiteral("- ");
			handler.AppendFormatted(text);
			stringBuilder2.AppendLine(ref handler);
			if (room.MonsterIds.Count > 0)
			{
				stringBuilder = builder;
				StringBuilder stringBuilder3 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(5, 1, stringBuilder);
				handler.AppendLiteral("  敌人：");
				handler.AppendFormatted(string.Join("、", room.MonsterIds));
				stringBuilder3.AppendLine(ref handler);
			}
			if (room.TurnsTaken > 0)
			{
				stringBuilder = builder;
				StringBuilder stringBuilder4 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(5, 1, stringBuilder);
				handler.AppendLiteral("  回合：");
				handler.AppendFormatted(room.TurnsTaken);
				stringBuilder4.AppendLine(ref handler);
			}
		}
	}

	private static void AppendEncounter(StringBuilder builder, EncounterModel encounter)
	{
		if (encounter.CustomRewardDescription != null)
		{
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(5, 1, builder);
			handler.AppendLiteral("特殊奖励：");
			handler.AppendFormatted(encounter.CustomRewardDescription.GetFormattedText());
			builder.AppendLine(ref handler);
		}
	}

	private static void AppendEventOptions(StringBuilder builder, EventModel eventModel, Player player, MapPointType pointType)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		string[] array = TryPreviewEventOptions(player, eventModel, pointType);
		if (array.Length != 0)
		{
			builder.AppendLine("选项：");
			string[] array2 = array;
			foreach (string value in array2)
			{
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(2, 1, builder);
				handler.AppendLiteral("- ");
				handler.AppendFormatted(value);
				builder.AppendLine(ref handler);
			}
		}
	}

	private static string[] TryPreviewEventOptions(Player player, EventModel eventModel, MapPointType pointType)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Invalid comparison between Unknown and I4
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			RunState runState;
			Player val = ClonePlayerInClonedRun(player, out runState);
			EventModel byId = ModelDb.GetById<EventModel>(((AbstractModel)eventModel).Id);
			if ((int)pointType != 8)
			{
				runState.AddVisitedEvent(byId);
			}
			runState.AppendToMapPointHistory(pointType, (RoomType)6, ((AbstractModel)byId).Id);
			EventModel mutableEvent = byId.ToMutable();
			ForeseeEventCompat.BeginEvent(mutableEvent, val, false);
			try
			{
				return (from option in mutableEvent.CurrentOptions
					select FormatEventOption(option, mutableEvent.DynamicVars) into text
					where !string.IsNullOrWhiteSpace(text)
					select text).Take(4).ToArray();
			}
			finally
			{
				mutableEvent.EnsureCleanup();
			}
		}
		catch
		{
			return Array.Empty<string>();
		}
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

	private static void AppendCardRewardPreview(StringBuilder builder, RunState runState, MapPointType pointType, RoomType roomType)
	{
		Player[] players = GetPreviewPlayers(runState).ToArray();
		bool isMultiplayer = players.Length > 1;
		foreach (var pair in players.Select((player, index) => new { player, index }))
		{
			CombatRewardPreview? preview = TryPreviewCombatReward(pair.player, pointType, roomType);
			if (preview == null)
			{
				foreach (string line in ForeseeFormatting.FormatCombatRewardPreviewLines(
					PlayerLabel(pair.player, pair.index),
					Array.Empty<string>(),
					Array.Empty<string>(),
					isMultiplayer,
					fallbackLine: "\u5956\u52b1\u6682\u4e0d\u53ef\u9884\u6d4b"))
				{
					builder.AppendLine(line);
				}
				continue;
			}

			if (preview.ExtraRewardLines.Count == 0 && preview.CardLines.Count == 0)
			{
				continue;
			}

			foreach (string line in ForeseeFormatting.FormatCombatRewardPreviewLines(
				PlayerLabel(pair.player, pair.index),
				preview.ExtraRewardLines,
				preview.CardLines,
				isMultiplayer))
			{
				builder.AppendLine(line);
			}
		}
	}

	private sealed record CombatRewardPreview(IReadOnlyList<string> ExtraRewardLines, IReadOnlyList<string> CardLines);

	private static void AppendCardRewardPreview(StringBuilder builder, Player player, MapPointType pointType, RoomType roomType)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		IReadOnlyList<string> readOnlyList = TryPreviewCardReward(player, pointType, roomType);
		if (readOnlyList.Count != 0)
		{
			builder.AppendLine("掉牌预览：");
			builder.AppendLine(string.Join(" / ", readOnlyList));
		}
	}

	private static IReadOnlyList<string> TryPreviewCardReward(Player player, MapPointType pointType, RoomType roomType)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			RunState runState;
			Player obj = ClonePlayerInClonedRun(player, out runState);
			CombatRoom val = CreatePreviewRoom(runState, roomType);
			runState.AppendToMapPointHistory(pointType, ((AbstractRoom)val).RoomType, ((AbstractRoom)val).ModelId);
			runState.Act.MarkRoomVisited(((AbstractRoom)val).RoomType);
			CardReward? obj2 = RewardsCmd.GenerateForRoomEnd(obj, (AbstractRoom)(object)val).GetAwaiter().GetResult()
				.Rewards.OfType<CardReward>().FirstOrDefault();
			return ((obj2 != null) ? obj2.Cards.Select(FormatCard).ToArray() : null) ?? Array.Empty<string>();
		}
		catch
		{
			return Array.Empty<string>();
		}
	}

	private static CombatRewardPreview? TryPreviewCombatReward(Player player, MapPointType pointType, RoomType roomType)
	{
		try
		{
			Player previewPlayer = ClonePlayerInClonedRun(player, out RunState runState);
			CombatRoom room = CreatePreviewRoom(runState, roomType);
			runState.AppendToMapPointHistory(pointType, ((AbstractRoom)room).RoomType, ((AbstractRoom)room).ModelId);
			runState.Act.MarkRoomVisited(((AbstractRoom)room).RoomType);
			Reward[] rewards = FlattenRewards(RewardsCmd.GenerateForRoomEnd(previewPlayer, (AbstractRoom)(object)room).GetAwaiter().GetResult()
				.Rewards).ToArray();
			string[] cards = rewards.OfType<CardReward>().FirstOrDefault()?.Cards.Select(FormatCard).ToArray() ?? Array.Empty<string>();
			return new CombatRewardPreview(FormatExtraRewardLines(rewards), cards);
		}
		catch
		{
			return null;
		}
	}

	private static IEnumerable<Reward> FlattenRewards(IEnumerable<Reward> rewards)
	{
		foreach (Reward reward in rewards)
		{
			if (reward is LinkedRewardSet linkedRewardSet)
			{
				foreach (Reward child in FlattenRewards(linkedRewardSet.Rewards))
				{
					yield return child;
				}
			}
			else
			{
				yield return reward;
			}
		}
	}

	private static IReadOnlyList<string> FormatExtraRewardLines(IReadOnlyList<Reward> rewards)
	{
		List<string> lines = new List<string>();
		int[] gold = rewards.OfType<GoldReward>()
			.Where(reward => reward.Amount >= 0)
			.Select(reward => reward.Amount)
			.ToArray();
		if (gold.Length != 0)
		{
			lines.Add($"\u91d1\u5e01\uff1a{string.Join(" / ", gold)}");
		}

		string[] potions = rewards.OfType<PotionReward>()
			.Select(reward => reward.Potion)
			.Where(potion => potion != null)
			.Select(FormatPotion)
			.ToArray();
		if (potions.Length != 0)
		{
			lines.Add($"\u836f\u6c34\uff1a{string.Join(" / ", potions)}");
		}

		string[] relics = rewards.OfType<RelicReward>()
			.Select(reward => reward.Relic)
			.Where(relic => relic != null)
			.Select(FormatRelic)
			.ToArray();
		if (relics.Length != 0)
		{
			lines.Add($"\u9057\u7269\uff1a{string.Join(" / ", relics)}");
		}

		return lines;
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

	private static string PlayerLabel(Player player, int index)
	{
		Player? localPlayer = LocalContext.GetMe(player.RunState.Players);
		return localPlayer != null && player.NetId == localPlayer.NetId
			? "\u4f60"
			: $"\u73a9\u5bb6 {index + 1}";
	}

	private static void AppendShopPreview(StringBuilder builder, Player player, MapPointType pointType)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		IReadOnlyList<string> readOnlyList = TryPreviewShopItems(player, pointType);
		if (readOnlyList.Count == 0)
		{
			return;
		}
		builder.AppendLine("商店预览：");
		foreach (string item in readOnlyList)
		{
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(2, 1, builder);
			handler.AppendLiteral("- ");
			handler.AppendFormatted(item);
			builder.AppendLine(ref handler);
		}
	}

	private static IReadOnlyList<string> TryPreviewShopItems(Player player, MapPointType pointType)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			RunState runState;
			Player obj = ClonePlayerInClonedRun(player, out runState);
			runState.AppendToMapPointHistory(pointType, (RoomType)5, (ModelId)null);
			List<string> list = new List<string>();
			MerchantInventory val = MerchantInventory.CreateForNormalMerchant(obj);
			foreach (MerchantCardEntry item in val.CharacterCardEntries.Concat(val.ColorlessCardEntries))
			{
				if (item.CreationResult != null)
				{
					list.Add($"卡牌：{FormatCard(item.CreationResult.Card)} {((MerchantEntry)item).Cost}金{(item.IsOnSale ? "（半价）" : "")}");
				}
			}
			foreach (MerchantRelicEntry relicEntry in val.RelicEntries)
			{
				if (relicEntry.Model != null)
				{
					list.Add($"遗物：{FormatRelic(relicEntry.Model)} {((MerchantEntry)relicEntry).Cost}金");
				}
			}
			foreach (MerchantPotionEntry potionEntry in val.PotionEntries)
			{
				if (potionEntry.Model != null)
				{
					list.Add($"药水：{FormatPotion(potionEntry.Model)} {((MerchantEntry)potionEntry).Cost}金");
				}
			}
			return list;
		}
		catch
		{
			return Array.Empty<string>();
		}
	}

	private static CardModel? PickShopCard(Player player, IEnumerable<CardModel> pool, CardType type, HashSet<ModelId> selected)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		CardModel[] cards = pool.Where((CardModel card) => card.Type == type && !selected.Contains(((AbstractModel)card).Id)).ToArray();
		CardRarity rarity = player.PlayerOdds.CardRarity.RollWithoutChangingFutureOdds((CardRarityOddsType)4);
		rarity = NextAllowedRarity(rarity, (CardRarity r) => cards.Any((CardModel card) => card.Rarity == r));
		if ((int)rarity != 0)
		{
			return player.PlayerRng.Shops.NextItem<CardModel>((IEnumerable<CardModel>)cards.Where((CardModel card) => card.Rarity == rarity).ToArray());
		}
		return null;
	}

	private static int RollShopPrice(Rng rng, CardModel card, bool onSale)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Invalid comparison between Unknown and I4
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		CardRarity rarity = card.Rarity;
		int num = (((int)rarity == 3) ? 75 : (((int)rarity != 4) ? 50 : 150));
		int num2 = num;
		if (card.Pool is ColorlessCardPool)
		{
			num2 = Mathf.RoundToInt((float)num2 * 1.15f);
		}
		int num3 = Mathf.RoundToInt((float)num2 * rng.NextFloat(0.95f, 1.05f));
		if (!onSale)
		{
			return num3;
		}
		return num3 / 2;
	}

	private static CardRarity NextAllowedRarity(CardRarity rarity, Func<CardRarity, bool> isAllowed)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		HashSet<CardRarity> hashSet = new HashSet<CardRarity>();
		while (!isAllowed(rarity) && (int)rarity != 0)
		{
			if (!hashSet.Add(rarity))
			{
				return (CardRarity)0;
			}
			rarity = CardRarityExtensions.GetNextHighestRarityWithWrapping(rarity);
		}
		return rarity;
	}

	private static Player ClonePlayerForPreview(Player player)
	{
		Player obj = Player.FromSerializable(player.ToSerializable());
		obj.RunState = player.RunState;
		return obj;
	}

	private static Player ClonePlayerInClonedRun(Player player, out RunState runState)
	{
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Expected O, but got Unknown
		SerializableRun val = RunManager.Instance.ToSave((AbstractRoom)null);
		runState = RunState.FromSerializable(val);
		SerializableActMap val2 = ((val.Acts.Count > runState.CurrentActIndex) ? val.Acts[runState.CurrentActIndex].SavedMap : null);
		if (val2 != null)
		{
			runState.Map = (ActMap)new SavedActMap(val2);
		}
		return ((IEnumerable<Player>)runState.Players).FirstOrDefault((Func<Player, bool>)((Player p) => p.NetId == player.NetId)) ?? throw new InvalidOperationException($"Unable to find cloned player {player.NetId}.");
	}

	private static CombatRoom CreatePreviewRoom(RunState runState, RoomType roomType)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0004: Invalid comparison between Unknown and I4
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Expected O, but got Unknown
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		if (roomType is not (RoomType.Monster or RoomType.Elite or RoomType.Boss))
		{
			throw new ArgumentOutOfRangeException("roomType", roomType, null);
		}
		return new CombatRoom(runState.Act.PullNextEncounter(roomType).ToMutable(), (IRunState)(object)runState);
	}

	private static EventModel? PeekNextEvent(RunState runState)
	{
		object? obj = typeof(ActModel).GetField("_rooms", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(runState.Act);
		RoomSet val = (RoomSet)((obj is RoomSet) ? obj : null);
		if (val == null || val.events.Count == 0)
		{
			return null;
		}
		for (int i = 0; i < val.events.Count; i++)
		{
			int index = (val.eventsVisited + i) % val.events.Count;
			EventModel val2 = val.events[index];
			if (val2.IsAllowed((IRunState)(object)runState) && !runState.VisitedEventIds.Contains(((AbstractModel)val2).Id))
			{
				return val2;
			}
		}
		return val.NextEvent;
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

	private static string NameOf(EventModel eventModel)
	{
		string formattedText = eventModel.Title.GetFormattedText();
		if (!string.IsNullOrWhiteSpace(formattedText))
		{
			return formattedText;
		}
		return ((object)((AbstractModel)eventModel).Id).ToString();
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

	private static string FormatRarity(CardRarity rarity)
	{
		return rarity switch
		{
			CardRarity.Common => "\u666e\u901a",
			CardRarity.Uncommon => "\u7f55\u89c1",
			CardRarity.Rare => "\u7a00\u6709",
			_ => rarity.ToString(),
		};
	}

	static MapForesee()
	{
		ShopCardTypes = new[]
		{
			CardType.Attack,
			CardType.Attack,
			CardType.Skill,
			CardType.Skill,
			CardType.Power,
		};
	}
}
