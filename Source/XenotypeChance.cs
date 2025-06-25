// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using System.Globalization;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace XenotypeSpawnControl;

public class XenotypeChance
{
	private readonly XenotypeChanceConfig _currentConfig;

	public ModifiableXenotype Xenotype { get; set; }

	private float _defaultValue = -1f;
	public float DefaultValue 
	{ 
		get => _defaultValue; 
		// ensure value is in allowed percentage range
		set
		{
			if (DefaultValue != value)
				_defaultValue = Mathf.Clamp(value, 0f, 1f);
		}
	}
	
	private float _defaultWeight = -1f;
	public float DefaultWeight 
	{ 
		get => _defaultWeight; 
		// ensure value is in allowed percentage range
		set
		{
			if (DefaultWeight != value)
				_defaultWeight = Mathf.Max(value, 0f);
		}
	}
	
	public bool DefaultIsAbsolute { get; set; } = true;

	public bool IsDefault
		=> IsAbsolute == DefaultIsAbsolute
			&& (IsAbsolute ? Mathf.Abs(Value - DefaultValue) < 0.0005f : Mathf.Abs(Weight - DefaultWeight) < 0.0005f);

	//TODO: refactor XenotypeChances so we can call SetXenotypeChance on setting the relevant properties
	public int RawValue
	{
		get => _currentConfig.RawChanceValue;

		// ensure value is in allowed percentage range
		set
		{
			if (RawValue == value)
				return;

			_currentConfig.RawChanceValue = Mathf.Clamp(value, 0, 1000);
			ResetChanceString();
		}
	}

	public float Value
	{
		get => _currentConfig.RawChanceValue / 1000f;
		// ensure value is in allowed percentage range
		set
		{
			if (Value == value)
				return;

			_currentConfig.RawChanceValue = Mathf.Clamp(Mathf.RoundToInt(value * 1000f), 0, 1000);
			ResetChanceString();
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
			if (Weight == value)
				return;

			_currentConfig.Weight = value >= 0f ? value : 0f;
			ResetWeightString();
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

	public void SetToDefault()
	{
		// ReSharper disable once AssignmentInConditionalExpression
		if (IsAbsolute = DefaultIsAbsolute)
			Value = DefaultValue;
		else
			Weight = DefaultWeight;
	}

	[MemberNotNull(nameof(ChanceString))]
	private void ResetChanceString() => ChanceString = (Value * 100f).ToString("##0.#", CultureInfo.InvariantCulture);

	[MemberNotNull(nameof(WeightString))]
	private void ResetWeightString() => WeightString = Weight.ToString(CultureInfo.InvariantCulture);
}