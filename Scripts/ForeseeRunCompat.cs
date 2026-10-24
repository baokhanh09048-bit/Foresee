using System.Globalization;
using System.Reflection;

namespace foresee.Scripts;

public static class ForeseeRunCompat
{
	public static string GetSeedKey(object? rngSet)
	{
		if (rngSet == null)
		{
			return "0";
		}

		Type rngSetType = rngSet.GetType();
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		if (rngSetType.GetProperty("StringSeed", flags)?.GetValue(rngSet) is string stringSeed
			&& !string.IsNullOrEmpty(stringSeed))
		{
			return stringSeed;
		}

		object? seed = rngSetType.GetProperty("Seed", flags)?.GetValue(rngSet);
		return Convert.ToString(seed, CultureInfo.InvariantCulture) ?? "0";
	}
}
