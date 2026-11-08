using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>Records repair requests and returns a scripted result.</summary>
public sealed class FakeUpdateRepair : IUpdateRepair
{
    /// <summary>The result to return (a successful two-step repair by default).</summary>
    public UpdateRepairResult Result { get; set; } = new(true, ["Stopped wuauserv.", "Started wuauserv."], [@"C:\Windows\SoftwareDistribution.otw-1"], null);

    /// <summary>The what-if flag of each call, in order.</summary>
    public IList<bool> Calls { get; } = [];

    /// <inheritdoc />
    public UpdateRepairResult Repair(bool whatIf)
    {
        Calls.Add(whatIf);
        return Result;
    }
}
