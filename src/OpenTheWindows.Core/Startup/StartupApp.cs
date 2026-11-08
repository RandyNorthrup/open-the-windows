namespace OpenTheWindows.Core.Startup;

/// <summary>One app Windows starts at sign-in, as found in a Run key or a Startup folder.</summary>
/// <param name="Location">Where it is registered.</param>
/// <param name="Name">The Run value name, or the Startup-folder file name.</param>
/// <param name="Command">The command line (Run) or the file path (Startup folder).</param>
/// <param name="Enabled">Whether the Task Manager / Settings startup switch lets it start.</param>
public sealed record StartupApp(StartupLocation Location, string Name, string Command, bool Enabled)
{
    /// <summary>True when the app is registered for the signed-in user only.</summary>
    public bool IsPerUser => Location is StartupLocation.UserRun or StartupLocation.UserStartupFolder;
}
