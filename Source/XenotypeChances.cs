// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using System.Globalization;
using System.Linq;
using System.Runtime.Remoting.Messaging;

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

	/// <summary>
	/// indicates whether the chance values taken away from baseliner should be distributed among all valid xenotypes. 
	/// Should be reset when all otherc xenotypes have been assigned a value.
	/// </summary>
	private bool _distributeAmongAllXenotypes = false;

	public void SetXenotypeChanceForDef(T def, XenotypeDef xenotypeDef, int rawChanceValue)
	{
		if (def is EmptyFactionDef)
			SetXenotypeChanceForEmptyFaction(xenotypeDef, rawChanceValue);
		else
			SetChanceInXenotypeSet(ref _xenotypeSetRef(def), xenotypeDef, rawChanceValue);
	}

	public float? GetXenotypeChance(T def, XenotypeDef xenotypeDef)
		=> xenotypeDef == XenotypeDefOf.Baseliner
		? GetBaselinerChance()
		: def is EmptyFactionDef
		? GetXenotypeChanceForEmptyFaction(xenotypeDef) 
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

		AllSavedValues[xenotype.Name].RawValue = rawChanceValue;
		if (xenotype.Def != XenotypeDefOf.Baseliner)
		{
			if (adjustIfNecessary)
				EnsureChancesTotal100(xenotype, rawChanceValue);
			//baseliner chance is derivative, so we need to update it's value manually
			AllSavedValues[XenotypeDefOf.Baseliner.defName].RawValue = 1000 - AllActiveXenotypes.Values.Where(xeno => xeno.Xenotype.Def != XenotypeDefOf.Baseliner).Sum(xeno => xeno.Chance.RawValue);
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
		//relevant xenotypes are every xenotype except Baseliner with some chance already assigned, or all if no chance is assigned to any xenotype
		var anyXenotypeChanceAssigned = AllActiveXenotypes.Values.Any(xeno => xeno.Xenotype.Def != XenotypeDefOf.Baseliner && xeno.Chance.RawValue > 0);
		if (!anyXenotypeChanceAssigned)
		{
			_distributeAmongAllXenotypes = true;
		}
		var relevantXenotypes = AllActiveXenotypes.Values
			.Where(pair
				=> pair.Xenotype.Def != XenotypeDefOf.Baseliner
				&& (_distributeAmongAllXenotypes || pair.Chance.RawValue > 0)).ToList();

		if (!relevantXenotypes.Any())
		{
			if (!AllActiveXenotypes.Any())
				throw new Exception("AllActiveXenotypes is somehow empty");
			else
				throw new Exception($"No xenotypes found to adjust chances for. AllXenotypes.Count: {AllActiveXenotypes.Count}");
		}

		//set baseliner chance by distributing the remaining percentages among the relevant xenotypes
		SetChancesInLoopUntilFitting(relevantXenotypes, 1000 - rawChanceValue);
		//check if every xenotype has a value assigned to reset distrubution flag
		var allXenotypeChanceAssigned = AllActiveXenotypes.Values.All(xeno => xeno.Xenotype.Def == XenotypeDefOf.Baseliner || xeno.Chance.RawValue > 0);
		if (allXenotypeChanceAssigned)
		{
			_distributeAmongAllXenotypes = false;
		}
	}

	/// <summary>
	/// set chances of given xenotypes until the sum of all chances matches the given chance
	/// </summary>
	private void SetChancesInLoopUntilFitting(List<(ModifiableXenotype Xenotype, Chance Chance)> xenotypes, int targetChance)
	{
		var originalCount = xenotypes.Count;
		//clamp targetchance to allowed values
		targetChance = Mathf.Clamp(targetChance, 0, 1000);
		var chanceDelta = targetChance - xenotypes.Sum(xeno => xeno.Chance.RawValue);
		while (chanceDelta != 0)
		{
			var deltaSign = Math.Sign(chanceDelta);
			_counter += deltaSign;

			ClampRepeat(ref _counter, 0, xenotypes.Count - 1);

			var currentXenotypeChancePair = xenotypes[_counter];
			//xenotype chance cannot be lowered or raised any further, stop trying to do so
			if ((currentXenotypeChancePair.Chance.RawValue == 0 && deltaSign < 0) || (currentXenotypeChancePair.Chance.RawValue == 1000 && deltaSign > 0))
			{
				xenotypes.RemoveAt(_counter);
				//we might have removed the last xenotype (shouldn't happen => throw exception)
				if (xenotypes.Count == 0)
					throw new ArgumentException("there were not enough xenotypes to ensure target chance of " + targetChance + "Starting xenotype count: " + originalCount + "Delta: " + chanceDelta, nameof(xenotypes));
				else
					continue;
			}

			SetChanceForXenotype(currentXenotypeChancePair.Xenotype, currentXenotypeChancePair.Chance.RawValue + deltaSign, false);
			chanceDelta -= deltaSign;
		}
	}

	private void EnsureChancesTotal100(ModifiableXenotype xenotype, int rawChanceValue)
	{
		if (xenotype.Def == XenotypeDefOf.Baseliner)
			throw new ArgumentException("Called EnsureChancesTotal100 for Baseliner. SetChanceForBaseliner should be used instead.");

		var sumOfOthers = GetActiveValuesSumExcluding(pair => pair.Key == xenotype.Name);
		if (sumOfOthers + rawChanceValue > 1000 /*+ Xenotypes.Count*/)
		{
			SetChancesInLoopUntilFitting(AllActiveXenotypes.Values
			.Where(pair
				=> pair.Xenotype.Def != XenotypeDefOf.Baseliner && pair.Xenotype != xenotype
				&& pair.Chance.RawValue > 0).ToList(),
				1000 - rawChanceValue);
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
			SetChanceForXenotype(value.Key, value.Value.RawValue);
		}
	}

	public void Initialize()
	{
		foreach (var xenotype in ModifiableXenotypeDatabase.AllValues)
			GetOrAddXenotypeAndChance(xenotype.Key);
		InitializeDefaultValues();
	}

	private void InitializeDefaultValues()
	{
		if (Def is null)
			return;
		//manually calculate default baseliner chance, since GetBaselinerChance can give false results
		var defaultBaselinerChance = 1f;
		foreach (var xenotypeChance in AllActiveXenotypes.Values.Where(xenoChance => xenoChance.Xenotype.Def != XenotypeDefOf.Baseliner))
		{
			// be careful default value may exceed 100% or be lower than 0%, so clamping is necessary
			var xenoDefaultValue
				= Mathf.Clamp(xenotypeChance.Xenotype.Def is { } xenotypeDef
				? GetXenotypeChance(Def, xenotypeDef) ?? 0f
				: xenotypeChance.Xenotype is ModifiableXenotype.Generated generatedXenotype
				? generatedXenotype.GetDefaultChanceIn(Def)
				: Def is EmptyFactionDef
				? 0.02f
				: 0f, 0f, 1f);
				
			xenotypeChance.Chance.DefaultValue = xenoDefaultValue;
			defaultBaselinerChance -= xenoDefaultValue;
		}
		AllActiveXenotypes[XenotypeDefOf.Baseliner.defName].Chance.DefaultValue = defaultBaselinerChance;
	}

	public void Reset()
	{
		foreach (var keyValuePair in AllSavedValues)
		{
			if (!keyValuePair.Value.IsDefaultValue)
			{
				if (Def != null && AllActiveXenotypes.TryGetValue(keyValuePair.Key, out var xenotypeChance))
				{
					ResetToDefaultChance(xenotypeChance.Xenotype);
				}
			}
		}
	}

	private void ResetToDefaultChance(ModifiableXenotype xenotype)
	{
		SetChanceForXenotype(xenotype, Mathf.RoundToInt(AllSavedValues[xenotype.Name].DefaultValue * 1000), false);
	}

	public void Remove(string defName)
	{
		AllSavedValues.Remove(defName);
		AllActiveXenotypes.Remove(defName);
		CustomXenotypes.RemoveAll(tuple => tuple.Xenotype.Name == defName);
	}

	public void SetChanceInXenotypeSet(ref XenotypeSet? set, XenotypeDef xenotypeDef, int chance)
	{
		if (xenotypeDef == XenotypeDefOf.Baseliner)
			return;

		set ??= new();
		var chances = set.xenotypeChances ??= new();

		var chanceIndex = chances.FindIndex(xenoChance => xenoChance.xenotype == xenotypeDef);
		//remove xenotypes from chances if chance is 0 to avoid cluttering the tooltip of the def
		var floatChance = chance / 1000f;
		var chanceZero = chance == 0;
		if (chanceIndex < 0 && !chanceZero)
			chances.Add(new(xenotypeDef, floatChance));
		else if (chanceIndex >= 0 && !chanceZero)
			chances[chanceIndex].chance = floatChance;
		else if (chanceIndex >= 0 && chanceZero)
			chances.RemoveAt(chanceIndex);

		//regenerate faction xenotype percentage in description
		if (Def is FactionDef factionDef)
			factionDef.cachedDescription = null;
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

		private int _rawValue = DEFAULT * 1000;

		private float _defaultValue = DEFAULT;
		public float DefaultValue 
		{ 
			get => _defaultValue; 
			//ensure value is in alllowed percentage range
			set 
			{
				//remember to change current value as well, if it was default
				var wasDefault = IsDefaultValue;
				_defaultValue = Mathf.Clamp(value, 0f, 1f);
				if (wasDefault)
					Value = DefaultValue;
			}
		}

		public bool IsDefaultValue => Mathf.Abs(Value - DefaultValue) < 0.005;

		public int RawValue
		{
			get => _rawValue;

			//ensure value is in allowed percentage range
			set 
			{
				Mathf.Clamp(_rawValue = value, 0, 1000);
				ResetChanceString();
			}
		}

		public float Value
		{
			get => _rawValue / 1000f;

			//ensure value is in allowed percentage range
			set
			{
				_rawValue = Mathf.Clamp(Mathf.RoundToInt(value * 1000f), 0, 1000);
				ResetChanceString();
			}
		}

		public string ChanceString { get; set; }

		public Chance() { }
		public Chance(int rawValue) 
		{
			 RawValue = rawValue;
		}
		public Chance(float value) 
		{
			Value = value;
		}

		public static explicit operator float(Chance chance) => chance.Value;

		public void ResetChanceString()
		{
			 ChanceString = (Value * 100f).ToString("##0.#", CultureInfo.InvariantCulture);
		}

		public void ExposeData() => Scribe_Values.Look(ref _rawValue, "chance", DEFAULT);
	}
}