using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Resources;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Releases;

namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// The About page: product identity, version, homepage, the trademark notice,
/// and an opt-in GitHub Releases check that never runs until the user asks.
/// </summary>
internal sealed partial class AboutViewModel : ObservableObject, IPageViewModel
{
    private readonly IAppReleaseChecker _checker;

    [ObservableProperty]
    private string _updateStatus = "Not checked — select “Check for updates” to query GitHub Releases.";

    [ObservableProperty]
    private bool _isChecking;

    /// <summary>Creates the page over the opt-in release checker.</summary>
    public AboutViewModel(IAppReleaseChecker checker)
    {
        ArgumentNullException.ThrowIfNull(checker);
        _checker = checker;
    }

    /// <inheritdoc />
    public string Key => PageKeys.About;

    /// <inheritdoc />
    public string Title => Strings.Nav_About;

    /// <summary>The product name.</summary>
    public string ProductName { get; } = AppInfo.Title;

    /// <summary>The informational version string.</summary>
    public string Version { get; } = AppInfo.Version;

    /// <summary>The project homepage URL.</summary>
    public string Homepage { get; } = AppInfo.HomepageUri;

    /// <summary>The licence identifier.</summary>
    public string License { get; } = "MIT";

    /// <summary>The trademark and non-affiliation disclaimer.</summary>
    public string Disclaimer { get; } =
        "Open the Windows is not affiliated with, endorsed by, or sponsored by " +
        "Microsoft. “Windows” and “Microsoft” are trademarks of " +
        "Microsoft Corporation, used here only to describe what this tool configures.";

    /// <summary>Queries GitHub Releases once. Makes a network request only when invoked.</summary>
    [RelayCommand(CanExecute = nameof(CanCheck))]
    private async Task CheckForUpdatesAsync()
    {
        IsChecking = true;
        try
        {
            AppReleaseCheckResult result = await _checker.CheckAsync(CancellationToken.None).ConfigureAwait(true);
            UpdateStatus = result.Message;
        }
        finally
        {
            IsChecking = false;
        }
    }

    private bool CanCheck => !IsChecking;

    partial void OnIsCheckingChanged(bool value) => CheckForUpdatesCommand.NotifyCanExecuteChanged();
}
