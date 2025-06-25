// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using System.Linq;

namespace XenotypeSpawnControl;

public class XenotypeChances<T> : IExposable where T : Def
{
	public IEnumerable<XenotypeChance> CustomXenotypeChances
		=> AllLoadedXenotypeChances.Values.Where(static xeno => xeno.Xenotype.CustomXenotype != null);

	[field: MaybeNull]
	public string DefName => field ??= GetNameFromDatabase();

	public T? Def
		=> field ??= DefName == Strings.NoFactionKey ? (T)(Def)EmptyFactionDef.Instance
		: DefDatabase<T>.GetNamedSilentFail(DefName);

	private readonly XenotypeChancesConfig _currentConfig = new();

	public IEnumerable<KeyValuePair<string, XenotypeChanceConfig>> UnloadedXenotypes
		=> _currentConfig.XenotypeChances.Where(keyValuePair
			=> !AllLoadedXenotypeChances.ContainsKey(keyValuePair.Key));

	private Dictionary<string, XenotypeChance> AllLoadedXenotypeChances { get; } = new();

	public IEnumerable<XenotypeChance> AllAllowedXenotypeChances
		=> AllLoadedXenotypeChances.Values.Where(xenoChance => PassesFilters(xenoChance.Xenotype));

	public bool AllowArchiteXenotypes
	{ 
		get => _currentConfig.AllowArchite; 
		set
		{
			if (value == AllowArchiteXenotypes)
				return;

			_currentConfig.AllowArchite = value;
			if (!AllowArchiteXenotypes)
				DisableDisallowedXenotypes();
		}
	}

	public bool OnlyAbsoluteChancesAllowed => AllAllowedXenotypeChances.All(static xenoChance => xenoChance.IsAbsolute);

	public bool OnlyWeightedChancesAllowed
		=> AllAllowedXenotypeChances.All(static xenoChance => !xenoChance.IsAbsolute);

	/// <summary>
	/// indicates whether the chance values taken away from baseliner should be distributed among all valid xenotypes. 
	/// Should be reset when all other xenotypes have been assigned a value.
	/// </summary>
	private bool _distributeAmongAllXenotypes;

	private void SetXenotypeChanceForDef(T def, XenotypeDef xenotypeDef, int rawChanceValue)
	{
		if (def is EmptyFactionDef)
			SetXenotypeChanceForEmptyFaction(xenotypeDef, rawChanceValue);
		else
			SetChanceInXenotypeSet(ref _xenotypeSetRef(def), xenotypeDef, rawChanceValue);
	}

	private static void SetXenotypeChanceForEmptyFaction(XenotypeDef xenotypeDef, int rawChanceValue)
		=> xenotypeDef.factionlessGenerationWeight = rawChanceValue / 10f;

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

	private static readonly AccessTools.FieldRef<T, XenotypeSet?> _xenotypeSetRef = XenotypeSetRefs.GetForType<T>();

	public float GetCustomXenotypeChanceValueSum() => CustomXenotypeChances.Sum(static xenoChance => xenoChance.Value);

	public float GetCustomXenotypeChanceSumExcluding(CustomXenotype? xenotype)
		=> CustomXenotypeChances.Where(xenoChance => xenoChance.Xenotype.CustomXenotype != xenotype)
			.Sum(static chance => chance.Value);

