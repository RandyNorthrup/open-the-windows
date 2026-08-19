using OpenTheWindows.App.Services;
using OpenTheWindows.App.ViewModels;

namespace OpenTheWindows.App.Tests.Fakes;

/// <summary>Records the theme the settings page applied.</summary>
internal sealed class FakeThemeService : IThemeService
{
    /// <summary>The last theme applied, if any.</summary>
    public AppTheme? LastApplied { get; private set; }

    /// <summary>How many times <see cref="Apply"/> was called.</summary>
    public int ApplyCount { get; private set; }

    /// <inheritdoc />
    public void Apply(AppTheme theme)
    {
        ApplyCount++;
        LastApplied = theme;
    }
}
