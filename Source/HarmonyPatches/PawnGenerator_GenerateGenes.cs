// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

namespace XenotypeSpawnControl.HarmonyPatches;

[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GenerateGenes))]
public class PawnGenerator_GenerateGenes
{
	[HarmonyPrefix]
	public static void Prefix(Pawn? pawn, ref XenotypeDef? xenotype, ref PawnGenerationRequest request)
	{
		if (pawn?.genes is null)
			return;

		if (xenotype is null)
			Log.Error($"xenotype is null");

		if (xenotype != XenotypeDefOf.Baseliner || request.ForcedCustomXenotype != null
			|| (request.ForcedXenotype != null && request.ForcedXenotype != XenotypeDefOf.Baseliner))
		{
			return;
		}

		if (request.Faction is { } faction)
			SetXenotypeForFaction(faction, ref request);

		if (request.KindDef is { } pawnKindDef)
			SetXenotypeForPawnKind(pawnKindDef, ref request);

		/*if (request.ForcedCustomXenotype == null
			&& TryGetReplacement(xenotype!, out var replacementXenotype))
		{
			request.ForcedCustomXenotype = replacementXenotype.CustomXenotype;
		}*/
	}

	// This here would replace the Baseliner xenotype if replacing it would be supported by this mod. It is not, however.
	//private static bool TryGetReplacement(XenotypeDef xenotypeDef, out ModifiableXenotype replacementXenotype)
	//	=> ModifiableXenotypeDatabase.CustomXenotypes.TryGetValue(xenotypeDef.defName, out replacementXenotype);

	private static void SetXenotypeForFaction(Faction faction, ref PawnGenerationRequest request)
	{
		if (TryGetCustomXenotypeByWeight<FactionDef>(faction.def.defName, out var factionXenotype))
			request.ForcedCustomXenotype = factionXenotype;

		if (faction?.ideos?.PrimaryIdeo?.memes is { } memes)
			SetXenotypeForMemes(memes, ref request);
	}

	private static void SetXenotypeForMemes(List<MemeDef> memes, ref PawnGenerationRequest request)
	{
		for (var j = 0; j < memes.Count; j++)
		{
			if (TryGetCustomXenotypeByWeight<MemeDef>(memes[j].defName, out var memeXenotype))
				request.ForcedCustomXenotype = memeXenotype;
		}
	}

	private static void SetXenotypeForPawnKind(PawnKindDef pawnKindDef, ref PawnGenerationRequest request)
	{
		if (TryGetCustomXenotypeByWeight<PawnKindDef>(pawnKindDef.defName, out var pawnKindXenotype))
			request.ForcedCustomXenotype = pawnKindXenotype;
	}

	private static bool TryGetCustomXenotypeByWeight<T>(string defName, [NotNullWhen(true)] out CustomXenotype? result)
		where T : Def
	{
		var xenotypeChances = XenotypeChanceDatabase<T>.For(defName);

		if (Rand.Range(0f, 1f) > xenotypeChances.GetBaselinerChance()
			&& xenotypeChances.AllActiveXenotypes.Count > 0)
		{
			xenotypeChances.CustomXenotypes.TryRandomElementByWeight(tuple => tuple.Chance.RawValue, out var randomResult);
			if (randomResult.Xenotype?.Def != XenotypeDefOf.Baseliner && randomResult.Xenotype?.CustomXenotype != null)
			{
				result = randomResult.Xenotype is ModifiableXenotype.Generated xenotypeGenerator
					? xenotypeGenerator.GenerateXenotype(xenotypeChances)
					: randomResult.Xenotype.CustomXenotype;

				return true;
			}
		}

		result = null;
		return false;
	}
}