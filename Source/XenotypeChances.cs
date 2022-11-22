// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using System.Linq;

namespace XenotypeSpawnControl;

public class XenotypeChances<T> : IExposable where T : Def
{
	public Dictionary<string, Chance> AllSavedValues => _allSavedValues;
	private Dictionary<string, Chance> _allSavedValues = new();

	public List<(ModifiableXenotype Xenotype, Chance Chance)> CustomXenotypes { get; } = new();

	public string DefName => _defName ??= GetNameFromDatabase();
	private string? _defName;

	public T? Def
		=> _def ??= DefName == Strings.NoFactionKey ? (T)(Def)EmptyFactionDef.Instance
		: DefDatabase<T>.GetNamedSilentFail(DefName);

	private T? _def;

	public Dictionary<string, (ModifiableXenotype Xenotype, Chance Chance)> AllActiveXenotypes { get; } = new();

	public void SetXenotypeChanceForDef(T def, XenotypeDef xenotypeDef, int rawChanceValue)
	{
		if (def is EmptyFactionDef)
			SetXenotypeChanceForEmptyFaction(xenotypeDef, rawChanceValue);
		else
			SetChanceInXenotypeSet(ref _xenotypeSetRef(def), xenotypeDef, rawChanceValue);
	}

	public float? GetXenotypeChance(T def, XenotypeDef xenotypeDef)
		=> def is EmptyFactionDef
		? GetXenotypeChanceForEmptyFaction(xenotypeDef)
		: xenotypeDef == XenotypeDefOf.Baseliner
		? GetBaselinerChance()
		: FindChanceInXenotypeSet(_xenotypeSetRef(def), xenotypeDef);

	private static float GetXenotypeChanceForEmptyFaction(XenotypeDef xenotypeDef)
		=> xenotypeDef.factionlessGenerationWeight / 50f;

	private static void SetXenotypeChanceForEmptyFaction(XenotypeDef xenotypeDef, int rawChanceValue)
		=> xenotypeDef.factionlessGenerationWeight
		= rawChanceValue / 20f;

	private float GetBaselinerChanceForEmptyFaction()
		=> 1f
		- ((DefDatabase<XenotypeDef>.AllDefsListForReading.Sum(def
			=> def == XenotypeDefOf.Baseliner ? 0f : def.factionlessGenerationWeight) / 50f)
		+ GetCustomXenotypeChanceSum());

	public float GetBaselinerChance()
		=> Def is EmptyFactionDef
		? GetBaselinerChanceForEmptyFaction()
		: (_xenotypeSetRef(Def!)?.BaselinerChance ?? 1f) - GetCustomXenotypeChanceSum();

	public ref XenotypeSet? GetXenotypeSet(T def) => ref _xenotypeSetRef(def);

	private static float? FindChanceInXenotypeSet(XenotypeSet? xenotypeSet, XenotypeDef? xenotypeDef)
	{
		if (xenotypeSet?.xenotypeChances is not { } xenotypeChances)
			return null;

		for (var i = 0; i < xenotypeChances.Count; i++)
		{
			if (xenotypeChances[i].xenotype == xenotypeDef)
				return xenotypeChances[i].chance;
		}

		return null;
	}

	private static AccessTools.FieldRef<T, XenotypeSet?> _xenotypeSetRef = XenotypeSetRefs.GetForType<T>();

	public void SetChanceForXenotype(string defName, int chance)
	{
		if (GetOrAddXenotypeAndChance(defName)?.Xenotype is { } xenotype)
			SetChanceForXenotype(xenotype, chance);
	}

	public float GetCustomXenotypeChanceSum()
	{
		var sum = 0;
		for (var i = 0; i < CustomXenotypes.Count; i++)
			sum += CustomXenotypes[i].Chance.RawValue;

		return sum / 1000f;
	}

