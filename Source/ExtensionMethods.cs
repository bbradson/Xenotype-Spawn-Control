// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

namespace XenotypeSpawnControl;

public static class ExtensionMethods
{
	public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key)
		where TValue : new() where TKey : notnull
	{
		if (!dictionary.TryGetValue(key, out var value) || value is null)
			dictionary[key] = value = new();

		return value;
	}

	public static int Sum<TKey>(this List<TKey> list, Func<TKey, int> selector)
	{
		var sum = 0;
		for (var i = 0; i < list.Count; i++)
			sum += selector(list[i]);
		return sum;
	}

	public static float Sum<TKey>(this List<TKey> list, Func<TKey, float> selector)
	{
		var sum = 0f;
		for (var i = 0; i < list.Count; i++)
			sum += selector(list[i]);
		return sum;
	}
}