using OpenTheWindows.Core.Policy;

namespace OpenTheWindows.Windows.Readers;

/// <summary>Reads a Local Group Policy <c>Registry.pol</c> file (read-only).</summary>
internal static class LocalPolicyFile
{
    /// <summary>The machine-side Local Group Policy file.</summary>
    public static string MachinePath { get; } = Path.Combine(Environment.SystemDirectory, "GroupPolicy", "Machine", "Registry.pol");

    /// <summary>The file's entries; empty when it does not exist, <see langword="null"/> when it cannot be read or parsed.</summary>
    public static IReadOnlyList<PolicyRegistryEntry>? Entries(string policyFile)
    {
        byte[] bytes;
        try
        {
            if (!File.Exists(policyFile))
            {
                return [];
            }

            bytes = File.ReadAllBytes(policyFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        try
        {
            return PolicyRegistryFile.Parse(bytes);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>Whether the file sets <paramref name="path"/>/<paramref name="name"/> directly; <see langword="null"/> when it cannot be read.</summary>
    public static bool? Sets(string policyFile, string path, string name)
        => Entries(policyFile)?.Any(e => string.Equals(e.Key, path, StringComparison.OrdinalIgnoreCase) && string.Equals(e.ValueName, name, StringComparison.OrdinalIgnoreCase));
}
