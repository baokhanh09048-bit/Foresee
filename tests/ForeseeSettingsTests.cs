using foresee.Scripts;
using MegaCrit.Sts2.Core.Models;
using Xunit;

namespace foresee.Tests;

public sealed class ForeseeSettingsTests
{
	[Fact]
	public void ResetForSession_EnablesForecasting()
	{
		ForeseeSettings.ToggleForecasting();

		ForeseeSettings.ResetForSession();

		Assert.True(ForeseeSettings.ForecastingEnabled);
	}

	[Fact]
	public void ToggleForecasting_TogglesForecastingButKeepsRoutePlanningAvailable()
	{
		ForeseeSettings.ResetForSession();

		var enabledAfterFirstToggle = ForeseeSettings.ToggleForecasting();
		var enabledAfterSecondToggle = ForeseeSettings.ToggleForecasting();

		Assert.False(enabledAfterFirstToggle);
		Assert.True(enabledAfterSecondToggle);
		Assert.True(ForeseeSettings.RoutePlanningEnabled);
	}

	[Fact]
	public void FrozenEyeTool_DoesNotRegisterAsGameRelicModel()
	{
		Assert.False(typeof(RelicModel).IsAssignableFrom(typeof(ForeseeFrozenEye)));
	}

	[Fact]
	public void FrozenEyeTool_HoverDescriptionIsChineseAndShowsCurrentState()
	{
		ForeseeSettings.ResetForSession();

		string enabledDescription = ForeseeFrozenEye.GetHoverDescription();
		ForeseeSettings.ToggleForecasting();
		string disabledDescription = ForeseeFrozenEye.GetHoverDescription();

		Assert.Contains("预见：开启", enabledDescription);
		Assert.Contains("右键：关闭预见", enabledDescription);
		Assert.Contains("左键：查看说明", enabledDescription);
		Assert.Contains("预见：关闭", disabledDescription);
		Assert.Contains("右键：开启预见", disabledDescription);
	}

	[Fact]
	public void FrozenEyeTool_InspectDescriptionKeepsCoreInfoButStaysCompact()
	{
		string description = ForeseeFrozenEye.GetInspectDescription();

		Assert.True(description.Length <= 70);
		Assert.Contains("右键", description);
		Assert.Contains("路线", description);
		Assert.Contains("重启", description);
	}

	[Fact]
	public void FrozenEyeTool_AlwaysTargetsLeftmostInventorySlot()
	{
		Assert.Equal(0, ForeseeFrozenEye.InventoryIndex);
	}

	[Fact]
	public void FrozenEyeTool_OffsetsRealRelicUiIndexByOne()
	{
		Assert.Equal(1, ForeseeFrozenEye.InventoryIndexForPlayerRelic(0));
		Assert.Equal(4, ForeseeFrozenEye.InventoryIndexForPlayerRelic(3));
	}
}
