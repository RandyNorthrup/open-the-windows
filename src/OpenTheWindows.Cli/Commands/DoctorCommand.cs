using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Diagnostics;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw doctor [--json]</c>: pre-flight support and elevation check.
/// Exit code: 0 when the platform is supported, 3 when it is not. Elevation
/// does not affect the exit code (doctor is informational).
/// </summary>
internal static class DoctorCommand
{
    public static Command Create(DoctorService doctorService)
    {
        ArgumentNullException.ThrowIfNull(doctorService);

        var jsonOption = new Option<bool>("--json")
        {
            Description = "Emit the report as JSON on stdout (machine-readable, stable schema).",
        };

        var command = new Command("doctor", "Check that this machine is supported and whether the process is elevated.");
        command.Options.Add(jsonOption);
        command.SetAction(parseResult =>
        {
            SystemReport report = doctorService.Run();
            bool asJson = parseResult.GetValue(jsonOption);
            TextWriter stdout = parseResult.InvocationConfiguration.Output;

            if (asJson)
            {
                stdout.WriteLine(JsonSerializer.Serialize(report, CliJsonContext.Default.SystemReport));
            }
            else
            {
                WriteHumanReadable(stdout, report);
            }

            return report.Status == SupportStatus.Supported ? ExitCodes.Success : ExitCodes.UnsupportedPlatform;
        });

        return command;
    }

    private static void WriteHumanReadable(TextWriter writer, SystemReport report)
    {
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{AppInfo.Title} {report.AppVersion}"));
        writer.WriteLine();
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"OS:        {report.OperatingSystem.ProductName} {report.OperatingSystem.DisplayVersion} (build {report.OperatingSystem.FullBuild}, {report.OperatingSystem.Architecture})"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Edition:   {report.OperatingSystem.EditionId} / {report.OperatingSystem.InstallationType}"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"User:      {report.ProcessUserName} ({(report.IsElevated ? "elevated" : "not elevated")})"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Status:    {report.Status}"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Can apply: {(report.CanApply ? "yes" : "no")}"));

        if (report.Notes.Count > 0)
        {
            writer.WriteLine();
            foreach (string note in report.Notes)
            {
                writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  - {note}"));
            }
        }
    }
}
