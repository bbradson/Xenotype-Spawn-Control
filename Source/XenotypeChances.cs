// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using System.Globalization;
using System.Linq;

namespace XenotypeSpawnControl;

public class XenotypeChances<T> : IExposable where T : Def
{
	public IEnumerable<XenotypeChance> CustomXenotypeChances => AllLoadedXenotypeChances.Values.Where(xeno => xeno.Xenotype.CustomXenotype != null);

	public string DefName => _defName ??= GetNameFromDatabase();
	private string? _defName;

	public T? Def
		=> _def ??= DefName == Strings.NoFactionKey ? (T)(Def)EmptyFactionDef.Instance
		: DefDatabase<T>.GetNamedSilentFail(DefName);

	private T? _def;
	private XenotypeChancesConfig _currentConfig = new();
	public IEnumerable<KeyValuePair<string, XenotypeChanceConfig>> UnloadedXenotypes => _currentConfig.XenotypeChances.Where(keyValuePair => !AllLoadedXenotypeChances.ContainsKey(keyValuePair.Key));

	private Dictionary<string, XenotypeChance> AllLoadedXenotypeChances { get; } = new();
	public IEnumerable<XenotypeChance> AllAllowedXenotypeChances => AllLoadedXenotypeChances.Values.Where(xenoChance => PassesFilters(xenoChance.Xenotype));

	public bool AllowArchiteXenotypes 
	{ 
		get => _currentConfig.AllowArchite; 
		set
		{
			if (value != AllowArchiteXenotypes)
			{
				_currentConfig.AllowArchite = value;
				if(!AllowArchiteXenotypes)
					DisableDisallowedXenotypes();
			}
		}
	} 

	/// <summary>
	/// indicates whether the chance values taken away from baseliner should be distributed among all valid xenotypes. 
	/// Should be reset when all other xenotypes have been assigned a value.
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

	public float GetCustomXenotypeChanceSum()
	{
		return CustomXenotypeChances.Sum(xenoChance => xenoChance.Value);
	}

	public float GetCustomXenotypeChanceSumExcluding(CustomXenotype? xenotype)
	{
		return CustomXenotypeChances.Where(xenoChance => xenoChance.Xenotype.CustomXenotype != xenotype).Sum(chance => chance.Value);
	}

	public void SetChanceForXenotype(ModifiableXenotype xenotype, int rawChanceValue, bool adjustIfNecessary = true)
	{
		if (!AllAllowedXenotypeChances.Any(xenoChance => xenoChance.Xenotype.Name == xenotype.Name) && rawChanceValue != 0)
			throw new ArgumentException("SetChanceForXenotype was called for a disallowed Xenotype with a chance differing from 0%", nameof(xenotype));

		rawChanceValue = Mathf.Clamp(rawChanceValue, 0, 1000);

		AllLoadedXenotypeChances[xenotype.Name].RawValue = rawChanceValue;
		if (xenotype.Def != XenotypeDefOf.Baseliner)
		{
			if (adjustIfNecessary)
				EnsureChancesTotal100(xenotype, rawChanceValue);
			//baseliner chance is derivative, so we need to update it's value manually
			UpdateBaselinerChanceValue();
		}
		else if (adjustIfNecessary)
		{
			AdjustChancesToFitBaseliner(rawChanceValue);
		}

		if (xenotype.CustomXenotype is null && Def is { } def && xenotype.Def is { } xenotypeDef)
			SetXenotypeChanceForDef(def, xenotypeDef, rawChanceValue);
	}

	private XenotypeChance GetOrAddBaselinerXenotypeChance() => GetOrAddXenotypeAndChance(XenotypeDefOf.Baseliner.defName);

