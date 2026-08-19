using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.Core.Engine;

/// <summary>
/// The cross-cutting services the apply engine orchestrates: the journal, the
/// restore-point provider, Explorer restart, pre-flight, the audit sink, the
/// elevation context and interactive-user resolution.
/// </summary>
/// <param name="Journal">Journal store (write-before-change).</param>
/// <param name="RestorePoint">System Restore point provider.</param>
/// <param name="Explorer">Explorer restarter for shell settings.</param>
/// <param name="Preflight">Pre-flight safety checks.</param>
/// <param name="Audit">Audit sink (Event Log + JSONL).</param>
/// <param name="Elevation">Elevation context (operator identity, elevated token).</param>
/// <param name="InteractiveUser">Resolver for the interactive user's SID.</param>
/// <param name="UserHives">Enumerator of the real user profiles an all-users apply targets.</param>
/// <param name="HiveLoader">Loads/unloads the hive of a user who is not logged on (all-users apply).</param>
public sealed record ApplyServices(
    IJournalStore Journal,
    IRestorePointService RestorePoint,
    IExplorerRestarter Explorer,
    IPreflight Preflight,
    IAuditSink Audit,
    IElevationContext Elevation,
    IInteractiveUserResolver InteractiveUser,
    IUserHiveEnumerator UserHives,
    IUserHiveLoader HiveLoader);
