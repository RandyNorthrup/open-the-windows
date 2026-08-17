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
}
