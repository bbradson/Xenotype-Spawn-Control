namespace XenotypeSpawnControl;

public class XenotypeChancesConfig : IExposable
{
	public bool AllowArchite = true;
	public Dictionary<string, XenotypeChanceConfig> XenotypeChances = new();

	public XenotypeChancesConfig()
	{
	}
	
	public XenotypeChancesConfig(XenotypeChancesConfig other)
	{
		AllowArchite = other.AllowArchite;
		
		foreach(var xenotypeChanceKeyValuePair in other.XenotypeChances)
			XenotypeChances[xenotypeChanceKeyValuePair.Key] = new(xenotypeChanceKeyValuePair.Value);
	}
	
	public void ExposeData()
	{
		Scribe_Values.Look(ref AllowArchite, "AllowArchite", true);
		Scribe_Collections.Look(ref XenotypeChances, "XenotypeChances", LookMode.Value, LookMode.Deep);
		
		XenotypeChances ??= new();
	}
}