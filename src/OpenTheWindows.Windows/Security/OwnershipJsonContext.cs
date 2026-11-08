using System.Text.Json.Serialization;

namespace OpenTheWindows.Windows.Security;

/// <summary>Source-generated JSON contract for the policy ownership ledger.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = true)]
[JsonSerializable(typeof(List<OwnedPolicyValue>))]
internal sealed partial class OwnershipJsonContext : JsonSerializerContext;
