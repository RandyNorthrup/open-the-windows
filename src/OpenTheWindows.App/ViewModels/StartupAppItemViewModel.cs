using System.Globalization;
using OpenTheWindows.Core.Startup;

namespace OpenTheWindows.App.ViewModels;

/// <summary>One startup app in the Startup page list.</summary>
internal sealed class StartupAppItemViewModel
{
    /// <summary>Projects a startup app.</summary>
    public StartupAppItemViewModel(StartupApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        App = app;
        LocationLabel = app.Location switch
        {
            StartupLocation.MachineRun => "All users (registry)",
            StartupLocation.MachineRun32 => "All users (registry, 32-bit)",
            StartupLocation.UserRun => "You (registry)",
            StartupLocation.MachineStartupFolder => "All users (Startup folder)",
            StartupLocation.UserStartupFolder => "You (Startup folder)",
            _ => app.Location.ToString(),
        };
    }

    /// <summary>The app this item shows.</summary>
    public StartupApp App { get; }

    /// <summary>The app's name.</summary>
    public string Name => App.Name;

    /// <summary>What it runs.</summary>
    public string Command => App.Command;

    /// <summary>Where it is registered, in plain words.</summary>
    public string LocationLabel { get; }

    /// <summary>Whether it starts at sign-in.</summary>
    public bool Enabled => App.Enabled;

    /// <summary>"On" or "Off".</summary>
    public string StateLabel => App.Enabled ? "On" : "Off";

    /// <summary>Spoken by screen readers for the list item.</summary>
    public override string ToString() => string.Create(CultureInfo.CurrentCulture, $"{Name}, {StateLabel}, {LocationLabel}");
}
