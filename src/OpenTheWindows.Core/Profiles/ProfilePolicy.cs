using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Profiles;

/// <summary>
/// The machine policy that governs how profiles may be used, read from
/// <c>HKLM\SOFTWARE\Policies\OpenTheWindows</c>. The product ships an ADMX
/// (<c>admx/OpenTheWindows.admx</c>) that writes these values; an administrator
/// can also set them directly or through Intune's PolicyManager.
/// </summary>
/// <param name="RequireSignedProfiles">
/// When <see langword="true"/>, a profile supplied as a file must carry a valid
/// detached signature from a trusted key before it may be scanned or applied
/// (see <see cref="ProfileTrust"/>). Built-in profiles are unaffected.
/// </param>
public sealed record ProfilePolicy(bool RequireSignedProfiles)
{
    /// <summary>The registry key, below <see cref="RegistryHive.LocalMachine"/>, that holds the policy values.</summary>
    public const string PolicyKeyPath = @"SOFTWARE\Policies\OpenTheWindows";

    /// <summary>The <c>RequireSignedProfiles</c> value name (REG_DWORD; 1 = required).</summary>
    public const string RequireSignedProfilesValueName = "RequireSignedProfiles";

    /// <summary>The default policy when nothing is configured: no restriction.</summary>
    public static ProfilePolicy Unrestricted { get; } = new(RequireSignedProfiles: false);

    /// <summary>
    /// Reads the policy from the machine hive via <paramref name="reader"/>. A
    /// missing key or value, or a zero value, means the policy is off; any non-zero
    /// <c>RequireSignedProfiles</c> DWORD turns it on.
    /// </summary>
    public static ProfilePolicy Read(IRegistryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        RegistryValueSnapshot snapshot = reader.Read(
            RegistryHive.LocalMachine, userSid: null, PolicyKeyPath, RequireSignedProfilesValueName);
        return new ProfilePolicy(IsTruthy(snapshot));
    }

    private static bool IsTruthy(RegistryValueSnapshot snapshot)
        => snapshot.Exists
            && snapshot.Data is JsonElement data
            && data.ValueKind == JsonValueKind.Number
            && data.TryGetInt64(out long value)
            && value != 0;
}
