using System.Collections.Generic;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace foresee.Scripts;

public static class ForeseeFormatting
{
	public static string ColorCardNameForRarity(string name, bool isUpgraded, CardRarity rarity)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		return ColorName(name + (isUpgraded ? "+" : string.Empty), RarityColor(rarity));
	}

	public static string ColorRelicNameForRarity(string name, RelicRarity rarity)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return ColorName(name, RarityColor(rarity));
	}

	public static string ColorPotionNameForRarity(string name, PotionRarity rarity)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return ColorName(name, RarityColor(rarity));
	}

	private static string ColorName(string name, string color)
	{
		return $"[color={color}]{name}[/color]";
	}

	private static string RarityColor(CardRarity rarity)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Invalid comparison between Unknown and I4
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Invalid comparison between Unknown and I4
		if ((int)rarity != 3)
		{
			if ((int)rarity == 4)
			{
				return "#ffd700";
			}
			return "#ffffff";
		}
		return "#4aa3ff";
	}

	private static string RarityColor(RelicRarity rarity)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Invalid comparison between Unknown and I4
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Invalid comparison between Unknown and I4
		if ((int)rarity != 3)
		{
			if ((int)rarity == 4)
			{
				return "#ffd700";
			}
			return "#ffffff";
		}
		return "#4aa3ff";
	}

	private static string RarityColor(PotionRarity rarity)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Invalid comparison between Unknown and I4
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Invalid comparison between Unknown and I4
		if ((int)rarity != 2)
		{
			if ((int)rarity == 3)
			{
				return "#ffd700";
			}
			return "#ffffff";
		}
		return "#4aa3ff";
	}

	public static string SanitizeEventText(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}
		return Regex.Replace(text, "\\{[^{}]+\\}", "?");
	}

	public static void AddEventDynamicVarsTo(LocString locString, DynamicVarSet dynamicVars)
	{
		dynamicVars.AddTo(locString);
	}

	public static string FormatPlayerPreviewLines(string playerLabel, IReadOnlyList<string> lines, bool isMultiplayer)
	{
		string text = string.Join(" / ", lines);
		if (!isMultiplayer)
		{
			return text;
		}
		return playerLabel + "：" + text;
	}
	public static IReadOnlyList<string> FormatCombatRewardPreviewLines(
		string playerLabel,
		IReadOnlyList<string> extraRewardLines,
		IReadOnlyList<string> cardLines,
		bool isMultiplayer,
		string detailIndent = "",
		string? fallbackLine = null)
	{
		List<string> lines = new List<string>();
		string prefix = isMultiplayer ? detailIndent : string.Empty;
		if (isMultiplayer)
		{
			lines.Add(playerLabel + "\uff1a");
		}

		foreach (string line in extraRewardLines)
		{
			if (!string.IsNullOrWhiteSpace(line))
			{
				lines.Add(prefix + line);
			}
		}

		if (cardLines.Count != 0)
		{
			lines.Add(prefix + "\u6389\u724c\u9884\u89c8\uff1a");
			lines.Add(prefix + string.Join(" / ", cardLines));
		}
		else if (extraRewardLines.Count == 0 && !string.IsNullOrWhiteSpace(fallbackLine))
		{
			lines.Add(prefix + fallbackLine);
		}

		return lines;
	}
}
