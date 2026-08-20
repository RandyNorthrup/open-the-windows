namespace OpenTheWindows.Core.Catalog;

/// <summary>
/// Services the catalogue may never reconfigure. Changing their start type
/// either breaks servicing/security (Windows Update, Defender, Security Center,
/// licensing, event logging) or is undone by Windows itself (WaaSMedicSvc).
/// Temporary stops inside the supervised update hold/repair flow are engine
/// behaviour, not catalogue actions, and are therefore not affected by this list.
/// </summary>
public static class ProtectedServices
{
    /// <summary>Case-insensitive short names.</summary>
    public static IReadOnlySet<string> Names { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Windows Update / servicing
        "WaaSMedicSvc", "wuauserv", "UsoSvc", "DoSvc", "BITS", "TrustedInstaller",
        // Security
        "SecurityHealthService", "WinDefend", "WdNisSvc", "Sense", "wscsvc", "MpsSvc", "BFE", "SgrmBroker",
        // Core platform / logging / licensing / identity
        "EventLog", "RpcSs", "RpcEptMapper", "DcomLaunch", "LSM", "SamSs", "KeyIso", "VaultSvc",
        "CryptSvc", "Schedule", "ProfSvc", "UserManager", "StateRepository", "AppXSvc", "ClipSVC",
        "LicenseManager", "TokenBroker", "wlidsvc", "NgcSvc", "NgcCtnrSvc", "Winmgmt", "Power", "PlugPlay",
        // Networking basics
        "Dnscache", "NlaSvc", "netprofm", "Netman", "nsi", "WinHttpAutoProxySvc",
        // Shell / input essentials
        "WpnService", "TextInputManagementService", "CoreMessagingRegistrar", "SystemEventsBroker",
        "TimeBrokerSvc", "Themes", "ShellHWDetection", "cbdhsvc",
    };

    /// <summary>Returns <see langword="true"/> when the service must never be reconfigured by the catalogue.</summary>
    public static bool IsProtected(string serviceName)
        => !string.IsNullOrWhiteSpace(serviceName) && Names.Contains(serviceName);
}
