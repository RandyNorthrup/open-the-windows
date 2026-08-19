using System.Globalization;
using System.Text;
using OpenTheWindows.Core.Catalog.Actions;

namespace OpenTheWindows.Windows.Writers;

/// <summary>
/// Parses and builds the minimal <c>secedit</c> security-template (<c>.inf</c>)
/// fragments the security-policy manager exports and imports. Pure text logic,
/// factored out so the parsing and the template shape are unit-testable without
/// running <c>secedit</c>.
/// </summary>
internal static class SecurityTemplate
{
    /// <summary>The template section header for a policy section (for example <c>[System Access]</c>).</summary>
    internal static string SectionHeader(SecurityPolicySection section) => section switch
    {
        SecurityPolicySection.SystemAccess => "[System Access]",
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Unsupported security-policy section."),
    };

    /// <summary>
    /// Reads the value of <paramref name="setting"/> within <paramref name="section"/>
    /// from <paramref name="templateText"/> (a <c>secedit</c> export), or
    /// <see langword="null"/> when the section does not list it.
    /// </summary>
    internal static string? ReadSetting(string templateText, SecurityPolicySection section, string setting)
    {
        ArgumentNullException.ThrowIfNull(templateText);
        string header = SectionHeader(section);
        bool inSection = false;

        foreach (string raw in templateText.Split('\n'))
        {
            string line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inSection = string.Equals(line, header, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inSection || line.Length == 0)
            {
                continue;
            }

            int eq = line.IndexOf('=', StringComparison.Ordinal);
            if (eq <= 0)
            {
                continue;
            }

            if (string.Equals(line[..eq].Trim(), setting, StringComparison.OrdinalIgnoreCase))
            {
                return line[(eq + 1)..].Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Builds a minimal security template that sets exactly one
    /// <paramref name="setting"/> in <paramref name="section"/> to
    /// <paramref name="value"/>, suitable for <c>secedit /configure</c>.
    /// </summary>
    internal static string BuildMinimal(SecurityPolicySection section, string setting, string value)
    {
        var builder = new StringBuilder();
        builder.Append("[Unicode]\r\n");
        builder.Append("Unicode=yes\r\n");
        builder.Append("[Version]\r\n");
        builder.Append("signature=\"$CHICAGO$\"\r\n");
        builder.Append("Revision=1\r\n");
        builder.Append(SectionHeader(section)).Append("\r\n");
        builder.Append(CultureInfo.InvariantCulture, $"{setting} = {value}\r\n");
        return builder.ToString();
    }
}
