using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Persistence;

namespace OpenTheWindows.Windows.Security;

/// <summary>
/// <see cref="IPersistenceSnapshotStore"/> keeping the baseline as
/// <c>%ProgramData%\OpenTheWindows\state\persistence-baseline.json</c>. The folder is kept
/// administrators-only before each write, the file is replaced atomically, and a baseline
/// that is not owned by an administrator (or that does not parse, or is of another schema)
/// is ignored — a planted baseline could otherwise hide an autostart from the comparison.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PersistenceSnapshotStore : IPersistenceSnapshotStore
{
    private const string FileName = "persistence-baseline.json";
    private const string TemporarySuffix = ".tmp";

    private readonly string _directory;
    private readonly ISecureDirectory? _guard;
    private readonly Func<string, bool> _isTrustedFile;

    /// <summary>Creates the store in the product's protected state folder.</summary>
    public PersistenceSnapshotStore()
        : this(WindowsDataPaths.State, new WindowsSecureDirectory(), new WindowsSecureDirectory().IsTrustedFile)
    {
    }

    /// <summary>Creates the store in <paramref name="directory"/> (tests), with an optional folder guard and file-trust check.</summary>
    internal PersistenceSnapshotStore(string directory, ISecureDirectory? guard, Func<string, bool> isTrustedFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(isTrustedFile);
        _directory = directory;
        _guard = guard;
        _isTrustedFile = isTrustedFile;
    }

    private string SnapshotPath => Path.Combine(_directory, FileName);

    /// <inheritdoc />
    public PersistenceSnapshot? Load()
    {
        string path = SnapshotPath;
        if (!File.Exists(path) || !_isTrustedFile(path))
        {
            return null;
        }

        try
        {
            PersistenceSnapshot? snapshot = JsonSerializer.Deserialize(File.ReadAllText(path, Encoding.UTF8), PersistenceJsonContext.Default.PersistenceSnapshot);
            return snapshot?.SchemaVersion == PersistenceSnapshot.CurrentSchemaVersion ? snapshot : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Save(PersistenceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (_guard is null)
        {
            Directory.CreateDirectory(_directory);
        }
        else
        {
            _guard.EnsureSafeForWrite(_directory);
        }

        string temporary = SnapshotPath + TemporarySuffix;
        File.WriteAllText(temporary, JsonSerializer.Serialize(snapshot, PersistenceJsonContext.Default.PersistenceSnapshot), Encoding.UTF8);
        File.Move(temporary, SnapshotPath, overwrite: true);
    }
}