	public void SetXenotypeChance(XenotypeChance xenotypeChance, bool adjustIfNecessary = true)
	{
		if (!AllLoadedXenotypeChances.ContainsKey(xenotypeChance.Xenotype.Name))
		{
			throw new ArgumentException("SetChanceForXenotype was called for an unloaded Xenotype",
				nameof(xenotypeChance));
		}

		if (!AllAllowedXenotypeChances.Contains(xenotypeChance) && xenotypeChance.RawValue != 0)
		{
			throw new ArgumentException(
				"SetChanceForXenotype was called for a disallowed Xenotype with a chance differing from 0%",
				nameof(xenotypeChance));
		}

		if (xenotypeChance.IsAbsolute)
		{
			if (xenotypeChance.Xenotype.Def != XenotypeDefOf.Baseliner)
			{
				if (adjustIfNecessary)
				{
					EnsureAbsoluteChancesNotExceeding100(xenotypeChance);
					// baseliner chance is derivative, so we need to update it's value manually
					UpdateBaselinerChanceValue();
				}
			}
			else if (adjustIfNecessary)
			{
				AdjustAbsoluteChancesToFitBaseliner();
			}
		}

		// weighted xenotypes always need to be rebalanced
		CalculateWeightedXenotypeChances();

		if (xenotypeChance.Xenotype.CustomXenotype is null
			&& Def is { } def
			&& xenotypeChance.Xenotype.Def is { } xenotypeDef)
		{
			SetXenotypeChanceForDef(def, xenotypeDef, xenotypeChance.RawValue);
		}
	}

	private bool _balancingWeightedChances;

	private void CalculateWeightedXenotypeChances()
	{
		if (_balancingWeightedChances || OnlyAbsoluteChancesAllowed)
			return;

		_balancingWeightedChances = true;
		var weightedXenotypeChances = GetAllowedWeightedXenotypeChances().ToList();
		var weightSum = weightedXenotypeChances.Sum(static xenoChance => xenoChance.Weight);
		
		// weighted xenotypes get distributed among the remaining chance of all absolute xenotypes
		var rawDistributionChance = GetRawWeightedDistributionChance();
		var weightedXenotypesForAdjustment = new List<XenotypeChance>();
		
		foreach (var weightedXenotypeChance in weightedXenotypeChances)
		{
			// if all weights are set to 0, average every xenotype instead
			var relativeWeight = weightSum > 0
				? weightedXenotypeChance.Weight / weightSum
				: 1f / weightedXenotypeChances.Count;
			
			weightedXenotypeChance.RawValue = Mathf.RoundToInt(rawDistributionChance * relativeWeight);
			SetXenotypeChance(weightedXenotypeChance, false);
			
			if (relativeWeight > 0)
				weightedXenotypesForAdjustment.Add(weightedXenotypeChance);
		}

		// thanks to rounding errors we need to balance the difference out manually
		SetChancesInLoopUntilFitting(weightedXenotypesForAdjustment, rawDistributionChance);

		_balancingWeightedChances = false;
	}

	public void SetWeightForAllowedWeightedXenotypes(float weight)
	{
		foreach(var xenotypeChance in GetAllowedWeightedXenotypeChances())
			xenotypeChance.Weight = weight;
		
		CalculateWeightedXenotypeChances();
	}

	private IEnumerable<XenotypeChance> GetAllowedWeightedXenotypeChances()
		=> AllAllowedXenotypeChances.Where(static xenoChance => !xenoChance.IsAbsolute);

	private IEnumerable<XenotypeChance> GetAllowedAbsoluteXenotypeChances()
		=> AllAllowedXenotypeChances.Where(static xenoChance => xenoChance.IsAbsolute);

	public int GetRawWeightedDistributionChance()
		=> Math.Max(1000 - GetAllowedAbsoluteXenotypeChances().Sum(static xenoChance => xenoChance.RawValue), 0);

	public void SetIsAbsoluteForAllowed(bool value)
	{
		// xenotypes need to be ordered, so the lowest chance gets assigned the weight 1
		foreach (var xenotypeChance in AllAllowedXenotypeChances.OrderBy(static xenoChance => xenoChance.RawValue)
			.ToList())
		{
			SetIsAbsoluteForXenotypeChance(xenotypeChance, value);
		}
	}

	public void SetIsAbsoluteForXenotypeChance(XenotypeChance xenotypeChance, bool value)
	{
		if (xenotypeChance.IsAbsolute == value)
			return;

		xenotypeChance.IsAbsolute = value;
		if (!value)
			SetWeightFromAbsoluteValue(xenotypeChance);
	}

