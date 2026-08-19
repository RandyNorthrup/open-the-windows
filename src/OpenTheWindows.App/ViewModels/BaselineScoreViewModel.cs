using System.Globalization;
using OpenTheWindows.Core.Audit;

namespace OpenTheWindows.App.ViewModels;

/// <summary>One baseline's audit score for the dashboard: the title, the 0-100 score and an outcome summary.</summary>
internal sealed class BaselineScoreViewModel
{
    /// <summary>Projects a baseline and its audit report into the dashboard row.</summary>
    public BaselineScoreViewModel(Baseline baseline, AuditReport report)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(report);
        Title = baseline.Title;
        Score = report.Score;
        AuditSummary summary = report.Summary;
        Summary = string.Create(CultureInfo.CurrentCulture,
            $"{summary.Pass} pass, {summary.Fail} fail, {summary.NotApplicable} n/a, {summary.Manual} manual, {summary.Unknown} unknown");
    }

    /// <summary>The baseline title.</summary>
    public string Title { get; }

    /// <summary>The weighted 0-100 score.</summary>
    public int Score { get; }

    /// <summary>The score as "N / 100".</summary>
    public string ScoreLabel => string.Create(CultureInfo.CurrentCulture, $"{Score} / 100");

    /// <summary>A short outcome summary (pass/fail/…).</summary>
    public string Summary { get; }
}
