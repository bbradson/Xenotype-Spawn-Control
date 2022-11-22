// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

namespace XenotypeSpawnControl;

public partial class ModifiableXenotype
{
	public class Random : Generated
	{
		public override string Label => Strings.Translated.RandomGenes;
		public override string? Tooltip => Strings.Translated.RandomGenesTooltip;

		public override CustomXenotype GenerateXenotype<T>(XenotypeChances<T> xenotypeChances)
		{
			CustomXenotype!.inheritable = Rand.Bool;

			var genes = CustomXenotype.genes;

			genes.Clear();
			var geneSetCount = Rand.Range(3, 14);

			AddRandomGeneSetGenes(genes, geneSetCount);

			genes.RemoveDuplicates();
			genes.RemoveAll(gene => gene.prerequisite != null && !genes.Contains(gene.prerequisite));

			var metabolism = genes.CalculateMetabolism();
			if (!GeneTuning.BiostatRange.Includes(metabolism))
			{
				var targetMetabolism = Rand.Range(-3, 4);
				var targetGeneCount = Rand.Range(6, 21);

				FixGeneMetabolism(genes, metabolism, targetMetabolism, targetGeneCount);
			}

			CustomXenotype.name = GeneUtility.GenerateXenotypeNameFromGenes(genes);

			return CustomXenotype;
		}

		private static void AddRandomGeneSetGenes(List<GeneDef> genes, int geneSetCount)
		{
			var allGeneDefs = DefDatabase<GeneDef>.AllDefsListForReading;
			var selectionWeights = allGeneDefs.ConvertAll(def => def.selectionWeight);
			var canGenerateInGeneSetFlags = allGeneDefs.ConvertAll(def => def.canGenerateInGeneSet);

			var allGeneDefsCount = allGeneDefs.Count;
			for (var i = 0; i < allGeneDefsCount; i++)
			{
				var gene = allGeneDefs[i];

				gene.canGenerateInGeneSet = true;

				if (gene.selectionWeight < 0.2f)
					gene.selectionWeight = 0.2f;
			}

			for (var i = 0; i < geneSetCount; i++)
				genes.AddRange(GeneUtility.GenerateGeneSet().genes);

			for (var i = 0; i < allGeneDefsCount; i++)
			{
				var gene = allGeneDefs[i];

				gene.canGenerateInGeneSet = canGenerateInGeneSetFlags[i];
				gene.selectionWeight = selectionWeights[i];
			}
		}

		public override float GetDefaultChanceIn<T>(T def)
			=> def.GetModExtension<Extension>()?.randomGenesChance ?? 0f;

		public Random() : base(Strings.RandomGenesKey) { }
	}
}