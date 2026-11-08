using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// Exercises the journal store against a real temporary directory (safe on any
/// machine): write-before-read, hash chain across runs, newest-first listing and
/// load-by-run-id. The restrictive ACL is applied on directory creation.
/// </summary>
public sealed class WindowsJournalStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("otw-journal-test").FullName;

    private WindowsJournalStore NewStore() => new(Path.Combine(_root, "journal"));

    private static JournalRecord Record(Guid runId, DateTimeOffset startedAt) => new()
    {
        RunId = runId,
        StartedAt = startedAt,
        AppVersion = "0.2.0",
        Os = new JournalOs(26100, 4351, "Professional"),
        Profile = "balanced",
        Operator = @"TEST\tester",
        RestorePoint = new JournalRestorePoint(false, false, "NotRequested", null),
        Entries = [],
        Result = RunState.Completed,
    };

    [Fact]
    public void Begin_writes_a_loadable_journal()
    {
        WindowsJournalStore store = NewStore();
        var runId = Guid.NewGuid();

        store.Begin(Record(runId, new DateTimeOffset(2026, 8, 17, 1, 0, 0, TimeSpan.Zero)));

        JournalRecord loaded = store.Load(runId);
        Assert.Equal(runId, loaded.RunId);
        Assert.True(JournalSerializer.VerifyHash(loaded));
    }

    [Fact]
    public void Successive_runs_chain_and_verify()
    {
        WindowsJournalStore store = NewStore();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        store.Begin(Record(first, new DateTimeOffset(2026, 8, 17, 1, 0, 0, TimeSpan.Zero)));
        JournalRecord firstStored = store.Load(first);
        store.Begin(Record(second, new DateTimeOffset(2026, 8, 17, 2, 0, 0, TimeSpan.Zero)));

        JournalRecord secondStored = store.Load(second);
        Assert.Null(firstStored.PreviousHash);
        Assert.Equal(firstStored.Hash, secondStored.PreviousHash);
    }

    [Fact]
    public void List_returns_runs_newest_first()
    {
        WindowsJournalStore store = NewStore();
        var older = Guid.NewGuid();
        var newer = Guid.NewGuid();
        store.Begin(Record(older, new DateTimeOffset(2026, 8, 17, 1, 0, 0, TimeSpan.Zero)));
        store.Begin(Record(newer, new DateTimeOffset(2026, 8, 17, 3, 0, 0, TimeSpan.Zero)));

        IReadOnlyList<JournalSummary> list = store.List();

        Assert.Equal(2, list.Count);
        Assert.Equal(newer, list[0].RunId);
        Assert.Equal(older, list[1].RunId);
    }

    [Fact]
    public void Load_of_a_missing_run_throws()
    {
        WindowsJournalStore store = NewStore();
        store.Begin(Record(Guid.NewGuid(), new DateTimeOffset(2026, 8, 17, 1, 0, 0, TimeSpan.Zero)));

        Assert.Throws<FileNotFoundException>(() => store.Load(Guid.NewGuid()));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class TestProtection(byte[]? key, Func<string, bool> trusted) : Security.IJournalProtection
    {
        public void PrepareForWrite(string directory) => Directory.CreateDirectory(directory);

        public bool IsTrustworthy(string directory) => true;

        public bool IsTrustedFile(string path) => trusted(path);

        public byte[]? Key(bool create) => key;
    }

    private static readonly byte[] MachineKey = [.. Enumerable.Range(1, 32).Select(i => (byte)i)];

    private WindowsJournalStore KeyedStore(byte[]? key = null, Func<string, bool>? trusted = null)
        => new(Path.Combine(_root, "journal"), new TestProtection(key ?? MachineKey, trusted ?? (_ => true)));

    private string OnlyJournalFile() => Assert.Single(Directory.GetFiles(Path.Combine(_root, "journal"), "*.json"));

    [Fact]
    public void A_machine_keyed_journal_is_sealed_with_a_mac_and_verifies()
    {
        WindowsJournalStore store = KeyedStore();
        var runId = Guid.NewGuid();
        store.Begin(Record(runId, new DateTimeOffset(2026, 8, 17, 1, 0, 0, TimeSpan.Zero)));

        JournalRecord loaded = store.Load(runId);

        Assert.True(loaded.Keyed);
        Assert.True(JournalSerializer.Verify(loaded, MachineKey));
        Assert.Equal(JournalIntegrity.Verified, Assert.Single(store.List()).Integrity);
    }

    [Fact]
    public void An_edited_journal_is_listed_as_tampered_and_never_loaded()
    {
        WindowsJournalStore store = KeyedStore();
        var runId = Guid.NewGuid();
        store.Begin(Record(runId, new DateTimeOffset(2026, 8, 17, 1, 0, 0, TimeSpan.Zero)));
        string file = OnlyJournalFile();
        File.WriteAllText(file, File.ReadAllText(file).Replace("\"balanced\"", "\"forged\"", StringComparison.Ordinal));

        Assert.Equal(JournalIntegrity.Tampered, Assert.Single(store.List()).Integrity);
        Assert.Throws<JournalIntegrityException>(() => store.Load(runId));
    }

    [Fact]
    public void A_journal_file_owned_by_a_standard_user_is_never_loaded()
    {
        var runId = Guid.NewGuid();
        KeyedStore().Begin(Record(runId, new DateTimeOffset(2026, 8, 17, 1, 0, 0, TimeSpan.Zero)));

        WindowsJournalStore untrusting = KeyedStore(trusted: _ => false);

        Assert.Equal(JournalIntegrity.Tampered, Assert.Single(untrusting.List()).Integrity);
        Assert.Throws<JournalIntegrityException>(() => untrusting.Load(runId));
    }

    [Fact]
    public void A_keyed_journal_read_without_the_key_is_unverified_and_not_replayed()
    {
        var runId = Guid.NewGuid();
        KeyedStore().Begin(Record(runId, new DateTimeOffset(2026, 8, 17, 1, 0, 0, TimeSpan.Zero)));

        var unelevated = new WindowsJournalStore(Path.Combine(_root, "journal"), new TestProtection(null, _ => true));

        Assert.Equal(JournalIntegrity.Unverified, Assert.Single(unelevated.List()).Integrity);
        Assert.Throws<JournalIntegrityException>(() => unelevated.Load(runId));
    }

    [Fact]
    public void A_torn_journal_is_listed_as_unreadable_and_does_not_block_new_runs()
    {
        WindowsJournalStore store = NewStore();
        Directory.CreateDirectory(Path.Combine(_root, "journal"));
        var tornId = Guid.NewGuid();
        File.WriteAllText(Path.Combine(_root, "journal", $"20260817-010000-{tornId:D}.json"), "{\"schemaVersion\": 1, \"runId\"");

        var next = Guid.NewGuid();
        store.Begin(Record(next, new DateTimeOffset(2026, 8, 17, 2, 0, 0, TimeSpan.Zero)));
        IReadOnlyList<JournalSummary> list = store.List();

        Assert.Equal(JournalIntegrity.Unreadable, list.Single(s => s.RunId == tornId).Integrity);
        Assert.Equal(JournalIntegrity.Verified, list.Single(s => s.RunId == next).Integrity);
        Assert.Throws<JournalIntegrityException>(() => store.Load(tornId));
    }

    [Fact]
    public void Writes_leave_no_temporary_file_behind()
    {
        WindowsJournalStore store = NewStore();
        JournalRecord record = Record(Guid.NewGuid(), new DateTimeOffset(2026, 8, 17, 1, 0, 0, TimeSpan.Zero));
        store.Begin(record);
        record.Result = RunState.RolledBack;
        store.Update(record);

        Assert.Empty(Directory.GetFiles(Path.Combine(_root, "journal"), "*.tmp"));
        Assert.Equal(RunState.RolledBack, store.Load(record.RunId).Result);
    }
}