	private void UpdateBaselinerChanceValue() =>
				GetOrAddBaselinerXenotypeChance().RawValue = 1000 - GetAllowedRawValuesSumExceptBaseliner();
	private void AdjustChancesToFitBaseliner(int rawChanceValue)
	{
		//relevant xenotypes are every xenotype except Baseliner with some chance already assigned, or all if no chance is assigned to any xenotype
		var anyXenotypeChanceAssigned = AllAllowedXenotypeChances.Any(xenoChance => xenoChance.Xenotype.Def != XenotypeDefOf.Baseliner && xenoChance.RawValue > 0);
		if (!anyXenotypeChanceAssigned)
		{
			_distributeAmongAllXenotypes = true;
		}
		var relevantXenotypes = AllAllowedXenotypeChances
			.Where(xenoChance
				=> xenoChance.Xenotype.Def != XenotypeDefOf.Baseliner
				&& (_distributeAmongAllXenotypes || xenoChance.RawValue > 0)).ToList();

		if (!relevantXenotypes.Any())
		{
			if (!AllAllowedXenotypeChances.Any())
				throw new Exception("AllActiveXenotypes is somehow empty");
			else
				throw new Exception($"No xenotypes found to adjust chances for. AllXenotypes.Count: {AllAllowedXenotypeChances.Count()}");
		}

		//set baseliner chance by distributing the remaining percentages among the relevant xenotypes
		SetChancesInLoopUntilFitting(relevantXenotypes, 1000 - rawChanceValue);
		//check if every xenotype has a value assigned to reset distrubution flag
		var allXenotypeChanceAssigned = AllAllowedXenotypeChances.All(xenoChance => xenoChance.Xenotype.Def == XenotypeDefOf.Baseliner || xenoChance.RawValue > 0);
		if (allXenotypeChanceAssigned)
		{
			_distributeAmongAllXenotypes = false;
		}
	}

	/// <summary>
	/// set chances of given xenotypes until the sum of all chances matches the given chance
	/// </summary>
	private void SetChancesInLoopUntilFitting(List<XenotypeChance> xenotypes, int targetChance)
	{
		var originalCount = xenotypes.Count;
		//clamp targetchance to allowed values
		targetChance = Mathf.Clamp(targetChance, 0, 1000);
		var chanceDelta = targetChance - xenotypes.Sum(xenoChance => xenoChance.RawValue);
		while (chanceDelta != 0)
		{
			var deltaSign = Math.Sign(chanceDelta);
			_counter += deltaSign;

			ClampRepeat(ref _counter, 0, xenotypes.Count - 1);

			var currentXenotypeChance = xenotypes[_counter];
			//xenotype chance cannot be lowered or raised any further, stop trying to do so
			if ((currentXenotypeChance.RawValue == 0 && deltaSign < 0) || (currentXenotypeChance.RawValue == 1000 && deltaSign > 0))
			{
				xenotypes.RemoveAt(_counter);
				//we might have removed the last xenotype (shouldn't happen => throw exception)
				if (xenotypes.Count == 0)
					throw new ArgumentException("there were not enough xenotypes to ensure target chance of " + targetChance + "Starting xenotype count: " + originalCount + "Delta: " + chanceDelta, nameof(xenotypes));
				else
					continue;
			}

			SetChanceForXenotype(currentXenotypeChance.Xenotype, currentXenotypeChance.RawValue + deltaSign, false);
			chanceDelta -= deltaSign;
		}
	}

	private void EnsureChancesTotal100(ModifiableXenotype xenotype, int rawChanceValue)
	{
		if (xenotype.Def == XenotypeDefOf.Baseliner)
			throw new ArgumentException("Called EnsureChancesTotal100 for Baseliner. SetChanceForBaseliner should be used instead.");

		var sumOfOthers = GetAllowedRawValuesSumExceptBaseliner(xenoChance => xenoChance.Xenotype.Name != xenotype.Name);
		if (sumOfOthers + rawChanceValue > 1000 /*+ Xenotypes.Count*/)
		{
			SetChancesInLoopUntilFitting(AllAllowedXenotypeChances
			.Where(xenoChance
				=> xenoChance.Xenotype.Def != XenotypeDefOf.Baseliner && xenoChance.Xenotype != xenotype
				&& xenoChance.RawValue > 0).ToList(),
				1000 - rawChanceValue);
		}
	}

	private int GetAllowedRawValuesSumExceptBaseliner(Func<XenotypeChance, bool>? predicate = null)
	{
		predicate ??= keyvaluePair => true;
		return AllAllowedXenotypeChances.Where(xenoChance => xenoChance.Xenotype.Def != XenotypeDefOf.Baseliner && predicate(xenoChance)).Sum(xenoChance => xenoChance.RawValue);
	}

	public XenotypeChance? GetOrAddXenotypeAndChance(string defName)
		=> AllLoadedXenotypeChances.TryGetValue(defName, out var xenotype) ? xenotype : TryAddXenotype(defName);

