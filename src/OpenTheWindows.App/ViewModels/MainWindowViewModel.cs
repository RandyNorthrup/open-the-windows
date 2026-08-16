using CommunityToolkit.Mvvm.ComponentModel;
using OpenTheWindows.Core;

namespace OpenTheWindows.App.ViewModels;

/// <summary>Window-level state: product identity and the child view models.</summary>
internal sealed class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(DoctorViewModel doctor)
    {
        ArgumentNullException.ThrowIfNull(doctor);
        Doctor = doctor;
    }

    public static string Title => AppInfo.Title;

    public static string Version => AppInfo.Version;

    public static string WindowTitle => $"{AppInfo.Title} {AppInfo.Version}";

    public DoctorViewModel Doctor { get; }
}
