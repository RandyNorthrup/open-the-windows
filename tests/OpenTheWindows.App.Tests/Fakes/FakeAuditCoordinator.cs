using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Audit;

namespace OpenTheWindows.App.Tests.Fakes;

/// <summary>Scriptable <see cref="IAuditCoordinator"/>: returns the report the test set per baseline and counts the calls.</summary>
internal sealed class FakeAuditCoordinator : IAuditCoordinator
{
    /// <summary>The baselines exposed to the view model.</summary>
    public IReadOnlyList<Baseline> Baselines { get; init; } = [];

    /// <summary>Produces the report for a baseline.</summary>
    public Func<Baseline, AuditReport> OnAudit { get; set; } = _ => throw new InvalidOperationException("no report configured");

    /// <summary>How many times <see cref="AuditAsync"/> was called.</summary>
    public int AuditCount { get; private set; }

    /// <inheritdoc />
    public Task<AuditReport> AuditAsync(Baseline baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        AuditCount++;
        return Task.FromResult(OnAudit(baseline));
    }
}
