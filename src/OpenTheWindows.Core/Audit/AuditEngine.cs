using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Audit;

/// <summary>
/// Scores the machine against a <see cref="Baseline"/> using the read-only
/// <see cref="ScanEngine"/> for tweak-backed rules and the machine health probe
/// for <c>checkOnly</c> rules. Read-only: it never changes machine state.
/// </summary>
public sealed class AuditEngine
{
    private static readonly IReadOnlyDictionary<string, HealthCheckResult> NoHealth =
        new Dictionary<string, HealthCheckResult>(StringComparer.Ordinal);

    private readonly ScanEngine _scan;
    private readonly IMachineHealthProbe _health;
    private readonly TweakCatalog _catalog;

    /// <summary>Creates an audit engine over a scan engine, a health probe and the catalogue.</summary>
    public AuditEngine(ScanEngine scan, IMachineHealthProbe health, TweakCatalog catalog)
    {
        _scan = scan ?? throw new ArgumentNullException(nameof(scan));
        _health = health ?? throw new ArgumentNullException(nameof(health));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <summary>Audits the machine against <paramref name="baseline"/> and returns the scored report.</summary>
    public AuditReport Audit(Baseline baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        // Resolve every referenced tweak once, scan them read-only, and index the observations by id.
        Dictionary<TweakId, TweakDefinition> definitions = [];
        foreach (BaselineRule rule in baseline.Rules)
        {
            foreach (TweakId id in rule.TweakIds)
            {
                if (!definitions.ContainsKey(id) && _catalog.TryGet(id, out TweakDefinition? definition))
                {
                    definitions[id] = definition;
                }
            }
        }

        ScanReport scan = _scan.Scan(definitions.Values, baseline.Id);
        var observations = scan.Results.ToDictionary(o => o.Id);

        IReadOnlyDictionary<string, HealthCheckResult> health = baseline.Rules.Any(r => r.CheckOnly is not null)
            ? _health.Run().GroupBy(r => r.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal)
            : NoHealth;

        List<AuditRuleResult> results = [.. baseline.Rules.Select(rule => Evaluate(rule, observations, health))];
        return new AuditReport(
            baseline.Id,
            baseline.Title,
            scan.At,
            scan.Os,
            AuditScoring.Score(results),
            AuditSummary.From(results),
            results);
    }

    private static AuditRuleResult Evaluate(
        BaselineRule rule,
        IReadOnlyDictionary<TweakId, TweakObservation> observations,
        IReadOnlyDictionary<string, HealthCheckResult> health)
    {
        if (rule.TweakIds.Count > 0)
        {
            return EvaluateTweaks(rule, observations);
        }

        if (rule.CheckOnly is { } check)
        {
            return EvaluateCheck(rule, check, health);
        }

        return new AuditRuleResult(rule.RuleId, rule.Title, rule.Severity, AuditOutcome.Manual, rule.TweakIds,
            "Manual check — no automated control is mapped to this rule.");
    }

    private static AuditRuleResult EvaluateTweaks(BaselineRule rule, IReadOnlyDictionary<TweakId, TweakObservation> observations)
    {
        var outcomes = new List<AuditOutcome>(rule.TweakIds.Count);
        var evidence = new List<string>(rule.TweakIds.Count);
        foreach (TweakId id in rule.TweakIds)
        {
            if (!observations.TryGetValue(id, out TweakObservation? observation))
            {
                outcomes.Add(AuditOutcome.Unknown);
                evidence.Add($"{id}: not scanned");
                continue;
            }

            outcomes.Add(FromComplianceState(observation.State));
            string actual = observation.Actions.Count == 0
                ? observation.State.ToString()
                : string.Join(", ", observation.Actions.Select(a => a.Actual));
            evidence.Add($"{id}: {actual}");
        }

        return new AuditRuleResult(rule.RuleId, rule.Title, rule.Severity, AggregateTweakOutcomes(outcomes), rule.TweakIds,
            string.Join("; ", evidence));
    }

    private static AuditRuleResult EvaluateCheck(BaselineRule rule, BaselineCheck check, IReadOnlyDictionary<string, HealthCheckResult> health)
    {
        if (!health.TryGetValue(check.HealthCheckId, out HealthCheckResult? result))
        {
            return new AuditRuleResult(rule.RuleId, rule.Title, rule.Severity, AuditOutcome.Unknown, rule.TweakIds,
                $"Health check '{check.HealthCheckId}' did not run.");
        }

        return new AuditRuleResult(rule.RuleId, rule.Title, rule.Severity, FromHealthStatus(result.Status), rule.TweakIds, result.Detail);
    }

    private static AuditOutcome FromComplianceState(ComplianceState state)
        => state switch
        {
            ComplianceState.Compliant => AuditOutcome.Pass,
            ComplianceState.Drift => AuditOutcome.Fail,
            ComplianceState.Managed => AuditOutcome.Manual,
            ComplianceState.NotApplicable => AuditOutcome.NotApplicable,
            _ => AuditOutcome.Unknown,
        };

    private static AuditOutcome FromHealthStatus(HealthStatus status)
        => status switch
        {
            HealthStatus.Pass => AuditOutcome.Pass,
            HealthStatus.Fail => AuditOutcome.Fail,
            HealthStatus.Warn => AuditOutcome.Fail,
            HealthStatus.NotApplicable => AuditOutcome.NotApplicable,
            _ => AuditOutcome.Unknown,
        };

    // Worst-wins across a rule's tweaks: NotApplicable is ignored unless every tweak is NotApplicable.
    private static AuditOutcome AggregateTweakOutcomes(IReadOnlyList<AuditOutcome> outcomes)
    {
        var applicable = outcomes.Where(o => o != AuditOutcome.NotApplicable).ToList();
        if (applicable.Count == 0)
        {
            return AuditOutcome.NotApplicable;
        }

        if (applicable.Contains(AuditOutcome.Fail))
        {
            return AuditOutcome.Fail;
        }

        if (applicable.Contains(AuditOutcome.Unknown))
        {
            return AuditOutcome.Unknown;
        }

        return applicable.Contains(AuditOutcome.Manual) ? AuditOutcome.Manual : AuditOutcome.Pass;
    }
}
