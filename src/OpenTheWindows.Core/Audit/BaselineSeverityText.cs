namespace OpenTheWindows.Core.Audit;

/// <summary>The framework label for a <see cref="BaselineSeverity"/> (no reflection).</summary>
public static class BaselineSeverityText
{
    /// <summary>Returns the framework label, e.g. <c>CAT I</c>, <c>L1</c>, <c>Baseline</c>.</summary>
    public static string Label(BaselineSeverity severity) => severity switch
    {
        BaselineSeverity.CatI => "CAT I",
        BaselineSeverity.CatII => "CAT II",
        BaselineSeverity.CatIII => "CAT III",
        BaselineSeverity.L1 => "L1",
        BaselineSeverity.L2 => "L2",
        BaselineSeverity.Bl => "BL",
        BaselineSeverity.Ng => "NG",
        BaselineSeverity.Baseline => "Baseline",
        _ => severity.ToString(),
    };
}
