using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Updates;

namespace OpenTheWindows.App.Tests.Fakes;

/// <summary>Scriptable <see cref="IUpdateCoordinator"/>: returns the status/managed flags the test sets and records pause/resume calls.</summary>
internal sealed class FakeUpdateCoordinator : IUpdateCoordinator
{
    /// <summary>The status <see cref="Status"/> returns.</summary>
    public UpdateStatus StatusToReturn { get; set; } = new(
        "24H2", "Professional", EndOfServicingEditionFamily.Consumer, null, null, false, false, null, null, null, false);

    /// <summary>What <see cref="IsManaged"/> returns.</summary>
    public bool ManagedToReturn { get; set; }

    /// <summary>How many times <see cref="PauseAsync"/> was called.</summary>
    public int PauseCount { get; private set; }

    /// <summary>How many times <see cref="ResumeAsync"/> was called.</summary>
    public int ResumeCount { get; private set; }

    /// <summary>The days of the last pause.</summary>
    public int LastPauseDays { get; private set; }

    /// <summary>Whether the last pause was extended.</summary>
    public bool LastPauseExtended { get; private set; }

    /// <summary>How many times <see cref="Status"/> was read.</summary>
    public int StatusCount { get; private set; }

    /// <inheritdoc />
    public UpdateStatus Status()
    {
        StatusCount++;
        return StatusToReturn;
    }

    /// <inheritdoc />
    public bool IsManaged() => ManagedToReturn;

    /// <inheritdoc />
    public Task<PauseOutcome> PauseAsync(int days, bool extended)
    {
        PauseCount++;
        LastPauseDays = days;
        LastPauseExtended = extended;
        return Task.FromResult(new PauseOutcome(new PausePlan(default, default, days, extended, []), TestResults.EmptyApplyResult(), extended));
    }

    /// <inheritdoc />
    public Task<ResumeOutcome> ResumeAsync()
    {
        ResumeCount++;
        return Task.FromResult(new ResumeOutcome(true, null, null, false));
    }

    /// <summary>Hidden updates <see cref="ListHiddenAsync"/> returns.</summary>
    public IReadOnlyList<DiscoveredUpdate> HiddenToReturn { get; set; } = [];

    /// <summary>Hide/unhide outcome to return.</summary>
    public UpdateHideOutcome HideOutcome { get; set; } = new(true, "1", 1, "Hid.");

    /// <summary>Last hide id.</summary>
    public string? LastHide { get; private set; }

    /// <summary>Last unhide id.</summary>
    public string? LastUnhide { get; private set; }

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
}
