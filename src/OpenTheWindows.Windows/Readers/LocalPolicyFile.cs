using OpenTheWindows.Core.Policy;

namespace OpenTheWindows.Windows.Readers;

/// <summary>Reads a Local Group Policy <c>Registry.pol</c> file (read-only).</summary>
internal static class LocalPolicyFile
{
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
}
