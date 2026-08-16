using System.CommandLine;
using OpenTheWindows.Cli.Commands;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.Windows;

namespace OpenTheWindows.Cli;

/// <summary>
/// Composes the command tree. Kept separate from <c>Program</c> so tests can
/// build and invoke the same tree with captured output.
/// </summary>
internal static class CommandLineBuilder
{
    /// <summary>Builds the root command with production dependencies.</summary>
    public static RootCommand Build()
        => Build(new DoctorService(new WindowsOperatingSystemInfo(), new WindowsElevationContext()));

    /// <summary>Builds the root command with injected services (tests).</summary>
    public static RootCommand Build(DoctorService doctorService)
    {
        ArgumentNullException.ThrowIfNull(doctorService);

        var root = new RootCommand($"{AppInfo.Title}: Windows 11 privacy, update, security and performance control.");
        root.Subcommands.Add(DoctorCommand.Create(doctorService));
        return root;
    }
}
