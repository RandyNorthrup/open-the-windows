namespace OpenTheWindows.Core.Catalog.Actions;

/// <summary>Sets a service start type (and optionally stops it now).</summary>
/// <param name="Name">Service short name, e.g. <c>DiagTrack</c>.</param>
/// <param name="StartType">Desired start type.</param>
/// <param name="Default">Windows default start type (for "reset to defaults").</param>
/// <param name="StopIfRunning">Stop the service after changing the start type.</param>
public sealed record ServiceAction(
    string Name,
    ServiceStartType StartType,
    ServiceStartType Default,
    bool StopIfRunning) : ITweakAction
{
    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public string Kind => "Service";
}