	private void SetWeightFromAbsoluteValue(XenotypeChance xenotypeChance)
	{
		// rawValue = distributionPercentage * (weight / weightSum)
		// weight = rawValue * weightSum / distributionPercentage
		// weightSum = weight + weightOthers		distributionPercentage >= rawValue
		// weight = rawValue * weight / distributionPercentage + rawValue * weightOthers / distributionPercentage
		// weight - rawValue * weight / distributionPercentage = rawValue * weightOthers / distributionPercentage
		// weight * distributionPercentage - rawValue * weight = rawValue * weightOthers
		// weight * (distributionPercentage - rawValue) = rawValue * weightOthers
		// weight = rawValue * weightOthers / (distributionPercentage - rawValue)
		// rawValue != distributionPercentage
		// weightOthers != 0
		// distributionPercent != 0
		
		if (xenotypeChance.IsAbsolute)
		{
			throw new ArgumentException($"{nameof(SetWeightFromAbsoluteValue)} was called for a non weighted xenotype",
				nameof(xenotypeChance));
		}

		var rawDistributionChance = GetRawWeightedDistributionChance();
		var otherWeightedXenotypes = GetAllowedWeightedXenotypeChances()
			.Where(xenoChance => xenoChance != xenotypeChance).ToList();
		
		var weightOthers = otherWeightedXenotypes.Sum(static xenoChance => xenoChance.Weight);
		
		if (rawDistributionChance == 0)
		{
			// the xenotype wasn't worth anything before, so it isn't worth anything now
			xenotypeChance.Weight = 0f;
			return;
		}
		else if (xenotypeChance.RawValue == rawDistributionChance)
		{
			// the xenotype makes up the entire weighted chances, any weight would have the same effect, settle for 1
			xenotypeChance.Weight = 1f;
			return;
		}
		else if (weightOthers == 0f)
		{
			// when calculating weighted xenotype chances we average all weighted xenotypes if all weights are 0.
			// To keep that behavior and their chances we need to set every weight to a non zero value
			weightOthers = 0f;
			foreach(var otherWeightedXenotype in otherWeightedXenotypes)
			{
				otherWeightedXenotype.Weight = 1f;
				++weightOthers;
			}
		}

		xenotypeChance.Weight
			= xenotypeChance.RawValue * weightOthers / (rawDistributionChance - xenotypeChance.RawValue);
	}

	private XenotypeChance GetOrAddBaselinerXenotypeChance()
		=> GetOrAddXenotypeAndChance(XenotypeDefOf.Baseliner.defName)!;

	private void UpdateBaselinerChanceValue()
	{
		var baselinerXenotypeChance = GetOrAddBaselinerXenotypeChance();
		
		// we can save recalculating value if baseliner is weighted
		if (!baselinerXenotypeChance.IsAbsolute)
		{
			SetXenotypeChance(baselinerXenotypeChance, false);
			return;
		}
		
		var othersChance = GetAllowedAbsoluteRawValuesSumExceptBaseliner();
		if (baselinerXenotypeChance.RawValue + othersChance <= 1000 && !OnlyAbsoluteChancesAllowed)
			return;

		baselinerXenotypeChance.RawValue = 1000 - othersChance;
		SetXenotypeChance(baselinerXenotypeChance, false);
	}

