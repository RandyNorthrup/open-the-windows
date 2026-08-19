namespace OpenTheWindows.Core.Catalog.Actions;

/// <summary>
/// Sets one Windows security-policy setting through <c>secedit</c> — the account,
/// password and lockout policy that has no registry value or public API (it lives in
/// the LSA security database, edited via a security template). Apply stages a minimal
/// template for the single <see cref="Setting"/> and imports it; revert replays the
/// value captured before the change, so the round-trip is exact and reversible.
/// </summary>
/// <param name="Section">The security-policy section the setting belongs to.</param>
/// <param name="Setting">The setting name exactly as it appears in a <c>secedit</c> template, e.g. <c>MinimumPasswordLength</c>.</param>
/// <param name="Value">Desired value, written into the template verbatim (numeric settings as their decimal text, e.g. <c>14</c>).</param>
/// <param name="Default">The documented Windows default value, used by "reset to Windows defaults" (revert of an apply replays the journaled prior, not this).</param>
public sealed record SecurityPolicyAction(
    SecurityPolicySection Section,
    string Setting,
    string Value,
    string Default) : ITweakAction
{
    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public string Kind => "SecurityPolicy";
}
