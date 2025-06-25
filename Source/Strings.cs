// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

// ReSharper disable UnassignedField.Global
// ReSharper disable UnassignedReadonlyField
namespace XenotypeSpawnControl;

public static class Strings
{
	public static readonly string
		HybridKey = "::Generated::Hybrid",
		RandomGenesKey = "::Generated::Random",
		NoFactionKey = "::Generated::NoFaction",
		ConfirmDelete = "ConfirmDelete";

	public static class Translated
	{
		public static readonly string
			
			// Vanilla Translated strings:
			Factions,
			Memes,
			Hybrid,
			Reset,
			ResetAll,
			
			// Keys added by this mod:
			PawnKinds = "bs.xsc.PawnKinds",
			Editor = "bs.xsc.Editor",
			NoFaction = "bs.xsc.NoFaction",
			HybridTooltip = "bs.xsc.HybridTooltip",
			RandomGenes = "bs.xsc.RandomGenes",
			RandomGenesTooltip = "bs.xsc.RandomGenesTooltip",
			MakeYourOwn = "bs.xsc.MakeYourOwn",
			Inactive = "bs.xsc.INACTIVE",
			Edit = "bs.xsc.Edit",
			Apply = "bs.xsc.Apply",
			ConfirmReset = "bs.xsc.ConfirmResetXenotypeSpawnControl",
			Templates = "bs.xsc.Templates",
			NoTemplateHint = "bs.xsc.NoTemplateHint",
			AllowArchiteXenotypes = "bs.xsc.AllowArchiteXenotypes",
			AbsoluteToggle = "bs.xsc.AbsoluteToggle",
			WeightedToggle = "bs.xsc.WeightedToggle",
			WeightSetterButton = "bs.xsc.WeightSetterButton",
			IndividualAbsoluteModeToggleTooltip = "bs.xsc.IndividualAbsoluteModeToggleTooltip",
			Weight = "bs.xsc.Weight";

#pragma warning disable CS8618
		static Translated() => TranslateAllFields(typeof(Translated));
#pragma warning restore CS8618

		private static void TranslateAllFields(Type type)
		{
			foreach (var field in type.GetFields(AccessTools.allDeclared))
			{
				if (field.FieldType == typeof(string))
				{
					field.SetValue(null,
						(field.GetValue(null) is string value && !string.IsNullOrEmpty(value) ? value : field.Name)
						.TranslateSimple());
				}
			}
		}
	}
}