	private void AdjustAbsoluteChancesToFitBaseliner()
	{
		var allowedAbsoluteChances = GetAllowedAbsoluteXenotypeChances().ToList();
		// if any xenotype chance is weighted we only want to adjust if the sum of all chances exceeds 100%
		if (!OnlyAbsoluteChancesAllowed && allowedAbsoluteChances.Sum(static xenoChance => xenoChance.RawValue) <= 1000)
			return;

		// relevant xenotypes are every xenotype except Baseliner with some chance already assigned, or all if no
		// chance is assigned to any xenotype
		var anyXenotypeChanceAssigned = allowedAbsoluteChances.Any(static xenoChance
			=> xenoChance.Xenotype.Def != XenotypeDefOf.Baseliner && xenoChance.RawValue > 0);
		
		if (!anyXenotypeChanceAssigned)
			_distributeAmongAllXenotypes = true;

		var relevantXenotypes = allowedAbsoluteChances
			.Where(xenoChance
				=> xenoChance.Xenotype.Def != XenotypeDefOf.Baseliner
				&& (_distributeAmongAllXenotypes || xenoChance.RawValue > 0)).ToList();

		if (!relevantXenotypes.Any())
		{
			if (!allowedAbsoluteChances.Any())
				throw new("allowedAbsoluteChances is somehow empty");
			else
				throw new("relevantXenotypes is somehow empty");
		}

		// set baseliner chance by distributing the remaining percentages among the relevant xenotypes
		SetChancesInLoopUntilFitting(relevantXenotypes, 1000 - GetOrAddBaselinerXenotypeChance().RawValue);
		
		// check if every xenotype has a value assigned to reset distribution flag
		var allXenotypeChanceAssigned = allowedAbsoluteChances.All(static xenoChance
			=> xenoChance.Xenotype.Def == XenotypeDefOf.Baseliner || xenoChance.RawValue > 0);
		
		if (allXenotypeChanceAssigned)
			_distributeAmongAllXenotypes = false;
	}

	/// <summary>
	/// set chances of given xenotypes until the sum of all chances matches the given chance
	/// </summary>
	private void SetChancesInLoopUntilFitting(List<XenotypeChance> xenotypes, int rawTargetChance)
	{
		var originalCount = xenotypes.Count;
		
		// clamp target chance to allowed values
		rawTargetChance = Mathf.Clamp(rawTargetChance, 0, 1000);
		var chanceDelta = rawTargetChance - xenotypes.Sum(static xenoChance => xenoChance.RawValue);
		
		// prevent recalculating the weighted chances on every tiny change and just do it once at the end
		var oldBalancingWeightedChances = _balancingWeightedChances;
		_balancingWeightedChances = true;
		
		while (chanceDelta != 0)
		{
			var deltaSign = Math.Sign(chanceDelta);
			_counter += deltaSign;

			ClampRepeat(ref _counter, 0, xenotypes.Count - 1);

			var currentXenotypeChance = xenotypes[_counter];
			// xenotype chance cannot be lowered or raised any further, stop trying to do so
			if ((currentXenotypeChance.RawValue == 0 && deltaSign < 0) || (currentXenotypeChance.RawValue == 1000 && deltaSign > 0))
			{
				xenotypes.RemoveAt(_counter);
				
				// we might have removed the last xenotype (shouldn't happen => throw exception)
				if (xenotypes.Count == 0)
				{
					throw new ArgumentException(
						$"there were not enough xenotypes to ensure target chance of {
							rawTargetChance}, Starting xenotype count: {originalCount}, Delta: {
								chanceDelta}", nameof(xenotypes));
				}
				else
				{
					continue;
				}
			}

			currentXenotypeChance.RawValue += deltaSign;
			SetXenotypeChance(currentXenotypeChance, false);
			chanceDelta -= deltaSign;
		}

		_balancingWeightedChances = oldBalancingWeightedChances;
		CalculateWeightedXenotypeChances();
	}

	private void EnsureAbsoluteChancesNotExceeding100(XenotypeChance xenotypeChance)
	{
		if (xenotypeChance.Xenotype.Def == XenotypeDefOf.Baseliner)
		{
			throw new ArgumentException("Called EnsureAbsoluteChancesNotExceed100 for Baseliner. "
				+ "AdjustAbsoluteChancesToFitBaseliner should be used instead.");
		}

		if (GetAllowedAbsoluteRawValuesSumExceptBaseliner() <= 1000)
			return;

		SetChancesInLoopUntilFitting(GetAllowedAbsoluteXenotypeChances()
				.Where(xenoChance
					=> xenoChance.Xenotype is var xenotype
					&& xenotype.Def != XenotypeDefOf.Baseliner
					&& xenotype != xenotypeChance.Xenotype
					&& xenoChance.RawValue > 0).ToList(),
			1000 - xenotypeChance.RawValue);
	}

