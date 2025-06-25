// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using System.Linq;

namespace XenotypeSpawnControl;

public partial class ModifiableXenotype
{
	public class Hybrid() : Generated(Strings.HybridKey)
	{
		public override string DisplayLabel => Strings.Translated.Hybrid.CapitalizeFirst();
		
		public override string Tooltip => Strings.Translated.HybridTooltip;

		public override CustomXenotype GenerateXenotype<T>(XenotypeChances<T> xenotypeChances)
		{
			CustomXenotype!.inheritable = Rand.Bool;

			var papaGenes = GetParentGenes(xenotypeChances, out var papaXenotype);
			var mamaGenes = GetParentGenes(xenotypeChances, out _, papaXenotype);

			CustomXenotype.genes.Clear();
			AddInheritedGenes(CustomXenotype.genes, mamaGenes, papaGenes);

			CustomXenotype.name = Strings.Translated.Hybrid;

			return CustomXenotype;
		}

		public override float GetDefaultChanceIn<T>(T def) => def.GetModExtension<Extension>()?.hybridChance ?? 0f;

		private static List<GeneDef> GetParentGenes<T>(XenotypeChances<T> xenotypeChances,
			out ModifiableXenotype? parentXenotype, ModifiableXenotype? otherParentXenotype = null)
			where T : Def
		{
			parentXenotype
				= xenotypeChances.AllAllowedXenotypeChances
					.Where(xenoChance => IsValidParentXenotype(xenoChance.Xenotype, otherParentXenotype))
					.TryRandomElementByWeight(static chance
						=> chance.RawValue, out var xenotypeChance)
					? xenotypeChance.Xenotype
					: null;
			
			parentXenotype ??= GetFallbackXenotype(xenotypeChances, otherParentXenotype);

			return GetGenesForXenotype(xenotypeChances, parentXenotype);
		}

		private static List<GeneDef> GetGenesForXenotype<T>(XenotypeChances<T> xenotypeChances,
			ModifiableXenotype? parentXenotype) where T : Def
		{
			List<GeneDef>? parentGenes = null;
			if (parentXenotype is not null)
			{
				if (parentXenotype is Generated generatedParent)
				{
					if (generatedParent is not Hybrid)
						parentGenes = generatedParent.GenerateXenotype(xenotypeChances).genes;
				}
				else
				{
					parentGenes = parentXenotype.XenotypeGenes;
				}
			}

			return parentGenes ?? throw new($"Failed to get genes for {parentXenotype}");
		}

		private static ModifiableXenotype GetFallbackXenotype<T>(XenotypeChances<T> xenotypeChances,
			ModifiableXenotype? excludedXenotype) where T : Def
		{
			var parentSource = xenotypeChances.AllAllowedXenotypeChances
				.Select(static xenoChance => xenoChance.Xenotype)
				.Where(xenotype => IsValidParentXenotype(xenotype, excludedXenotype))
				.ToList();
			
			// if not enough xenotypes allowed check all
			if (!parentSource.Any())
			{
				parentSource = ModifiableXenotypeDatabase.AllValues.Values
					.Where(xenotype => IsValidParentXenotype(xenotype, excludedXenotype))
					.ToList();
			}

			return parentSource.RandomElement();
		}

		private static bool IsValidParentXenotype(ModifiableXenotype checkXenotype,
			ModifiableXenotype? excludedXenotype)
			=> checkXenotype is not Hybrid
				&& checkXenotype.Def != XenotypeDefOf.Baseliner
				&& checkXenotype != excludedXenotype;

		public static void AddInheritedGenes(List<GeneDef> targetList, IEnumerable<GeneDef> mamaGenes,
			IEnumerable<GeneDef> papaGenes)
		{
			targetList.AddRange(mamaGenes);
			targetList.AddRange(papaGenes);
			targetList.RemoveDuplicates();
			FixGeneMetabolism(targetList, targetList.CalculateMetabolism(),
				Rand.Range(-3, 4), allowAdding: false);
		}
	}
}