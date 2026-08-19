using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog.Actions;

namespace OpenTheWindows.Windows.Writers;

/// <summary>
/// Reads and writes Windows security policy — account, password and lockout policy,
/// which has no registry value or public API — through <c>secedit.exe</c>. Reading
/// exports the current <c>SECURITYPOLICY</c> area and parses the requested setting;
/// writing imports a minimal template that carries exactly one setting. Every
/// process launch goes through the allow-listed <see cref="ICommandRunner"/> (the
/// single sanctioned process host); temporary templates and databases are written
/// under the per-user temp directory and deleted after use.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsSecurityPolicyManager : ISecurityPolicyReader, ISecurityPolicyWriter
{
    private const string Secedit = "secedit.exe";
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);
    private readonly ICommandRunner _commands;

    /// <summary>Creates the manager over the allow-listed command runner used to invoke <c>secedit</c>.</summary>
    public WindowsSecurityPolicyManager(ICommandRunner commands)
        => _commands = commands ?? throw new ArgumentNullException(nameof(commands));

    /// <inheritdoc />
    public string? Read(SecurityPolicySection section, string setting)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setting);

        string export = TempPath("inf");
        try
        {
            CommandResult result = _commands.Run(
                Secedit, ["/export", "/cfg", export, "/areas", "SECURITYPOLICY", "/quiet"], Timeout);
            if (result.TimedOut || result.ExitCode != 0 || !File.Exists(export))
            {
                throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                    $"secedit export failed (exit {result.ExitCode}{(result.TimedOut ? ", timed out" : string.Empty)})."));
            }

            return SecurityTemplate.ReadSetting(File.ReadAllText(export, Encoding.Unicode), section, setting);
        }
        finally
        {
            TryDelete(export);
        }
    }

    /// <inheritdoc />
    public void Configure(SecurityPolicySection section, string setting, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setting);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        string inf = TempPath("inf");
        string db = TempPath("sdb");
        try
        {
            File.WriteAllText(inf, SecurityTemplate.BuildMinimal(section, setting, value), Encoding.Unicode);
            CommandResult result = _commands.Run(
                Secedit, ["/configure", "/db", db, "/cfg", inf, "/areas", "SECURITYPOLICY", "/quiet"], Timeout);
            if (result.TimedOut || result.ExitCode != 0)
            {
                throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                    $"secedit configure of '{setting}' failed (exit {result.ExitCode}{(result.TimedOut ? ", timed out" : string.Empty)})."));
            }
        }
        finally
        {
            TryDelete(inf);
            TryDelete(db);
        }
    }

    private static string TempPath(string extension)
        => Path.Combine(Path.GetTempPath(), string.Create(CultureInfo.InvariantCulture, $"otw-secpol-{Guid.NewGuid():N}.{extension}"));

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup: a leftover temp template/database is harmless and is overwritten next run.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: a temp file we cannot delete is not worth failing the operation over.
        }
    }
}
