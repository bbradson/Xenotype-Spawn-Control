// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using JetBrains.Annotations;

namespace XenotypeSpawnControl.HarmonyPatches;

[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.AdjustXenotypeForFactionlessPawn))]
// ReSharper disable once InconsistentNaming
public static class PawnGenerator_AdjustXenotypeForFactionlessPawn
{
	[HarmonyPrefix]
	[HarmonyPriority(Priority.VeryLow)]
	[UsedImplicitly]
	public static bool Prefix(Pawn pawn, ref PawnGenerationRequest request, ref XenotypeDef xenotype)
	{
		var xenotypeChances = XenotypeChanceDatabase<FactionDef>.For(EmptyFactionDef.Instance.defName);

		if (Rand.Range(0f, 1f) <= xenotypeChances.GetCustomXenotypeChanceValueSum()
			&& xenotypeChances.CustomXenotypeChances.TryRandomElementByWeight(static chance
				=> chance.RawValue, out var result))
		{
			request.ForcedCustomXenotype = result.Xenotype is ModifiableXenotype.Generated xenotypeGenerator
				? xenotypeGenerator.GenerateXenotype(xenotypeChances)
				: result.Xenotype.CustomXenotype;
			xenotype = XenotypeDefOf.Baseliner;
		}
		else if (DefDatabase<XenotypeDef>.AllDefs.TryRandomElementByWeight(static xenotypeDef
			=> xenotypeDef.factionlessGenerationWeight, out var result2))
		{
			xenotype = result2;
		}

		return false;
	}
}