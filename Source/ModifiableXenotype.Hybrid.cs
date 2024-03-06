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

			var xenotypeSet = xenotypeChances.GetXenotypeSet(xenotypeChances.Def!);
			var papaGenes = GetPapaGenes(xenotypeChances, xenotypeSet, out var papaXenotypeSet, out var papaCustomXenotype);
			var mamaGenes = GetMamaGenes(xenotypeChances, xenotypeSet, papaXenotypeSet, papaCustomXenotype, papaGenes);

			CustomXenotype.genes.Clear();
			AddInheritedGenes(CustomXenotype.genes, mamaGenes, papaGenes);

			CustomXenotype.name = Strings.Translated.Hybrid;

			return CustomXenotype;
		}

		public override float GetDefaultChanceIn<T>(T def)
			=> def.GetModExtension<Extension>()?.hybridChance ?? 0f;

		private static List<GeneDef> GetPapaGenes<T>(XenotypeChances<T> xenotypeChances, XenotypeSet? xenotypeSet, out XenotypeDef? papaXenotypeSet, out CustomXenotype? papaCustomXenotype)
			where T : Def
		{
			papaXenotypeSet
				= xenotypeSet?.xenotypeChances.TryRandomElementByWeight(chance
					=> chance.xenotype == XenotypeDefOf.Baseliner
					? xenotypeChances.GetCustomXenotypeChanceValueSum()
					: chance.chance, out var xenotypeChance) ?? false
				? xenotypeChance.xenotype
				: null;

			papaCustomXenotype
				= (papaXenotypeSet == XenotypeDefOf.Baseliner || papaXenotypeSet == null)
				&& xenotypeChances.CustomXenotypeChances.TryRandomElementByWeight(chance
					=> chance.RawValue, out var customXenotypeChance)
				? customXenotypeChance.Xenotype.CustomXenotype
				: null;

			return papaCustomXenotype?.genes
				?? papaXenotypeSet?.genes
				?? GetFallbackGenesExcluding(null);
		}

		private static List<GeneDef> GetMamaGenes<T>(XenotypeChances<T> xenotypeChances, XenotypeSet? xenotypeSet, XenotypeDef? papaXenotypeSet, CustomXenotype? papaCustomXenotype, List<GeneDef> papaGenes)
			where T : Def
		{
			var mamaXenotypeSet = xenotypeSet?.xenotypeChances.TryRandomElementByWeight(chance
				=> chance.xenotype == XenotypeDefOf.Baseliner ? xenotypeChances.GetCustomXenotypeChanceSumExcluding(papaCustomXenotype)
				: chance.xenotype == papaXenotypeSet ? 0f
				: chance.chance, out var xenotypeChance) ?? false
				? xenotypeChance.xenotype
				: null;

			var mamaCustomXenotype
				= (mamaXenotypeSet == XenotypeDefOf.Baseliner || mamaXenotypeSet == null)
				&& xenotypeChances.CustomXenotypeChances.TryRandomElementByWeight(chance
					=> chance.Xenotype.CustomXenotype == papaCustomXenotype ? 0 : chance.RawValue, out var customXenotypeChance)
				? customXenotypeChance.Xenotype.CustomXenotype
				: null;

			return mamaCustomXenotype?.genes
				?? mamaXenotypeSet?.genes
				?? GetFallbackGenesExcluding(papaGenes);
		}

		private static List<GeneDef> GetFallbackGenesExcluding(List<GeneDef>? genes)
			=> DefDatabase<XenotypeDef>.AllDefs
			.Where(xenotype => xenotype != XenotypeDefOf.Baseliner)
			.Select(def => def.genes)
			.Concat(ModifiableXenotypeDatabase.CustomXenotypes.Values
				.Select(modifiable => modifiable.CustomXenotype!.genes))
			.Where(geneEntry => geneEntry != genes && geneEntry != null)
			.RandomElement();

		public Hybrid() : base(Strings.HybridKey) { }

		public static void AddInheritedGenes(List<GeneDef> targetList, List<GeneDef> mamaGenes, List<GeneDef> papaGenes)
		{
			targetList.AddRange(mamaGenes);
			targetList.AddRange(papaGenes);
			targetList.RemoveDuplicates();
			FixGeneMetabolism(targetList, targetList.CalculateMetabolism(), Rand.Range(-3, 4), allowAdding: false);
		}
	}
}