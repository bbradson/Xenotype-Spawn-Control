// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

namespace XenotypeSpawnControl;

public static class Strings
{
	public static string HybridKey = "::Generated::Hybrid";
	public static string RandomGenesKey = "::Generated::Random";
	public static string NoFactionKey = "::Generated::NoFaction";
	public static string ConfirmDelete = "ConfirmDelete";

	public static class Translated
	{
		// Vanilla Translated strings:
		public static string Factions = "Factions";
		public static string Memes = "Memes";
		public static string Hybrid = "Hybrid";
		public static string Reset = "Reset";
		public static string ResetAll = "ResetAll";

		// Keys added by this mod:
		public static string PawnKinds = "PawnKinds";
		public static string Editor = "Editor";
		public static string NoFaction = "NoFaction";
		public static string HybridTooltip = "HybridTooltip";
		public static string RandomGenes = "RandomGenes";
		public static string RandomGenesTooltip = "RandomGenesTooltip";
		public static string MakeYourOwn = "MakeYourOwn";
		public static string Inactive = "INACTIVE";
		public static string Edit = "edit";
		public static string ConfirmReset = "ConfirmResetXenotypeSpawnControl";

		public static string AllowArchiteXenotypes = "AllowArchiteXenotypes";

		static Translated()
			=> TranslateAllFields(typeof(Translated));

		private static void TranslateAllFields(Type type)
		{
			foreach (var field in type.GetFields(AccessTools.allDeclared))
			{
				if (field.FieldType == typeof(string))
					field.SetValue(null, ((string)field.GetValue(null)).TranslateSimple());
			}
		}
	}
}