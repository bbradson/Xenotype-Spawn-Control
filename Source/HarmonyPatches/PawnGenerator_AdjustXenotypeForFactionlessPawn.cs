// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

namespace XenotypeSpawnControl.HarmonyPatches;

[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.AdjustXenotypeForFactionlessPawn))]
public static class PawnGenerator_AdjustXenotypeForFactionlessPawn
{
	[HarmonyPrefix]
	[HarmonyPriority(Priority.VeryLow)]
	public static bool Prefix(Pawn pawn, ref PawnGenerationRequest request, ref XenotypeDef xenotype)
	{
		var xenotypeChances = XenotypeChanceDatabase<FactionDef>.For(EmptyFactionDef.Instance.defName);

		var premadeXenotypeChanceSum = DefDatabase<XenotypeDef>.AllDefsListForReading.Sum(xenotypeDef
			=> DefWeightSelector(xenotypeDef, xenotypeChances));

		var customXenotypeChanceSum = xenotypeChances.GetCustomXenotypeChanceSum() * 2f;

		if (Rand.Range(0f, premadeXenotypeChanceSum + customXenotypeChanceSum) <= customXenotypeChanceSum
			&& xenotypeChances.CustomXenotypes.TryRandomElementByWeight(tuple
				=> tuple.Chance.RawValue, out var result))
		{
			request.ForcedCustomXenotype = result.Xenotype.CustomXenotype;
			xenotype = XenotypeDefOf.Baseliner;
		}
		else if (DefDatabase<XenotypeDef>.AllDefs.TryRandomElementByWeight(xenotypeDef
			=> DefWeightSelector(xenotypeDef, xenotypeChances), out var result2))
		{
			xenotype = result2;
		}

		return false;
	}

	private static float DefWeightSelector(XenotypeDef xenotypeDef, XenotypeChances<FactionDef> xenotypeChances)
		=> xenotypeDef == XenotypeDefOf.Baseliner ? xenotypeChances.GetBaselinerChance() : xenotypeDef.factionlessGenerationWeight;
}