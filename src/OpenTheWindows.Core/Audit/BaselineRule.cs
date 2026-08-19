using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Audit;

/// <summary>
/// One rule of a <see cref="Baseline"/>: an external control id mapped to the
/// catalogue tweaks and/or the read-only health check that satisfy it.
/// </summary>
/// <remarks>
/// A rule is <em>tweak-backed</em> when <see cref="TweakIds"/> is non-empty
/// (the audit engine scans those tweaks and aggregates their compliance),
/// <em>check-backed</em> when <see cref="CheckOnly"/> is set (it reads one health
/// check), or a <em>manual</em> rule when both are empty (the report marks it
/// <c>Manual</c> for a human to confirm). CIS rules carry only the section id and
/// a paraphrased title — never benchmark text (ADR 0002); the model has no field
/// for verbatim text, so that constraint is structural.
/// </remarks>
/// <param name="RuleId">The framework control id (STIG <c>V-253254</c>, a CIS section id, or a Microsoft baseline token). Also the SARIF rule id.</param>
/// <param name="Title">A short paraphrased description of the control.</param>
/// <param name="Severity">The framework severity / profile level.</param>
/// <param name="TweakIds">Catalogue tweak ids that satisfy this rule (may be empty).</param>
/// <param name="CheckOnly">A read-only health check to use instead of tweaks (may be <see langword="null"/>).</param>
/// <param name="Sources">At least one https documentation source (the framework page).</param>
public sealed record BaselineRule(
    string RuleId,
    string Title,
    BaselineSeverity Severity,
    IReadOnlyList<TweakId> TweakIds,
    BaselineCheck? CheckOnly,
    IReadOnlyList<Uri> Sources);
