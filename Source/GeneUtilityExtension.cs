// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

namespace XenotypeSpawnControl;

public static class GeneUtilityExtension
{
	public static bool IsOverriddenByAnotherGeneWithin(this GeneDef gene, List<GeneDef> genes)
	{
		for (var i = 0; i < genes.Count; i++)
		{
			var otherGene = genes[i];
			if (otherGene == gene)
				continue;

			if (otherGene.Overrides(gene, true, true))
				return true;
		}

		return false;
	}

	public static bool OverridesGeneWithin(this GeneDef gene, List<GeneDef> genes)
	{
		for (var i = 0; i < genes.Count; i++)
		{
			var otherGene = genes[i];
			if (otherGene == gene)
				continue;

			if (gene.Overrides(otherGene, true, true))
				return true;
		}

		return false;
	}

	public static int CalculateMetabolism(this List<GeneDef> genes)
	{
		var sum = 0;
		for (var i = 0; i < genes.Count; i++)
		{
			var gene = genes[i];
			if (!gene.IsOverriddenByAnotherGeneWithin(genes))
				sum += gene.biostatMet;
		}

		return sum;
	}
}