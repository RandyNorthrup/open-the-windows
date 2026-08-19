namespace OpenTheWindows.App.ViewModels;

/// <summary>The theme the user chose in Settings; maps to the WPF Fluent <c>ThemeMode</c>.</summary>
internal enum AppTheme
{
    /// <summary>Follow the Windows system theme.</summary>
    System,

    /// <summary>Always light.</summary>
    Light,

    /// <summary>Always dark.</summary>
    Dark,
}
