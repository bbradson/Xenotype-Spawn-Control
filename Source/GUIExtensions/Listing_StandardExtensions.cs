// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using System.Globalization;
using Verse.Sound;

namespace XenotypeSpawnControl.GUIExtensions;
public static class Listing_StandardExtensions
{
	public static (int ChanceValue, string ChanceString) FloatBoxSlider(this Listing_Standard listing, string label, string currentString, float currentValue, string? tooltip = null)
	{
		//remember previous text alignment
		var previousAlignment = Text.Anchor;
		Text.Anchor = TextAnchor.MiddleRight;

		var percentageSize = Text.CalcSize("%");
		//draw percentage Label
		Widgets.Label(new(0f, listing.curY, listing.ColumnWidth, percentageSize.y), "%");
		//draw percentage string
		var textFieldSize = Text.CalcSize("77.77)");
		var textFieldResult = Widgets.TextField(new(listing.ColumnWidth - textFieldSize.x - percentageSize.x, listing.curY, textFieldSize.x, textFieldSize.y), currentString);
		// sanitize entered percentage
		if (textFieldResult.NullOrEmpty() || textFieldResult == ".")
		{
			currentValue = 0f;
			currentString = textFieldResult;
			
		}
		else if (float.TryParse(textFieldResult, NumberStyles.Float, CultureInfo.InvariantCulture, out var enteredValue))
		{
			if (enteredValue < 0)
			{
				// TODO: maybe dynamic string
				currentValue = 0f;
				currentString = "0";
			}
			else if (enteredValue >= 100)
			{
				currentValue = 1f;
				currentString = "100";
			}
			else
			{
				// truncate string if it contains a decimal and decimal is longer than one digit
				var decimalPointIndex = textFieldResult.IndexOf('.');
				if (decimalPointIndex != -1 && decimalPointIndex < textFieldResult.Length - 1)
				{
					currentString = textFieldResult.Substring(0, decimalPointIndex + 2);
					currentValue = float.Parse(currentString, CultureInfo.InvariantCulture) / 100f;
				}
				else
				{
					currentValue = enteredValue / 100f;
					currentString = textFieldResult;
				}
			}
		}

		//draw xenotype label
		Text.Anchor = previousAlignment;
		listing.Label(label, tooltip: tooltip);
		
		return (Mathf.RoundToInt(listing.Slider(currentValue * 1000, 0, 1000)), currentString);
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