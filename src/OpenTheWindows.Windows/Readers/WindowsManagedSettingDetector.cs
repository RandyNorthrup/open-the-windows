using System.Runtime.Versioning;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Policy;

namespace OpenTheWindows.Windows.Readers;

/// <summary>
/// Detects whether a registry value is owned by Group Policy by reading the
/// Local Group Policy <c>Registry.pol</c> files. Read-only: it never writes a
/// policy.
/// </summary>
/// <remarks>
/// <para>
/// Group Policy's registry client-side extension only manages the four policy
/// trees, and it records every managed value in <c>Registry.pol</c>. A value the
/// Local GPO owns therefore appears there, keyed by the same path the catalogue
/// targets. The files are world-readable, so this check needs no elevation and
/// is deterministic — unlike an RSOP query, which requires administrator rights
/// and returns nothing on an unmanaged machine.
/// </para>
/// <para>
/// Scope for M2: the Local GPO machine and (base) user policy files. Per-user
/// MLGPO files under <c>GroupPolicyUsers\&lt;sid&gt;</c> need a SID the detector
/// signature does not carry, and per-value <em>MDM</em> attribution needs the
/// Policy CSP area/policy identifier the catalogue does not yet record; both are
/// deferred to the policy-engine milestone (M5). A value this detector cannot
/// positively tie to a policy is reported as <see cref="ManagedState.NotManaged"/>,
/// which is the safe direction — it never hides real drift, it only declines to
/// suppress it.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsManagedSettingDetector : IManagedSettingDetector
{
    private const string RegistryPolFileName = "Registry.pol";
    private readonly string _machinePolicyPath;
    private readonly string _userPolicyPath;

    /// <summary>Uses the system Local Group Policy directory.</summary>
    public WindowsManagedSettingDetector()
        : this(Path.Combine(Environment.SystemDirectory, "GroupPolicy"))
    {
    }

    /// <summary>Uses a specific Local Group Policy root (for tests).</summary>
    internal WindowsManagedSettingDetector(string groupPolicyRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupPolicyRoot);
        _machinePolicyPath = Path.Combine(groupPolicyRoot, "Machine", RegistryPolFileName);
        _userPolicyPath = Path.Combine(groupPolicyRoot, "User", RegistryPolFileName);
    }

    /// <inheritdoc />
    public ManagedState Detect(RegistryHive hive, string path, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(name);

        string policyPath = hive == RegistryHive.LocalMachine ? _machinePolicyPath : _userPolicyPath;
        return OwnedByPolicy(policyPath, path, name) ? ManagedState.GroupPolicy : ManagedState.NotManaged;
    }

    private static bool OwnedByPolicy(string policyPath, string path, string name)
    {
        byte[] bytes;
        try
        {
            if (!File.Exists(policyPath))
            {
                return false;
            }

            bytes = File.ReadAllBytes(policyPath);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        IReadOnlyList<PolicyRegistryEntry> entries;
        try
        {
            entries = PolicyRegistryFile.Parse(bytes);
        }
        catch (FormatException)
        {
            return false;
        }

        return entries.Any(entry =>
            string.Equals(entry.Key, path, StringComparison.OrdinalIgnoreCase)
            && IsValueMatch(entry.ValueName, name));
    }

    private static bool IsValueMatch(string entryValueName, string name)
    {
        // A direct write, a managed removal of the value, or removal of all values in the key.
        return string.Equals(entryValueName, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(entryValueName, "**Del." + name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(entryValueName, "**DelVals", StringComparison.OrdinalIgnoreCase);
    }
}
