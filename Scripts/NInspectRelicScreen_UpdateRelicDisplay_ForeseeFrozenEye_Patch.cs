using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;

namespace foresee.Scripts;

[HarmonyPatch(typeof(NInspectRelicScreen), "UpdateRelicDisplay")]
public static class NInspectRelicScreen_UpdateRelicDisplay_ForeseeFrozenEye_Patch
{
	private const string IconPath = "res://FrozenEye.png";

	private static readonly FieldInfo? RelicsField = typeof(NInspectRelicScreen).GetField("_relics", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly FieldInfo? IndexField = typeof(NInspectRelicScreen).GetField("_index", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly FieldInfo? NameLabelField = typeof(NInspectRelicScreen).GetField("_nameLabel", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly FieldInfo? RarityLabelField = typeof(NInspectRelicScreen).GetField("_rarityLabel", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly FieldInfo? DescriptionField = typeof(NInspectRelicScreen).GetField("_description", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly FieldInfo? FlavorField = typeof(NInspectRelicScreen).GetField("_flavor", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly FieldInfo? RelicImageField = typeof(NInspectRelicScreen).GetField("_relicImage", BindingFlags.Instance | BindingFlags.NonPublic);

	public static void Postfix(NInspectRelicScreen __instance)
	{
		if (!TryGetCurrentRelic(__instance, out RelicModel? relic) || !ForeseeFrozenEye.IsForeseeRelic(relic))
		{
			return;
		}

		(NameLabelField?.GetValue(__instance) as MegaLabel)?.SetTextAutoSize("冻结之眼");
		(RarityLabelField?.GetValue(__instance) as MegaLabel)?.SetTextAutoSize("工具");
		(DescriptionField?.GetValue(__instance) as MegaRichTextLabel)?.SetTextAutoSize(ForeseeFrozenEye.GetInspectDescription());
		(FlavorField?.GetValue(__instance) as MegaRichTextLabel)?.SetTextAutoSize("看清前路，也可以选择闭上眼。");

		TextureRect? image = RelicImageField?.GetValue(__instance) as TextureRect;
		Texture2D? icon = ResourceLoader.Load<Texture2D>(IconPath, null, ResourceLoader.CacheMode.Reuse);
		if (image != null && icon != null)
		{
			image.SelfModulate = Colors.White;
			image.Texture = icon;
		}
	}

	private static bool TryGetCurrentRelic(NInspectRelicScreen screen, out RelicModel? relic)
	{
		relic = null;
		if (RelicsField?.GetValue(screen) is not IReadOnlyList<RelicModel> relics || IndexField?.GetValue(screen) is not int index)
		{
			return false;
		}
		if (index < 0 || index >= relics.Count)
		{
			return false;
		}
		relic = relics[index];
		return true;
	}
}
