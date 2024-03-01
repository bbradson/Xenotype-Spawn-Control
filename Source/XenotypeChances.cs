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

	public bool OnlyAbsoluteChancesAllowed => AllAllowedXenotypeChances.All(xenoChance => xenoChance.IsAbsolute);

	public bool OnlyWeightedChancesAllowed => AllAllowedXenotypeChances.All(xenoChance => !xenoChance.IsAbsolute);

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

	public float GetCustomXenotypeChanceSum() => CustomXenotypeChances.Sum(xenoChance => xenoChance.Value);

	public float GetCustomXenotypeChanceSumExcluding(CustomXenotype? xenotype) => CustomXenotypeChances.Where(xenoChance => xenoChance.Xenotype.CustomXenotype != xenotype).Sum(chance => chance.Value);

	public void SetXenotypeChance(XenotypeChance xenotypeChance, bool adjustIfNecessary = true)
	{
		if (!AllLoadedXenotypeChances.ContainsKey(xenotypeChance.Xenotype.Name))
			throw new ArgumentException("SetChanceForXenotype was called for an unloaded Xenotype", nameof(xenotypeChance));
		if (!AllAllowedXenotypeChances.Contains(xenotypeChance) && xenotypeChance.RawValue != 0)
			throw new ArgumentException("SetChanceForXenotype was called for a disallowed Xenotype with a chance differing from 0%", nameof(xenotypeChance));

		if (xenotypeChance.IsAbsolute)
		{
			if (xenotypeChance.Xenotype.Def != XenotypeDefOf.Baseliner)
			{
				if (adjustIfNecessary)
					EnsureAbsoluteChancesNotExceed100(xenotypeChance);
				//baseliner chance is derivative, so we need to update it's value manually
				UpdateBaselinerChanceValue();
			}
			else if (adjustIfNecessary)
			{
				AdjustAbsoluteChancesToFitBaseliner();
			}
		}

		// weighted xenotypes always need to be rebalanced
		CalculateWeightedXenotypeChances();

		if (xenotypeChance.Xenotype.CustomXenotype is null && Def is { } def && xenotypeChance.Xenotype.Def is { } xenotypeDef)
			SetXenotypeChanceForDef(def, xenotypeDef, xenotypeChance.RawValue);
	}

	private bool _balancingWeightedChances;
	private void CalculateWeightedXenotypeChances()
	{
		if (_balancingWeightedChances || OnlyAbsoluteChancesAllowed)
			return;
		_balancingWeightedChances = true;
		var weightedXenotypeChances = GetAllowedWeightedXenotypeChances().ToList();
		var weightSum = weightedXenotypeChances.Sum(xenoChance => xenoChance.Weight);
		//weighted xenotypes get distrubuted among the remaining chance of all absolute xenotypes
		var rawDistrubutionChance = GetRawWeightedDistrubutionChance();

		var weightedXenotypesForAdjustment = new List<XenotypeChance>();
		foreach(var weightedXenotypeChance in weightedXenotypeChances)
		{
			//if all weights are set to 0, average every xenotype instead
			var relativeWeight = weightSum > 0 ? weightedXenotypeChance.Weight / weightSum : 1f / weightedXenotypeChances.Count;
			weightedXenotypeChance.RawValue = Mathf.RoundToInt(rawDistrubutionChance * relativeWeight);
			SetXenotypeChance(weightedXenotypeChance, false);
			if (relativeWeight > 0)
				weightedXenotypesForAdjustment.Add(weightedXenotypeChance);
		}

		//thanks to rounding errors we need to balance the difference out manually
		SetChancesInLoopUntilFitting(weightedXenotypesForAdjustment, rawDistrubutionChance);

		_balancingWeightedChances = false;
	}

	public void SetWeightForAllowedWeightedXenotypes(float weight)
	{
		foreach(var xenotypeChance in GetAllowedWeightedXenotypeChances())
		{
			xenotypeChance.Weight = weight;
		}
		CalculateWeightedXenotypeChances();
	}

	private IEnumerable<XenotypeChance> GetAllowedWeightedXenotypeChances() => AllAllowedXenotypeChances.Where(xenoChance => !xenoChance.IsAbsolute);

	private IEnumerable<XenotypeChance> GetAllowedAbsoluteXenotypeChances() => AllAllowedXenotypeChances.Where(xenoChance => xenoChance.IsAbsolute);

	public int GetRawWeightedDistrubutionChance() => Math.Max(1000 - GetAllowedAbsoluteXenotypeChances().Sum(xenoChance => xenoChance.RawValue), 0);

	public void SetIsAbsoluteForAllowed(bool value)
	{
		//xenotypes need to be ordered, so the lowest chance gets assigned the weight 1
		foreach (var xenotypeChance in AllAllowedXenotypeChances.OrderBy(xenoChance => xenoChance.RawValue).ToList())
		{
			SetIsAbsoluteForXenotypeChance(xenotypeChance, value);
		}
	}

	public void SetIsAbsoluteForXenotypeChance(XenotypeChance xenotypeChance, bool value)
	{
		if (xenotypeChance.IsAbsolute != value)
		{
			xenotypeChance.IsAbsolute = value;
			if (!xenotypeChance.IsAbsolute)
				SetWeightFromAbsoluteValue(xenotypeChance);
		}
	}

	private void SetWeightFromAbsoluteValue(XenotypeChance xenotypeChance)
	{
		//rawValue = distrubutionPercentage * (weight / weightSum)
		//weight = rawValue * weightSum / distrubutionPercentage
		//	weightSum = weight + weightOthers		distrubutionPercentage >= rawValue
		//weight = rawValue * weight / distrubutionPercentage + rawValue * weightOthers / distrubutionPercentage
		//weight - rawValue * weight / distrubutionPercentage = rawValue * weightOthers / distrubutionPercentage
		//weight * distrubutionPercentage - rawValue * weight = rawValue * weightOthers
		//weight * (distrubutionPercentage - rawValue) = rawValue * weightOthers
		//weight = rawValue * weightOthers / (distrubutionPercentage - rawValue)		rawValue != distrubutionPercentage 	weightOthers != 0	distrubutionPercent != 0
		if(xenotypeChance.IsAbsolute)
			throw new ArgumentException(nameof(SetWeightFromAbsoluteValue) + " was called for a non weighted xenotype", nameof(xenotypeChance));
		var rawDistrubutionChance = GetRawWeightedDistrubutionChance();
		var otherWeightedXenotypes = GetAllowedWeightedXenotypeChances().Where(xenoChance => xenoChance != xenotypeChance).ToList();
		var weightOthers = otherWeightedXenotypes.Sum(xenoChance => xenoChance.Weight);
		if (rawDistrubutionChance == 0)
		{
			//the xenotype wasn't worth anything before, so it isn't worth anything now
			xenotypeChance.Weight = 0;
			return;
		}
		else if (xenotypeChance.RawValue == rawDistrubutionChance)
		{
			//the xenotype makes up the entire weighted chances, any weight would have the same effect, settle for 1
			xenotypeChance.Weight = 1;
			return;
		}
		else if (weightOthers == 0)
		{
			//when calculating weighted xenotype chances we average all weighted xenotypes if all weights are 0. To keep that behavior and their chances we need to set every weight to a non zero value
			weightOthers = 0;
			foreach(var otherWeightedXenotype in otherWeightedXenotypes)
			{
				otherWeightedXenotype.Weight = 1;
				++weightOthers;
			}
		}
		xenotypeChance.Weight = xenotypeChance.RawValue * weightOthers / (rawDistrubutionChance - xenotypeChance.RawValue);
	}

	private XenotypeChance GetOrAddBaselinerXenotypeChance() => GetOrAddXenotypeAndChance(XenotypeDefOf.Baseliner.defName);

	private void UpdateBaselinerChanceValue()
	{
		var othersChance = GetAllowedAbsoluteRawValuesSumExceptBaseliner();
		var baselinerXenotypeChance = GetOrAddBaselinerXenotypeChance();
		if (baselinerXenotypeChance.RawValue + othersChance > 1000 || OnlyAbsoluteChancesAllowed)
		{
			baselinerXenotypeChance.RawValue = 1000 - othersChance;
			SetXenotypeChance(baselinerXenotypeChance, false);
		}
	}
	private void AdjustAbsoluteChancesToFitBaseliner()
	{
		var allowedAbsoluteChances = GetAllowedAbsoluteXenotypeChances().ToList();
		//if any xenotype chance is weighted we only want to adjust if the sum of all chances exceeds 100%
		if(!OnlyAbsoluteChancesAllowed && allowedAbsoluteChances.Sum(xenoChance => xenoChance.RawValue) <= 1000)
			return;

		//relevant xenotypes are every xenotype except Baseliner with some chance already assigned, or all if no chance is assigned to any xenotype
		var anyXenotypeChanceAssigned = allowedAbsoluteChances.Any(xenoChance => xenoChance.Xenotype.Def != XenotypeDefOf.Baseliner && xenoChance.RawValue > 0);
		if (!anyXenotypeChanceAssigned)
		{
			_distributeAmongAllXenotypes = true;
		}
		var relevantXenotypes = allowedAbsoluteChances
			.Where(xenoChance
				=> xenoChance.Xenotype.Def != XenotypeDefOf.Baseliner
				&& (_distributeAmongAllXenotypes || xenoChance.RawValue > 0)).ToList();

		if (!relevantXenotypes.Any())
		{
			if (!allowedAbsoluteChances.Any())
				throw new Exception("allowedAbsoluteChances is somehow empty");
			else
				throw new Exception($"relevantXenotypes is somehow empty");
		}

		//set baseliner chance by distributing the remaining percentages among the relevant xenotypes
		SetChancesInLoopUntilFitting(relevantXenotypes, 1000 - GetOrAddBaselinerXenotypeChance().RawValue);
		//check if every xenotype has a value assigned to reset distrubution flag
		var allXenotypeChanceAssigned = allowedAbsoluteChances.All(xenoChance => xenoChance.Xenotype.Def == XenotypeDefOf.Baseliner || xenoChance.RawValue > 0);
		if (allXenotypeChanceAssigned)
		{
			_distributeAmongAllXenotypes = false;
		}
	}

	/// <summary>
	/// set chances of given xenotypes until the sum of all chances matches the given chance
	/// </summary>
	private void SetChancesInLoopUntilFitting(List<XenotypeChance> xenotypes, int rawTargetChance)
	{
		var originalCount = xenotypes.Count;
		//clamp targetchance to allowed values
		rawTargetChance = Mathf.Clamp(rawTargetChance, 0, 1000);
		var chanceDelta = rawTargetChance - xenotypes.Sum(xenoChance => xenoChance.RawValue);
		//prevent recalcululating the weighted chances on every tiny change and just do it once at the end
		var oldBalancingWeightedChances = _balancingWeightedChances;
		_balancingWeightedChances = true;
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
					throw new ArgumentException("there were not enough xenotypes to ensure target chance of " + rawTargetChance + "Starting xenotype count: " + originalCount + "Delta: " + chanceDelta, nameof(xenotypes));
				else
					continue;
			}

			currentXenotypeChance.RawValue += deltaSign;
			SetXenotypeChance(currentXenotypeChance, false);
			chanceDelta -= deltaSign;
		}

		_balancingWeightedChances = oldBalancingWeightedChances;
		CalculateWeightedXenotypeChances();
	}

	private void EnsureAbsoluteChancesNotExceed100(XenotypeChance xenotypeChance)
	{
		if (xenotypeChance.Xenotype.Def == XenotypeDefOf.Baseliner)
			throw new ArgumentException("Called EnsureAbsoluteChancesNotExceed100 for Baseliner. AdjustAbsoluteChancesToFitBaseliner should be used instead.");

		if (GetAllowedAbsoluteRawValuesSumExceptBaseliner() > 1000)
		{
			SetChancesInLoopUntilFitting(GetAllowedAbsoluteXenotypeChances()
			.Where(xenoChance
				=> xenoChance.Xenotype.Def != XenotypeDefOf.Baseliner && xenoChance.Xenotype != xenotypeChance.Xenotype
				&& xenoChance.RawValue > 0).ToList(),
				1000 - xenotypeChance.RawValue);
		}
	}

	private int GetAllowedAbsoluteRawValuesSumExceptBaseliner() => GetAllowedAbsoluteXenotypeChances().Where(xenoChance => xenoChance.Xenotype.Def != XenotypeDefOf.Baseliner).Sum(xenoChance => xenoChance.RawValue);

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
		//first set chance of original xenotype to 0
		var originalIsAbsolute = xenotypeChance.IsAbsolute;
		var originalChanceValue = originalIsAbsolute ? xenotypeChance.Value : xenotypeChance.Weight;
		DisableXenotypeChance(xenotypeChance, false);
		//then switch xenotype
		xenotypeChance.Xenotype = newXenotype;
		
		//finally set chance for new xenotype to original value
		xenotypeChance.IsAbsolute = originalIsAbsolute;
		if (originalIsAbsolute)
			xenotypeChance.Value = originalChanceValue;
		else
			xenotypeChance.Weight = originalChanceValue;
		SetXenotypeChance(xenotypeChance, false);
	}

	private XenotypeChance? TryAddXenotype(string defName)
		=> ModifiableXenotypeDatabase.TryGetXenotype(defName) is { } xenotype ? GetOrAddModifiableXenotype(xenotype) : null;

	private string GetNameFromDatabase() => XenotypeChanceDatabase<T>.From(this);

	private bool _initialized = false;
	public void Initialize()
	{
		_initialized = false;
		foreach (var xenotype in ModifiableXenotypeDatabase.AllValues)
			GetOrAddXenotypeAndChance(xenotype.Key);
		
		foreach (var xenotype in AllLoadedXenotypeChances.Values)
			SetInitialValueForXenotypeChance(xenotype);
		
		//make sure all chances total up to 100%
		var allowedAbsolute = GetAllowedAbsoluteXenotypeChances().ToList();
		if (allowedAbsolute.Sum(xenoChance => xenoChance.RawValue) > 1000)
			SetChancesInLoopUntilFitting(allowedAbsolute, 1000);
		_initialized = true;
	}

	private void SetInitialValueForXenotypeChance(XenotypeChance xenotypeChance)
	{
		if (AllAllowedXenotypeChances.Contains(xenotypeChance))
			SetXenotypeChance(xenotypeChance, _initialized);
		else
			DisableXenotypeChance(xenotypeChance, _initialized);
	}

	private void InitializeDefaultValueForXenotypeChance(XenotypeChance xenotypeChance)
	{
		if (Def is null)
			return;
		
		if (xenotypeChance.Xenotype.Def == XenotypeDefOf.Baseliner)
		{
			//fallback in case we only have the baseliner xenotype (should basically never happen)
			if (AllLoadedXenotypeChances.Count == 1)
				xenotypeChance.DefaultValue = 1f;
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
				
			xenotypeChance.DefaultValue = xenoDefaultValue;
			
		//manually calculate default baseliner chance, since GetBaselinerChance can give false results
		var baselinerDefaultChance = 1f - AllLoadedXenotypeChances.Where(xenoChanceKeyValuePair => xenoChanceKeyValuePair.Value.Xenotype.Def != XenotypeDefOf.Baseliner).Sum(xenoChance => xenoChance.Value.DefaultValue);
		GetOrAddBaselinerXenotypeChance().DefaultValue = baselinerDefaultChance;
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
			ResetToDefaultChance(xenotypeChance);
		}
	}

	private void ResetToDefaultChance(XenotypeChance xenotypeChance)
	{
		xenotypeChance.IsAbsolute = true;
		if(!xenotypeChance.IsDefaultValue)
		{
			xenotypeChance.Value = xenotypeChance.DefaultValue;
			SetXenotypeChance(xenotypeChance, false);
		}
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
			DisableXenotypeChance(xenotypeChance, true);
		}
	}

	private void DisableXenotypeChance(XenotypeChance xenotypeChance, bool adjustChances)
	{
		xenotypeChance.IsAbsolute = true;
		xenotypeChance.RawValue = 0;
		SetXenotypeChance(xenotypeChance, adjustChances);
	}

	private static int _counter;

	public bool RequiresSaving()
	{
		var chancesNotDefault = AllLoadedXenotypeChances.Any(pair => !pair.Value.IsDefaultValue);
		var allowArchiteNotDefault = !AllowArchiteXenotypes;
		var anyUnloadedXenotypes = UnloadedXenotypes.Any();
		var anyWeightedXenotypeChances = !OnlyAbsoluteChancesAllowed;
		return chancesNotDefault || allowArchiteNotDefault || anyUnloadedXenotypes || anyWeightedXenotypeChances;
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
}

