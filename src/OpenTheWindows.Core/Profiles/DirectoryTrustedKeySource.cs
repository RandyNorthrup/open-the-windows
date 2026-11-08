namespace OpenTheWindows.Core.Profiles;

/// <summary>
/// Reads every <c>*.pem</c> file in a folder as a trusted key, with no ownership
/// check. For tests and for a caller that has already secured the folder.
/// </summary>
public sealed class DirectoryTrustedKeySource : ITrustedKeySource
{
    /// <summary>The file pattern of a trusted public key.</summary>
    public const string KeyFilePattern = "*.pem";

    /// <summary>Reads keys from <paramref name="directory"/>.</summary>
    public DirectoryTrustedKeySource(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Description = directory;
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> ReadPublicKeysPem()
        => Directory.Exists(Description) ? ReadPemFiles(Directory.EnumerateFiles(Description, KeyFilePattern)) : [];

    /// <summary>Reads each file's text; an unreadable key file is skipped, not fatal.</summary>
    public static IReadOnlyList<string> ReadPemFiles(IEnumerable<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        List<string> keys = [];
        foreach (string pem in files)
        {
            try
            {
                keys.Add(File.ReadAllText(pem));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Skipped: the remaining keys are still usable.
            }
        }

        return keys;
    }
}
