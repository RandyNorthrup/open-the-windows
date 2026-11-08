using System.Globalization;
using System.IO;
using System.Text;

namespace OpenTheWindows.App.Services;

/// <summary>
/// Appends an unexpected GUI error to <c>%LocalAppData%\OpenTheWindows\gui-errors.log</c>
/// so a fault the user dismissed can still be reported. Failing to write the log never
/// throws: the caller is already handling an error.
/// </summary>
internal static class GuiErrorLog
{
    /// <summary>The default log location.</summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenTheWindows", "gui-errors.log");

    /// <summary>Appends <paramref name="error"/> (with its type, message and stack) under a UTC timestamp; returns whether it was written.</summary>
    public static bool Append(string path, DateTimeOffset at, Exception error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(error);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(
                path,
                string.Create(CultureInfo.InvariantCulture, $"[{at.UtcDateTime:u}] {error}{Environment.NewLine}{Environment.NewLine}"),
                Encoding.UTF8);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
