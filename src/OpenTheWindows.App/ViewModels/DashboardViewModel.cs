using CommunityToolkit.Mvvm.Input;
using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Resources;
using OpenTheWindows.App.Services;

namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// The landing page: system health (the pre-flight <see cref="DoctorViewModel"/>)
/// and quick actions that jump to the category pages. Last-scan drift is added
/// once scan integration lands.
/// </summary>
internal sealed partial class DashboardViewModel : IPageViewModel
{
    private readonly INavigationService _navigation;

    /// <summary>Builds the dashboard over the shared doctor view model and navigation.</summary>
    public DashboardViewModel(DoctorViewModel doctor, INavigationService navigation)
    {
        ArgumentNullException.ThrowIfNull(doctor);
        ArgumentNullException.ThrowIfNull(navigation);
        Doctor = doctor;
        _navigation = navigation;
    }

    /// <inheritdoc />
    public string Key => PageKeys.Dashboard;

    /// <inheritdoc />
    public string Title => Strings.Nav_Dashboard;

    /// <summary>The system health / pre-flight panel shown on the dashboard.</summary>
    public DoctorViewModel Doctor { get; }

    /// <summary>Navigates to the page identified by <paramref name="pageKey"/>.</summary>
    [RelayCommand]
    private void Open(string pageKey) => _navigation.NavigateTo(pageKey);
}
