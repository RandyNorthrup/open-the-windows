using OpenTheWindows.Core.Audit;

namespace OpenTheWindows.App.Services;

/// <summary>
/// The GUI's read-only audit boundary: the built-in baselines and an async audit
/// against one of them. Implementations run the audit off the UI thread.
/// </summary>
internal interface IAuditCoordinator
{
    /// <summary>The built-in security baselines, id-sorted.</summary>
    IReadOnlyList<Baseline> Baselines { get; }

    /// <summary>Audits the machine against <paramref name="baseline"/> on the thread pool.</summary>
    Task<AuditReport> AuditAsync(Baseline baseline);
}
