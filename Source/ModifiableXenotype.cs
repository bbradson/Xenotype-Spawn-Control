// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

namespace XenotypeSpawnControl;

public partial class ModifiableXenotype
{
	public string Name { get; }

	public virtual string DisplayLabel => Def?.LabelCap.Resolve() is not [_, ..] label ? Name : label;

	public virtual string? Tooltip => Def?.descriptionShort;
	
	public XenotypeDef? Def { get; }
	
	public CustomXenotype? CustomXenotype { get; }

	public List<GeneDef> XenotypeGenes { get; }

	public bool IsArchite => XenotypeGenes.Any(static gene => gene.biostatArc > 0);

	public ModifiableXenotype(XenotypeDef def)
	{
		Def = def;
		Name = def.defName;
		XenotypeGenes = def.AllGenes;
	}

	public ModifiableXenotype(CustomXenotype customXenotype)
	{
		CustomXenotype = customXenotype;
		Name = customXenotype.name;
		XenotypeGenes = CustomXenotype.genes;
	}
}