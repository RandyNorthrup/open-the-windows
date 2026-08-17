namespace OpenTheWindows.Core.Catalog.Actions;

/// <summary>
/// Guarded command for the few settings that have no API. Only executables on
/// <see cref="AllowedExecutables"/> may be used; no shell, no user input
/// interpolation. Both apply and revert commands are required.
/// </summary>
/// <param name="Executable">File name of an allow-listed executable, e.g. <c>powercfg.exe</c>.</param>
/// <param name="Arguments">Arguments to apply the change.</param>
/// <param name="RevertArguments">Arguments to revert the change.</param>
public sealed record CommandAction(
    string Executable,
    IReadOnlyList<string> Arguments,
    IReadOnlyList<string> RevertArguments) : ITweakAction
{
    /// <summary>The closed allow-list of executables a catalogue command may invoke.</summary>
    public static IReadOnlySet<string> AllowedExecutables { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "powercfg.exe",
        "netsh.exe",
        "auditpol.exe",
        "secedit.exe",
        "dism.exe",
        "fsutil.exe",
        "usoclient.exe",
    };

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public string Kind => "Command";
}
