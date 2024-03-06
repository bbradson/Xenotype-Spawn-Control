// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KTrie;
using NVorbis;
using RimWorld.Planet;
using UnityEngine.UIElements;
using Verse;
using XenotypeSpawnControl.GUIExtensions;

namespace XenotypeSpawnControl;

public static class ModSettingsWindow
{
	public static Tab[] Tabs = new Tab[]
	{
		new Tab<FactionDef>
		{
			Title = Strings.Translated.Factions,
			Defs = DefDatabase<FactionDef>.AllDefs.Where(faction => faction.humanlikeFaction)
		},
		new Tab<MemeDef>
		{
			RequirementsMet = ModsConfig.IdeologyActive,
			Title = Strings.Translated.Memes,
			Defs = DefDatabase<MemeDef>.AllDefs
		},
		new Tab<PawnKindDef>
		{
			Title = Strings.Translated.PawnKinds,
			Defs = DefDatabase<PawnKindDef>.AllDefs.Where(p => p.RaceProps is { } props && props.Humanlike)
		},
		new EditorTab
		{
			Title = Strings.Translated.Editor
		}
	};

	public static void DoWindowContents(Rect inRect)
	{
		var windowRect = Find.WindowStack.currentlyDrawnWindow.windowRect;
		if (Widgets.ButtonText(
			new(windowRect.width - Window.CloseButSize.x - (Window.StandardMargin * 2f), windowRect.height - Window.FooterRowHeight - Window.StandardMargin, Window.CloseButSize.x, Window.CloseButSize.y),
			Strings.Translated.ResetAll))
		{
			Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(Strings.Translated.ConfirmReset, XenotypeChanceDatabases.ResetAll, destructive: true));
		}

		inRect.yMin += 30f;

		Widgets.DrawMenuSection(inRect);
		TabDrawer.DrawTabs(inRect, _tabList);

		inRect = inRect.ContractedBy(20f);

		inRect.width -= 200f;

		/*var titleListing = new Listing_Standard();
		titleListing.Begin(inRect);

		//searchText = titleListing.TextEntry(searchText);
		titleListing.End();*/

