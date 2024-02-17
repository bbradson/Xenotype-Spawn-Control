// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using System.Linq;

namespace XenotypeSpawnControl;

public static class XenotypeChanceDatabase<T> where T : Def
{
	private static Dictionary<string, XenotypeChances<T>> _allValues = new();

	public static XenotypeChances<T> For(string defName) => _allValues.GetOrAdd(defName);

	public static string From(XenotypeChances<T> xenotypeChances) => _allValues.First(pair => pair.Value == xenotypeChances).Key;

	public static void ResetAll()
	{
		foreach (var xenotypeChances in _allValues.Values)
			xenotypeChances.Reset();
	}

	public static void ExposeData()
	{
		if (Scribe.mode == LoadSaveMode.Saving
			&& !_allValues.Any(pair
				=> pair.Value.RequiresSaving()))
		{
			return;
		}

		Scribe_Collections.Look(ref _allValues, $"XenotypesBy{typeof(T).Name}", LookMode.Value, LookMode.Deep);

		_allValues ??= new();
	}
}