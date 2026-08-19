using OpenTheWindows.Core.Catalog.Actions;

namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Reads a single Windows security-policy setting from the LSA security database
/// (exported with <c>secedit</c>) so the engine can capture the prior value and
/// verify a change. Read-only: it never imports a template.
/// </summary>
public interface ISecurityPolicyReader
{
    /// <summary>
    /// The current value of <paramref name="setting"/> in <paramref name="section"/>,
    /// or <see langword="null"/> when the export does not contain it.
    /// </summary>
    string? Read(SecurityPolicySection section, string setting);
}
