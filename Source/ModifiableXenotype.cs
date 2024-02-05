// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

namespace XenotypeSpawnControl;

public partial class ModifiableXenotype
{
	public string Name { get; }
	public virtual string Label => Def != null ? Def.LabelCap : Name.CapitalizeFirst();
	public virtual string? Tooltip => Def?.descriptionShort;
	public XenotypeDef? Def { get; }
	public CustomXenotype? CustomXenotype { get; }

	public ModifiableXenotype(XenotypeDef def)
	{
		Def = def;
		Name = def.defName;
	}

	public ModifiableXenotype(CustomXenotype customXenotype)
	{
		CustomXenotype = customXenotype;
		Name = customXenotype.name;
	}
}