// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using System.Globalization;
using Verse.Sound;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace XenotypeSpawnControl.GUIExtensions;

// ReSharper disable once InconsistentNaming
public static class Listing_StandardExtensions
{
	public static void XenotypeChanceDisplay<T>(this Listing_Standard listing, XenotypeChances<T> xenotypeChancesSet,
		XenotypeChance xenotypeChance, bool showName) where T : Def
	{
		var isAbsolute = xenotypeChance.IsAbsolute;
		(float Value, string String) absoluteChance = (xenotypeChance.Value, xenotypeChance.ChanceString);
		(float Value, string String) weightedChance = (xenotypeChance.Weight, xenotypeChance.WeightString);
		
		// remember previous text alignment
		var previousAlignment = Text.Anchor;
		Text.Anchor = TextAnchor.MiddleRight;

		if (isAbsolute)
		{
			var percentageSize = Text.CalcSize("%");
			
			// draw percentage Label
			Widgets.Label(new(0f, listing.curY, listing.ColumnWidth, percentageSize.y), "%");
			
			// draw percentage string
			var textFieldSize = Text.CalcSize("77.77)");
			var textFieldResult
				= Widgets.TextField(
					new(listing.ColumnWidth - textFieldSize.x - percentageSize.x, listing.curY, textFieldSize.x,
						textFieldSize.y), absoluteChance.String);
			
			// sanitize entered percentage
			if (textFieldResult.NullOrEmpty() || textFieldResult == ".")
			{
				absoluteChance.Value = 0f;
				absoluteChance.String = textFieldResult;
			}
			else if (float.TryParse(textFieldResult, NumberStyles.Float, CultureInfo.InvariantCulture,
				out var enteredValue))
			{
				switch (enteredValue)
				{
					case < 0f:
						absoluteChance.Value = 0f;
						absoluteChance.String = "0";
						break;
					
					case >= 100f:
						absoluteChance.Value = 1f;
						absoluteChance.String = "100";
						break;
					
					default:
					{
						// truncate string if it contains a decimal and decimal is longer than one digit
						var decimalPointIndex = textFieldResult.IndexOf('.');
						if (decimalPointIndex != -1 && decimalPointIndex < textFieldResult.Length - 1)
						{
							absoluteChance.String = textFieldResult[..(decimalPointIndex + 2)];
							absoluteChance.Value = float.Parse(absoluteChance.String, CultureInfo.InvariantCulture) / 100f;
						}
						else
						{
							absoluteChance.Value = enteredValue / 100f;
							absoluteChance.String = textFieldResult;
						}

						break;
					}
				}
			}
		}
		else
		{
			// draw percentage Label
			var percentageSize = Text.CalcSize("|77.7%");
			Widgets.Label(new(0f, listing.curY, listing.ColumnWidth, percentageSize.y), absoluteChance.String + "%");
			
			// draw weight text box
			var textFieldSize = Text.CalcSize("77.77)");
			textFieldSize.x = Math.Max(textFieldSize.x, Text.CalcSize(weightedChance.String + ")").x);
			var textFieldResult
				= Widgets.TextField(
					new(listing.ColumnWidth - textFieldSize.x - percentageSize.x, listing.curY, textFieldSize.x,
						textFieldSize.y), weightedChance.String);
			
			// sanitize entered value
			InterpretWeightString(ref weightedChance, textFieldResult);
		}

		// draw xenotype label
		Text.Anchor = previousAlignment;
		var xenotype = xenotypeChance.Xenotype;
		var label = xenotype.DisplayLabel;
		if (showName)
			label += $" ({xenotype.Name})";
		
		listing.Label(label, tooltip: xenotype.Tooltip);

		// draw a slider with a checkbox to switch chance mode next to it
		var checkBoxSize = Widgets.CheckboxSize;
		var previousColumnWidth = listing.ColumnWidth;
		listing.ColumnWidth = checkBoxSize;
		listing.curX += previousColumnWidth - checkBoxSize;
		
		// cannot create checkbox as widget, or we wouldn't be able to have a tooltip
		listing.CheckboxLabeled(string.Empty, ref isAbsolute, Strings.Translated.IndividualAbsoluteModeToggleTooltip);
		listing.curX -= previousColumnWidth - checkBoxSize;
		listing.ColumnWidth = previousColumnWidth;

		listing.curY -= checkBoxSize;
		listing.ColumnWidth -= checkBoxSize;
		
		var sliderResult = listing.Slider(absoluteChance.Value, 0, 1);
		if (isAbsolute)
			absoluteChance.Value = sliderResult;
		
		listing.ColumnWidth += checkBoxSize;
		listing.Gap(2);

		// apply changes
		var isAbsoluteModified = xenotypeChance.IsAbsolute != isAbsolute;
		var chanceValueModified = isAbsolute && xenotypeChance.Value != absoluteChance.Value;
		var chanceStringModified = xenotypeChance.ChanceString != absoluteChance.String;
		var weightModified = !isAbsolute && xenotypeChance.Weight != weightedChance.Value;
		var weightStringModified = xenotypeChance.WeightString != weightedChance.String;
		
		if (isAbsoluteModified)
			xenotypeChancesSet.SetIsAbsoluteForXenotypeChance(xenotypeChance, isAbsolute);
		
		if (chanceValueModified)
			xenotypeChance.Value = absoluteChance.Value;
		
		if (weightModified)
			xenotypeChance.Weight = weightedChance.Value;
		
		if (chanceValueModified || weightModified)
			xenotypeChancesSet.SetXenotypeChance(xenotypeChance);
		
		if (chanceStringModified)
			xenotypeChance.ChanceString = absoluteChance.String;
		
		if (weightStringModified)
			xenotypeChance.WeightString = weightedChance.String;
	}