		inRect.width += 200f;
		//inRect.yMin += 60f;
		try
		{
			_currentTab.DoWindowContents(inRect);
		}
		catch (Exception ex)
		{
			Log.Error($"{ex}");
		}
	}

	static ModSettingsWindow()
	{
		for (var i = 0; i < Tabs.Length; i++)
		{
			if (Tabs[i].RequirementsMet)
				_tabList.Add(new(Tabs[i], () => ref _currentTab));
		}
	}

	public abstract class Tab
	{
		public virtual string Title { get; set; }
		public virtual bool RequirementsMet { get; set; } = true;
		public abstract void DoWindowContents(Rect inRect);

		public Tab() => Title ??= GetType().Name;
	}

	public class Tab<T> : Tab where T : Def
	{
		public IEnumerable<T> Defs { get; set; } = DefDatabase<T>.AllDefs;

		public IEnumerable<ModifiableXenotype> Xenotypes { get; set; }
			= ModifiableXenotypeDatabase.AllValues.Values;

		//public Listing_Standard Listing { get; } = new() { verticalSpacing = 4f };

		private string? _currentlySelectedDefName;
		private ScrollViewStatus _defScrollViewStatus = new();
		private ScrollViewStatus _xenotypeScrollViewStatus = new();

		public override void DoWindowContents(Rect inRect)
		{
			var leftHalfRect = inRect.LeftHalf();
			var rightHalfRect = inRect.RightHalf();
			leftHalfRect.width -= 20f;
			ListDefs(leftHalfRect);
			ListXenotypes(rightHalfRect);
		}

		public void ListDefs(Rect outRect)
		{
			using var listingScope = new ScrollableListingScope(outRect, _defScrollViewStatus, new() { verticalSpacing = 4f });
			var listing = listingScope.Listing;

			foreach (var def in Defs.OrderBy(def => def.defName))
			{
				var defName = def.defName;

				//if (searchOn && !factionDefName.ToLower().Contains(searchText.ToLower()))
				//	continue;
				//if (factionDefName.Length > LINE_MAX)
				//	factionDefName = factionDefName.Substring(0, LINE_MAX) + "...";

				if (listing.SelectionButton(def.LabelCap + " (" + defName + ")", _currentlySelectedDefName == defName))
					_currentlySelectedDefName = defName;
			}

			if (typeof(T) == typeof(FactionDef))
			{
				if (listing.SelectionButton(Strings.Translated.NoFaction, _currentlySelectedDefName == Strings.NoFactionKey))
					_currentlySelectedDefName = Strings.NoFactionKey;
			}
		}

		public void ListXenotypes(Rect outRect)
		{
			if (_currentlySelectedDefName is null)
				return;

			using var listingScope = new ScrollableListingScope(outRect, _xenotypeScrollViewStatus, new() { verticalSpacing = 4f });
			var listing = listingScope.Listing;

			var xenotypeChances = XenotypeChanceDatabase<T>.For(_currentlySelectedDefName);

			TemplateButtons(listing, xenotypeChances);
			listing.Gap(6);

			//ref of property is not allowed
			var architeCheckboxResult = xenotypeChances.AllowArchiteXenotypes;
			listing.CheckboxLabeled(Strings.Translated.AllowArchiteXenotypes, ref architeCheckboxResult);
			xenotypeChances.AllowArchiteXenotypes = architeCheckboxResult;
			listing.Gap(6);

			XenotypeChancesTypeButtons(listing, xenotypeChances);

			SetAllWeightsButton(listing, xenotypeChances);

			//hint to notify user about the total chance of weighted xenotypes
			listing.Label(xenotypeChances.OnlyAbsoluteChancesAllowed ? Strings.Translated.NoWeightedHint : Strings.Translated.WeightDistrubutionHint + " " + (xenotypeChances.GetRawWeightedDistrubutionChance() / 10f).ToString("##0.#", CultureInfo.InvariantCulture) + "%");

			listing.GapLine();

			//first show all INACTIVE xenotypes
			foreach (var unloadedXenotypeKeyValuePair in xenotypeChances.UnloadedXenotypes.OrderBy(unloaded => unloaded.Key))
			{
				InactiveLabel(listing, unloadedXenotypeKeyValuePair);
			}

			//then show all configurable xenotypes
			foreach (var xenotypeChance in xenotypeChances.AllAllowedXenotypeChances.OrderBy(xenotypeChance => xenotypeChance.Xenotype.DisplayLabel))
			{
				listing.XenotypeChanceDisplay(xenotypeChances, xenotypeChance);
			}

			if (listing.ButtonText(Strings.Translated.Reset))
				xenotypeChances.Reset();
		}
		
		private string _lastTemplate = XenotypeChanceDatabases.Templates.Keys.FirstOrDefault();
		private void TemplateButtons(Listing_Standard listing, XenotypeChances<T> xenotypeChances)
		{
			//TODO: I don't know where to get the correct button size for the listing, so modify listing instead of using Widgets
			var listingTemplateY = listing.curY;
			listing.ColumnWidth /= 2;
			//quick acces for last used template, or first, if none have been used before
			if (_lastTemplate is null || !XenotypeChanceDatabases.Templates.ContainsKey(_lastTemplate))
				_lastTemplate = XenotypeChanceDatabases.Templates.Keys.FirstOrDefault();
			if (_lastTemplate is null)
			{
				listing.Label(Strings.Translated.NoTemplateHint);
			}
			else if (listing.ButtonText(_lastTemplate))
			{
				xenotypeChances.ApplyTemplate(XenotypeChanceDatabases.Templates[_lastTemplate]);
			}
			//template menu
			listing.curX += listing.ColumnWidth;
			listing.curY = listingTemplateY;
			if (listing.ButtonText(Strings.Translated.Templates))
			{
				var templateDialog = new Dialog_Templates<T>(xenotypeChances);
				templateDialog.OnClosed = () => {
					if (templateDialog.AppliedOrSavedTemplateName is not null)
						_lastTemplate = templateDialog.AppliedOrSavedTemplateName;
				};
				Find.WindowStack.Add(templateDialog);
			}
			listing.curX -= listing.ColumnWidth;
			listing.ColumnWidth *= 2;
		}

		private static void XenotypeChancesTypeButtons(Listing_Standard listing, XenotypeChances<T> xenotypeChances)
		{
			var onlyAbsolute = xenotypeChances.OnlyAbsoluteChancesAllowed;
			var onlyWeighted = xenotypeChances.OnlyWeightedChancesAllowed;

			var drawDoubleButton = !onlyAbsolute && !onlyWeighted;
			var listingButtonY = listing.curY;
			if (drawDoubleButton)
				listing.ColumnWidth /= 2;
			if (!onlyAbsolute && listing.ButtonText(Strings.Translated.AbsoluteToggle))
				xenotypeChances.SetIsAbsoluteForAllowed(true);
			if (drawDoubleButton)
			{
				listing.curX += listing.ColumnWidth;
				listing.curY = listingButtonY;
			}
			if (!onlyWeighted && listing.ButtonText(Strings.Translated.WeightedToggle))
				xenotypeChances.SetIsAbsoluteForAllowed(false);
			if (drawDoubleButton)
			{
				listing.curX -= listing.ColumnWidth;
				listing.ColumnWidth *= 2;
			}
		}

		private (float Value, string String) _allWeightSetterValue = (1, "1");

		private void SetAllWeightsButton(Listing_Standard listing, XenotypeChances<T> xenotypeChances)
		{
			var weightsButtonResult = listing.TextBoxButton(Strings.Translated.WeightSetterButton, _allWeightSetterValue.String);
			Listing_StandardExtensions.InterpretWeightString(ref _allWeightSetterValue, weightsButtonResult.Text);
			if(weightsButtonResult.Clicked)
				xenotypeChances.SetWeightForAllowedWeightedXenotypes(_allWeightSetterValue.Value);
		}

		private static void InactiveLabel(Listing_Standard listing, KeyValuePair<string, XenotypeChanceConfig> xenotypeNameConfigPair)
		{
			var inactiveRect = listing.GetRect(Text.CalcHeight($"{Strings.Translated.Inactive}: ", listing.ColumnWidth) + Listing_Standard.PinnableActionHeight + (listing.verticalSpacing * 2f));
			var currentColor = GUI.color;
			GUI.color = Color.red with { a = 0.3f };
			GUI.DrawTexture(inactiveRect, BaseContent.WhiteTex);
			GUI.color = currentColor;
			if (Widgets.CloseButtonFor(inactiveRect))
				ModifiableXenotypeDatabase.RemoveCustomXenotype(xenotypeNameConfigPair.Key);
			listing.curY -= inactiveRect.height;

			var inactiveLabel = $"{Strings.Translated.Inactive}: {xenotypeNameConfigPair.Key}, {xenotypeNameConfigPair.Value.RawChanceValue / 10m}%";
			if (!xenotypeNameConfigPair.Value.IsAbsolute)
				inactiveLabel += $", {Strings.Translated.Weight}: {xenotypeNameConfigPair.Value.Weight}";
			listing.Label(inactiveLabel);
			listing.Gap(Listing_Standard.PinnableActionHeight + listing.verticalSpacing);
		}
	}

	public class EditorTab : Tab
	{
		private ScrollViewStatus _scrollViewStatus = new();
		public Listing_Standard Listing { get; } = new() { verticalSpacing = 4f };

		public override void DoWindowContents(Rect inRect)
		{
			using var listingScope = new ScrollableListingScope(inRect, _scrollViewStatus, Listing);

			foreach (var customXenotype in ModifiableXenotypeDatabase.CustomXenotypes)
			{
				if (customXenotype.Value is ModifiableXenotype.Generated)
					continue;

				var rect = Listing.GetRect(Math.Max(Text.CalcHeight(customXenotype.Key, Listing.ColumnWidth * 0.5f), 30f) + Listing.verticalSpacing);
				Listing.curY -= rect.height;
				if (Widgets.CloseButtonFor(rect))
					OpenDeletionDialog(customXenotype.Value);

				Listing.columnWidthInt -= Widgets.CloseButtonSize + (Widgets.CloseButtonMargin * 2f);
				if (Listing.ButtonTextLabeled(customXenotype.Key.CapitalizeFirst(), Strings.Translated.Edit))
					Find.WindowStack.Add((Dialog_CreateXenotype?)CreateXenotypeDialogFor(customXenotype));
				Listing.columnWidthInt += Widgets.CloseButtonSize + (Widgets.CloseButtonMargin * 2f);
			}

			if (Listing.ButtonTextLabeled(Strings.Translated.MakeYourOwn, "+"))
				Find.WindowStack.Add(new Dialog_CreateXenotype(HarmonyPatches.StartingPawnUtility.MAGIC_NUMBER, null));
		}

		public static void OpenDeletionDialog(ModifiableXenotype xenotype)
			=> Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(Strings.ConfirmDelete.Translate(xenotype.Name), ()
				=> ModifiableXenotypeDatabase.DeleteCustomXenotype(xenotype.Name), destructive: true));

		public static Dialog_CreateXenotype CreateXenotypeDialogFor(KeyValuePair<string, ModifiableXenotype> customXenotype)
		{
			var createXenotypeDialog = new Dialog_CreateXenotype(HarmonyPatches.StartingPawnUtility.MAGIC_NUMBER, null)
			{
				xenotypeName = customXenotype.Key
			};
			createXenotypeDialog.selectedGenes.Clear();
			createXenotypeDialog.selectedGenes.AddRange(customXenotype.Value.CustomXenotype!.genes);
			createXenotypeDialog.inheritable = customXenotype.Value.CustomXenotype.inheritable;
			createXenotypeDialog.OnGenesChanged();
			createXenotypeDialog.ignoreRestrictions = createXenotypeDialog.selectedGenes.Any(def => def.biostatArc > 0) || !createXenotypeDialog.WithinAcceptableBiostatLimits(false);
			return createXenotypeDialog;
		}
	}

	private static Tab _currentTab = Tabs.First();
	private static List<TabRecord> _tabList = new();

	public class TabRecord : Verse.TabRecord
	{
		public Tab Tab { get; }
		public AccessTools.FieldRef<Tab> CurrentTab { get; }
		public TabRecord(Tab tab, AccessTools.FieldRef<Tab> currentTab) : base(tab.Title, null, null)
		{
			Tab = tab;
			CurrentTab = currentTab;
			clickedAction = () => CurrentTab() = Tab;
			selectedGetter = () => CurrentTab() == Tab;
		}
	}
}

