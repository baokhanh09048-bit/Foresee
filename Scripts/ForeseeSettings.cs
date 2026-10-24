namespace foresee.Scripts;

public static class ForeseeSettings
{
	public static bool ForecastingEnabled { get; private set; } = true;

	public static bool RoutePlanningEnabled => true;

	public static void ResetForSession()
	{
		ForecastingEnabled = true;
	}

	public static bool ToggleForecasting()
	{
		ForecastingEnabled = !ForecastingEnabled;
		return ForecastingEnabled;
	}
}