	public float GetCustomXenotypeChanceSumExcluding(CustomXenotype? xenotype)
	{
		var sum = 0;
		for (var i = 0; i < CustomXenotypes.Count; i++)
		{
			if (CustomXenotypes[i].Xenotype.CustomXenotype != xenotype)
				sum += CustomXenotypes[i].Chance.RawValue;
		}

		return sum / 1000f;
	}

	public void SetChanceForXenotype(ModifiableXenotype xenotype, int rawChanceValue, bool adjustIfNecessary = true)
	{
		rawChanceValue = Mathf.Clamp(rawChanceValue, 0, 1000);

		if (xenotype.Def != XenotypeDefOf.Baseliner)
		{
			AllSavedValues[xenotype.Name].RawValue = rawChanceValue;
			if (adjustIfNecessary)
				EnsureChancesTotal100(xenotype, rawChanceValue);
		}
		else
		{
			SetChancesForBaseliner(rawChanceValue);
		}

		if (xenotype.CustomXenotype is null && Def is { } def && xenotype.Def is { } xenotypeDef)
			SetXenotypeChanceForDef(def, xenotypeDef, rawChanceValue);
	}

	private void SetChancesForBaseliner(int rawChanceValue)
	{
		var activeChancesSum = GetActiveValuesSumExcluding(null);
		var delta = Mathf.Abs(1000 - activeChancesSum);
		if (rawChanceValue - delta == 0 /*>= 0 && rawChanceValue - delta <= Xenotypes.Count - 2*/)
			return;

		if (activeChancesSum <= 0)
		{
			foreach (var activeXenotype in AllActiveXenotypes)
			{
				if (activeXenotype.Value.Xenotype.Def == XenotypeDefOf.Baseliner)
					continue;

				if (activeChancesSum == rawChanceValue || activeChancesSum == AllActiveXenotypes.Count - 1)
					break;

				SetChanceForXenotype(activeXenotype.Value.Xenotype, 1, false);
				activeChancesSum++;
				_counter++;
				ClampRepeat(ref _counter, 0, AllActiveXenotypes.Count - 2);
			}
		}

		var relevantXenotypes = AllActiveXenotypes
			.Where(pair
				=> pair.Value.Xenotype.Def != XenotypeDefOf.Baseliner
				&& pair.Value.Chance.RawValue > 0).ToList();

		if (!relevantXenotypes.Any())
		{
			if (!AllActiveXenotypes.Any())
				throw new Exception("AllActiveXenotypes is somehow empty");
			else
				throw new Exception($"No xenotypes found to adjust chances for. AllXenotypes.Count: {AllActiveXenotypes.Count}");
		}

		SetChancesInLoopUntilFitting(relevantXenotypes, delta, rawChanceValue);
	}

	private void SetChancesInLoopUntilFitting(List<KeyValuePair<string, (ModifiableXenotype Xenotype, Chance Chance)>> relevantXenotypes, int chanceDelta, int targetChance)
	{
		while (targetChance - chanceDelta < 0)
		{
			_counter++;

			ClampRepeat(ref _counter, 0, relevantXenotypes.Count - 1);

			var currentXenotypeChancePair = relevantXenotypes[_counter].Value;

			SetChanceForXenotype(currentXenotypeChancePair.Xenotype, currentXenotypeChancePair.Chance.RawValue + 1, false);
			chanceDelta -= 1;
		}

		while (targetChance - chanceDelta > 0)
		{
			_counter--;

			ClampRepeat(ref _counter, 0, relevantXenotypes.Count - 1);

			var currentXenotypeChancePair = relevantXenotypes[_counter].Value;

			SetChanceForXenotype(currentXenotypeChancePair.Xenotype, currentXenotypeChancePair.Chance.RawValue - 1, false);
			chanceDelta += 1;
		}
	}

