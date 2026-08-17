using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows.Writers;

/// <summary>
/// Runs an allow-listed executable with an explicit argument vector. This is the
/// only component in the product that starts a process; the executable is
/// resolved to <c>%SystemRoot%\System32</c> when a bare file name is given, no
/// shell is used, and output is captured with a hard timeout.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCommandRunner : ICommandRunner
{
    private const int TimedOutExitCode = -1;

    /// <inheritdoc />
    public CommandResult Run(string executable, IReadOnlyList<string> args, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(args);

        string path = ResolveExecutable(executable);
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        // RS0030: System.Diagnostics.Process is banned in src/ except here — this guarded runner is
        // the single sanctioned process host (BannedSymbols.txt; PLAN.md section 7.3).
#pragma warning disable RS0030
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.OutputDataReceived += (_, e) => Append(stdout, e.Data);
        process.ErrorDataReceived += (_, e) => Append(stderr, e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        int budget = (int)Math.Clamp(timeout.TotalMilliseconds, 0, int.MaxValue);
        if (!process.WaitForExit(budget))
        {
            process.Kill(entireProcessTree: true);
            return new CommandResult(TimedOutExitCode, stdout.ToString().Trim(), stderr.ToString().Trim(), TimedOut: true);
        }

        // A second wait with no timeout flushes any pending asynchronous output events.
        process.WaitForExit();
        return new CommandResult(process.ExitCode, stdout.ToString().Trim(), stderr.ToString().Trim(), TimedOut: false);
#pragma warning restore RS0030
    }

    private static void Append(StringBuilder buffer, string? line)
    {
        if (line is not null)
        {
            buffer.AppendLine(line);
        }
    }

    private static string ResolveExecutable(string executable)
    {
        if (Path.IsPathRooted(executable))
        {
            return executable;
        }

        // Bare file names resolve to System32 so a spoofed executable on PATH cannot be run.
        return Path.Combine(Environment.SystemDirectory, executable);
    }
}
