// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

namespace XenotypeSpawnControl;

[StaticConstructorOnStartup]
public static class StaticConstructor
{
	static StaticConstructor()
		=> XenotypeChanceDatabases.AssignXenotypeChancesFromSettings();
}