// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

namespace XenotypeSpawnControl.GUIExtensions;

public struct ScrollViewScope : IDisposable
{
	public ScrollViewScope(Rect outRect, ref Vector2 scrollPosition, Rect viewRect, bool showScrollbars = true)
		=> Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect, showScrollbars);

	public void Dispose() => Widgets.EndScrollView();
}