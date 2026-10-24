using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace foresee.Scripts;

public static class ForeseeEventCompat
{
	public static int? SelectBeginEventParameterCount(IEnumerable<int> parameterCounts)
	{
		HashSet<int> counts = parameterCounts.ToHashSet();
		if (counts.Contains(3))
		{
			return 3;
		}
		if (counts.Contains(2))
		{
			return 2;
		}
		return null;
	}

	public static void BeginEvent(EventModel eventModel, Player player, bool isPreFinished)
	{
		MethodInfo[] candidates = eventModel.GetType()
			.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			.Where(method => method.Name == "BeginEvent")
			.ToArray();
		int? parameterCount = SelectBeginEventParameterCount(candidates.Select(method => method.GetParameters().Length));
		MethodInfo? method = candidates.FirstOrDefault(candidate => candidate.GetParameters().Length == parameterCount);
		if (method == null)
		{
			throw new MissingMethodException(eventModel.GetType().FullName, "BeginEvent");
		}

		object? result = parameterCount == 3
			? method.Invoke(eventModel, new object?[] { player, null, isPreFinished })
			: method.Invoke(eventModel, new object?[] { player, isPreFinished });
		CompleteAwaitable(result);
	}

	public static void CompleteAwaitable(object? result)
	{
		if (result == null)
		{
			return;
		}
		if (result is Task task)
		{
			task.GetAwaiter().GetResult();
			return;
		}
		MethodInfo? getAwaiter = result.GetType().GetMethod("GetAwaiter", BindingFlags.Instance | BindingFlags.Public);
		object? awaiter = getAwaiter?.Invoke(result, null);
		MethodInfo? getResult = awaiter?.GetType().GetMethod("GetResult", BindingFlags.Instance | BindingFlags.Public);
		getResult?.Invoke(awaiter, null);
	}
}