	private void EnsureChancesTotal100(ModifiableXenotype xenotype, int rawChanceValue)
	{
		if (xenotype.Def == XenotypeDefOf.Baseliner)
			throw new ArgumentException("Called EnsureChancesTotal100 for Baseliner. SetChanceForBaseliner should be used instead.");

		var sumOfOthers = GetActiveValuesSumExcluding(pair => pair.Key == xenotype.Name);
		if (sumOfOthers > (1000 - rawChanceValue /*+ Xenotypes.Count*/))
		{
			SetChancesInLoopUntilFitting(AllActiveXenotypes
			.Where(pair
				=> pair.Value.Xenotype.Def != XenotypeDefOf.Baseliner && pair.Value.Xenotype != xenotype
				&& pair.Value.Chance.RawValue > 0).ToList(),
				1000 - sumOfOthers, rawChanceValue);
		}
	}

	private int GetActiveValuesSumExcluding(Predicate<KeyValuePair<string, (ModifiableXenotype Xenotype, Chance Chance)>>? predicate)
	{
		var sum = 0;
		foreach (var xenotype in AllActiveXenotypes)
		{
			if (xenotype.Value.Xenotype.Def == XenotypeDefOf.Baseliner
				|| (predicate != null && predicate(xenotype)))
			{
				continue;
			}
			sum += xenotype.Value.Chance.RawValue;
		}

		return sum;
	}

	public (ModifiableXenotype Xenotype, Chance? Chance)? GetOrAddXenotypeAndChance(string defName)
		=> AllActiveXenotypes.TryGetValue(defName, out var xenotype) ? xenotype : TryAddXenotype(defName);

	public (ModifiableXenotype Xenotype, Chance Chance) GetOrAddModifiableXenotype(ModifiableXenotype xenotype)
	{
		if (AllActiveXenotypes.TryGetValue(xenotype.Name, out var xenotypeChance) && xenotypeChance.Xenotype == xenotype)
			return xenotypeChance;

		xenotypeChance.Xenotype = xenotype;

		if (!AllSavedValues.TryGetValue(xenotype.Name, out xenotypeChance.Chance))
			AllSavedValues[xenotype.Name] = xenotypeChance.Chance = new();

		AllActiveXenotypes[xenotype.Name] = xenotypeChance;

		if (xenotypeChance.Xenotype.CustomXenotype != null)
		{
			var customXenotypeIndex = CustomXenotypes.FindIndex(tuple => tuple.Xenotype.Name == xenotype.Name);
			if (customXenotypeIndex >= 0)
				CustomXenotypes[customXenotypeIndex] = xenotypeChance;
			else
				CustomXenotypes.Add(xenotypeChance);

			TryDisableXenotypeDefOfMatchingName(xenotype.Name);
		}

		if (!xenotypeChance.Chance.HasDefaultValue && Def is { } def)
		{
			xenotypeChance.Chance.DefaultValue
				= xenotypeChance.Xenotype.Def is { } xenotypeDef
				? GetXenotypeChance(def, xenotypeDef) ?? 0f
				: xenotypeChance.Xenotype is ModifiableXenotype.Generated generatedXenotype
				? generatedXenotype.GetDefaultChanceIn(def)
				: def is EmptyFactionDef
				? 0.02f
				: 0f;
		}

		return xenotypeChance;
	}

	private void TryDisableXenotypeDefOfMatchingName(string defName)
	{
		var defNameLowered = defName.ToLower();
		for (var i = 0; i < DefDatabase<XenotypeDef>.AllDefsListForReading.Count; i++)
		{
			var def = DefDatabase<XenotypeDef>.AllDefsListForReading[i];

			if (def == XenotypeDefOf.Baseliner)
				continue;

			if (def.defName.ToLower() != defNameLowered)
				continue;

			if (def.defName != defNameLowered)
				Remove(defName);

			if (Def is { } tDef
				&& GetXenotypeSet(tDef)?.xenotypeChances is { } xenotypeChances)
			{
				for (var j = 0; j < xenotypeChances.Count; j++)
				{
					if (xenotypeChances[j].xenotype == def)
					{
						xenotypeChances.Remove(xenotypeChances[j]);
						return;
					}
				}
			}
		}
	}

