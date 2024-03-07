// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using System.Linq;

namespace XenotypeSpawnControl;

public partial class ModifiableXenotype
{
	public class Hybrid : Generated
	{
		public override string DisplayLabel => Strings.Translated.Hybrid.CapitalizeFirst();
		public override string? Tooltip => Strings.Translated.HybridTooltip;

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

		public override float GetDefaultChanceIn<T>(T def)
			=> def.GetModExtension<Extension>()?.hybridChance ?? 0f;

		private static IEnumerable<GeneDef> GetParentGenes<T>(XenotypeChances<T> xenotypeChances, out ModifiableXenotype? parentXenotype, ModifiableXenotype? otherParentXenotype = null)
			where T : Def
		{
			parentXenotype
				= xenotypeChances.AllAllowedXenotypeChances.Where(xenoChance => xenoChance.Xenotype.Def != XenotypeDefOf.Baseliner && xenoChance.Xenotype != otherParentXenotype).TryRandomElementByWeight(chance
					=> chance.RawValue, out var xenotypeChance)
				? xenotypeChance.Xenotype
				: null;

			IEnumerable<GeneDef> parentGenes = null;
			if (parentXenotype is not null)
			{
				if (parentXenotype is Generated generatedParent)
				{
					if (generatedParent is not Hybrid)
						parentGenes = generatedParent.GenerateXenotype(xenotypeChances).genes;
				}
				else
				{
					parentGenes = parentXenotype.xenotypeGenes;
				}
			}
			return parentGenes
				?? GetFallbackGenesExcluding(otherParentXenotype?.Def);
		}

		private static List<GeneDef> GetFallbackGenesExcluding(XenotypeDef? excludedDef)
			=> DefDatabase<XenotypeDef>.AllDefs
			.Where(xenotype => xenotype != XenotypeDefOf.Baseliner && xenotype != excludedDef)
			.Select(def => def.genes)
			.Concat(ModifiableXenotypeDatabase.CustomXenotypes.Values
				.Select(modifiable => modifiable.CustomXenotype!.genes))
			.Where(geneEntry => geneEntry != null)
			.RandomElement();

		public Hybrid() : base(Strings.HybridKey) { }

		public static void AddInheritedGenes(List<GeneDef> targetList, IEnumerable<GeneDef> mamaGenes, IEnumerable<GeneDef> papaGenes)
		{
			targetList.AddRange(mamaGenes);
			targetList.AddRange(papaGenes);
			targetList.RemoveDuplicates();
			FixGeneMetabolism(targetList, targetList.CalculateMetabolism(), Rand.Range(-3, 4), allowAdding: false);
		}
	}
}