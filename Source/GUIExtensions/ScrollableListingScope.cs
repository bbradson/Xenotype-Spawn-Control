// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

namespace XenotypeSpawnControl.GUIExtensions;

public readonly struct ScrollableListingScope : IDisposable
{
	public Listing_Standard Listing { get; }

	private readonly ScrollViewStatus _scrollViewStatus;

	public ScrollableListingScope(Rect outRect, ScrollViewStatus scrollViewStatus, Listing_Standard? listing = null,
		bool showScrollbars = true)
	{
		_scrollViewStatus = scrollViewStatus;
		var viewRect = outRect with { width = outRect.width - 20f, height = _scrollViewStatus.Height };

		Listing = listing ?? new();
		Widgets.BeginScrollView(outRect, ref scrollViewStatus.Position, viewRect, showScrollbars);

		viewRect.height = float.PositiveInfinity;
		Listing.Begin(viewRect);
	}

	public void Dispose()
	{
		_scrollViewStatus.Height = Listing.CurHeight + 12f;
		Listing.End();
		Widgets.EndScrollView();
	}
}
