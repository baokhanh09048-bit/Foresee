using foresee.Scripts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Map;
using Xunit;

namespace foresee.Tests;

public sealed class MapForeseeFormattingTests
{
    [Fact]
    public void RunCompat_GetSeedKey_PrefersStableStringSeed()
    {
        Assert.Equal("seed-text", ForeseeRunCompat.GetSeedKey(new StringSeedRng("seed-text", 99UL)));
    }

    [Fact]
    public void RunCompat_GetSeedKey_SupportsOldUIntSeedGetter()
    {
        Assert.Equal("42", ForeseeRunCompat.GetSeedKey(new UIntSeedRng(42U)));
    }

    [Fact]
    public void RunCompat_GetSeedKey_SupportsNewULongSeedGetter()
    {
        Assert.Equal("4294967296", ForeseeRunCompat.GetSeedKey(new ULongSeedRng(4294967296UL)));
    }

    [Theory]
    [InlineData(CardRarity.Common, "#ffffff", "Strike")]
    [InlineData(CardRarity.Uncommon, "#4aa3ff", "Coolheaded")]
    [InlineData(CardRarity.Rare, "#ffd700", "Echo Form")]
    public void ColorCardNameForRarity_UsesExpectedColors(CardRarity rarity, string color, string name)
    {
        var formatted = ForeseeFormatting.ColorCardNameForRarity(name, isUpgraded: false, rarity);

        Assert.Equal($"[color={color}]{name}[/color]", formatted);
    }

    [Fact]
    public void ColorCardNameForRarity_KeepsUpgradeInsideColor()
    {
        var formatted = ForeseeFormatting.ColorCardNameForRarity("Echo Form", isUpgraded: true, CardRarity.Rare);

        Assert.Equal("[color=#ffd700]Echo Form+[/color]", formatted);
    }

    [Fact]
    public void SanitizeEventText_PreservesConcreteNumbersAndPercentages()
    {
        var sanitized = ForeseeFormatting.SanitizeEventText("Lose 12 HP. Gain 25% bonus. Receive 42 gold.");

        Assert.Equal("Lose 12 HP. Gain 25% bonus. Receive 42 gold.", sanitized);
    }

    [Fact]
    public void SanitizeEventText_ReplacesUnresolvedPlaceholdersOnly()
    {
        var sanitized = ForeseeFormatting.SanitizeEventText("Lose {Damage} HP. Gain 12 gold.");

        Assert.Equal("Lose ? HP. Gain 12 gold.", sanitized);
    }

    [Fact]
    public void ColorCardNameForRarity_UsesWhiteForUnknownRarity()
    {
        var formatted = ForeseeFormatting.ColorCardNameForRarity("Mystery", isUpgraded: false, CardRarity.None);

        Assert.Equal("[color=#ffffff]Mystery[/color]", formatted);
    }

    [Theory]
    [InlineData(RelicRarity.Common, "#ffffff", "Anchor")]
    [InlineData(RelicRarity.Uncommon, "#4aa3ff", "Orichalcum")]
    [InlineData(RelicRarity.Rare, "#ffd700", "Old Coin")]
    public void ColorRelicNameForRarity_UsesExpectedColors(RelicRarity rarity, string color, string name)
    {
        var formatted = ForeseeFormatting.ColorRelicNameForRarity(name, rarity);

        Assert.Equal($"[color={color}]{name}[/color]", formatted);
    }

    [Theory]
    [InlineData(PotionRarity.Common, "#ffffff", "Fire Potion")]
    [InlineData(PotionRarity.Uncommon, "#4aa3ff", "Dexterity Potion")]
    [InlineData(PotionRarity.Rare, "#ffd700", "Entropic Brew")]
    public void ColorPotionNameForRarity_UsesExpectedColors(PotionRarity rarity, string color, string name)
    {
        var formatted = ForeseeFormatting.ColorPotionNameForRarity(name, rarity);

        Assert.Equal($"[color={color}]{name}[/color]", formatted);
    }

