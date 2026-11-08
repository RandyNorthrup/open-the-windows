using System.Runtime.Versioning;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;

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
    private const string PoliciesSegment = @"\Policies\";
    private const string PoliciesRoot = @"SOFTWARE\Policies\";
    private readonly string _machinePolicyPath;
    private readonly string _userPolicyPath;
    private readonly Security.PolicyOwnershipLedger? _ownership;
    private readonly IRegistryReader? _registry;
    private readonly Lazy<ManagedState?> _organisation;

    /// <summary>
    /// Uses the system Local Group Policy directory, the machine policy-ownership ledger
    /// (so values this product wrote are not mistaken for an administrator's), and the
    /// device's MDM enrolment and domain membership.
    /// </summary>
    public WindowsManagedSettingDetector()
        : this(Path.Combine(Environment.SystemDirectory, "GroupPolicy"), new Security.PolicyOwnershipLedger(), new WindowsRegistryReader(), Interop.DeviceManagement.Detect)
    {
    }

    /// <summary>Uses a specific Local Group Policy root only (for tests).</summary>
    internal WindowsManagedSettingDetector(string groupPolicyRoot)
        : this(groupPolicyRoot, null, null, static () => null)
    {
    }

    /// <summary>Uses a specific Local Group Policy root, ownership ledger, registry and management source (for tests).</summary>
    internal WindowsManagedSettingDetector(
        string groupPolicyRoot, Security.PolicyOwnershipLedger? ownership, IRegistryReader? registry, Func<ManagedState?> organisation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupPolicyRoot);
        ArgumentNullException.ThrowIfNull(organisation);
        _machinePolicyPath = Path.Combine(groupPolicyRoot, "Machine", RegistryPolFileName);
        _userPolicyPath = Path.Combine(groupPolicyRoot, "User", RegistryPolFileName);
        _ownership = ownership;
        _registry = registry;
        _organisation = new Lazy<ManagedState?>(organisation);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A value in the Local GPO is managed by Group Policy unless this product wrote it and
    /// still owns it. On a device an organisation manages (MDM or a domain), a live machine
    /// policy value that is neither in the Local GPO nor owned by this product came from
    /// that organisation and is managed too.
    /// </remarks>
    public ManagedState Detect(RegistryHive hive, string path, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(name);

        string policyPath = hive == RegistryHive.LocalMachine ? _machinePolicyPath : _userPolicyPath;
        if (OwnedByPolicy(policyPath, path, name))
        {
            return hive == RegistryHive.LocalMachine && IsOwnedByThisProduct(path, name) ? ManagedState.NotManaged : ManagedState.GroupPolicy;
        }

        if (hive == RegistryHive.LocalMachine && IsPolicyKey(path) && _organisation.Value is { } organisation
            && LiveValue(path, name) is not null && !IsOwnedByThisProduct(path, name))
        {
            return organisation;
        }

        return ManagedState.NotManaged;
    }

    private bool IsOwnedByThisProduct(string path, string name)
        => _ownership is not null && _ownership.IsOwned(path, name, LiveValue(path, name));

    private System.Text.Json.JsonElement? LiveValue(string path, string name)
        => _registry?.Read(RegistryHive.LocalMachine, null, path, name) is { Exists: true, Data: { } data } ? data : null;

    private static bool IsPolicyKey(string path)
        => path.StartsWith(PoliciesRoot, StringComparison.OrdinalIgnoreCase)
            || path.Contains(PoliciesSegment, StringComparison.OrdinalIgnoreCase);

    private static bool OwnedByPolicy(string policyPath, string path, string name)
        => LocalPolicyFile.Entries(policyPath)?.Any(entry =>
            string.Equals(entry.Key, path, StringComparison.OrdinalIgnoreCase)
            && IsValueMatch(entry.ValueName, name)) == true;

    private static bool IsValueMatch(string entryValueName, string name)
    {
        // A direct write, a managed removal of the value, or removal of all values in the key.
        return string.Equals(entryValueName, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(entryValueName, "**Del." + name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(entryValueName, "**DelVals", StringComparison.OrdinalIgnoreCase);
    }
}
