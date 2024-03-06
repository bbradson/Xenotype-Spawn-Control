public class XenotypeChancesConfig : IExposable
{
	public bool AllowArchite = true;
	public Dictionary<string, XenotypeChanceConfig> XenotypeChances = new();
	public XenotypeChancesConfig(){}
	public XenotypeChancesConfig(XenotypeChancesConfig other)
	{
		AllowArchite = other.AllowArchite;
		foreach(var xenotypeChanceKeyValuePair in other.XenotypeChances)
		{
			XenotypeChances[xenotypeChanceKeyValuePair.Key] = new(xenotypeChanceKeyValuePair.Value);
		}
	}
	public void ExposeData()
	{
		Scribe_Values.Look(ref AllowArchite, "AllowArchite", true);

		Scribe_Collections.Look(ref XenotypeChances, "XenotypeChances", LookMode.Value, LookMode.Deep);
		XenotypeChances ??= new();
	}
}

public class XenotypeChanceConfig : IExposable
{
	public int RawChanceValue = -1000;
	public bool IsAbsolute = true;
	public float Weight = 0;

	public XenotypeChanceConfig(){}
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
			Scribe_Values.Look(ref Weight, "weight", 0);
	}
}