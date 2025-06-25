// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using JetBrains.Annotations;

namespace XenotypeSpawnControl;

[StaticConstructorOnStartup]
[UsedImplicitly]
public static class StaticConstructor
{
	static StaticConstructor()
		=> XenotypeChanceDatabases.AssignXenotypeChancesFromSettings();
}