using System.Globalization;
using System.Runtime.Versioning;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Updates;

namespace OpenTheWindows.Windows.Updates;

/// <summary>
/// Hides and unhides updates through <see cref="IWindowsUpdateAgent"/> and
/// writes an audit event before the change. Verification is a re-read of the
/// hidden list.
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

    private UpdateHideOutcome SetHidden(string knowledgeBaseId, bool hidden)
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

        string verb = hidden ? "Hid" : "Unhid";
        return new UpdateHideOutcome(true, normalized, affected, string.Create(CultureInfo.InvariantCulture, $"{verb} {formatted} ({affected} update(s))."));
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