    [Fact]
    public void ColorRelicAndPotionNames_UseWhiteForSpecialRarities()
    {
        Assert.Equal("[color=#ffffff]Shop Relic[/color]", ForeseeFormatting.ColorRelicNameForRarity("Shop Relic", RelicRarity.Shop));
        Assert.Equal("[color=#ffffff]Event Potion[/color]", ForeseeFormatting.ColorPotionNameForRarity("Event Potion", PotionRarity.Event));
    }

    [Theory]
    [InlineData(false, false, true, false, true)]
    [InlineData(false, false, false, true, true)]
    [InlineData(false, false, false, false, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(false, true, true, true, false)]
    public void HoverRules_ShowOnlyUnvisitedNextReachableOrRouteRooms(
        bool isVisited,
        bool isCurrentOrPast,
        bool isNextReachable,
        bool isOnRoute,
        bool expected)
    {
        var shouldShow = ForeseeHoverRules.ShouldShowForecast(isVisited, isCurrentOrPast, isNextReachable, isOnRoute);

        Assert.Equal(expected, shouldShow);
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, false, false, false)]
    public void HoverRules_ShowRouteFallbackOnlyWhenRouteForecastAndDirectPreviewAreUnavailable(
        bool isOnRoute,
        bool routeForecastBuilt,
        bool hasDirectRoomPreview,
        bool expected)
    {
        var shouldShow = ForeseeHoverRules.ShouldShowRouteFallback(isOnRoute, routeForecastBuilt, hasDirectRoomPreview);

        Assert.Equal(expected, shouldShow);
    }

    [Fact]
    public void MapForesee_TryBuildForecastSafely_DowngradesRouteForecastExceptions()
    {
        bool built = MapForesee.TryBuildForecastSafely(
            () => throw new InvalidOperationException("forecast failed"),
            out MapRoutePlanner.ForecastTip tip);

        Assert.False(built);
        Assert.Equal(string.Empty, tip.Title);
        Assert.Equal(string.Empty, tip.Description);
    }

    [Fact]
    public void MapForesee_TryBuildRouteFailureFallback_ReturnsVisibleRouteTipForRouteRooms()
    {
        bool built = MapForesee.TryBuildRouteFailureFallback(
            MapPointType.Monster,
            routeIndex: 4,
            isOnRoute: true,
            out string title,
            out string description);

        Assert.True(built);
        Assert.Equal("\u8def\u7ebf\u623f\u95f4", title);
        Assert.Contains("\u89c4\u5212\u8def\u7ebf\u7b2c 4 \u6b65", description);
        Assert.Contains("\u8def\u7ebf\u9884\u6d4b\u6682\u4e0d\u53ef\u7528", description);
    }

    [Fact]
    public void MapForesee_TryBuildForecastFailureFallback_ReturnsGenericVisibleTipForEligibleNonRouteRooms()
    {
        bool built = MapForesee.TryBuildForecastFailureFallback(
            MapPointType.Monster,
            routeIndex: 0,
            isOnRoute: false,
            isForecastEligible: true,
            out string title,
            out string description);

        Assert.True(built);
        Assert.Equal("\u666e\u901a\u6218\u6597", title);
        Assert.Equal("\u9884\u6d4b\u6682\u4e0d\u53ef\u7528\u3002", description);
    }

    [Fact]
    public void AddEventDynamicVarsTo_AddsKnownEventVariablesToLocString()
    {
        var locString = new LocString("events", "BYRDONIS_NEST.pages.INITIAL.options.EAT.title");
        var vars = new DynamicVarSet(new DynamicVar[]
        {
            new MaxHpVar(7m),
            new StringVar("Card", "Byrdonis Egg"),
        });

        ForeseeFormatting.AddEventDynamicVarsTo(locString, vars);

        Assert.True(locString.Variables.ContainsKey("MaxHp"));
        Assert.True(locString.Variables.ContainsKey("Card"));
        Assert.Equal("7", locString.Variables["MaxHp"].ToString());
        Assert.Equal("Byrdonis Egg", locString.Variables["Card"].ToString());
    }

