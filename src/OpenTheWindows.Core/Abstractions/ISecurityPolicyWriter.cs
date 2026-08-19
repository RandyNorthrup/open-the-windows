using OpenTheWindows.Core.Catalog.Actions;

namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Sets a single Windows security-policy setting by staging and importing a minimal
/// <c>secedit</c> security template that carries just that one setting. Requires an
/// elevated token. The engine captures the prior value first (through
/// <see cref="ISecurityPolicyReader"/>) so the change is reversible.
/// </summary>
public interface ISecurityPolicyWriter
{
    /// <summary>Configures <paramref name="setting"/> in <paramref name="section"/> to <paramref name="value"/>.</summary>
    void Configure(SecurityPolicySection section, string setting, string value);
}
