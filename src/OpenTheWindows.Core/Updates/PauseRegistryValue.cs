namespace OpenTheWindows.Core.Updates;

/// <summary>
/// One registry value the Pause button writes: the value name under
/// <see cref="WindowsUpdateSettings.UxSettingsPath"/> and its ISO-8601 UTC data.
/// </summary>
/// <param name="Name">The registry value name (e.g. <c>PauseUpdatesExpiryTime</c>).</param>
/// <param name="Value">The ISO-8601 UTC timestamp, formatted <c>yyyy-MM-ddTHH:mm:ssZ</c>.</param>
public sealed record PauseRegistryValue(string Name, string Value);
