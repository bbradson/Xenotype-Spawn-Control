// Copyright (c) 2025 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using System.Runtime.CompilerServices;

namespace XenotypeSpawnControl.HarmonyPatches;

[HarmonyPatch(typeof(NameUseChecker), nameof(NameUseChecker.XenotypeNameIsUsed))]
public static class XenotypeNameIsUsed
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool Prefix() => Current.Game != null;
}
