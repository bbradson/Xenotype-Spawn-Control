// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

namespace XenotypeSpawnControl.HarmonyPatches;

public static class StartingPawnUtility
{
	public const int MAGIC_NUMBER = 736452476; // Don't start pawn generation when passing this specific number as index

	[HarmonyPatch(typeof(Verse.StartingPawnUtility), nameof(Verse.StartingPawnUtility.GetGenerationRequest))]
	public static class GetGenerationRequest
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.First)]
		public static bool Prefix(ref PawnGenerationRequest __result, int index) => index != MAGIC_NUMBER || SetEmptyRequest(ref __result);
		private static bool SetEmptyRequest(ref PawnGenerationRequest __result)
		{
			__result = default;
			return false;
		}
	}

	[HarmonyPatch(typeof(Verse.StartingPawnUtility), nameof(Verse.StartingPawnUtility.SetGenerationRequest))]
	public static class SetGenerationRequest
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.First)]
		public static bool Prefix(int index, in PawnGenerationRequest request)
		{
			if (request.ForcedCustomXenotype != null)
				ModifiableXenotypeDatabase.GetOrAddCustomXenotype(request.ForcedCustomXenotype);

			return index != MAGIC_NUMBER;
		}
	}
}