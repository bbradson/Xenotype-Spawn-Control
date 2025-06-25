// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

namespace XenotypeSpawnControl.GUIExtensions;

public readonly struct ListingScope : IDisposable
{
	public Listing_Standard Listing { get; }

	public ListingScope(Rect rect, Listing_Standard? listing = null)
	{
		Listing = listing ?? new();
		Listing.Begin(rect);
	}

	public void Dispose() => Listing.End();
}
