using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Engine;

/// <summary>The result of planning an apply run (also the <c>--what-if</c> output).</summary>
/// <param name="ProfileName">The profile the plan is for.</param>
/// <param name="Entries">Every entry considered, in apply order, with its disposition.</param>
/// <param name="Errors">Blocking plan errors (conflicts, dependency cycles); non-empty means the run must not proceed.</param>
public sealed record PlanResult(
    string ProfileName,
    IReadOnlyList<PlannedEntry> Entries,
    IReadOnlyList<string> Errors)
{
    /// <summary><see langword="true"/> when the plan has blocking errors and must not be applied.</summary>
    public bool HasErrors => Errors.Count > 0;

    /// <summary>The entries that will actually be written.</summary>
    public IEnumerable<PlannedEntry> WillApply
        => Entries.Where(e => e.Disposition is PlanDisposition.Apply or PlanDisposition.ManagedBreakGlass);

    /// <summary>The aggregate restart requirement over the entries that will apply.</summary>
    public RestartRequirement Requires
        => WillApply.Select(e => e.Requires).DefaultIfEmpty(RestartRequirement.None).Max();
}
