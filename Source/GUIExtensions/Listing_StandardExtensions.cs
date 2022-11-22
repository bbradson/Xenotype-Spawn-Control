// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using Verse.Sound;

namespace XenotypeSpawnControl.GUIExtensions;
public static class Listing_StandardExtensions
{
	public static float SliderRounded(this Listing_Standard listing, string label, float currentValue, float min, float max, int digits, string? tooltip = null)
	{
		var previousAlignment = Text.Anchor;
		Text.Anchor = TextAnchor.MiddleRight;
		Widgets.Label(new(0f, listing.curY, listing.ColumnWidth, Text.CalcHeight("99.9%", listing.ColumnWidth)), $"{Math.Round(Convert.ToDecimal(currentValue * 100f), digits - 2)}%");
		Text.Anchor = previousAlignment;
		listing.Label(label, tooltip: tooltip);
		return (float)Math.Round(listing.Slider(currentValue, min, max), digits);
	}

	public static int IntSlider(this Listing_Standard listing, string label, int currentValue, int min, int max, int digits, string? tooltip = null)
	{
		var previousAlignment = Text.Anchor;
		Text.Anchor = TextAnchor.MiddleRight;
		/*Widgets.Label(
			   new(0f, listing.curY, listing.ColumnWidth, Text.CalcHeight("99.9%", listing.ColumnWidth)),
			   digits >= 2 ? $"{currentValue / (decimal)Mathf.RoundToInt((float)Math.Pow(10, digits - 2))}%"
			   : digits != 0 ? $"{currentValue / (decimal)Mathf.RoundToInt((float)Math.Pow(10, digits))}"
			   : $"{currentValue}");*/
		var percentageSize = digits >= 2 ? Text.CalcSize("%") : Vector2.zero;
		var textFieldSize = Text.CalcSize("99.9)");

		if (digits >= 2)
			Widgets.Label(new(0f, listing.curY, listing.ColumnWidth, percentageSize.y), "%");

		if (decimal.TryParse(Widgets.TextField(new(listing.ColumnWidth - textFieldSize.x - percentageSize.x, listing.curY, textFieldSize.x, textFieldSize.y),
			   digits >= 2 ? $"{currentValue / (decimal)Mathf.RoundToInt((float)Math.Pow(10, digits - 2))}"
			   : digits != 0 ? $"{currentValue / (decimal)Mathf.RoundToInt((float)Math.Pow(10, digits))}"
			   : $"{currentValue}"), out var newValue))
		{
			currentValue = (int)(newValue * 10m);
		}

		Text.Anchor = previousAlignment;
		listing.Label(label, tooltip: tooltip);
		return Mathf.RoundToInt(listing.Slider(currentValue, min, max));
	}

	public static bool SelectionButton(this Listing_Standard listing, string label, bool active)
	{
		var rect = listing.GetRect(36f);
		rect = rect.ContractedBy(4f);
		Widgets.DrawOptionBackground(rect, active);
		if (Widgets.ButtonInvisible(rect))
		{
			SoundDefOf.Click.PlayOneShotOnCamera();
			active = true;
		}
		rect.xMin += 20f;
		Widgets.Label(rect, label);
		return active;
	}
}