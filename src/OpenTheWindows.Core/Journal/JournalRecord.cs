using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Journal;

/// <summary>
/// The complete record of one apply or revert run. Written before the first
/// machine change (with every action's prior state), then updated after each
/// action and at completion. The <see cref="Hash"/>/<see cref="PreviousHash"/>
/// pair forms a tamper-evident chain over the journal directory.
/// </summary>
public sealed class JournalRecord
{
    /// <summary>Journal schema version; bumped only on a breaking format change.</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Unique identifier of this run.</summary>
    public required Guid RunId { get; init; }

    /// <summary>When the run started (from <see cref="TimeProvider"/>).</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>Product version that produced the journal.</summary>
    public required string AppVersion { get; init; }

    /// <summary>OS identity at run time.</summary>
    public required JournalOs Os { get; init; }

    /// <summary>The profile name that drove the run.</summary>
    public required string Profile { get; init; }

    /// <summary>DOMAIN\user of the elevating account.</summary>
    public required string Operator { get; init; }

    /// <summary>The interactive user whose hive per-user changes targeted, if any.</summary>
    public JournalUser? TargetUser { get; init; }

    /// <summary>The System Restore point outcome for the run.</summary>
    public required JournalRestorePoint RestorePoint { get; init; }

    /// <summary>The journaled entries in apply order.</summary>
    public required IReadOnlyList<JournalEntry> Entries { get; init; }

    /// <summary>The run's overall state.</summary>
    public RunState Result { get; set; }

    /// <summary>The aggregate restart requirement over applied entries.</summary>
    public RestartRequirement Requires { get; set; }

    /// <summary>When set, the id of the run this run reverts.</summary>
    public Guid? RevertOf { get; init; }

    /// <summary>Hash of the newest earlier journal in the directory, or <see langword="null"/> for the first.</summary>
    public string? PreviousHash { get; set; }

    /// <summary>SHA-256 (lowercase hex) of this document with the hash field itself omitted.</summary>
    public string? Hash { get; set; }
}
