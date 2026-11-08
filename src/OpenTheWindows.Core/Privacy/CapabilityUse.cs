namespace OpenTheWindows.Core.Privacy;

/// <summary>When an app last used a privacy-sensitive capability (camera, microphone, location…).</summary>
/// <param name="Capability">The ConsentStore capability name (for example <c>webcam</c>, <c>microphone</c>, <c>location</c>).</param>
/// <param name="App">The package family name of a Store app, or the executable path of a desktop app.</param>
/// <param name="IsDesktopApp">True for a desktop (non-packaged) app.</param>
/// <param name="PerUser">True when recorded for the signed-in user; false for the machine-wide record.</param>
/// <param name="LastStarted">When the app last started using the capability, if recorded.</param>
/// <param name="LastStopped">When it last stopped, if recorded.</param>
public sealed record CapabilityUse(
    string Capability,
    string App,
    bool IsDesktopApp,
    bool PerUser,
    DateTimeOffset? LastStarted,
    DateTimeOffset? LastStopped)
{
    /// <summary>True when the app started using the capability and has not stopped (it is using it now).</summary>
    public bool InUse => LastStarted is not null && LastStopped is null;
}
