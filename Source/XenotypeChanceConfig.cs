// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

namespace XenotypeSpawnControl;

public class XenotypeChanceConfig : IExposable
{
	public int RawChanceValue = -1000;
	public bool IsAbsolute = true;
	public float Weight;

	public XenotypeChanceConfig()
	{
	}

	public XenotypeChanceConfig(XenotypeChanceConfig other)
	{
		RawChanceValue = other.RawChanceValue;
		IsAbsolute = other.IsAbsolute;
		Weight = other.Weight;
	}

	public void ExposeData()
	{
		Scribe_Values.Look(ref RawChanceValue, "chance", -1000);
		Scribe_Values.Look(ref IsAbsolute, "isAbsolute", true);
		Scribe_Values.Look(ref Weight, "weight");
	}
}