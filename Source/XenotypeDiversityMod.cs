// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

global using System;
global using System.Collections.Generic;
global using System.Diagnostics.CodeAnalysis;
global using HarmonyLib;
global using RimWorld;
global using UnityEngine;
global using Verse;
using System.Diagnostics;
using System.Security;

[assembly: AllowPartiallyTrustedCallers]
[assembly: SecurityTransparent]
[assembly: SecurityRules(SecurityRuleSet.Level2, SkipVerificationInFullTrust = true)]
[assembly: Debuggable(false, false)]

namespace XenotypeSpawnControl;

public class XenotypeDiversityMod : Mod
{
	public XenotypeDiversityMod(ModContentPack content) : base(content)
	{
		_harmony = new(content.PackageId);
		_settings = GetSettings<ModSettings>();
		_content = content;
		_harmony.PatchAll();
	}

	public override string SettingsCategory() => Content.Name;

	public override void DoSettingsWindowContents(Rect inRect) => ModSettingsWindow.DoWindowContents(inRect);

	public static Harmony Harmony => _harmony!;
	private static Harmony? _harmony;
	public static ModSettings Settings => _settings!;
	private static ModSettings? _settings;

	public new static ModContentPack Content => _content!;
	private static ModContentPack? _content;
}