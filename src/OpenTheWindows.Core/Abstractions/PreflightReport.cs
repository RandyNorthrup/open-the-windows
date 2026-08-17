namespace OpenTheWindows.Core.Abstractions;

/// <summary>The result of the pre-flight checks run before an apply.</summary>
/// <param name="Issues">Every finding; the blocking ones prevent the run.</param>
public sealed record PreflightReport(IReadOnlyList<PreflightIssue> Issues)
{
    /// <summary><see langword="true"/> when no blocking issue was found.</summary>
    public bool CanProceed => !Issues.Any(i => i.Blocking);

    /// <summary>The blocking issues, if any.</summary>
    public IEnumerable<PreflightIssue> Blocking => Issues.Where(i => i.Blocking);
}