    [Fact]
    public void FormatPlayerPreviewLines_UsesCompactSinglePlayerOutput()
    {
        var text = ForeseeFormatting.FormatPlayerPreviewLines("你", new[] { "Strike", "Defend" }, isMultiplayer: false);

        Assert.Equal("Strike / Defend", text);
    }

    [Fact]
    public void FormatPlayerPreviewLines_PrefixesMultiplayerOutputWithPlayerLabel()
    {
        var text = ForeseeFormatting.FormatPlayerPreviewLines("玩家 2", new[] { "Strike", "Defend" }, isMultiplayer: true);

        Assert.Equal("玩家 2：Strike / Defend", text);
    }
    [Fact]
    public void FormatCombatRewardPreviewLines_IncludesExtraRewardsAndCardsForSinglePlayer()
    {
        var lines = ForeseeFormatting.FormatCombatRewardPreviewLines(
            "\u4f60",
            new[] { "\u91d1\u5e01\uff1a25", "\u9057\u7269\uff1aAnchor" },
            new[] { "Strike", "Defend" },
            isMultiplayer: false);

        Assert.Equal(new[]
        {
            "\u91d1\u5e01\uff1a25",
            "\u9057\u7269\uff1aAnchor",
            "\u6389\u724c\u9884\u89c8\uff1a",
            "Strike / Defend",
        }, lines);
    }

    [Fact]
    public void FormatCombatRewardPreviewLines_GroupsMultiplayerRewardsUnderPlayerLabel()
    {
        var lines = ForeseeFormatting.FormatCombatRewardPreviewLines(
            "\u73a9\u5bb6 2",
            new[] { "\u836f\u6c34\uff1aFire Potion" },
            new[] { "Strike" },
            isMultiplayer: true,
            detailIndent: "  ");

        Assert.Equal(new[]
        {
            "\u73a9\u5bb6 2\uff1a",
            "  \u836f\u6c34\uff1aFire Potion",
            "  \u6389\u724c\u9884\u89c8\uff1a",
            "  Strike",
        }, lines);
    }

    [Fact]
    public void FormatCombatRewardPreviewLines_KeepsMultiplayerPlayerVisibleWhenPreviewFails()
    {
        var lines = ForeseeFormatting.FormatCombatRewardPreviewLines(
            "\u73a9\u5bb6 2",
            Array.Empty<string>(),
            Array.Empty<string>(),
            isMultiplayer: true,
            detailIndent: "  ",
            fallbackLine: "\u5956\u52b1\u6682\u4e0d\u53ef\u9884\u6d4b");

        Assert.Equal(new[]
        {
            "\u73a9\u5bb6 2\uff1a",
            "  \u5956\u52b1\u6682\u4e0d\u53ef\u9884\u6d4b",
        }, lines);
    }

    [Theory]
    [InlineData(new[] { 3 }, 3)]
    [InlineData(new[] { 2 }, 2)]
    [InlineData(new[] { 2, 3 }, 3)]
    [InlineData(new[] { 1, 4 }, null)]
    public void EventCompat_SelectsBeginEventSignatureForCurrentAndOldGameVersions(int[] parameterCounts, int? expected)
    {
        var selected = ForeseeEventCompat.SelectBeginEventParameterCount(parameterCounts);

        Assert.Equal(expected, selected);
    }

    [Fact]
    public void EventCompat_CompletesCustomAwaitableResults()
    {
        var awaiter = new FakeAwaiter();

        ForeseeEventCompat.CompleteAwaitable(new FakeAwaitable(awaiter));

        Assert.True(awaiter.Completed);
    }

    private sealed class FakeAwaitable
    {
        private readonly FakeAwaiter _awaiter;

        public FakeAwaitable(FakeAwaiter awaiter)
        {
            _awaiter = awaiter;
        }

        public FakeAwaiter GetAwaiter()
        {
            return _awaiter;
        }
    }

    private sealed class FakeAwaiter
    {
        public bool Completed { get; private set; }

        public void GetResult()
        {
            Completed = true;
        }
    }

    private sealed record StringSeedRng(string StringSeed, ulong Seed);

    private sealed record UIntSeedRng(uint Seed);

    private sealed record ULongSeedRng(ulong Seed);
}