	private int GetAllowedAbsoluteRawValuesSumExceptBaseliner()
		=> GetAllowedAbsoluteXenotypeChances()
			.Where(static xenoChance => xenoChance.Xenotype.Def != XenotypeDefOf.Baseliner)
			.Sum(static xenoChance => xenoChance.RawValue);

	public XenotypeChance? GetOrAddXenotypeAndChance(string defName)
		=> AllLoadedXenotypeChances.TryGetValue(defName, out var xenotype) ? xenotype : TryAddXenotype(defName);

	public XenotypeChance GetOrAddModifiableXenotype(ModifiableXenotype xenotype)
	{
		if (AllLoadedXenotypeChances.TryGetValue(xenotype.Name, out var xenotypeChance)
			&& xenotypeChance.Xenotype == xenotype)
			return xenotypeChance;

		if (xenotypeChance != null)
		{
			// update xenotype
			SwitchXenotypeOfXenotypeChance(xenotypeChance, xenotype);
		}
		else
		{
			// add xenotype to loaded xenotypes
			if (!_currentConfig.XenotypeChances.TryGetValue(xenotype.Name, out var chanceConfig))
			{
				chanceConfig = new();
				_currentConfig.XenotypeChances[xenotype.Name] = chanceConfig;
			}
			else if (UnloadedXenotypes.FirstOrDefault(keyValuePair => keyValuePair.Key == xenotype.Name) is
			{
				Key: not null
			} unloadedXenotype)
			{
				RemoveBaselinerAdjustmentForUnloadedXenotype(unloadedXenotype);
			}
			
			xenotypeChance  = new(chanceConfig, xenotype);
			AllLoadedXenotypeChances[xenotype.Name] = xenotypeChance;
			InitializeDefaultValueForXenotypeChance(xenotypeChance);

			// ensure a value gets set if xenotype was added after initialization
			if (_initialized)
				SetInitialValueForXenotypeChance(xenotypeChance);
		}

		return xenotypeChance;
	}

	private void SwitchXenotypeOfXenotypeChance(XenotypeChance xenotypeChance, ModifiableXenotype newXenotype)
	{
		if (xenotypeChance.Xenotype.Def == XenotypeDefOf.Baseliner)
			return;
		
		if (Def is { } def && xenotypeChance.Xenotype.Def is { } xenotypeDef)
			SetXenotypeChanceForDef(def, xenotypeDef, 0);
		
		xenotypeChance.Xenotype = newXenotype;
		SetXenotypeChance(xenotypeChance, false);
	}

	private XenotypeChance? TryAddXenotype(string defName)
		=> ModifiableXenotypeDatabase.TryGetXenotype(defName) is { } xenotype
			? GetOrAddModifiableXenotype(xenotype)
			: null;

	private string GetNameFromDatabase() => XenotypeChanceDatabase<T>.From(this);

	private bool _initialized;
	private bool _baselinerAdjustedForUnloadedDuringInit;
	