	private (ModifiableXenotype Xenotype, Chance? Chance)? TryAddXenotype(string defName)
		=> ModifiableXenotypeDatabase.TryGetXenotype(defName) is { } xenotype ? GetOrAddModifiableXenotype(xenotype) : null;

	private string GetNameFromDatabase() => XenotypeChanceDatabase<T>.From(this);

	public void SetAllValues()
	{
		foreach (var value in AllSavedValues)
		{
			if (!value.Value.IsDefaultValue)
				SetChanceForXenotype(value.Key, value.Value.RawValue);
		}
	}

	public void Initialize()
	{
		foreach (var xenotype in ModifiableXenotypeDatabase.AllValues)
			GetOrAddXenotypeAndChance(xenotype.Key);
	}

	public void Reset()
	{
		foreach (var keyValuePair in AllSavedValues)
		{
			if (!keyValuePair.Value.IsDefaultValue)
			{
				if (Def != null && AllActiveXenotypes.ContainsKey(keyValuePair.Key))
					SetChanceForXenotype(keyValuePair.Key, Mathf.RoundToInt(keyValuePair.Value.DefaultValue * 1000f));

				keyValuePair.Value.IsDefaultValue = true;
			}
		}
	}

	public void Remove(string defName)
	{
		AllSavedValues.Remove(defName);
		AllActiveXenotypes.Remove(defName);
		CustomXenotypes.RemoveAll(tuple => tuple.Xenotype.Name == defName);
	}

	public static void SetChanceInXenotypeSet(ref XenotypeSet? set, XenotypeDef xenotypeDef, int chance)
	{
		if (xenotypeDef == XenotypeDefOf.Baseliner)
			return;

		set ??= new();
		var chances = set.xenotypeChances ??= new();

		var chanceIndex = chances.FindIndex(xenoChance => xenoChance.xenotype == xenotypeDef);
		if (chanceIndex < 0)
			chances.Add(new(xenotypeDef, chance / 1000f));
		else
			chances[chanceIndex].chance = chance / 1000f;
	}

	private static void ClampRepeat(ref int value, int min, int max)
	{
		if (value > max)
			value = min;
		else if (value < min)
			value = max;
	}

	private static int _counter;

	public void ExposeData()
	{
		if (Scribe.mode == LoadSaveMode.Saving
			&& !AllSavedValues.Any(pair
				=> !pair.Value.IsDefaultValue))
		{
			return;
		}

		Scribe_Collections.Look(ref _allSavedValues, "XenotypeChances", LookMode.Value, LookMode.Deep);
		_allSavedValues ??= new();
	}

	public class Chance : IExposable
	{
		private const int DEFAULT = -1;

		private int _rawValue = DEFAULT;

		public float DefaultValue { get; set; } = DEFAULT;

		public bool HasDefaultValue => Mathf.Abs(DefaultValue - DEFAULT) > 0.01f;

		public bool IsDefaultValue
		{
			get => _rawValue == DEFAULT;
			set => _rawValue = DEFAULT;
		}

		public int RawValue
		{
			get => IsDefaultValue ? Mathf.RoundToInt(DefaultValue * 1000f)
				: _rawValue;

			set => _rawValue = value;
		}

		public float Value
		{
			get => IsDefaultValue ? DefaultValue
				: _rawValue / 1000f;

			set => _rawValue = Mathf.RoundToInt(value * 1000f);
		}

		public Chance() { }
		public Chance(int rawValue) => RawValue = rawValue;
		public Chance(float value) => Value = value;

		public static explicit operator float(Chance chance) => chance.Value;

		public void ExposeData() => Scribe_Values.Look(ref _rawValue, "chance", DEFAULT);
	}
}