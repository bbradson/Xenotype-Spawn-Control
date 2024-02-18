// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using System.IO;
using System.Linq;

namespace XenotypeSpawnControl;

public static class ModifiableXenotypeDatabase
{
	public static Dictionary<string, ModifiableXenotype> AllValues = new();
	public static Dictionary<string, ModifiableXenotype> CustomXenotypes = new();

	static ModifiableXenotypeDatabase()
	{
		if (DefDatabase<XenotypeDef>.AllDefsListForReading.Count == 0)
			Log.Error($"XenotypeDef database is empty.{(ModsConfig.BiotechActive ? "" : " Consider activating Biotech. This is a mod for Biotech genes.")}");

		foreach (var def in DefDatabase<XenotypeDef>.AllDefsListForReading)
			AllValues[def.defName] = new(def);

		AddModifiableXenotype(new ModifiableXenotype.Random());
		AddModifiableXenotype(new ModifiableXenotype.Hybrid());

		LoadAllCustomXenotypes(); // overwrite premade xenotypes with identical names
	}

	public static ModifiableXenotype? TryGetXenotype(string name)
		=> AllValues.TryGetValue(name, out var xenotype)
		? xenotype
		: TryLoadCustomXenotype(name)
		?? TryLoadXenotypeDef(name);

	public static ModifiableXenotype? GetOrAddCustomXenotype(CustomXenotype xenotype)
		=> CustomXenotypes.TryGetValue(xenotype.name, out var modifiableXenotype)
		? modifiableXenotype
		: SetCustomXenotype(xenotype);

	public static void RemoveCustomXenotype(string name)
	{
		//if custom xenotype overrides premade xenotype enable premade one instead
		if (DefDatabase<XenotypeDef>.AllDefsListForReading.FirstOrDefault(def => def.defName == name) is {} def)
			AllValues[def.defName] = new(def);
		else
			AllValues.Remove(name);
		CustomXenotypes.Remove(name);
		XenotypeChanceDatabases.RemoveCustomXenotype(name);
	}

	public static void DeleteCustomXenotype(string name)
	{
		GenFilePaths.AllCustomXenotypeFiles.FirstOrDefault(file => file.Name.Contains(GenFile.SanitizedFileName(name)))?.Delete();
		RemoveCustomXenotype(name);
	}

	private static void LoadAllCustomXenotypes()
	{
		var customXenotypeDatabase = Current.Game?.customXenotypeDatabase?.customXenotypes;
		if (customXenotypeDatabase != null) // always false tbh
		{
			foreach (var customXenotype in customXenotypeDatabase)
				TryLoadCustomXenotype(customXenotype.name);
		}

		// CharacterCardUtility.CustomXenotypes, but without mod mismatch and version check
		foreach (var xenotypeFile in GenFilePaths.AllCustomXenotypeFiles.OrderBy((FileInfo f) => f.LastWriteTime))
			TryLoadCustomXenotype(Path.GetFileNameWithoutExtension(xenotypeFile.Name));
	}

	private static ModifiableXenotype? TryLoadXenotypeDef(string name)
		=> DefDatabase<XenotypeDef>.GetNamedSilentFail(name) is { } xenoDef
		? AllValues[name] = new(xenoDef)
		: null;

	private static ModifiableXenotype? TryLoadCustomXenotype(string name)
	{
		var xenotype = Current.Game?.customXenotypeDatabase?.customXenotypes.Find(xenotype => xenotype.name == name);
		if (xenotype is null)
			GameDataSaveLoader.TryLoadXenotype(GenFilePaths.AbsFilePathForXenotype(GenFile.SanitizedFileName(name)), out xenotype);

		return xenotype is null ? null
			: SetCustomXenotype(xenotype);
	}

	private static ModifiableXenotype? SetCustomXenotype(CustomXenotype xenotype)
	{
		if (Array.IndexOf(InvalidNames, xenotype.name) >= 0)
			return null;

		//forbid adding baseliner custom xenotype
		if (xenotype.name == XenotypeDefOf.Baseliner.defName)
		{
			//TODO:maybe add some kind of warning that baseliner won't be overridden
			return null;
		}

		var modifiableXenotype = new ModifiableXenotype(xenotype);
		AddModifiableXenotype(modifiableXenotype);
		return modifiableXenotype;
	}

	private static void AddModifiableXenotype(ModifiableXenotype xenotype)
	{
		AllValues[xenotype.Name] = CustomXenotypes[xenotype.Name] = xenotype;
		XenotypeChanceDatabases.AddModifiableXenotype(xenotype);
	}

	private static string[] InvalidNames { get; } = new[]
	{
		Strings.HybridKey,
		Strings.RandomGenesKey
	};
}
