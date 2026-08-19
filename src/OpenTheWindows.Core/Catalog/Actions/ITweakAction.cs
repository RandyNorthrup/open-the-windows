using System.Text.Json.Serialization;

namespace OpenTheWindows.Core.Catalog.Actions;

/// <summary>
/// One declarative state change. The set of kinds is closed on purpose: it is
/// the safety boundary of the product (ADR 0004). Adding a kind means adding a
/// record here, its schema, its detect/apply/revert implementation and tests.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(RegistryAction), "Registry")]
[JsonDerivedType(typeof(ServiceAction), "Service")]
[JsonDerivedType(typeof(ScheduledTaskAction), "ScheduledTask")]
[JsonDerivedType(typeof(AppxAction), "Appx")]
[JsonDerivedType(typeof(OptionalFeatureAction), "OptionalFeature")]
[JsonDerivedType(typeof(DefenderPreferenceAction), "DefenderPreference")]
[JsonDerivedType(typeof(PowerSettingAction), "PowerSetting")]
[JsonDerivedType(typeof(CommandAction), "Command")]
[JsonDerivedType(typeof(SecurityPolicyAction), "SecurityPolicy")]
public interface ITweakAction
{
    /// <summary>Stable discriminator name used in JSON, logs and journals.</summary>
    [JsonIgnore]
    string Kind { get; }
}
