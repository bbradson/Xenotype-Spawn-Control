// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using System.Linq;
using XenotypeSpawnControl.GUIExtensions;

namespace XenotypeSpawnControl;

// ReSharper disable once InconsistentNaming
public class Dialog_Templates<T> : Window where T : Def
{
	private const float
		ENTRY_HEIGHT = Widgets.RadioButtonSize,
		ENTRY_MARGIN = Widgets.CloseButtonMargin;
	
	private readonly XenotypeChances<T> _xenotypeChances;
	private string _newTemplateName = string.Empty;
	private readonly ScrollViewStatus _scrollViewStatus = new();
	
	public Action? OnClosed { get; set; }
	
	public string? AppliedOrSavedTemplateName { get; private set; }
	
	public Dialog_Templates(XenotypeChances<T> xenotypeChances)
	{
		// setup window properties
		closeOnClickedOutside = true;
		absorbInputAroundWindow = true;
		doCloseX = true;
		doCloseButton = true;
		draggable = true;
		resizeable = true;
		optionalTitle = Strings.Translated.Templates;
		_xenotypeChances = xenotypeChances;
	}

	public override void OnAcceptKeyPressed()
	{
		// try to save the template instead of closing the window
		if (!_newTemplateName.NullOrEmpty())
			SaveTemplate(_newTemplateName);
		
		base.OnAcceptKeyPressed();
	}

	public override void DoWindowContents(Rect inRect)
	{
		// do not draw over the close button and leave a small margin above it
		inRect.height -= CloseButSize.y + ENTRY_MARGIN;
		using var scrollScope = new ScrollableListingScope(inRect, _scrollViewStatus);
		var listing = scrollScope.Listing;

		foreach (var templateKeyValuePair in XenotypeChanceDatabases.Templates.ToArray())
		{
			var entryRect = listing.GetRect(ENTRY_HEIGHT);
			entryRect.SplitVertically(entryRect.width - ENTRY_HEIGHT, out entryRect, out var deleteIconButtonRect);

			if (Widgets.ButtonText(entryRect, templateKeyValuePair.Key))
			{
				_xenotypeChances.ApplyTemplate(templateKeyValuePair.Value);
				AppliedOrSavedTemplateName = templateKeyValuePair.Key;
				Close();
			}
			
			if (Widgets.ButtonImage(deleteIconButtonRect,
#if V1_4
				TexButton.DeleteX
#else
				TexButton.Delete
#endif
			))
			{
				XenotypeChanceDatabases.Templates.Remove(templateKeyValuePair.Key);
			}

			listing.Gap(ENTRY_MARGIN);
		}
		
		// only allow alphanumerical strings
		var rect = listing.GetRect(ENTRY_HEIGHT);
		rect.SplitVertically(rect.width - ENTRY_HEIGHT, out var textRect, out var buttonRect);
		_newTemplateName = Widgets.TextField(textRect, _newTemplateName) ?? string.Empty;
		_newTemplateName = new(_newTemplateName.Where(char.IsLetterOrDigit).ToArray());
		
		if (Widgets.ButtonImage(buttonRect, TexButton.Save) && !_newTemplateName.NullOrEmpty())
			SaveTemplate(_newTemplateName);
	}

	private void SaveTemplate(string templateName)
	{
		XenotypeChanceDatabases.Templates[templateName] = _xenotypeChances.CreateTemplate();
		AppliedOrSavedTemplateName = templateName;
		Close();
	}
	
	public override void PostClose()
	{
		OnClosed?.Invoke();
		base.PostClose();
	}
}