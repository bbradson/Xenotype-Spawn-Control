// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

namespace XenotypeSpawnControl;

public static class XenotypeSetRefs
{
	public static AccessTools.FieldRef<T, XenotypeSet?> GetForType<T>() where T : Def
		=> AccessTools.FieldRefAccess<T, XenotypeSet?>(nameof(FactionDef.xenotypeSet))
		?? throw new NotImplementedException($"Failed fetching XenotypeSetRef for type {typeof(T)}");
}