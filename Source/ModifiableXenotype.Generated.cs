// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

namespace XenotypeSpawnControl;

public partial class ModifiableXenotype
{
	public abstract class Generated : ModifiableXenotype
	{
		public abstract CustomXenotype GenerateXenotype<T>(XenotypeChances<T> xenotypeChances)
			where T : Def;

		public abstract float GetDefaultChanceIn<T>(T def)
			where T : Def;

		protected static void FixGeneMetabolism(List<GeneDef> genes, int metabolism, int targetMetabolism, int targetGeneCount = 1, bool allowAdding = true)
		{
			var maxAttemptsLeft = 13;
			var shouldRecalculate = false;

			if (!allowAdding || genes.Count > targetGeneCount)
			{
				if (genes.Count <= 1)
					return;

				while (metabolism - targetMetabolism > 2 && maxAttemptsLeft-- > 0)
				{
					var i = Rand.Range(0, genes.Count);
					var geneMetabolism = genes[i].biostatMet;

					if (geneMetabolism > 0)
					{
						if (!genes[i].IsOverriddenByAnotherGeneWithin(genes))
							metabolism -= geneMetabolism;

						if (genes[i].OverridesGeneWithin(genes))
							shouldRecalculate = true;

						genes.RemoveAt(i);
					}
				}
				while (metabolism - targetMetabolism < 2 && maxAttemptsLeft-- > 0)
				{
					var i = Rand.Range(0, genes.Count);
					var geneMetabolism = genes[i].biostatMet;

					if (geneMetabolism < 0)
					{
						if (!genes[i].IsOverriddenByAnotherGeneWithin(genes))
							metabolism -= geneMetabolism;

						if (genes[i].OverridesGeneWithin(genes))
							shouldRecalculate = true;

						genes.RemoveAt(i);
					}
				}
			}
			else
			{
				while (metabolism - targetMetabolism > 2 && maxAttemptsLeft-- > 0)
				{
					var genesToTryAdding = GeneUtility.GenerateGeneSet().genes;
					for (var i = 0; i < genesToTryAdding.Count; i++)
					{
						var gene = genesToTryAdding[i];
						if (gene.biostatArc != 0)
							continue;

						var geneMetabolism = gene.biostatMet;
						if (geneMetabolism > 0)
						{
							genes.Add(gene);

							if (!gene.IsOverriddenByAnotherGeneWithin(genes))
								metabolism += geneMetabolism;

							if (gene.OverridesGeneWithin(genes))
								shouldRecalculate = true;

							if (metabolism - targetMetabolism > 2)
								break;
						}
					}
				}
				while (metabolism - targetMetabolism < 2 && maxAttemptsLeft-- > 0)
				{
					var genesToTryAdding = GeneUtility.GenerateGeneSet().genes;
					for (var i = 0; i < genesToTryAdding.Count; i++)
					{
						var gene = genesToTryAdding[i];
						if (gene.biostatArc != 0)
							continue;

						var geneMetabolism = gene.biostatMet;
						if (geneMetabolism < 0)
						{
							genes.Add(gene);

							if (!gene.IsOverriddenByAnotherGeneWithin(genes))
								metabolism += geneMetabolism;

							if (gene.OverridesGeneWithin(genes))
								shouldRecalculate = true;

							if (metabolism - targetMetabolism < 2)
								break;
						}
					}
				}
			}

			//if (maxAttemptsLeft <= 0)
			//	Log.Warning($"FixGeneMetabolism failed to finish within 13 attempts");

			while (genes.FindIndex(gene => gene.prerequisite != null && !genes.Contains(gene.prerequisite))
				is var invalidGeneIndex
				&& invalidGeneIndex >= 0)
			{
				if (!genes[invalidGeneIndex].IsOverriddenByAnotherGeneWithin(genes))
					metabolism -= genes[invalidGeneIndex].biostatMet;

				if (genes[invalidGeneIndex].OverridesGeneWithin(genes))
					shouldRecalculate = true;

				genes.RemoveAt(invalidGeneIndex);
			}

			// gene overrides could change, so recalculate
			if (shouldRecalculate)
				metabolism = genes.CalculateMetabolism();

			if (/*(allowAdding && genes.Count is < 5 or > 21) ||*/ !GeneTuning.BiostatRange.Includes(metabolism))
				FixGeneMetabolism(genes, metabolism, targetMetabolism, targetGeneCount);
		}

		public Generated(string name) : base(new CustomXenotype { name = name }) { }
	}
}