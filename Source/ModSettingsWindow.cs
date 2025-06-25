// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

using System.Linq;
using XenotypeSpawnControl.GUIExtensions;
// ReSharper disable StringCompareToIsCultureSpecific

namespace XenotypeSpawnControl;

public static class ModSettingsWindow
{
	public static readonly Tab[] Tabs =
	[
		new Tab<FactionDef>
		{
			Title = Strings.Translated.Factions,
			Defs = DefDatabase<FactionDef>.AllDefsListForReading.Where(static faction => faction.humanlikeFaction)
		},
		new Tab<MemeDef>
		{
			RequirementsMet = ModsConfig.IdeologyActive,
			Title = Strings.Translated.Memes,
			Defs = DefDatabase<MemeDef>.AllDefsListForReading
		},
		new Tab<PawnKindDef>
		{
			Title = Strings.Translated.PawnKinds,
			Defs = DefDatabase<PawnKindDef>.AllDefsListForReading.Where(static p => p.RaceProps is { Humanlike: true })
		},
		new EditorTab
		{
			Title = Strings.Translated.Editor
		}
	];

	public static void DoWindowContents(Rect inRect)
	{
		if (Event.current.type == EventType.Layout)
			return;
		
		var windowRect = Find.WindowStack.currentlyDrawnWindow.windowRect;
		if (Widgets.ButtonText(
			new(windowRect.width - Window.CloseButSize.x - (Window.StandardMargin * 2f),
				windowRect.height - Window.FooterRowHeight - Window.StandardMargin, Window.CloseButSize.x,
				Window.CloseButSize.y),
			Strings.Translated.ResetAll))
		{
			Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(Strings.Translated.ConfirmReset,
				XenotypeChanceDatabases.ResetAll, destructive: true));
		}

		inRect.yMin += 30f;

		Widgets.DrawMenuSection(inRect);
		TabDrawer.DrawTabs(inRect, _tabList);

		inRect = inRect.ContractedBy(20f);

		inRect.width -= 200f;

		/*var titleListing = new Listing_Standard();
		titleListing.Begin(inRect);

		// searchText = titleListing.TextEntry(searchText);
		titleListing.End();*/

