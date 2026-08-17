using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Engine;

/// <summary>
/// Value-equality for observed vs desired state, shared by the read-only
/// <see cref="ScanEngine"/> and the apply-time verify step so the two never
/// disagree about what "compliant" means. Verify deliberately does not consult
/// managed-setting detection: after the engine writes a policy value through the
/// Local GPO it becomes managed by this product, and the check that matters is
/// whether the value now equals the desired value.
/// </summary>
public static class StateComparison
{
    /// <summary>Compares two registry values of the given <paramref name="type"/> for equality.</summary>
    public static bool RegistryEquals(JsonElement actual, JsonElement desired, RegistryValueType type) => type switch
    {
        RegistryValueType.Dword or RegistryValueType.Qword =>
            actual.ValueKind == JsonValueKind.Number && desired.ValueKind == JsonValueKind.Number
            && actual.TryGetUInt64(out ulong a) && desired.TryGetUInt64(out ulong d) && a == d,
        RegistryValueType.Sz or RegistryValueType.ExpandSz =>
            actual.ValueKind == JsonValueKind.String && desired.ValueKind == JsonValueKind.String
            && string.Equals(actual.GetString(), desired.GetString(), StringComparison.Ordinal),
        RegistryValueType.MultiSz => ArrayOfStringsEquals(actual, desired),
        RegistryValueType.Binary => BinaryEquals(actual, desired),
        _ => false,
    };

    /// <summary>
    /// Whether an Appx package's observed state satisfies a removal action. For
    /// an all-users removal, a package that is absent everywhere and either
    /// explicitly deprovisioned or never provisioned counts as compliant — a
    /// package that was never provisioned cannot return for new users, so its
    /// absence already meets the intent.
    /// </summary>
    public static bool AppxCompliant(AppxSnapshot snapshot, AppxRemovalMode mode)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return mode == AppxRemovalMode.CurrentUser
            ? !snapshot.InstalledForUser
            : !snapshot.InstalledForAnyUser && (snapshot.Deprovisioned || !snapshot.Provisioned);
    }

    /// <summary>Structural equality for arbitrary JSON values (numbers compared numerically).</summary>
    public static bool JsonEquals(JsonElement actual, JsonElement desired)
    {
        if (actual.ValueKind != desired.ValueKind)
        {
            return false;
        }

        switch (actual.ValueKind)
        {
            case JsonValueKind.Number:
                return actual.TryGetDecimal(out decimal a) && desired.TryGetDecimal(out decimal d)
                    ? a == d
                    : string.Equals(actual.GetRawText(), desired.GetRawText(), StringComparison.Ordinal);
            case JsonValueKind.String:
                return string.Equals(actual.GetString(), desired.GetString(), StringComparison.Ordinal);
            case JsonValueKind.Array:
                if (actual.GetArrayLength() != desired.GetArrayLength())
                {
                    return false;
                }

                for (int i = 0; i < actual.GetArrayLength(); i++)
                {
                    if (!JsonEquals(actual[i], desired[i]))
                    {
                        return false;
                    }
                }

                return true;
            default:
                return string.Equals(actual.GetRawText(), desired.GetRawText(), StringComparison.Ordinal);
        }
    }

    private static bool BinaryEquals(JsonElement actual, JsonElement desired)
    {
        if (actual.ValueKind != JsonValueKind.String || desired.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return actual.TryGetBytesFromBase64(out byte[]? actualBytes)
            && desired.TryGetBytesFromBase64(out byte[]? desiredBytes)
            && actualBytes.AsSpan().SequenceEqual(desiredBytes);
    }

    private static bool ArrayOfStringsEquals(JsonElement actual, JsonElement desired)
    {
        if (actual.ValueKind != JsonValueKind.Array || desired.ValueKind != JsonValueKind.Array
            || actual.GetArrayLength() != desired.GetArrayLength())
        {
            return false;
        }

        for (int i = 0; i < actual.GetArrayLength(); i++)
        {
            if (!string.Equals(actual[i].GetString(), desired[i].GetString(), StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
