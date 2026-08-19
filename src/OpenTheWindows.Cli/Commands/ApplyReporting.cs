using System.Globalization;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// The shared apply-result plumbing used by both <c>otw apply</c> and
/// <c>otw remediate</c>: how a resolved profile's options seed an
/// <see cref="ApplyOptions"/>, how an <see cref="ApplyResult"/> maps to a process
/// exit code, and how it is rendered as text or the stable JSON report.
/// </summary>
internal static class ApplyReporting
{
    /// <summary>
    /// Builds the run options: the CLI flag values with a resolved profile's own
    /// <paramref name="po"/> as the defaults. The enable-switches
    /// (advanced/breaking/restart-explorer) turn a behaviour on; the restore point
    /// is created unless <paramref name="noRestorePoint"/> or the profile opts out.
    /// A level name or <c>--only</c> passes <paramref name="po"/> as
    /// <see langword="null"/>, so the flags decide alone.
    /// </summary>
    public static ApplyOptions BuildOptions(
        bool whatIf,
        bool noRestorePoint,
        bool breakGlass,
        bool allowAdvanced,
        bool allowBreaking,
        bool restartExplorer,
        string profileName,
        ProfileOptions? po,
        Scope scope = Scope.Machine)
        => new(
            whatIf,
            !noRestorePoint && (po?.RestorePoint ?? true),
            breakGlass,
            allowAdvanced || (po?.AllowAdvanced ?? false),
            allowBreaking || (po?.AllowBreaking ?? false),
            profileName,
            restartExplorer || (po?.RestartExplorer ?? false),
            scope);

    /// <summary>
    /// Maps an apply/revert-style result to a process exit code: 4 on plan errors,
    /// 5/2 on failure (elevation vs other), 2 on rollback, 3010 when a real (not
    /// <paramref name="whatIf"/>) run needs a reboot, else 0.
    /// </summary>
    public static int ExitCode(ApplyResult result, bool whatIf)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Plan.HasErrors)
        {
            return ExitCodes.InvalidInput;
        }

        if (result.State == RunState.Failed)
        {
            return result.Preflight.Blocking.Any(i => i.Check == PreflightCheck.Elevation)
                ? ExitCodes.ElevationRequired
                : ExitCodes.Error;
        }

        if (result.State == RunState.RolledBack)
        {
            return ExitCodes.Error;
        }

        // A --what-if run changes nothing, so it never itself requires a reboot: the
        // preview succeeded. The would-be reboot requirement is still reported in the
        // text/JSON output; only an actual apply returns the 3010 reboot-required code.
        if (whatIf)
        {
            return ExitCodes.Success;
        }

        return result.Requires == RestartRequirement.Reboot ? ExitCodes.RebootRequired : ExitCodes.Success;
    }

    /// <summary>The stable JSON report for a run (same schema for apply and remediate).</summary>
    public static ApplyJsonReport ToJson(ApplyResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new(
            result.RunId.ToString(),
            result.State.ToString(),
            result.Requires.ToString(),
            new ApplyJsonRestore(result.RestorePoint.Status.ToString(), result.RestorePoint.SequenceNumber),
            result.Plan.Errors,
            [.. result.Entries.Select(e => new ApplyJsonEntry(e.Id.Value, e.Result.ToString(), e.Risk.ToString(), e.Requires.ToString(), e.Note))]);
    }

    /// <summary>Renders a run as the human-readable text summary.</summary>
    public static void WriteText(ApplyResult result, bool whatIf, TextWriter stdout)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(stdout);

        if (result.Plan.HasErrors)
        {
            foreach (string error in result.Plan.Errors)
            {
                stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  ERROR  {error}"));
            }

            return;
        }

        foreach (PreflightIssue issue in result.Preflight.Blocking)
        {
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  BLOCKED  {issue.Check}: {issue.Detail}"));
        }

        string verb = whatIf ? "Would apply" : "Applied";
        foreach (EntryOutcome entry in result.Entries)
        {
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {entry.Result,-13} {entry.Id.Value}{Suffix(entry.Note)}"));
        }

        stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{verb}: {result.State}. Restart requirement: {result.Requires}. Restore point: {result.RestorePoint.Status}."));
    }

    private static string Suffix(string? note) => note is null ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"  ({note})");
}