		inRect.width += 200f;
		// inRect.yMin += 60f;
		
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
		var tabs = Tabs;
		for (var i = 0; i < tabs.Length; i++)
		{
			var tab = tabs[i];
			if (tab.RequirementsMet)
				_tabList.Add(new(tab, static () => ref _currentTab));
		}
	}

	public abstract class Tab
	{
		public string Title { get; init; }
		
		public bool RequirementsMet { get; init; } = true;
		
		public abstract void DoWindowContents(Rect inRect);

		protected Tab() => Title ??= GetType().Name;
	}

	public class Tab<T> : Tab where T : Def
	{
		public IEnumerable<T> Defs { get; init; } = DefDatabase<T>.AllDefs;

		public IEnumerable<ModifiableXenotype> Xenotypes { get; set; }
			= ModifiableXenotypeDatabase.AllValues.Values;

		public Listing_Standard Listing { get; } = new() { verticalSpacing = 4f };

		private string? _currentlySelectedDefName;
		private readonly ScrollViewStatus _defScrollViewStatus = new();
		private readonly ScrollViewStatus _xenotypeScrollViewStatus = new();

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
			using var listingScope
				= new ScrollableListingScope(outRect, _defScrollViewStatus, Listing);
			
			var listing = listingScope.Listing;

			var defs = Defs.Select(static def => (def.LabelCap.Resolve(), def.defName)).ToList();
			defs.Sort(static (a, b)
				=> string.CompareOrdinal(a.Item1, b.Item1) is var labelResult && labelResult != 0
					? labelResult
					: string.CompareOrdinal(a.defName, b.defName));

			foreach (var def in defs)
			{
				var (label, defName) = def;

				if (!defName.EqualsIgnoreCase(label)
					&& defs.Exists(otherDef => otherDef.Item1.EqualsIgnoreCase(label) && otherDef.defName != defName))
				{
					label += $" ({defName})";
				}

				// if (searchOn && !defName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
				//	continue;
				// if (defName.Length > LINE_MAX)
				//	defName = defName.Substring(0, LINE_MAX) + "...";

				if (listing.SelectionButton(label, _currentlySelectedDefName == defName))
					_currentlySelectedDefName = defName;
			}

			if (typeof(T) != typeof(FactionDef))
				return;

			if (listing.SelectionButton(Strings.Translated.NoFaction,
				_currentlySelectedDefName == Strings.NoFactionKey))
			{
				_currentlySelectedDefName = Strings.NoFactionKey;
			}
		}

		public void ListXenotypes(Rect outRect)
		{
			if (_currentlySelectedDefName is null)
				return;

			using var listingScope
				= new ScrollableListingScope(outRect, _xenotypeScrollViewStatus, new() { verticalSpacing = 4f });
			
			var listing = listingScope.Listing;
			var xenotypeChances = XenotypeChanceDatabase<T>.For(_currentlySelectedDefName);

			TemplateButtons(listing, xenotypeChances);
			listing.Gap(6f);

			// first show all INACTIVE xenotypes
			foreach (var unloadedXenotypeKeyValuePair in xenotypeChances.UnloadedXenotypes.OrderBy(static unloaded
				=> unloaded.Key))
			{
				InactiveLabel(listing, unloadedXenotypeKeyValuePair);
			}

			var allowedXenotypes = xenotypeChances.AllAllowedXenotypeChances
				.Select(static xenotypeChance => (xenotypeChance.Xenotype, xenotypeChance)).ToList();
			
			allowedXenotypes.Sort(static (aKey, bKey) =>
			{
				var a = aKey.Xenotype;
				var b = bKey.Xenotype;
				return a.Def == XenotypeDefOf.Baseliner
					? b.Def == XenotypeDefOf.Baseliner ? CompareLabelsAndNames(a, b) : -1
					: a is ModifiableXenotype.Generated == b is ModifiableXenotype.Generated
						? a is ModifiableXenotype.Random != b is ModifiableXenotype.Random
							? a is ModifiableXenotype.Random ? -1 : 1
							: CompareLabelsAndNames(a, b)
						: a is ModifiableXenotype.Generated
							? -1
							: 1;
			});

			// then show all configurable xenotypes
			foreach (var (xenotype, xenotypeChance) in allowedXenotypes)
			{
				var label = xenotype.DisplayLabel;

				listing.XenotypeChanceDisplay(xenotypeChances, xenotypeChance, (label != xenotype.Name
						|| (xenotype.Def?.LabelCap.Resolve() is [_, ..] defLabel && !defLabel.EqualsIgnoreCase(label)))
					&& allowedXenotypes.Exists(other
						=> other.xenotypeChance != xenotypeChance
						&& other.Xenotype.DisplayLabel.EqualsIgnoreCase(label)));
			}

			listing.GapLine();

			var architeCheckboxResult = xenotypeChances.AllowArchiteXenotypes;
			listing.CheckboxLabeled(Strings.Translated.AllowArchiteXenotypes, ref architeCheckboxResult);
			xenotypeChances.AllowArchiteXenotypes = architeCheckboxResult;

			listing.Gap(6f);
			XenotypeChancesTypeButtons(listing, xenotypeChances);
			SetAllWeightsButton(listing, xenotypeChances);

			// hint to notify user about the total chance of weighted xenotypes
			// seems useless
			// if (!xenotypeChances.OnlyAbsoluteChancesAllowed)
			// {
			// 	listing.Label($"{Strings.Translated.WeightDistributionHint} {
			// 		(xenotypeChances.GetRawWeightedDistributionChance() / 10f).ToString("##0.#",
			// 			CultureInfo.InvariantCulture)}%");
			// }
			
			listing.Gap(6f);

			if (listing.ButtonText(Strings.Translated.Reset))
				xenotypeChances.Reset();
		}

		private static int CompareLabelsAndNames(ModifiableXenotype a, ModifiableXenotype b)
			=> string.CompareOrdinal(a.DisplayLabel, b.DisplayLabel) is var labelResult && labelResult != 0
				? labelResult
				: string.CompareOrdinal(a.Name, b.Name) is var nameResult && nameResult != 0
					? nameResult
					: string.CompareOrdinal(a.Def?.label, b.Def?.label);

		private string? _selectedTemplate = XenotypeChanceDatabases.Templates.Keys.FirstOrDefault();
		
		private void TemplateButtons(Listing_Standard listing, XenotypeChances<T> xenotypeChances)
		{
			// TODO: use Widgets with listing.GetRect
			var previousWidth = listing.ColumnWidth;
			var previousX = listing.curX;
			var previousY = listing.curY;

			try
			{
				var templates = XenotypeChanceDatabases.Templates;
				var templateNames = templates.Keys;
				
				listing.ColumnWidth /= 2f;
				
				listing.curX += listing.ColumnWidth / 2f;
				
				// quick access for last used template, or first, if none have been used before
				if (_selectedTemplate is null || !templates.ContainsKey(_selectedTemplate))
				{
					_selectedTemplate = templateNames.FirstOrDefault();
				}

				if (_selectedTemplate is null)
				{
					var widthBeforeLabel = listing.ColumnWidth;
					listing.ColumnWidth += listing.ColumnWidth / 2f;
					listing.ButtonText(Strings.Translated.NoTemplateHint);
					listing.ColumnWidth = widthBeforeLabel;
				}
				else
				{
					if (listing.ButtonText(_selectedTemplate))
					{
						var templateOptions = new List<FloatMenuOption>();
						
						foreach (var templateName in templateNames)
						{
							var currentTemplateName = templateName;
							templateOptions.Add(new(templateName, () => _selectedTemplate = currentTemplateName));
						}
						
						Find.WindowStack.Add(new FloatMenu(templateOptions));
					}

					listing.curY = previousY;
					listing.curX += listing.ColumnWidth;
					listing.ColumnWidth = previousWidth / 4f;
					if (listing.ButtonText(Strings.Translated.Apply))
						xenotypeChances.ApplyTemplate(templates[_selectedTemplate]);
				}
			
				// template menu
				listing.curY = previousY;
				listing.curX = previousX;
				// listing.curY = listingTemplateY;
				listing.ColumnWidth = previousWidth / 4f;
			
				if (listing.ButtonText(Strings.Translated.Edit))
				{
					var templateDialog = new Dialog_Templates<T>(xenotypeChances);

					templateDialog.OnClosed = () =>
					{
						if (templateDialog.AppliedOrSavedTemplateName is not null)
							_selectedTemplate = templateDialog.AppliedOrSavedTemplateName;
					};
				
					Find.WindowStack.Add(templateDialog);
				}
			}
			finally
			{
				listing.curX = previousX;
				listing.ColumnWidth = previousWidth;
			}
		}

		private static void XenotypeChancesTypeButtons(Listing_Standard listing, XenotypeChances<T> xenotypeChances)
		{
			var onlyAbsolute = xenotypeChances.OnlyAbsoluteChancesAllowed;
			var onlyWeighted = xenotypeChances.OnlyWeightedChancesAllowed;

			var drawDoubleButton = !onlyAbsolute && !onlyWeighted;
			var listingButtonY = listing.curY;
			
			if (drawDoubleButton)
				listing.ColumnWidth /= 2f;
			
			if (!onlyAbsolute && listing.ButtonText(Strings.Translated.AbsoluteToggle))
				xenotypeChances.SetIsAbsoluteForAllowed(true);
			
			if (drawDoubleButton)
			{
				listing.curX += listing.ColumnWidth;
				listing.curY = listingButtonY;
			}
			
			if (!onlyWeighted && listing.ButtonText(Strings.Translated.WeightedToggle))
				xenotypeChances.SetIsAbsoluteForAllowed(false);

			if (!drawDoubleButton)
				return;

			listing.curX -= listing.ColumnWidth;
			listing.ColumnWidth *= 2;
		}

		private (float Value, string String) _allWeightSetterValue = (1, "1");

		private void SetAllWeightsButton(Listing_Standard listing, XenotypeChances<T> xenotypeChances)
		{
			var weightsButtonResult = listing.TextBoxButton(Strings.Translated.WeightSetterButton, _allWeightSetterValue.String);
			Listing_StandardExtensions.InterpretWeightString(ref _allWeightSetterValue, weightsButtonResult.Text);
			
			if (weightsButtonResult.Clicked)
				xenotypeChances.SetWeightForAllowedWeightedXenotypes(_allWeightSetterValue.Value);
		}

		private static void InactiveLabel(Listing_Standard listing,
			KeyValuePair<string, XenotypeChanceConfig> xenotypeNameConfigPair)
		{
			var inactiveRect = listing.GetRect(Text.CalcHeight($"{Strings.Translated.Inactive}: ", listing.ColumnWidth)
				+ Listing_Standard.PinnableActionHeight
				+ (listing.verticalSpacing * 2f));
			
			var currentColor = GUI.color;
			GUI.color = Color.red with { a = 0.3f };
			GUI.DrawTexture(inactiveRect, BaseContent.WhiteTex);
			GUI.color = currentColor;
			
			if (Widgets.CloseButtonFor(inactiveRect))
				ModifiableXenotypeDatabase.RemoveCustomXenotype(xenotypeNameConfigPair.Key);

			listing.curY -= inactiveRect.height;

			var inactiveLabel = $"{Strings.Translated.Inactive}: {xenotypeNameConfigPair.Key}, {
				xenotypeNameConfigPair.Value.RawChanceValue / 10m}%";
			
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

				var rect = Listing.GetRect(
					Math.Max(Text.CalcHeight(customXenotype.Key, Listing.ColumnWidth * 0.5f), 30f)
					+ Listing.verticalSpacing);
				
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
			=> Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(Strings.ConfirmDelete.Translate(xenotype.Name),
				()
					=> ModifiableXenotypeDatabase.DeleteCustomXenotype(xenotype.Name), destructive: true));

		public static Dialog_CreateXenotype CreateXenotypeDialogFor(
			KeyValuePair<string, ModifiableXenotype> customXenotype)
		{
			var createXenotypeDialog = new Dialog_CreateXenotype(HarmonyPatches.StartingPawnUtility.MAGIC_NUMBER, null)
			{
				xenotypeName = customXenotype.Key
			};
			
			createXenotypeDialog.selectedGenes.Clear();
			createXenotypeDialog.selectedGenes.AddRange(customXenotype.Value.CustomXenotype!.genes);
			createXenotypeDialog.inheritable = customXenotype.Value.CustomXenotype.inheritable;
			createXenotypeDialog.OnGenesChanged();
			
			createXenotypeDialog.ignoreRestrictions
				= createXenotypeDialog.selectedGenes.Any(static def => def.biostatArc > 0)
				|| !createXenotypeDialog.WithinAcceptableBiostatLimits(false);
			
			return createXenotypeDialog;
		}
	}

	private static Tab _currentTab = Tabs.First();
	private static readonly List<TabRecord> _tabList = [];

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