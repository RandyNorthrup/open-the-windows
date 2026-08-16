using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTheWindows.Core.Diagnostics;

namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// Presents the pre-flight <see cref="SystemReport"/>. Refreshable so the
/// user can re-check after elevating or after an OS update.
/// </summary>
internal sealed partial class DoctorViewModel : ObservableObject
{
    private readonly DoctorService _doctorService;

    [ObservableProperty]
    private string _operatingSystemSummary = string.Empty;

    [ObservableProperty]
    private string _editionSummary = string.Empty;

    [ObservableProperty]
    private string _userSummary = string.Empty;

    [ObservableProperty]
    private SupportStatus _status;

    [ObservableProperty]
    private bool _canApply;

    public DoctorViewModel(DoctorService doctorService)
    {
        ArgumentNullException.ThrowIfNull(doctorService);
        _doctorService = doctorService;
        Refresh();
    }

    /// <summary>Human-readable findings from the last <see cref="Refresh"/>.</summary>
    public ObservableCollection<string> Notes { get; } = [];

    /// <summary>Re-runs the doctor and updates every bound property.</summary>
    [RelayCommand]
    public void Refresh()
    {
        SystemReport report = _doctorService.Run();

        OperatingSystemSummary =
            $"{report.OperatingSystem.ProductName} {report.OperatingSystem.DisplayVersion} " +
            $"(build {report.OperatingSystem.FullBuild}, {report.OperatingSystem.Architecture})";
        EditionSummary = $"{report.OperatingSystem.EditionId} / {report.OperatingSystem.InstallationType}";
        UserSummary = $"{report.ProcessUserName} ({(report.IsElevated ? "elevated" : "not elevated")})";
        Status = report.Status;
        CanApply = report.CanApply;

        Notes.Clear();
        foreach (string note in report.Notes)
        {
            Notes.Add(note);
        }
    }
}
