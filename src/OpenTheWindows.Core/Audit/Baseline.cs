using System.Text.Json.Serialization;

namespace OpenTheWindows.Core.Audit;

/// <summary>
/// One security baseline: an ordered set of <see cref="BaselineRule"/>s that map
/// an external framework's control ids (Microsoft Security Baseline settings,
/// DISA STIG rule ids, CIS section ids) to the catalogue tweaks and read-only
/// health checks that satisfy them. Baseline files are catalogue data
/// (<c>catalog/baselines/*.json</c>), validated against
/// <c>catalog/schema/baseline.schema.json</c> and the structural rules in
/// <see cref="BaselineValidator"/>.
/// </summary>
/// <param name="Schema">Optional <c>$schema</c> reference for editors.</param>
/// <param name="SchemaVersion">Document schema version; this build understands <see cref="CurrentSchemaVersion"/>.</param>
/// <param name="Id">Stable baseline id, matching the file name, e.g. <c>disa-stig-v2r8</c>.</param>
/// <param name="Title">Human title, e.g. "DISA STIG for Microsoft Windows 11 V2R8".</param>
/// <param name="Rules">The rules, in document order.</param>
public sealed record Baseline(
    [property: JsonPropertyName("$schema")] string? Schema,
    int SchemaVersion,
    string Id,
    string Title,
    IReadOnlyList<BaselineRule> Rules)
{
    /// <summary>The only baseline schema version this build understands.</summary>
    public const int CurrentSchemaVersion = 1;
}