public class XenotypeChance
{
	private XenotypeChanceConfig _currentConfig;

	public ModifiableXenotype Xenotype { get; set; }

	private float _defaultValue = -1;
	public float DefaultValue 
	{ 
		get => _defaultValue; 
		//ensure value is in alllowed percentage range
		set 
		{
			if (DefaultValue != value)
			{
				//remember to change current value as well, if it was default
				var wasDefault = IsDefaultValue;
				_defaultValue = Mathf.Clamp(value, 0f, 1f);
				if (wasDefault)
					Value = DefaultValue;
			}
		}
	}

	public bool IsDefaultValue => Mathf.Abs(Value - DefaultValue) < 0.0005;

	//TODO: refactor XenotypeChances so we can call SetXenotypeChance on setting the relevant properties
	//TODO: remove RawValue
	public int RawValue
	{
		get => _currentConfig.RawChanceValue;

		//ensure value is in allowed percentage range
		set 
		{
			if (RawValue != value)
			{
				_currentConfig.RawChanceValue = Mathf.Clamp(value, 0, 1000);
				ResetChanceString();
			}
		}
	}

	public float Value
	{
		get => _currentConfig.RawChanceValue / 1000f;
		//ensure value is in allowed percentage range
		set
		{
			if (Value != value)
			{
				_currentConfig.RawChanceValue = Mathf.Clamp(Mathf.RoundToInt(value * 1000f), 0, 1000);
				ResetChanceString();
			}
		}
	}

	public bool IsAbsolute
	{
		get => _currentConfig.IsAbsolute;
		set => _currentConfig.IsAbsolute = value;
	}

	public float Weight
	{
		get => _currentConfig.Weight;
		set
		{
			if (Weight != value)
			{
				_currentConfig.Weight = value >= 0 ? value : 0;
				ResetWeightString();
			}
		}
	}

	public string ChanceString { get; set; }
	public string WeightString { get; set; }


	public XenotypeChance(XenotypeChanceConfig chanceConfig, ModifiableXenotype xenotype) 
	{
		_currentConfig = chanceConfig;
		Xenotype = xenotype;
		ResetChanceString();
		ResetWeightString();
	}

	private void ResetChanceString()
	{
		 ChanceString = (Value * 100f).ToString("##0.#", CultureInfo.InvariantCulture);
	}

	private void ResetWeightString()
	{
		WeightString = Weight.ToString(CultureInfo.InvariantCulture);
	}
}