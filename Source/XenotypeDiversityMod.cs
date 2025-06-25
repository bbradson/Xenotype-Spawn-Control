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
using System.Security;
using JetBrains.Annotations;

[assembly: AllowPartiallyTrustedCallers]
[assembly: SecurityTransparent]
[assembly: SecurityRules(SecurityRuleSet.Level2, SkipVerificationInFullTrust = true)]

namespace XenotypeSpawnControl;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class XenotypeDiversityMod : Mod
{
	public XenotypeDiversityMod(ModContentPack content) : base(content)
	{
		Harmony = new(content.PackageId);
		Settings = GetSettings<ModSettings>();
		Content = content;
		Harmony.PatchAll();
	}

	public override string SettingsCategory() => Content.Name;

	public override void DoSettingsWindowContents(Rect inRect) => ModSettingsWindow.DoWindowContents(inRect);

	public static Harmony Harmony { get; private set; } = null!;
	public static ModSettings Settings { get; private set; } = null!;
	public new static ModContentPack Content { get; private set; } = null!;
}