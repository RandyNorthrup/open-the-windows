using System.Text.Json;
using OpenTheWindows.Core.Catalog.Actions;

namespace OpenTheWindows.Core.Engine;

/// <summary>
/// The Microsoft Defender safety boundary the engine never crosses, regardless of
/// what a catalogue entry asks for: nothing may disable real-time protection or
/// interfere with signature (security-intelligence) updates. Refusing these is a
/// product non-negotiable (AGENTS.md) and is also asserted directly
/// against the shipped catalogue by a test.
/// </summary>
public static class DefenderSafety
{
    /// <summary>Registry subtree that controls Defender signature updates; writes here are refused.</summary>
    public const string SignatureUpdatesPathFragment = @"Windows Defender\Signature Updates";

    /// <summary>
    /// <c>MSFT_MpPreference</c> properties whose "true" value turns off a Defender
    /// protection; setting any of them is refused. Enabling protections (e.g.
    /// <c>PUAProtection</c>, <c>MAPSReporting</c>) is not on this list and is allowed.
    /// </summary>
    public static IReadOnlySet<string> ProtectionDisablingPreferences { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "DisableRealtimeMonitoring",
        "DisableAntiSpyware",
        "DisableAntiVirus",
        "DisableBehaviorMonitoring",
        "DisableIOAVProtection",
        "DisableOnAccessProtection",
        "DisableScriptScanning",
        "DisableSignatureRetirement",
        "SignatureDisableUpdateOnStartupWithoutEngine",
    };

    /// <summary>Whether a registry action targets the Defender signature-updates subtree.</summary>
    public static bool RefusesRegistry(RegistryAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action.Path.Contains(SignatureUpdatesPathFragment, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether a Defender-preference action would disable a protection or signature updates.</summary>
    public static bool RefusesDefender(DefenderPreferenceAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return ProtectionDisablingPreferences.Contains(action.Property) && IsTruthy(action.Value);
    }

    private static bool IsTruthy(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.Number => value.TryGetInt64(out long number) && number != 0,
        _ => false,
    };
}
