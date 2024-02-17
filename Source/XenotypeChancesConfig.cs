public class XenotypeChancesConfig : IExposable
{
	public bool AllowArchite = true;
	public Dictionary<string, XenotypeChanceConfig> XenotypeChances = new();
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
	public void ExposeData()
	{
			Scribe_Values.Look(ref RawChanceValue, "chance", -1000);
	}
}