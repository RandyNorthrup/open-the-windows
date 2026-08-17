namespace OpenTheWindows.Core.Catalog.Actions;

/// <summary>Enables or disables a Windows optional feature (DISM feature name).</summary>
/// <param name="Name">Feature name as shown by <c>Get-WindowsOptionalFeature</c>.</param>
/// <param name="Enabled">Desired state.</param>
/// <param name="Default">Windows default state.</param>
public sealed record OptionalFeatureAction(string Name, bool Enabled, bool Default) : ITweakAction
{
    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public string Kind => "OptionalFeature";
}