	public XenotypeChance GetOrAddModifiableXenotype(ModifiableXenotype xenotype)
	{
		if (AllLoadedXenotypeChances.TryGetValue(xenotype.Name, out var xenotypeChance) && xenotypeChance.Xenotype == xenotype)
			return xenotypeChance;

		if (xenotypeChance != null)
		{
			//update xenotype
			SwitchXenotypeOfXenotypeChance(xenotypeChance, xenotype);
		}
		else
		{
			//add xenotype to loaded xenotypes
			var hadConfig = _currentConfig.XenotypeChances.TryGetValue(xenotype.Name, out var chanceConfig);
			if (!hadConfig)
			{
				chanceConfig = new();
				_currentConfig.XenotypeChances[xenotype.Name] = chanceConfig;
			}
			xenotypeChance  = new(chanceConfig, xenotype);
			AllLoadedXenotypeChances[xenotype.Name] = xenotypeChance;
			InitializeDefaultValueForXenotypeChance(xenotypeChance);

			//ensure a value gets set if xenotype was added after initialization
			if (_initialized)
				SetInitialValueForXenotypeChance(xenotypeChance);
		}

		return xenotypeChance;
	}

	private void SwitchXenotypeOfXenotypeChance(XenotypeChance xenotypeChance, ModifiableXenotype newXenotype)
	{
		//do not allow switching chance for baseliner
		if(xenotypeChance.Xenotype.Def == XenotypeDefOf.Baseliner)
			return;
		var originalChanceValue = xenotypeChance.RawValue;
		//first set chance of original xenotype to 0
		SetChanceForXenotype(xenotypeChance.Xenotype, 0, false);
		//then switch xenotype
		xenotypeChance.Xenotype = newXenotype;
		//finally set chance for new xenotype to original value
		SetChanceForXenotype(newXenotype, originalChanceValue, false);
	}

	private XenotypeChance? TryAddXenotype(string defName)
		=> ModifiableXenotypeDatabase.TryGetXenotype(defName) is { } xenotype ? GetOrAddModifiableXenotype(xenotype) : null;

	private string GetNameFromDatabase() => XenotypeChanceDatabase<T>.From(this);

	private bool _initialized = false;
	public void Initialize()
	{
		foreach (var xenotype in ModifiableXenotypeDatabase.AllValues)
			GetOrAddXenotypeAndChance(xenotype.Key);
		
		foreach (var xenotype in AllLoadedXenotypeChances.Values)
			SetInitialValueForXenotypeChance(xenotype);
		//this flag may have been falsely set during initialization
		_initialized = false;
	}

	private void SetInitialValueForXenotypeChance(XenotypeChance xenotypeChance)
	{
		SetChanceForXenotype(xenotypeChance.Xenotype, AllAllowedXenotypeChances.Contains(xenotypeChance) ? AllLoadedXenotypeChances[xenotypeChance.Xenotype.Name].RawValue : 0, _initialized);
	}

	private void InitializeDefaultValueForXenotypeChance(XenotypeChance xenotypeChance)
	{
		if (Def is null)
			return;
		
		if (xenotypeChance.Xenotype.Def == XenotypeDefOf.Baseliner)
		{
			//fallback in case we only have the baseliner xenotype (should basically never happen)
			if (AllLoadedXenotypeChances.Count == 1)
				SetDefaultValueForXenotypeChance(xenotypeChance, 1f);
			return;
		}
		// be careful default value may exceed 100% or be lower than 0%, so clamping is necessary
		var xenoDefaultValue
			= Mathf.Clamp(xenotypeChance.Xenotype.Def is { } xenotypeDef
			? GetXenotypeChance(Def, xenotypeDef) ?? 0f
			: xenotypeChance.Xenotype is ModifiableXenotype.Generated generatedXenotype
			? generatedXenotype.GetDefaultChanceIn(Def)
			: Def is EmptyFactionDef
			? 0.02f
			: 0f, 0f, 1f);
				
			SetDefaultValueForXenotypeChance(xenotypeChance, xenoDefaultValue);
			
		//manually calculate default baseliner chance, since GetBaselinerChance can give false results
		var baselinerDefaultChance = 1f - AllAllowedXenotypeChances.Where(xenoChanceKeyValuePair => xenoChanceKeyValuePair.Xenotype.Def != XenotypeDefOf.Baseliner).Sum(xenoChance => xenoChance.DefaultValue);
		SetDefaultValueForXenotypeChance(GetOrAddBaselinerXenotypeChance(), baselinerDefaultChance);
	}

