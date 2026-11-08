using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Resources;
using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Audit;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// The shared page for a tweak category (Privacy, Security, Performance,
/// Debloat, Shell). A level dial pre-selects the Verified entries a named profile at
/// that level would apply (conflicting variants settled the same way); the user then
/// toggles individual entries, Drafts included. Scan reads what this PC already has and
/// badges each entry; search and the risk / baseline / not-applied filters narrow the
/// visible list without changing the selection. The selected row drives the detail pane.
/// </summary>
internal sealed partial class CategoryPageViewModel : ObservableObject, IPageViewModel
{
    private readonly Category _category;
    private readonly TweakCatalog _catalog;
    private readonly IApplyFlowLauncher _applyFlow;
    private readonly AppSettings _settings;
    private readonly IApplyCoordinator _coordinator;
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
    private TweakItemViewModel? _selectedItem;

    [ObservableProperty]
    private string _selectionSummary = string.Empty;

    [ObservableProperty]
    private string _emptyMessage = string.Empty;

    [ObservableProperty]
    private bool _driftOnly;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanSummary = "Not scanned — select “Scan” to see which of these this PC already has.";

    /// <summary>Builds the page for one category over the catalogue, machine facts, the baselines, the apply launcher, settings and the scan coordinator.</summary>
    public CategoryPageViewModel(
        Category category, TweakCatalog catalog, OperatingSystemFacts facts, IReadOnlyList<Baseline> baselines, IApplyFlowLauncher applyFlow, AppSettings settings,
        IApplyCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(baselines);
        ArgumentNullException.ThrowIfNull(applyFlow);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(coordinator);
        _category = category;
        _catalog = catalog;
        _applyFlow = applyFlow;
        _settings = settings;
        _coordinator = coordinator;

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

    /// <summary>True when no entries are visible after the current search and filters (drives the empty-state hint).</summary>
    public bool IsEmpty => Items.Count == 0;

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

    /// <summary>Whether a scan can start (none is running).</summary>
    public bool CanScan => !IsScanning;

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

    /// <summary>Reads, without changing anything, which entries on this page are already in place on this PC.</summary>
    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        IsScanning = true;
        ScanSummary = "Scanning…";
        try
        {
            ScanReport report = await _coordinator.ScanAsync([.. AllItems.Select(i => i.Definition)]).ConfigureAwait(true);
            var states = report.Results.ToDictionary(r => r.Id, r => r.State);
            foreach (TweakItemViewModel item in AllItems)
            {
                item.State = states.TryGetValue(item.Id, out ComplianceState state) ? state : null;
            }

            ScanSummary = string.Create(CultureInfo.CurrentCulture,
                $"{report.CompliantCount} in place, {report.DriftCount} not applied, {report.ManagedCount} managed by your organisation (scanned {report.At.LocalDateTime:t}).");
            RebuildItems();
        }
        catch (Exception ex) when (UiFaults.IsReportable(ex))
        {
            ScanSummary = string.Create(CultureInfo.CurrentCulture, $"The scan failed: {ex.Message}");
        }
        finally
        {
            IsScanning = false;
        }
    }

    partial void OnIsScanningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanScan));
        ScanCommand.NotifyCanExecuteChanged();
    }

    partial void OnDriftOnlyChanged(bool value) => RebuildItems();

    partial void OnLevelChanged(Level value) => ApplyLevelSelection();

    partial void OnSearchTextChanged(string value) => RebuildItems();

    partial void OnRiskFilterChanged(RiskTier? value) => RebuildItems();

    partial void OnBaselineFilterChanged(Baseline? value)
    {
        _baselineTweakIds = value is null ? [] : [.. value.Rules.SelectMany(r => r.TweakIds)];
        RebuildItems();
    }

    private void ApplyLevelSelection()
    {
        // The dial selects what the named profile at this level would (Verified entries only,
        // mutually exclusive variants settled), so a dial selection always plans cleanly.
        // Draft entries stay listed and can still be ticked by hand.
        HashSet<TweakId> selectedIds = [.. NamedProfile.Select(_catalog, Level, includeDraft: false)
            .Where(e => e.Category == _category)
            .Select(e => e.Id)];
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
            if (RiskFilter is RiskTier risk && item.Risk != risk)
            {
                continue;
            }

            if (BaselineFilter is not null && !_baselineTweakIds.Contains(item.Id))
            {
                continue;
            }

            if (DriftOnly && !item.IsDrift)
            {
                continue;
            }

            if (term.Length > 0 && !item.Matches(term))
            {
                continue;
            }

            Items.Add(item);
        }

        UpdateEmptyState();
    }

    // The message shown in place of the list when nothing is visible, so an empty
    // section explains itself instead of looking broken.
    private void UpdateEmptyState()
    {
        OnPropertyChanged(nameof(IsEmpty));
        if (Items.Count > 0)
        {
            EmptyMessage = string.Empty;
        }
        else if (AllItems.Count == 0)
        {
            EmptyMessage = "No entries in this section apply to this edition of Windows.";
        }
        else
        {
            EmptyMessage = "No entries match the current search and filters.";
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
