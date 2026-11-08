using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Resources;
using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Privacy;

namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// The App access page: which apps used the camera, microphone or location (or any recorded
/// capability), most recent first, with the ones using it right now on top. Read-only; it
/// loads when the page is shown.
/// </summary>
internal sealed partial class AccessViewModel : ObservableObject, IPageViewModel, IActivatable
{
    private readonly IActivityCoordinator _coordinator;

    [ObservableProperty]
    private bool _showAll;

    [ObservableProperty]
    private string _status = string.Empty;

    /// <summary>Creates the page over the activity coordinator.</summary>
    public AccessViewModel(IActivityCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        _coordinator = coordinator;
    }

    /// <inheritdoc />
    public string Key => PageKeys.Access;

    /// <inheritdoc />
    public string Title => Strings.Nav_Access;

    /// <summary>The recorded uses.</summary>
    public ObservableCollection<CapabilityUseViewModel> Uses { get; } = [];

    /// <inheritdoc />
    public void OnActivated() => RefreshCommand.Execute(null);

    /// <summary>Reloads the recorded uses.</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IReadOnlyList<CapabilityUse> uses = await _coordinator.RecentAccessAsync(ShowAll).ConfigureAwait(true);
            Uses.Clear();
            foreach (CapabilityUse use in uses)
            {
                Uses.Add(new CapabilityUseViewModel(use));
            }

            int now = uses.Count(u => u.InUse);
            string inUse = now > 0 ? string.Create(CultureInfo.CurrentCulture, $", {now} in use now") : string.Empty;
            Status = uses.Count == 0
                ? "No recorded use."
                : string.Create(CultureInfo.CurrentCulture, $"{uses.Count} app use(s) recorded{inUse}.");
        }
        catch (Exception ex) when (UiFaults.IsReportable(ex))
        {
            Status = string.Create(CultureInfo.CurrentCulture, $"Could not read the recorded use: {ex.Message}");
        }
    }

    partial void OnShowAllChanged(bool value) => RefreshCommand.Execute(null);
}
