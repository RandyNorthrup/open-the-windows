using System.Globalization;
using System.Runtime.Versioning;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Updates;

namespace OpenTheWindows.Windows.Updates;

/// <summary>
/// Hides and unhides updates through <see cref="IWindowsUpdateAgent"/> and
/// writes an audit event before the change. After the change it re-reads the
/// agent and reports failure when an update did not take the new state.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsUpdateHideService : IUpdateHideService
{
    private readonly IWindowsUpdateAgent _agent;
    private readonly IAuditSink _audit;
    private readonly TimeProvider _time;

    /// <summary>Creates the service over the agent, audit sink and clock.</summary>
    public WindowsUpdateHideService(IWindowsUpdateAgent agent, IAuditSink audit, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(time);
        _agent = agent;
        _audit = audit;
        _time = time;
    }

    /// <inheritdoc />
    public IReadOnlyList<DiscoveredUpdate> ListHidden() => _agent.Search(WindowsUpdateSearchCriteria.Hidden);

    /// <inheritdoc />
    public UpdateHideOutcome Hide(string knowledgeBaseId) => SetHidden(knowledgeBaseId, hidden: true);

    /// <inheritdoc />
    public UpdateHideOutcome Unhide(string knowledgeBaseId) => SetHidden(knowledgeBaseId, hidden: false);

    /// <inheritdoc />
    public UpdateHideOutcome Preview(string knowledgeBaseId, bool hide) => SetHidden(knowledgeBaseId, hide, whatIf: true);

    private UpdateHideOutcome SetHidden(string knowledgeBaseId, bool hidden, bool whatIf = false)
    {
        string? normalized = KnowledgeBaseId.Normalize(knowledgeBaseId);
        if (normalized is null)
        {
            return new UpdateHideOutcome(false, knowledgeBaseId, 0, "A knowledge-base id looks like KB5063878 or 5063878.");
        }

        List<DiscoveredUpdate> matches = [.. Find(normalized)];
        if (matches.Count == 0)
        {
            return new UpdateHideOutcome(
                false,
                normalized,
                0,
                string.Create(CultureInfo.InvariantCulture,
                    $"Windows Update Agent has not discovered {KnowledgeBaseId.Format(normalized)}. Search for updates, then retry."));
        }

        return whatIf ? Previewed(normalized, matches, hidden) : Change(normalized, matches, hidden);
    }

    // Audits, flips each update not already in the requested state, then re-reads to verify.
    private UpdateHideOutcome Change(string normalized, List<DiscoveredUpdate> matches, bool hidden)
    {
        string gerund = hidden ? "Hiding" : "Unhiding";
        _audit.Write(new AuditEvent(
            hidden ? AuditEventId.UpdateHidden : AuditEventId.UpdateUnhidden,
            _time.GetUtcNow(),
            string.Create(CultureInfo.InvariantCulture, $"{gerund} {KnowledgeBaseId.Format(normalized)} ({matches.Count} discovered update(s))."),
            null,
            KnowledgeBaseId.Format(normalized)));

        int affected = 0;
        foreach (DiscoveredUpdate match in matches)
        {
            if (match.IsHidden == hidden)
            {
                continue;
            }

            _agent.SetHidden(match.Identity, hidden);
            affected++;
        }

        string formatted = KnowledgeBaseId.Format(normalized);
        if (affected == 0)
        {
            string already = hidden ? "hidden" : "visible";
            return new UpdateHideOutcome(true, normalized, 0, string.Create(CultureInfo.InvariantCulture, $"{formatted} was already {already}."));
        }

        // Verify by re-reading what the agent now reports for the same updates.
        HashSet<string> changed = [.. matches.Where(m => m.IsHidden != hidden).Select(m => m.Identity)];
        int stuck = Find(normalized).Count(u => changed.Contains(u.Identity) && u.IsHidden != hidden);
        if (stuck > 0)
        {
            return new UpdateHideOutcome(false, normalized, affected - stuck, string.Create(CultureInfo.InvariantCulture,
                $"Windows Update Agent still reports {stuck} update(s) of {formatted} as {(hidden ? "visible" : "hidden")} after the change."));
        }

        string verb = hidden ? "Hid" : "Unhid";
        return new UpdateHideOutcome(true, normalized, affected, string.Create(CultureInfo.InvariantCulture, $"{verb} {formatted} ({affected} update(s))."));
    }

    private static UpdateHideOutcome Previewed(string normalized, List<DiscoveredUpdate> matches, bool hidden)
    {
        string formatted = KnowledgeBaseId.Format(normalized);
        int wouldChange = matches.Count(m => m.IsHidden != hidden);
        if (wouldChange == 0)
        {
            string already = hidden ? "hidden" : "visible";
            return new UpdateHideOutcome(true, normalized, 0, string.Create(CultureInfo.InvariantCulture, $"{formatted} is already {already}; nothing would change."));
        }

        string verb = hidden ? "hide" : "unhide";
        return new UpdateHideOutcome(true, normalized, wouldChange,
            string.Create(CultureInfo.InvariantCulture, $"Would {verb} {formatted} ({wouldChange} update(s)); nothing was changed."));
    }

    private IEnumerable<DiscoveredUpdate> Find(string normalized)
    {
        IReadOnlyList<DiscoveredUpdate> notInstalled = _agent.Search(WindowsUpdateSearchCriteria.NotInstalled);
        IReadOnlyList<DiscoveredUpdate> hidden = _agent.Search(WindowsUpdateSearchCriteria.Hidden);
        Dictionary<string, DiscoveredUpdate> byId = new(StringComparer.OrdinalIgnoreCase);
        foreach (DiscoveredUpdate update in notInstalled.Concat(hidden))
        {
            byId[update.Identity] = update;
        }

        return byId.Values.Where(update =>
            update.KnowledgeBaseIds.Any(id => string.Equals(KnowledgeBaseId.Normalize(id), normalized, StringComparison.Ordinal)));
    }
}