	public void Initialize()
	{
		_initialized = false;
		foreach (var xenotype in ModifiableXenotypeDatabase.AllValues)
			GetOrAddXenotypeAndChance(xenotype.Key);

		_baselinerAdjustedForUnloadedDuringInit = UnloadedXenotypes.Any() && OnlyAbsoluteChancesAllowed;

		foreach (var xenotype in AllLoadedXenotypeChances.Values)
			SetInitialValueForXenotypeChance(xenotype);
		
		UpdateBaselinerChanceValue();
		// make sure all chances total up to 100%
		var allowedAbsolute = GetAllowedAbsoluteXenotypeChances().ToList();
		if (allowedAbsolute.Sum(static xenoChance => xenoChance.RawValue) > 1000)
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
		if (Def is not { } def)
			return;
		
		// remember to change current value as well, if it was default
		var wasDefault = xenotypeChance.IsDefault;
		var isEmptyFaction = def is EmptyFactionDef;
		
		xenotypeChance.DefaultIsAbsolute = !isEmptyFaction;
		
		if (isEmptyFaction)
		{
			// do not spawn custom xenotypes in empty faction by default
			xenotypeChance.DefaultWeight = xenotypeChance.Xenotype.Def?.factionlessGenerationWeight ?? 0f;
		}
		else
		{
			xenotypeChance.DefaultValue = GetDefaultValueFromDef(xenotypeChance.Xenotype);
		}

		if (wasDefault)
			xenotypeChance.SetToDefault();
	}

