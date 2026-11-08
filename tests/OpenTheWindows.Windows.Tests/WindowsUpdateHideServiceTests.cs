using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Updates;
using OpenTheWindows.TestSupport.Fakes;
using OpenTheWindows.Windows.Updates;

namespace OpenTheWindows.Windows.Tests;

public sealed class WindowsUpdateHideServiceTests
{
    [Fact]
    public void Hide_marks_a_discovered_kb_and_audits()
    {
        var agent = new FakeWindowsUpdateAgent();
        agent.NotInstalled.Add(new DiscoveredUpdate("id-1", "Cumulative 2026-08", ["5063878"], false));
        var audit = new FakeAuditSink();
        var service = new WindowsUpdateHideService(agent, audit, TimeProvider.System);

        UpdateHideOutcome outcome = service.Hide("KB5063878");

        Assert.True(outcome.Succeeded);
        Assert.Equal(1, outcome.Affected);
        Assert.True(agent.Hidden.Single().IsHidden);
        Assert.True(audit.Has(AuditEventId.UpdateHidden));
    }

    [Fact]
    public void Hide_fails_when_the_agent_has_not_discovered_the_kb()
    {
        var service = new WindowsUpdateHideService(new FakeWindowsUpdateAgent(), new FakeAuditSink(), TimeProvider.System);

        UpdateHideOutcome outcome = service.Hide("KB1");

        Assert.False(outcome.Succeeded);
        Assert.Equal(0, outcome.Affected);
    }

    [Fact]
    public void Unhide_clears_a_hidden_kb()
    {
        var agent = new FakeWindowsUpdateAgent();
        agent.Hidden.Add(new DiscoveredUpdate("id-1", "Cumulative 2026-08", ["5063878"], true));
        var service = new WindowsUpdateHideService(agent, new FakeAuditSink(), TimeProvider.System);

        UpdateHideOutcome outcome = service.Unhide("5063878");

        Assert.True(outcome.Succeeded);
        Assert.Equal(1, outcome.Affected);
        Assert.False(agent.NotInstalled.Single().IsHidden);
        Assert.Empty(agent.Hidden);
    }

    [Fact]
    public void Rejects_null_dependencies()
    {
        var agent = new FakeWindowsUpdateAgent();
        var audit = new FakeAuditSink();
        Assert.Throws<ArgumentNullException>(() => new WindowsUpdateHideService(null!, audit, TimeProvider.System));
        Assert.Throws<ArgumentNullException>(() => new WindowsUpdateHideService(agent, null!, TimeProvider.System));
        Assert.Throws<ArgumentNullException>(() => new WindowsUpdateHideService(agent, audit, null!));
    }

    [Fact]
    public void Hide_reports_failure_when_the_agent_does_not_take_the_change()
    {
        var agent = new FakeWindowsUpdateAgent { IgnoreChanges = true };
        agent.NotInstalled.Add(new DiscoveredUpdate("id-1", "Cumulative 2026-08", ["5063878"], false));
        var service = new WindowsUpdateHideService(agent, new FakeAuditSink(), TimeProvider.System);

        UpdateHideOutcome outcome = service.Hide("KB5063878");

        Assert.False(outcome.Succeeded);
        Assert.Contains("still reports", outcome.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_reports_what_would_change_and_changes_and_audits_nothing()
    {
        var agent = new FakeWindowsUpdateAgent();
        agent.NotInstalled.Add(new DiscoveredUpdate("id-1", "Cumulative 2026-08", ["5063878"], false));
        agent.Hidden.Add(new DiscoveredUpdate("id-2", "Hidden one", ["5000001"], true));
        var audit = new FakeAuditSink();
        var service = new WindowsUpdateHideService(agent, audit, TimeProvider.System);

        UpdateHideOutcome hide = service.Preview("KB5063878", hide: true);
        UpdateHideOutcome alreadyHidden = service.Preview("KB5000001", hide: true);
        UpdateHideOutcome unknown = service.Preview("KB1", hide: false);

        Assert.True(hide.Succeeded);
        Assert.Equal(1, hide.Affected);
        Assert.Contains("Would hide KB5063878", hide.Message, StringComparison.Ordinal);
        Assert.Contains("already hidden", alreadyHidden.Message, StringComparison.Ordinal);
        Assert.Equal(0, alreadyHidden.Affected);
        Assert.False(unknown.Succeeded);
        Assert.False(agent.NotInstalled.Single().IsHidden);
        Assert.False(audit.Has(AuditEventId.UpdateHidden));
    }
}
