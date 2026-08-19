using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Resources;
using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Audit;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// The shared page for a tweak category (Privacy, Security, Performance,
/// Debloat, Shell). A level dial pre-selects the entries a profile at that level
/// would apply; the user then toggles individual entries. Search and the risk /
/// draft filters narrow the visible list without changing the selection. The
/// selected row drives the detail pane. Scan-driven drift state is layered on in
/// a later part.
/// </summary>
internal sealed partial class CategoryPageViewModel : ObservableObject, IPageViewModel
{
    private readonly Category _category;
    private readonly TweakCatalog _catalog;
    private readonly IApplyFlowLauncher _applyFlow;
    private readonly AppSettings _settings;
    private bool _updatingSelection;
    private HashSet<TweakId> _baselineTweakIds = [];

    [ObservableProperty]
    private Level _level = Level.Off;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private RiskTier? _riskFilter;

    [ObservableProperty]
    private Baseline? _baselineFilter;

    [ObservableProperty]
    private bool _includeDraft;

    [ObservableProperty]
    private TweakItemViewModel? _selectedItem;

    [ObservableProperty]
    private string _selectionSummary = string.Empty;

    /// <summary>Builds the page for one category over the catalogue, machine facts, the baselines, the apply launcher and settings.</summary>
    public CategoryPageViewModel(Category category, TweakCatalog catalog, OperatingSystemFacts facts, IReadOnlyList<Baseline> baselines, IApplyFlowLauncher applyFlow, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(baselines);
        ArgumentNullException.ThrowIfNull(applyFlow);
        ArgumentNullException.ThrowIfNull(settings);
        _category = category;
        _catalog = catalog;
        _applyFlow = applyFlow;
        _settings = settings;

        AllItems = [.. catalog.InCategory(category)
            .Where(e => e.Status != TweakStatus.Deprecated && e.AppliesTo.Matches(facts))
            .Select(e => new TweakItemViewModel(e))];
        foreach (TweakItemViewModel item in AllItems)
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }

        LevelOptions = [.. new[] { Level.Off, Level.Basic, Level.Balanced, Level.Strict, Level.Paranoid }
            .Select(l => new LabeledOption(l, DisplayLabels.For(l)))];
        RiskFilterOptions =
        [
            new LabeledOption(null, Strings.Filter_AllRisks),
            new LabeledOption(RiskTier.Safe, DisplayLabels.For(RiskTier.Safe)),
            new LabeledOption(RiskTier.Caution, DisplayLabels.For(RiskTier.Caution)),
            new LabeledOption(RiskTier.Advanced, DisplayLabels.For(RiskTier.Advanced)),
            new LabeledOption(RiskTier.Breaking, DisplayLabels.For(RiskTier.Breaking)),
        ];

        // Baselines map almost entirely to security controls, so the baseline filter is
        // offered on the Security page only; other categories get no options and hide it.
        BaselineFilterOptions = category == Category.Security
            ? [new LabeledOption(null, Strings.Filter_AllBaselines), .. baselines.Select(b => new LabeledOption(b, b.Title))]
            : [];

        Items = [];
        _includeDraft = settings.IncludeDraft;
        RebuildItems();
        UpdateSelectionSummary();
    }

    /// <inheritdoc />
    public string Key => CategoryPages.KeyFor(_category);

    /// <inheritdoc />
    public string Title => CategoryPages.TitleFor(_category);

    /// <summary>The entries visible after search and filters (selection persists across filtering).</summary>
    public ObservableCollection<TweakItemViewModel> Items { get; }

    /// <summary>Every applicable entry in the category, regardless of the current filter.</summary>
    public IReadOnlyList<TweakItemViewModel> AllItems { get; }

    /// <summary>The level dial options.</summary>
    public IReadOnlyList<LabeledOption> LevelOptions { get; }

    /// <summary>The risk-filter options (including "all").</summary>
    public IReadOnlyList<LabeledOption> RiskFilterOptions { get; }

    /// <summary>The baseline-filter options (an "all" entry plus each baseline); empty except on the Security page.</summary>
    public IReadOnlyList<LabeledOption> BaselineFilterOptions { get; }

    /// <summary>Whether the baseline filter is offered on this page (Security only).</summary>
    public bool ShowBaselineFilter => _category == Category.Security;

    /// <summary>The entries currently selected for apply.</summary>
    public IReadOnlyList<TweakItemViewModel> SelectedItems => [.. AllItems.Where(i => i.IsSelected)];

    /// <summary>True when at least one entry is selected.</summary>
    public bool HasSelection => AllItems.Any(i => i.IsSelected);

    /// <summary>Opens the review-and-apply dialog for the selected entries.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ReviewAndApply()
    {
        IReadOnlyList<TweakDefinition> entries = [.. SelectedItems.Select(i => i.Definition)];
        if (entries.Count == 0)
        {
            return;
        }

        GuiApply.Launch(_applyFlow, Title, entries, $"GUI {Title}", Scope.Machine, _settings.CreateRestorePoint);
    }

    partial void OnLevelChanged(Level value) => ApplyLevelSelection();

    partial void OnIncludeDraftChanged(bool value)
    {
        ApplyLevelSelection();
        RebuildItems();
    }

    partial void OnSearchTextChanged(string value) => RebuildItems();

    partial void OnRiskFilterChanged(RiskTier? value) => RebuildItems();

    partial void OnBaselineFilterChanged(Baseline? value)
    {
        _baselineTweakIds = value is null ? [] : [.. value.Rules.SelectMany(r => r.TweakIds)];
        RebuildItems();
    }

    private void ApplyLevelSelection()
    {
        HashSet<TweakId> selectedIds = [.. _catalog.Select(_category, Level, IncludeDraft).Select(e => e.Id)];
        _updatingSelection = true;
        try
        {
            foreach (TweakItemViewModel item in AllItems)
            {
                item.IsSelected = selectedIds.Contains(item.Id);
            }
        }
        finally
        {
            _updatingSelection = false;
        }

        UpdateSelectionSummary();
    }

    private void RebuildItems()
    {
        string term = SearchText.Trim();
        Items.Clear();
        foreach (TweakItemViewModel item in AllItems)
        {
            if (!IncludeDraft && item.Status == TweakStatus.Draft)
            {
                continue;
            }

            if (RiskFilter is RiskTier risk && item.Risk != risk)
            {
                continue;
            }

            if (BaselineFilter is not null && !_baselineTweakIds.Contains(item.Id))
            {
                continue;
            }

            if (term.Length > 0 && !item.Matches(term))
            {
                continue;
            }

            Items.Add(item);
        }
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_updatingSelection && string.Equals(e.PropertyName, nameof(TweakItemViewModel.IsSelected), StringComparison.Ordinal))
        {
            UpdateSelectionSummary();
        }
    }

    private void UpdateSelectionSummary()
    {
        SelectionSummary = DisplayLabels.SelectedCount(AllItems.Count(i => i.IsSelected), AllItems.Count);
        OnPropertyChanged(nameof(HasSelection));
        ReviewAndApplyCommand.NotifyCanExecuteChanged();
    }
}
