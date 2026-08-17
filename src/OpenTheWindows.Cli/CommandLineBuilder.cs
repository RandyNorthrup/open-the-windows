using System.CommandLine;
using OpenTheWindows.Cli.Commands;
using OpenTheWindows.Core;

namespace OpenTheWindows.Cli;

/// <summary>
/// Composes the command tree. Kept separate from <c>Program</c> so tests can
/// build and invoke the same tree with captured output.
/// </summary>
internal static class CommandLineBuilder
{
    /// <summary>Builds the root command with production dependencies.</summary>
    public static RootCommand Build() => Build(CliServices.CreateDefault());

    /// <summary>Builds the root command with injected services (tests).</summary>
    public static RootCommand Build(CliServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var root = new RootCommand($"{AppInfo.Title}: Windows 11 privacy, update, security and performance control.");
        root.Subcommands.Add(DoctorCommand.Create(services.Doctor));
        root.Subcommands.Add(CatalogCommand.Create(services.LoadCatalog));
        root.Subcommands.Add(ScanCommand.Create(services));
        root.Subcommands.Add(HealthCommand.Create(services.Health));
        root.Subcommands.Add(ApplyCommand.Create(services));
        root.Subcommands.Add(RevertCommand.Create(services));
        root.Subcommands.Add(HistoryCommand.Create(services));
        return root;
    }
}