	// TODO: move this to an appropriate place and make it look nice
	public static void InterpretWeightString(ref (float Value, string String) weight, string text)
	{
		if (text.NullOrEmpty() || text == ".")
		{
			weight.Value = 0f;
			weight.String = text;
		}
		else if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var enteredValue))
		{
			if (enteredValue < 0f)
			{
				weight.Value = 0f;
				weight.String = "0";
			}
			else
			{
				// do not truncate weights, might cause issues
				weight.Value = enteredValue;
				weight.String = text;
			}
		}
	}

	public static bool SelectionButton(this Listing_Standard listing, string label, bool active, string? tooltip = null)
	{
		//remember to remove text contraction from column width and add downwards margin to get accurate sized rect
		var rect = listing.GetRect(Text.CalcHeight(label, listing.ColumnWidth - 33f) + 15f);
		
		//contract the rect a bit so they aren't pressed flush against the borders
		rect = rect.ContractedBy(4f);
		
		if (Mouse.IsOver(rect))
			TooltipHandler.TipRegion(rect, tooltip);
		
		Widgets.DrawOptionBackground(rect, active);
		
		if (Widgets.ButtonInvisible(rect))
		{
			SoundDefOf.Click.PlayOneShotOnCamera();
			active = true;
		}

		//leave some space around the text
		rect.xMin += 20f;
		rect.xMax -= 5f;
		Widgets.Label(rect, label);
		
		return active;
	}

	public static (bool Clicked, string Text) TextBoxButton(this Listing_Standard listing, string label, string text)
	{
		var textFieldSize = Text.CalcSize("77.77)");
		textFieldSize.x = Math.Max(textFieldSize.x, Text.CalcSize(text + ")").x);

		var listingInitialY = listing.curY;

		// add a small margin as well
		var buttonMargin = 2;
		listing.ColumnWidth -= textFieldSize.x + buttonMargin;

		var buttonResult = listing.ButtonText(label);
		listing.ColumnWidth += textFieldSize.x + buttonMargin;

		// subtract the empty space below the button, the CloseButtonMargin might not be the correct button margin, but it works
		var buttonSize = listing.curY - listingInitialY - Widgets.CloseButtonMargin;

		textFieldSize.y = buttonSize;
		text = Widgets.TextField(
			new(listing.ColumnWidth - textFieldSize.x, listingInitialY, textFieldSize.x, textFieldSize.y), text);

		return (buttonResult, text);
	}
}