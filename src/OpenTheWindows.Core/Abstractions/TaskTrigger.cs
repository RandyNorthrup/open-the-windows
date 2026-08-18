namespace OpenTheWindows.Core.Abstractions;

/// <summary>When the remediation scheduled task runs.</summary>
public enum TaskTrigger
{
    /// <summary>Once a day.</summary>
    Daily = 0,

    /// <summary>Once a week.</summary>
    Weekly = 1,

    /// <summary>At every user logon.</summary>
    Logon = 2,
}