	private void SetDefaultValueForXenotypeChance(XenotypeChance xenotypeChance, float normalizedValue)
	{
		xenotypeChance.DefaultValue = normalizedValue;
	}

	public void Reset()
	{
		//first reset filters to default
		AllowArchiteXenotypes = true;

		//then clear all the unloaded chances
		foreach (var chance in UnloadedXenotypes.ToList())
		{
			_currentConfig.XenotypeChances.Remove(chance.Key);
		}

		//finally reset every loaded value to its default
		foreach (var xenotypeChance in AllLoadedXenotypeChances.Values)
		{
			if (!xenotypeChance.IsDefaultValue && Def != null)
			{
				ResetToDefaultChance(xenotypeChance.Xenotype);
			}
		}
	}

	private void ResetToDefaultChance(ModifiableXenotype xenotype)
	{
		SetChanceForXenotype(xenotype, Mathf.RoundToInt(AllLoadedXenotypeChances[xenotype.Name].DefaultValue * 1000), false);
	}

	public void Remove(string defName)
	{
		//if a premade xenotype was overridden switch back to it, else remove it entirely
		if (ModifiableXenotypeDatabase.AllValues.TryGetValue(defName, out var premadeXenotype))
		{
			SwitchXenotypeOfXenotypeChance(AllLoadedXenotypeChances[defName], premadeXenotype);
		}
		else
		{
			//remove xenotype from the config as well
			_currentConfig.XenotypeChances.Remove(defName);

			if (AllLoadedXenotypeChances.Remove(defName))
			{
				UpdateBaselinerChanceValue();
			}
		}
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

	//TODO: allow more filter modifications (e.g. filter for necessary/forbidden genes)=> disable chance modification, if only baseliner available
	private bool PassesFilters(ModifiableXenotype xenotype)
	{
		//always allow baseliners
		var isBaseliner = xenotype.Def == XenotypeDefOf.Baseliner;
		var passesArchiteFilter = AllowArchiteXenotypes || !xenotype.IsArchite;
		return isBaseliner || passesArchiteFilter;
	}

	private void DisableDisallowedXenotypes()
	{
		foreach (var xenotypeChance in AllLoadedXenotypeChances.Values.Except(AllAllowedXenotypeChances))
		{
			SetChanceForXenotype(xenotypeChance.Xenotype, 0, true);
		}
	}

	private static int _counter;

	public bool RequiresSaving()
	{
		var chancesNotDefault = AllLoadedXenotypeChances.Any(pair => !pair.Value.IsDefaultValue);
		var allowArchiteNotDefault = !AllowArchiteXenotypes;
		var anyUnloadedXenotypes = UnloadedXenotypes.Any();
		return chancesNotDefault || allowArchiteNotDefault || anyUnloadedXenotypes;
	}

	public void ExposeData()
	{
		if (Scribe.mode == LoadSaveMode.Saving && !RequiresSaving())
		{
			return;
		}
		//we can't use scribe in order to keep backwards compatibility
		//actual loading happens in Initialize function, so just setting the config is enough
		_currentConfig.ExposeData();
	}

	public class XenotypeChance
	{
		private const int DEFAULT = -1;

		private XenotypeChanceConfig _currentConfig;

		public ModifiableXenotype Xenotype { get; set; }

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
			get => _currentConfig.RawChanceValue;

			//ensure value is in allowed percentage range
			set 
			{
				Mathf.Clamp(_currentConfig.RawChanceValue = value, 0, 1000);
				ResetChanceString();
			}
		}

		public float Value
		{
			get => _currentConfig.RawChanceValue / 1000f;

			//ensure value is in allowed percentage range
			set
			{
				_currentConfig.RawChanceValue = Mathf.Clamp(Mathf.RoundToInt(value * 1000f), 0, 1000);
				ResetChanceString();
			}
		}

		public string ChanceString { get; set; }

		public XenotypeChance(XenotypeChanceConfig chanceConfig, ModifiableXenotype xenotype) 
		{
			_currentConfig = chanceConfig;
			Xenotype = xenotype;
			ResetChanceString();
		}

		public void ResetChanceString()
		{
			 ChanceString = (Value * 100f).ToString("##0.#", CultureInfo.InvariantCulture);
		}
	}
}