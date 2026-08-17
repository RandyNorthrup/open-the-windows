namespace OpenTheWindows.Core.Catalog.Actions;

/// <summary>Enables or disables a scheduled task.</summary>
/// <param name="Path">Full task path, e.g. <c>\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser</c>.</param>
/// <param name="Enabled">Desired state.</param>
/// <param name="Default">Windows default state.</param>
public sealed record ScheduledTaskAction(string Path, bool Enabled, bool Default) : ITweakAction
{
    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public string Kind => "ScheduledTask";
}
