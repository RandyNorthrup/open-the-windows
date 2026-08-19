using System.Globalization;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Core.Engine;

/// <summary>
/// Builds the <see cref="ActiveSetupComponent"/> for a profile's per-user logon
/// fallback: the deterministic key name and the <c>otw remediate --user-scope</c>
/// stub command an all-users apply installs so logged-off and future users are
/// re-enforced at their next sign-in. Pure (no registry access); the registrar
/// writes it. The profile id is validated here so an untrusted id can never inject
/// characters into the Active Setup registry path.
/// </summary>
public static class ActiveSetupFallback
{
    /// <summary>The registry path (under HKLM) that holds Active Setup components.</summary>
    public const string InstalledComponentsKey = @"SOFTWARE\Microsoft\Active Setup\Installed Components";

    /// <summary>
    /// The prefix every component this product registers shares, so they can be told
    /// apart from Windows' own components (for removal). Full keys are
    /// <c>{OTW-&lt;profileId&gt;}</c>.
    /// </summary>
    public const string ComponentKeyPrefix = "{OTW-";

    /// <summary>
    /// Builds the component for <paramref name="profileId"/>. The stub runs
    /// <paramref name="executablePath"/> as <c>remediate --user-scope --profile
    /// &lt;id&gt;</c>, which a signing-in user applies to their own hive without
    /// elevation.
    /// </summary>
    /// <param name="profileId">The profile's kebab-case id; validated to <c>[a-z0-9-]</c> only.</param>
    /// <param name="displayName">The component's display name (the key's default value).</param>
    /// <param name="executablePath">The full path to <c>otw.exe</c> (quoted in the stub).</param>
    /// <exception cref="ArgumentException">The id is empty or holds a character outside <c>[a-z0-9-]</c>.</exception>
    public static ActiveSetupComponent For(string profileId, string displayName, string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        if (!IsSafeId(profileId))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture,
                    $"Profile id '{profileId}' is not a kebab-case identifier; refusing to build an Active Setup key from it."),
                nameof(profileId));
        }

        string componentKey = string.Create(CultureInfo.InvariantCulture, $"{ComponentKeyPrefix}{profileId}}}");
        string stubPath = string.Create(CultureInfo.InvariantCulture, $"\"{executablePath}\" remediate --user-scope --profile {profileId}");
        return new ActiveSetupComponent(componentKey, displayName, stubPath);
    }

    /// <summary>Whether <paramref name="id"/> is made only of lower-case letters, digits and hyphens.</summary>
    private static bool IsSafeId(string id)
    {
        foreach (char c in id)
        {
            bool ok = c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-';
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }
}
