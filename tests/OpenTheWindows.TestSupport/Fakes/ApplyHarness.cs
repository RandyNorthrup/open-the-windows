using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>An assembled <see cref="ApplyEngine"/> with all its in-memory fakes exposed for assertions.</summary>
/// <param name="Engine">The apply engine under test.</param>
/// <param name="Scan">The scan engine over the same machine.</param>
/// <param name="Machine">The in-memory machine (readers + writers + fault injection).</param>
/// <param name="Journal">The in-memory journal store (with the real hash chain).</param>
/// <param name="Audit">The audit sink recording every event.</param>
/// <param name="RestorePoint">The restore-point stub.</param>
/// <param name="Explorer">The Explorer-restart stub.</param>
/// <param name="Preflight">The pre-flight stub.</param>
/// <param name="Elevation">The elevation context stub.</param>
public sealed record ApplyHarness(
    ApplyEngine Engine,
    ScanEngine Scan,
    FakeMachine Machine,
    FakeJournalStore Journal,
    FakeAuditSink Audit,
    FakeRestorePointService RestorePoint,
    FakeExplorerRestarter Explorer,
    FakePreflight Preflight,
    FakeElevationContext Elevation);
