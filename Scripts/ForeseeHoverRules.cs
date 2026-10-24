namespace foresee.Scripts;

public static class ForeseeHoverRules
{
	public static bool ShouldShowForecast(bool isVisited, bool isCurrentOrPast, bool isNextReachable, bool isOnRoute)
	{
		if (!isVisited && !isCurrentOrPast)
		{
			return isNextReachable || isOnRoute;
		}
		return false;
	}

	public static bool ShouldShowRouteFallback(bool isOnRoute, bool routeForecastBuilt, bool hasDirectRoomPreview)
	{
		return isOnRoute && !routeForecastBuilt && !hasDirectRoomPreview;
	}
}