public class Dialog_Templates<T> : Window where T : Def
{
	private const float ENTRYHEIGHT = Widgets.RadioButtonSize;
	private const float ENTRYMARGIN = Widgets.CloseButtonMargin;
	private XenotypeChances<T> _xenotypeChances;
	private string _newTemplateName = string.Empty;
	private ScrollViewStatus _scrollViewStatus = new();
	public Action? OnClosed { get; set; }
	public string? AppliedOrSavedTemplateName { get; private set; }
	public Dialog_Templates(XenotypeChances<T> xenotypeChances) : base()
	{
		//setup window properties
		closeOnClickedOutside = true;
		absorbInputAroundWindow = true;
		doCloseX = true;
		doCloseButton = true;
		optionalTitle = Strings.Translated.Templates;

		_xenotypeChances = xenotypeChances;
	}

	public override void OnAcceptKeyPressed()
	{
		//try to save the template instead of closing the window
		if(!_newTemplateName.NullOrEmpty())
			SaveTemplate(_newTemplateName);
		base.OnAcceptKeyPressed();
	}

	public override void DoWindowContents(Rect inRect)
	{
		//do not draw over the close button and leave a small margin above it
		inRect.height -= CloseButSize.y + ENTRYMARGIN;
		using var scrollScope = new ScrollableListingScope(inRect, _scrollViewStatus);
		var listing = scrollScope.Listing;

		foreach (var templateKeyValuePair in XenotypeChanceDatabases.Templates.ToArray())
		{
			var entryRect = listing.GetRect(ENTRYHEIGHT);
			entryRect.SplitVertically(entryRect.width - ENTRYHEIGHT, out entryRect, out var firstIconButtonRect);
			entryRect.SplitVertically(entryRect.width - ENTRYHEIGHT, out entryRect, out var secondIconButtonRect);

			if(Widgets.ButtonText(entryRect, templateKeyValuePair.Key))
			{
				_xenotypeChances.ApplyTemplate(templateKeyValuePair.Value);
				AppliedOrSavedTemplateName = templateKeyValuePair.Key;
				Close();
			}
			if (Widgets.ButtonImage(secondIconButtonRect, TexButton.Save))
			{
				SaveTemplate(templateKeyValuePair.Key);
			}
			if (Widgets.ButtonImage(firstIconButtonRect, TexButton.DeleteX))
			{
				XenotypeChanceDatabases.Templates.Remove(templateKeyValuePair.Key);
			}

			listing.Gap(ENTRYMARGIN);
		}
		//only allow alphanumerical strings
		var rect = listing.GetRect(ENTRYHEIGHT);
		rect.SplitVertically(rect.width - ENTRYHEIGHT, out var textRect, out var buttonRect);
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
		if (OnClosed is not null)
			OnClosed();
		base.PostClose();
	}
}