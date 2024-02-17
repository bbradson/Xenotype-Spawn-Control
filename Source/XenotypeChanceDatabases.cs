// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

namespace XenotypeSpawnControl;

public static class XenotypeChanceDatabases
{
	// To add a new Def type:
	// - Add to SupportedDefTypes here
	// - Add name to Strings
	// - Add to ModSettingsWindow.Tabs
	// - Add to PawnGenerator_GenerateGenes
	// - possibly add to XenotypeSetRefs

	private static GenericBase[] SupportedDefTypes { get; } = new GenericBase[]
	{
		new Generic<FactionDef>(),
		new Generic<PawnKindDef>(),
		new Generic<MemeDef>()
	};

	public static void AddModifiableXenotype(ModifiableXenotype xenotype)
	{
		foreach (var defType in SupportedDefTypes)
			defType.AddXenotypeToChanceDatabase(xenotype);
	}

	/// <summary>
	/// Called from ModifiableXenotypeDatabase
	/// </summary>
	internal static void RemoveCustomXenotype(string name)
	{
		foreach (var defType in SupportedDefTypes)
			defType.RemoveXenotypeFromChanceDatabase(name);

		Current.Game?.customXenotypeDatabase?.customXenotypes.RemoveAll(xenotype => xenotype.name == name);
	}

	public static void AssignXenotypeChancesFromSettings()
	{
		foreach (var defType in SupportedDefTypes)
			defType.AssignXenotypeChancesFromSettings();
	}

	public static void ResetAll()
	{
		foreach (var defType in SupportedDefTypes)
			defType.ResetAll();
	}

	public static void ExposeData()
	{
		foreach (var defType in SupportedDefTypes)
			defType.ExposeData();
	}

	private abstract class GenericBase
	{
		internal abstract void AddXenotypeToChanceDatabase(ModifiableXenotype xenotype);
		internal abstract void RemoveXenotypeFromChanceDatabase(string name);
		internal abstract void AssignXenotypeChancesFromSettings();
		internal abstract void ResetAll();
		internal abstract void ExposeData();

	}
	private class Generic<T> : GenericBase where T : Def
	{
		private static bool RequiresIdeology => typeof(T) == typeof(MemeDef);

		private static bool RequirementsMet => !RequiresIdeology || ModsConfig.IdeologyActive;

		private static IEnumerable<T> Defs
			=> typeof(T) == typeof(FactionDef)
			? DefDatabase<T>.AllDefs.Concat((T)(Def)EmptyFactionDef.Instance)
			: DefDatabase<T>.AllDefs;

		internal override void AddXenotypeToChanceDatabase(ModifiableXenotype xenotype)
		{
			if (!RequirementsMet)
				return;

			foreach (var def in Defs)
			{
				var xenotypeChances = XenotypeChanceDatabase<T>.For(def.defName);
				xenotypeChances.GetOrAddModifiableXenotype(xenotype);
			}
		}

		internal override void RemoveXenotypeFromChanceDatabase(string name)
		{
			if (!RequirementsMet)
				return;

			foreach (var def in Defs)
			{
				var xenotypeChances = XenotypeChanceDatabase<T>.For(def.defName);
				xenotypeChances.Remove(name);
			}
		}

		internal override void AssignXenotypeChancesFromSettings()
		{
			if (!RequirementsMet)
				return;

			foreach (var def in Defs)
			{
				var xenotypeChances = XenotypeChanceDatabase<T>.For(def.defName);

				xenotypeChances.Initialize();
			}
		}

		internal override void ResetAll()
			=> XenotypeChanceDatabase<T>.ResetAll();

		internal override void ExposeData()
			=> XenotypeChanceDatabase<T>.ExposeData();
	}
}