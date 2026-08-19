using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Resources;
using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// The Profiles page: the built-in profiles (each shown with the entries it
/// resolves to on this machine), a one-click apply that reuses the review-and-
/// apply flow, export of a profile to a file, and import of a profile file with
/// its validation result and signing state.
/// </summary>
internal sealed partial class ProfilesViewModel : ObservableObject, IPageViewModel, IActivatable
{
    // Consumer-only built-in profiles, hidden when enterprise mode is on.
    private static readonly string[] ConsumerProfileIds = ["home", "gamer"];

    private readonly TweakCatalog _catalog;
    private readonly OperatingSystemFacts _facts;
    private readonly IApplyFlowLauncher _applyFlow;
    private readonly IFileDialogService _fileDialog;
    private readonly AppSettings _settings;
    private readonly List<ProfileItemViewModel> _imported = [];

    [ObservableProperty]
    private ProfileItemViewModel? _selectedProfile;

    [ObservableProperty]
    private string _status = string.Empty;

    /// <summary>Creates the page and loads the built-in profiles.</summary>
    public ProfilesViewModel(TweakCatalog catalog, OperatingSystemFacts facts, IApplyFlowLauncher applyFlow, IFileDialogService fileDialog, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(applyFlow);
        ArgumentNullException.ThrowIfNull(fileDialog);
        ArgumentNullException.ThrowIfNull(settings);
        _catalog = catalog;
        _facts = facts;
        _applyFlow = applyFlow;
        _fileDialog = fileDialog;
        _settings = settings;

        LoadProfiles();
    }

    /// <inheritdoc />
    public void OnActivated() => LoadProfiles();

    private void LoadProfiles()
    {
        Profiles.Clear();
        IEnumerable<ProfileLoadResult> builtIns = ProfileLoader.LoadBuiltIns().Where(r =>
            r.Profile is not null
            && !(_settings.EnterpriseMode && ConsumerProfileIds.Contains(r.Profile.Id, StringComparer.Ordinal)));
        foreach (ProfileLoadResult result in builtIns)
        {
            Profiles.Add(new ProfileItemViewModel(result.Profile!, _catalog, _facts, ProfileSignatureStatus.BuiltIn, isBuiltIn: true, []));
        }

        foreach (ProfileItemViewModel imported in _imported)
        {
            Profiles.Add(imported);
        }
    }

    /// <inheritdoc />
    public string Key => PageKeys.Profiles;

    /// <inheritdoc />
    public string Title => Strings.Nav_Profiles;

    /// <summary>The built-in and imported profiles.</summary>
    public ObservableCollection<ProfileItemViewModel> Profiles { get; } = [];

    /// <summary>True when a profile is selected.</summary>
    public bool HasSelection => SelectedProfile is not null;

    /// <summary>True when the selected profile is valid and can be applied.</summary>
    public bool CanApplySelected => SelectedProfile?.IsValid == true;

    /// <summary>Opens the review-and-apply flow for the selected profile's resolved entries.</summary>
    [RelayCommand(CanExecute = nameof(CanApplySelected))]
    private void ApplySelected()
    {
        if (SelectedProfile is not { IsValid: true } item)
        {
            return;
        }

        GuiApply.Launch(_applyFlow, item.Name, item.ResolvedEntries, $"profile {item.Profile.Id}", item.Profile.Scope, _settings.CreateRestorePoint);
    }

    /// <summary>Exports the selected profile to a file.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Export()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        string? path = _fileDialog.SaveProfile(string.Create(CultureInfo.InvariantCulture, $"{SelectedProfile.Profile.Id}.json"));
        if (path is null)
        {
            return;
        }

        try
        {
            string json = JsonSerializer.Serialize(SelectedProfile.Profile, ProfileJsonContext.Default.Profile);
            File.WriteAllText(path, json);
            Status = string.Create(CultureInfo.CurrentCulture, $"Exported “{SelectedProfile.Name}” to {path}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Status = ex.Message;
        }
    }

    /// <summary>Imports a profile file, validating it and reading its signing state.</summary>
    [RelayCommand]
    private void Import()
    {
        string? path = _fileDialog.OpenProfile();
        if (path is null)
        {
            return;
        }

        ProfileLoadResult loaded = ProfileLoader.LoadFile(path);
        List<string> errors = [.. loaded.Errors.Select(e => e.Message)];
        if (loaded.Profile is null)
        {
            Status = string.Create(CultureInfo.CurrentCulture, $"Import failed: {string.Join("; ", errors)}");
            return;
        }

        errors.AddRange(ProfileValidator.Validate(loaded.Profile, _catalog, isBuiltIn: false)
            .Where(i => i.Severity == CatalogIssueSeverity.Error)
            .Select(i => i.Message));

        ProfileItemViewModel item = new(loaded.Profile, _catalog, _facts, InspectSignature(path), isBuiltIn: false, errors);
        _imported.Add(item);
        Profiles.Add(item);
        SelectedProfile = item;
        Status = string.Create(CultureInfo.CurrentCulture, $"Imported “{item.Name}” — {item.SignatureLabel}.");
    }

    private static ProfileSignatureStatus InspectSignature(string profilePath)
    {
        string signaturePath = profilePath + ProfileTrust.SignatureSuffix;
        if (!File.Exists(signaturePath))
        {
            return ProfileSignatureStatus.Unsigned;
        }

        if (!ProfileTrust.TryReadSignature(signaturePath, out ProfileSignatureDocument signature))
        {
            return ProfileSignatureStatus.Invalid;
        }

        try
        {
            string json = File.ReadAllText(profilePath);
            return ProfileTrust.VerifyAgainstTrustStore(json, signature, ProfileTrust.DefaultTrustStoreDirectory)
                ? ProfileSignatureStatus.Trusted
                : ProfileSignatureStatus.Untrusted;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ProfileSignatureStatus.Invalid;
        }
    }

    partial void OnSelectedProfileChanged(ProfileItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanApplySelected));
        ApplySelectedCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
    }
}
