using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Windows.Security;

namespace OpenTheWindows.Windows;

/// <summary>
/// Persists journals as <c>&lt;yyyyMMdd-HHmmss&gt;-&lt;runId&gt;.json</c>. The machine
/// store (the default constructor) lives under <c>%ProgramData%\OpenTheWindows\journal</c>,
/// keeps that folder administrator-owned and administrator-writable, and MACs every
/// record under a key only administrators and SYSTEM can read; a file whose MAC, hash
/// or owner does not check out is listed as tampered and never replayed. Every write
/// goes to a temporary file that is flushed to disk and then moved over the journal,
/// so a crash leaves either the previous or the new record, never a torn one.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsJournalStore : IJournalStore
{
    private const string JournalExtension = ".json";
    private const string TemporaryExtension = ".tmp";
    private const string StampFormat = "yyyyMMdd-HHmmss";
    private const string UnreadableProfile = "(unreadable)";

    private readonly string _directory;
    private readonly IJournalProtection _protection;

    /// <summary>Creates the machine store at the default ProgramData journal folder.</summary>
    public WindowsJournalStore()
        : this(WindowsDataPaths.Journal, new MachineJournalProtection())
    {
    }

    /// <summary>
    /// Creates an unprotected store at <paramref name="directory"/>: plain hash chain, no
    /// folder hardening. For the per-user journal (which records changes to the caller's
    /// own hive only) and for tests.
    /// </summary>
    public WindowsJournalStore(string directory)
        : this(directory, NoJournalProtection.Instance)
    {
    }

    internal WindowsJournalStore(string directory, IJournalProtection protection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        _protection = protection ?? throw new ArgumentNullException(nameof(protection));
    }

    /// <summary>The per-user journal folder (<c>%LocalAppData%\OpenTheWindows\journal</c>) used by unelevated per-user runs.</summary>
    public static string UserDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenTheWindows", "journal");

    /// <inheritdoc />
    public void Begin(JournalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _protection.PrepareForWrite(_directory);
        record.Keyed = _protection.Key(create: true) is null ? null : true;
        record.PreviousHash = NewestHash();
        Seal(record);
        WriteAtomically(PathFor(record), JournalSerializer.Serialize(record));
    }

    /// <inheritdoc />
    public void Update(JournalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        Seal(record);
        WriteAtomically(PathFor(record), JournalSerializer.Serialize(record));
    }

    /// <inheritdoc />
    public IReadOnlyList<JournalSummary> List()
    {
        if (!Directory.Exists(_directory))
        {
            return [];
        }

        bool trustworthy = _protection.IsTrustworthy(_directory);
        List<JournalSummary> summaries = [.. JournalFiles().Select(file => Summarize(file, trustworthy))];
        return [.. summaries.OrderByDescending(s => s.StartedAt)];
    }

    /// <inheritdoc />
    /// <exception cref="FileNotFoundException">No journal exists for <paramref name="runId"/>.</exception>
    /// <exception cref="JournalIntegrityException">The journal exists but does not verify.</exception>
    public JournalRecord Load(Guid runId)
    {
        string suffix = string.Create(CultureInfo.InvariantCulture, $"-{runId:D}{JournalExtension}");
        string file = (Directory.Exists(_directory) ? JournalFiles().FirstOrDefault(f => f.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) : null)
            ?? throw new FileNotFoundException(string.Create(CultureInfo.InvariantCulture, $"No journal found for run {runId}."));

        (JournalRecord? record, JournalIntegrity integrity) = Read(file, _protection.IsTrustworthy(_directory));
        if (record is null || integrity != JournalIntegrity.Verified)
        {
            string reason = integrity switch
            {
                JournalIntegrity.Unverified => "cannot be verified without elevation; run elevated so its signature can be checked",
                JournalIntegrity.Unreadable => "unreadable (damaged or incomplete) and will not be replayed",
                _ => "tampered with (its signature, hash or owner does not check out) and will not be replayed",
            };
            throw new JournalIntegrityException(string.Create(CultureInfo.InvariantCulture, $"The journal for run {runId} is {reason}."));
        }

        return record;
    }

    private JournalSummary Summarize(string file, bool trustworthy)
    {
        (JournalRecord? record, JournalIntegrity integrity) = Read(file, trustworthy);
        if (record is not null)
        {
            return new JournalSummary(record.RunId, record.StartedAt, record.Profile, record.Result, record.Entries.Count, record.RevertOf, integrity);
        }

        (Guid runId, DateTimeOffset startedAt) = ParseFileName(file);
        return new JournalSummary(runId, startedAt, UnreadableProfile, RunState.Failed, 0, null, JournalIntegrity.Unreadable);
    }

    private (JournalRecord? Record, JournalIntegrity Integrity) Read(string file, bool trustworthy)
    {
        JournalRecord record;
        try
        {
            record = JournalSerializer.Deserialize(File.ReadAllText(file, Encoding.UTF8));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return (null, JournalIntegrity.Unreadable);
        }

        if (!trustworthy || !_protection.IsTrustedFile(file))
        {
            return (record, JournalIntegrity.Tampered);
        }

        byte[]? key = _protection.Key(create: false);
        if (record.Keyed == true && key is null)
        {
            return (record, JournalIntegrity.Unverified);
        }

        return JournalSerializer.Verify(record, key ?? []) ? (record, JournalIntegrity.Verified) : (record, JournalIntegrity.Tampered);
    }

    private void Seal(JournalRecord record)
    {
        if (record.Keyed == true)
        {
            byte[] key = _protection.Key(create: false)
                ?? throw new UnauthorizedAccessException("The journal key could not be read; the journal cannot be sealed.");
            record.Hash = JournalSerializer.ComputeMac(record, key);
        }
        else
        {
            record.Hash = JournalSerializer.ComputeHash(record);
        }
    }

    private IEnumerable<string> JournalFiles()
        => Directory.EnumerateFiles(_directory, "*" + JournalExtension)
            .Where(f => string.Equals(Path.GetExtension(f), JournalExtension, StringComparison.OrdinalIgnoreCase));

    private string PathFor(JournalRecord record)
    {
        string stamp = record.StartedAt.UtcDateTime.ToString(StampFormat, CultureInfo.InvariantCulture);
        return Path.Combine(_directory, string.Create(CultureInfo.InvariantCulture, $"{stamp}-{record.RunId:D}{JournalExtension}"));
    }

    private string? NewestHash()
    {
        bool trustworthy = _protection.IsTrustworthy(_directory);
        string? newest = null;
        DateTimeOffset newestAt = DateTimeOffset.MinValue;
        foreach (string file in JournalFiles())
        {
            (JournalRecord? record, JournalIntegrity integrity) = Read(file, trustworthy);
            if (record is not null && integrity == JournalIntegrity.Verified && record.StartedAt >= newestAt)
            {
                newestAt = record.StartedAt;
                newest = record.Hash;
            }
        }

        return newest;
    }

    /// <summary>Writes <paramref name="content"/> to a temporary file, flushes it to disk, then atomically replaces <paramref name="path"/>.</summary>
    private static void WriteAtomically(string path, string content)
    {
        string temporary = path + TemporaryExtension;
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, bytes.Length, FileOptions.WriteThrough))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temporary, path, overwrite: true);
    }

    private static (Guid RunId, DateTimeOffset StartedAt) ParseFileName(string file)
    {
        string name = Path.GetFileNameWithoutExtension(file);
        Guid runId = name.Length > StampFormat.Length + 1 && Guid.TryParse(name[(StampFormat.Length + 1)..], out Guid parsed) ? parsed : Guid.Empty;
        DateTimeOffset startedAt = name.Length >= StampFormat.Length
            && DateTime.TryParseExact(name[..StampFormat.Length], StampFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime stamp)
            ? new DateTimeOffset(stamp, TimeSpan.Zero)
            : DateTimeOffset.MinValue;
        return (runId, startedAt);
    }
}
