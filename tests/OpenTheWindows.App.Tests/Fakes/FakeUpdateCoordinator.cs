using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Updates;

namespace OpenTheWindows.App.Tests.Fakes;

/// <summary>Scriptable <see cref="IUpdateCoordinator"/>: returns the status, outcomes and flags the test sets and records the calls.</summary>
internal sealed class FakeUpdateCoordinator : IUpdateCoordinator
{
    /// <summary>The status <see cref="StatusAsync"/> returns.</summary>
    public UpdateStatus StatusToReturn { get; set; } = new(
        "24H2", "Professional", EndOfServicingEditionFamily.Consumer, null, null, false, false, null, null, null, false);

    /// <summary>What <see cref="IsManagedAsync"/> returns.</summary>
    public bool ManagedToReturn { get; set; }

    /// <summary>When set, <see cref="StatusAsync"/> faults.</summary>
    public bool ThrowOnStatus { get; set; }

    /// <summary>The apply result a pause or hold returns.</summary>
    public ApplyResult ApplyToReturn { get; set; } = TestResults.EmptyApplyResult();

    /// <summary>The outcome <see cref="ResumeAsync"/> returns.</summary>
    public ResumeOutcome ResumeToReturn { get; set; } = new(true, null, null, false);

    /// <summary>The releases <see cref="TargetVersionsAsync"/> returns.</summary>
    public IReadOnlyList<string> VersionsToReturn { get; set; } = ["23H2", "24H2", "25H2"];

    /// <summary>The results <see cref="ClearHoldAsync"/> returns.</summary>
    public IReadOnlyList<RevertResult> ClearToReturn { get; set; } = [new(Guid.Empty, Guid.Empty, RunState.Completed, [])];

    /// <summary>The result <see cref="RepairAsync"/> returns.</summary>
    public UpdateRepairResult RepairToReturn { get; set; } = new(true, ["Stopped wuauserv."], [@"C:\Windows\SoftwareDistribution.otw-1"], null);

    /// <summary>How many times <see cref="PauseAsync"/> was called.</summary>
    public int PauseCount { get; private set; }

    /// <summary>How many times <see cref="ResumeAsync"/> was called.</summary>
    public int ResumeCount { get; private set; }

    /// <summary>How many times <see cref="RepairAsync"/> was called.</summary>
    public int RepairCount { get; private set; }

    /// <summary>How many times <see cref="ClearHoldAsync"/> was called.</summary>
    public int ClearHoldCount { get; private set; }

    /// <summary>The release of the last <see cref="HoldAtAsync"/> call.</summary>
    public string? LastHold { get; private set; }

    /// <summary>The days of the last pause.</summary>
    public int LastPauseDays { get; private set; }

    /// <summary>Whether the last pause was extended.</summary>
    public bool LastPauseExtended { get; private set; }

    /// <summary>How many times <see cref="StatusAsync"/> was read.</summary>
    public int StatusCount { get; private set; }

    /// <summary>Hidden updates <see cref="ListHiddenAsync"/> returns.</summary>
    public IReadOnlyList<DiscoveredUpdate> HiddenToReturn { get; set; } = [];

    /// <summary>Hide/unhide outcome to return.</summary>
    public UpdateHideOutcome HideOutcome { get; set; } = new(true, "1", 1, "Hid.");

    /// <summary>Last hide id.</summary>
    public string? LastHide { get; private set; }

    /// <summary>Last unhide id.</summary>
    public string? LastUnhide { get; private set; }

    /// <inheritdoc />
    public Task<UpdateStatus> StatusAsync()
    {
        StatusCount++;
        return ThrowOnStatus ? Task.FromException<UpdateStatus>(new InvalidOperationException("status failed")) : Task.FromResult(StatusToReturn);
    }

    /// <inheritdoc />
    public Task<bool> IsManagedAsync() => Task.FromResult(ManagedToReturn);

    /// <inheritdoc />
    public Task<PauseOutcome> PauseAsync(int days, bool extended)
    {
        PauseCount++;
        LastPauseDays = days;
        LastPauseExtended = extended;
        DateTimeOffset start = new(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);
        return Task.FromResult(new PauseOutcome(new PausePlan(start, start.AddDays(days), days, extended, []), ApplyToReturn, extended));
    }

    /// <inheritdoc />
    public Task<ResumeOutcome> ResumeAsync()
    {
        ResumeCount++;
        return Task.FromResult(ResumeToReturn);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DiscoveredUpdate>> ListHiddenAsync() => Task.FromResult(HiddenToReturn);

    /// <inheritdoc />
    public Task<UpdateHideOutcome> HideAsync(string knowledgeBaseId)
    {
        LastHide = knowledgeBaseId;
        return Task.FromResult(HideOutcome);
    }

    /// <inheritdoc />
    public Task<UpdateHideOutcome> UnhideAsync(string knowledgeBaseId)
    {
        LastUnhide = knowledgeBaseId;
        return Task.FromResult(HideOutcome);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> TargetVersionsAsync() => Task.FromResult(VersionsToReturn);

    /// <inheritdoc />
    public Task<ApplyResult> HoldAtAsync(string version)
    {
        LastHold = version;
        return Task.FromResult(ApplyToReturn);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<RevertResult>> ClearHoldAsync()
    {
        ClearHoldCount++;
        return Task.FromResult(ClearToReturn);
    }

    /// <inheritdoc />
    public Task<UpdateRepairResult> RepairAsync()
    {
        RepairCount++;
        return Task.FromResult(RepairToReturn);
    }
}
