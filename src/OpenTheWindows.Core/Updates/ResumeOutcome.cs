using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.Core.Updates;

/// <summary>
/// The result of <c>otw updates resume</c>: whether an Open the Windows pause was
/// found and reverted, and whether a fresh update scan was triggered.
/// </summary>
/// <param name="WasPaused">Whether an active Open the Windows pause run was found to revert.</param>
/// <param name="RevertedRunId">The pause run that was reverted, if any.</param>
/// <param name="Revert">The revert run result, if a pause was found.</param>
/// <param name="ScanTriggered">Whether the post-resume update scan was triggered successfully.</param>
public sealed record ResumeOutcome(
    bool WasPaused,
    Guid? RevertedRunId,
    RevertResult? Revert,
    bool ScanTriggered);
