// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using System.Linq;

namespace XenotypeSpawnControl;

public partial class ModifiableXenotype
{
	public string Name { get; }
	public virtual string DisplayLabel => (Def != null ? Def.LabelCap : Name) + " (" + (Def is not null || this is Generated ? Name : Strings.Translated.CustomHint) + ")";
	public virtual string? Tooltip => Def?.descriptionShort;
	public XenotypeDef? Def { get; }
	public CustomXenotype? CustomXenotype { get; }

	public IEnumerable<GeneDef> xenotypeGenes;

	public bool IsArchite => xenotypeGenes.Any(gene => gene.biostatArc > 0);

	public ModifiableXenotype(XenotypeDef def)
	{
		Def = def;
		Name = def.defName;
		xenotypeGenes = def.AllGenes;
	}

	public ModifiableXenotype(CustomXenotype customXenotype)
	{
		CustomXenotype = customXenotype;
		Name = customXenotype.name;
		xenotypeGenes = CustomXenotype.genes;
	}
}