	private float GetDefaultValueFromDef(ModifiableXenotype xenotype)
	{
		float result;
		if (xenotype.Def == XenotypeDefOf.Baseliner)
		{
			// baseliner isn't directly set in the def, calculate instead
			result = 1f
				- ModifiableXenotypeDatabase.AllValues.Where(static pair => pair.Value.Def != XenotypeDefOf.Baseliner)
					.Sum(pair => GetDefaultValueFromDef(pair.Value));
		}
		else
		{
			result = xenotype.Def is { } xenotypeDef
				? FindChanceInXenotypeSet(_xenotypeSetRef(Def!), xenotypeDef) ?? 0f
				: xenotype is ModifiableXenotype.Generated generatedXenotype
					? generatedXenotype.GetDefaultChanceIn(Def)
					: 0f;
		}

		return Mathf.Clamp(result, 0f, 1f);
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
		xenotypeChance.SetToDefault();
		SetXenotypeChance(xenotypeChance, false);
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

			if (AllLoadedXenotypeChances.TryGetValue(defName, out var removedXenotypeChance))
			{
				DisableXenotypeChance(removedXenotypeChance, true);
				AllLoadedXenotypeChances.Remove(defName);
			}
			_currentConfig.XenotypeChances.Remove(defName);
		}
	}

	public void SetChanceInXenotypeSet(ref XenotypeSet? set, XenotypeDef xenotypeDef, int chance)
	{
		if (xenotypeDef == XenotypeDefOf.Baseliner)
			return;

		set ??= new();
		var chances = set.xenotypeChances ??= [];
		var chanceIndex = chances.FindIndex(xenoChance => xenoChance.xenotype == xenotypeDef);
		
		// remove xenotypes from chances if chance is 0 to avoid cluttering the tooltip of the def
		if (chance == 0)
		{
			if (chanceIndex >= 0)
				chances.RemoveAt(chanceIndex);
		}
		else
		{
			var floatChance = chance / 1000f;
			if (chanceIndex >= 0)
				chances[chanceIndex].chance = floatChance;
			else
				chances.Add(new(xenotypeDef, floatChance));
		}

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

	// TODO: allow more filter modifications (e.g. filter for necessary/forbidden genes)
	// => disable chance modification, if only baseliner available
	private bool PassesFilters(ModifiableXenotype xenotype)
		=> xenotype.Def == XenotypeDefOf.Baseliner || AllowArchiteXenotypes || !xenotype.IsArchite;

	private void DisableDisallowedXenotypes()
	{
		foreach (var xenotypeChance in AllLoadedXenotypeChances.Values.Except(AllAllowedXenotypeChances))
			DisableXenotypeChance(xenotypeChance, true);
	}

	private void DisableXenotypeChance(XenotypeChance xenotypeChance, bool adjustChances)
	{
		xenotypeChance.IsAbsolute = true;
		xenotypeChance.RawValue = 0;
		SetXenotypeChance(xenotypeChance, adjustChances);
	}

	private static int _counter;

	public bool RequiresSaving()
		=> !AllowArchiteXenotypes
			|| AllLoadedXenotypeChances.Any(static pair => !pair.Value.IsDefault)
			|| UnloadedXenotypes.Any();

	public void ExposeData()
	{
		var configToExpose = _currentConfig;
		if (Scribe.mode == LoadSaveMode.Saving)
		{
			if (!RequiresSaving())
				return;
			
			// remove any value that has been added during load for the unloaded xenotypes
			// this cannot be done during loading as we cannot know what xenotypes were previously unloaded by then
			foreach (var unloadedXenotype in UnloadedXenotypes)
				RemoveBaselinerAdjustmentForUnloadedXenotype(unloadedXenotype);
			
			// do not save loaded values that have not been modified
			configToExpose = CreateFilteredConfigCopy(keyConfigPair
				=> !AllLoadedXenotypeChances.TryGetValue(keyConfigPair.Key, out var config) || !config.IsDefault);
		}
		
		configToExpose.ExposeData();
	}

	// ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
	private void RemoveBaselinerAdjustmentForUnloadedXenotype(
		KeyValuePair<string, XenotypeChanceConfig> unloadedXenotype)
	{
		if (!UnloadedXenotypes.Any(kvp => kvp.Key == unloadedXenotype.Key && kvp.Value == unloadedXenotype.Value))
		{
			throw new ArgumentException(
				$"{nameof(RemoveBaselinerAdjustmentForUnloadedXenotype)} wasn't called for unloaded xenotype");
		}

		if (!_baselinerAdjustedForUnloadedDuringInit)
			return;

		var baselinerChance = GetOrAddBaselinerXenotypeChance();
		baselinerChance.RawValue -= Math.Min(baselinerChance.RawValue,
			UnloadedXenotypes.Sum(static keyValue => keyValue.Value.RawChanceValue));
	}

	public XenotypeChancesConfig CreateTemplate()
		// only write relevant xenotypes (xenotypes which are weighted or nonzero) into template
		=> CreateFilteredConfigCopy(static keyConfigPair
			=> !keyConfigPair.Value.IsAbsolute || keyConfigPair.Value.RawChanceValue != 0);

	private XenotypeChancesConfig CreateFilteredConfigCopy(Predicate<KeyValuePair<string, XenotypeChanceConfig>> filter)
	{
		var result = new XenotypeChancesConfig(_currentConfig);
		foreach (var keyValuePair in result.XenotypeChances.Where(keyValuePair => !filter(keyValuePair)).ToArray())
			result.XenotypeChances.Remove(keyValuePair.Key);

		return result;
	}

	public void ApplyTemplate(XenotypeChancesConfig template)
	{
		// load all xenotypes from template
		foreach (var templateXenotypeKeyValuePair in template.XenotypeChances)
		{
			if (AllLoadedXenotypeChances.TryGetValue(templateXenotypeKeyValuePair.Key, out var config))
			{
				config.IsAbsolute = templateXenotypeKeyValuePair.Value.IsAbsolute;
				config.RawValue = templateXenotypeKeyValuePair.Value.RawChanceValue;
				config.Weight = templateXenotypeKeyValuePair.Value.Weight;
			}
			// we do not care that a xenotype doesn't exist if the chance of it appearing was 0
			else if (templateXenotypeKeyValuePair.Value.RawChanceValue != 0)
			{
				_currentConfig.XenotypeChances[templateXenotypeKeyValuePair.Key]
					= new(templateXenotypeKeyValuePair.Value);
			}
		}

		// disable all xenotypes not part of template
		foreach (var xenoChanceKeyValuePair in AllLoadedXenotypeChances)
		{
			if (!template.XenotypeChances.ContainsKey(xenoChanceKeyValuePair.Key))
				DisableXenotypeChance(xenoChanceKeyValuePair.Value, false);
		}

		_currentConfig.AllowArchite = template.AllowArchite;

		// set all chances in game
		foreach(var loadedXenotype in AllLoadedXenotypeChances.Values)
			SetInitialValueForXenotypeChance(loadedXenotype);
	}
}