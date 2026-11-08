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
/// Adding a capability (Feature on Demand) downloads it from Windows Update, which
/// takes well over ten minutes for some, so that verb gets a longer timeout.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsOptionalFeatureWriter : IOptionalFeatureWriter
{
    private const int RebootRequiredExitCode = 3010;

    // CBS_E_UNKNOWN_UPDATE: Windows has no feature of that name (for example, removing its
    // capability took the feature with it). For a disable that already is the wanted state.
    private const int UnknownFeatureExitCode = unchecked((int)0x800F080C);

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AddCapabilityTimeout = TimeSpan.FromMinutes(60);

    private readonly ICommandRunner _runner;

    /// <summary>Creates the writer over the guarded command runner.</summary>
    public WindowsOptionalFeatureWriter(ICommandRunner runner)
        => _runner = runner ?? throw new ArgumentNullException(nameof(runner));

    /// <inheritdoc />
    public void SetEnabled(string name, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // A name carrying ~~~~ is a Windows capability (Feature on Demand), managed with the capability verbs.
        bool capability = Readers.DismState.IsCapability(name);
        string verb = (capability, enabled) switch
        {
            (true, true) => "/add-capability",
            (true, false) => "/remove-capability",
            (false, true) => "/enable-feature",
            (false, false) => "/disable-feature",
        };
        string nameSwitch = capability ? "/capabilityname:" : "/featurename:";
        IReadOnlyList<string> args =
        [
            "/online",
            verb,
            string.Create(CultureInfo.InvariantCulture, $"{nameSwitch}{name}"),
            "/norestart",
        ];

        CommandResult result = _runner.Run("dism.exe", args, capability && enabled ? AddCapabilityTimeout : Timeout);
        if (result.TimedOut)
        {
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                $"dism {verb} for feature '{name}' did not finish in time and was stopped."));
        }

        if (!capability && !enabled && result.ExitCode == UnknownFeatureExitCode)
        {
            return;
        }

        if (result.ExitCode is not (0 or RebootRequiredExitCode))
        {
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                $"dism {verb} for feature '{name}' failed (exit {result.ExitCode}): {result.StandardError}"));
        }
    }
}
