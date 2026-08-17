using System.Globalization;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.Windows;

/// <summary>
/// Persists journals under <c>%ProgramData%\OpenTheWindows\journal</c> with a
/// restrictive ACL (Administrators and SYSTEM full control, Users read-only),
/// applied when the directory is created. Files are named
/// <c>&lt;yyyyMMdd-HHmmss&gt;-&lt;runId&gt;.json</c> and hash-chained so tampering
/// is evident.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsJournalStore : IJournalStore
{
    private readonly string _directory;

    /// <summary>Creates a store rooted at the default ProgramData journal directory.</summary>
    public WindowsJournalStore()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OpenTheWindows", "journal"))
    {
    }

    /// <summary>Creates a store rooted at <paramref name="directory"/> (injectable for tests).</summary>
    public WindowsJournalStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    /// <inheritdoc />
    public void Begin(JournalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        EnsureDirectory();
        record.PreviousHash = NewestHash();
        record.Hash = JournalSerializer.ComputeHash(record);
        File.WriteAllText(PathFor(record), JournalSerializer.Serialize(record));
    }

    /// <inheritdoc />
    public void Update(JournalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        record.Hash = JournalSerializer.ComputeHash(record);
        File.WriteAllText(PathFor(record), JournalSerializer.Serialize(record));
    }

    /// <inheritdoc />
    public IReadOnlyList<JournalSummary> List()
    {
        if (!Directory.Exists(_directory))
        {
            return [];
        }

        List<(DateTimeOffset At, JournalSummary Summary)> summaries = [];
        foreach (string file in Directory.EnumerateFiles(_directory, "*.json"))
        {
            JournalRecord record = JournalSerializer.Deserialize(File.ReadAllText(file));
            summaries.Add((record.StartedAt,
                new JournalSummary(record.RunId, record.StartedAt, record.Profile, record.Result, record.Entries.Count, record.RevertOf)));
        }

        return [.. summaries.OrderByDescending(s => s.At).Select(s => s.Summary)];
    }

    /// <inheritdoc />
    public JournalRecord Load(Guid runId)
    {
        string suffix = string.Create(CultureInfo.InvariantCulture, $"-{runId:D}.json");
        string file = (Directory.Exists(_directory)
                ? Directory.EnumerateFiles(_directory, "*.json").FirstOrDefault(f => f.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                : null)
            ?? throw new FileNotFoundException(string.Create(CultureInfo.InvariantCulture, $"No journal found for run {runId}."));

        return JournalSerializer.Deserialize(File.ReadAllText(file));
    }

    private string PathFor(JournalRecord record)
    {
        string stamp = record.StartedAt.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return Path.Combine(_directory, string.Create(CultureInfo.InvariantCulture, $"{stamp}-{record.RunId:D}.json"));
    }

    private string? NewestHash()
    {
        string? newest = null;
        DateTimeOffset newestAt = DateTimeOffset.MinValue;
        foreach (string file in Directory.EnumerateFiles(_directory, "*.json"))
        {
            JournalRecord record = JournalSerializer.Deserialize(File.ReadAllText(file));
            if (record.StartedAt >= newestAt)
            {
                newestAt = record.StartedAt;
                newest = record.Hash;
            }
        }

        return newest;
    }

    private void EnsureDirectory()
    {
        if (Directory.Exists(_directory))
        {
            return;
        }

        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        AddRule(security, WellKnownSidType.BuiltinAdministratorsSid, FileSystemRights.FullControl);
        AddRule(security, WellKnownSidType.LocalSystemSid, FileSystemRights.FullControl);
        AddRule(security, WellKnownSidType.BuiltinUsersSid, FileSystemRights.ReadAndExecute);

        // The creating identity (the elevated operator in production) keeps full control so the
        // process that owns the journal can always write it; standard users still get read-only.
        SecurityIdentifier? owner = WindowsIdentity.GetCurrent().User;
        if (owner is not null)
        {
            security.AddAccessRule(new FileSystemAccessRule(
                owner,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }

        var directory = new DirectoryInfo(_directory);
        // Create parents first (they inherit their own defaults), then the journal directory with the restrictive ACL.
        directory.Parent?.Create();
        FileSystemAclExtensions.Create(directory, security);
    }

    private static void AddRule(DirectorySecurity security, WellKnownSidType sid, FileSystemRights rights)
        => security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(sid, null),
            rights,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
}
