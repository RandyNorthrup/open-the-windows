using System.Globalization;
using System.Runtime.Versioning;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows.Writers;

/// <summary>
/// Enables or disables a Windows optional feature by driving the in-box
/// <c>dism.exe</c> through the guarded command runner (<c>dism.exe</c> is on the
/// command allow-list). This avoids a fragile hand-rolled binding to the DISM
/// API while producing the same result; a reboot requirement is surfaced by the
/// entry's declared <c>Requires</c>, so exit code 3010 is treated as success.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsOptionalFeatureWriter : IOptionalFeatureWriter
{
    private const int RebootRequiredExitCode = 3010;
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);

    private readonly ICommandRunner _runner;

    /// <summary>Creates the writer over the guarded command runner.</summary>
    public WindowsOptionalFeatureWriter(ICommandRunner runner)
        => _runner = runner ?? throw new ArgumentNullException(nameof(runner));

    /// <inheritdoc />
    public void SetEnabled(string name, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string verb = enabled ? "/enable-feature" : "/disable-feature";
        IReadOnlyList<string> args =
        [
            "/online",
            verb,
            string.Create(CultureInfo.InvariantCulture, $"/featurename:{name}"),
            "/norestart",
        ];

        CommandResult result = _runner.Run("dism.exe", args, Timeout);
        if (result.ExitCode is not (0 or RebootRequiredExitCode))
        {
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                $"dism {verb} for feature '{name}' failed (exit {result.ExitCode}): {result.StandardError}"));
        }
    }
}
