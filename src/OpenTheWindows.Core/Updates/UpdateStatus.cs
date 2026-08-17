namespace OpenTheWindows.Core.Updates;

/// <summary>
/// A snapshot for the <c>otw updates status</c> dashboard: the running release and
/// how close it is to end-of-servicing, whether updates are paused and until when,
/// and any <c>TargetReleaseVersion</c> pin with its own end-of-servicing warning.
/// </summary>
/// <param name="CurrentVersion">The running marketing version, e.g. "24H2".</param>
/// <param name="EditionId">The registry <c>EditionID</c>.</param>
/// <param name="Family">The servicing track (consumer / enterprise).</param>
/// <param name="EndOfServiceDate">When the running version stops servicing, or null when unknown.</param>
/// <param name="DaysUntilEndOfService">Days until that date (negative when past), or null when unknown.</param>
/// <param name="CurrentVersionWithinWarning">Whether the running version is within 90 days of, or past, end-of-servicing.</param>
/// <param name="IsPaused">Whether updates are paused.</param>
/// <param name="PausedUntil">The paused-until date read from the registry, if paused.</param>
/// <param name="TargetReleaseVersion">The pinned release, if a <c>TargetReleaseVersion</c> pin is set.</param>
/// <param name="TargetDaysUntilEndOfService">Days until the pinned release's end-of-servicing, or null.</param>
/// <param name="TargetWithinWarning">Whether the pinned release is within 90 days of, or past, end-of-servicing.</param>
public sealed record UpdateStatus(
    string CurrentVersion,
    string EditionId,
    EndOfServicingEditionFamily Family,
    DateOnly? EndOfServiceDate,
    int? DaysUntilEndOfService,
    bool CurrentVersionWithinWarning,
    bool IsPaused,
    DateTimeOffset? PausedUntil,
    string? TargetReleaseVersion,
    int? TargetDaysUntilEndOfService,
    bool TargetWithinWarning);
