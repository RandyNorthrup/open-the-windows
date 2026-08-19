namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// The stable ids of the read-only machine health checks emitted by
/// <see cref="IMachineHealthProbe.Run"/>. They live in Core so that baseline
/// <c>checkOnly</c> rules (<see cref="OpenTheWindows.Core.Audit.BaselineCheck"/>)
/// and the Windows probe that produces the results share one vocabulary and
/// cannot drift: the probe references these constants, the baseline validator
/// checks every referenced id against <see cref="All"/>.
/// </summary>
public static class HealthCheckIds
{
    /// <summary>Defender real-time protection is on.</summary>
    public const string DefenderRealtime = "defender.realtime";

    /// <summary>Tamper Protection is on.</summary>
    public const string TamperProtection = "defender.tamper-protection";

    /// <summary>UEFI Secure Boot is enabled.</summary>
    public const string SecureBoot = "boot.secure-boot";

    /// <summary>A TPM is present, enabled and activated.</summary>
    public const string Tpm = "boot.tpm";

    /// <summary>BitLocker protects the OS volume.</summary>
    public const string BitLockerOsVolume = "bitlocker.os-volume";

    /// <summary>Virtualisation-based security is running.</summary>
    public const string VbsDeviceGuard = "vbs.device-guard";

    /// <summary>LSA runs as a protected process (RunAsPPL).</summary>
    public const string LsaPpl = "lsa.ppl";

    /// <summary>Kernel DMA protection is available.</summary>
    public const string KernelDma = "dma.kernel";

    /// <summary>The Windows build is a supported Windows 11 build.</summary>
    public const string BuildSupport = "update.build-support";

    /// <summary>The local Administrators group membership count.</summary>
    public const string LocalAdmins = "accounts.local-admins";

    /// <summary>Device domain / workgroup join state.</summary>
    public const string JoinState = "identity.join-state";

    /// <summary>Windows Hello enrolment (per-user; machine scope informational).</summary>
    public const string WindowsHello = "identity.windows-hello";

    /// <summary>The Windows Firewall is enabled on all profiles.</summary>
    public const string FirewallProfiles = "firewall.profiles";

    /// <summary>Remote Desktop exposure and NLA state.</summary>
    public const string Rdp = "remote.rdp";

    /// <summary>SMBv1 removal and server signing.</summary>
    public const string SmbHardening = "smb.hardening";

    /// <summary>Remote-management services (WinRM, RemoteRegistry, Spooler).</summary>
    public const string RemoteManagementServices = "services.remote-management";

    /// <summary>Sudo and Developer Mode are off.</summary>
    public const string SudoDevMode = "apps.sudo-dev-mode";

    /// <summary>Smart App Control / WDAC policy state.</summary>
    public const string AppControlPolicy = "apps.control-policy";

    /// <summary>The Microsoft vulnerable-driver blocklist is enabled.</summary>
    public const string DriverBlocklist = "drivers.blocklist";

    /// <summary>Core isolation (HVCI / memory integrity) is enabled.</summary>
    public const string CoreIsolationHvci = "core-isolation.hvci";

    /// <summary>A supported (client, not Server) Windows edition.</summary>
    public const string EditionSupport = "edition.support";

    /// <summary>Windows Recovery Environment status.</summary>
    public const string WinRe = "recovery.winre";

    /// <summary>Host/port-proxy exposure.</summary>
    public const string NetworkExposure = "network.exposure";

    /// <summary>The Security event-log size.</summary>
    public const string EventLogSecuritySize = "eventlog.security-size";

    /// <summary>Defender exclusion paths.</summary>
    public const string DefenderExclusions = "defender.exclusions";

    /// <summary>Local security policy snapshot (needs elevation).</summary>
    public const string LocalSecurityPolicy = "policy.local-security";

    /// <summary>Registered antivirus products (Security Center).</summary>
    public const string ThirdPartyAv = "av.third-party";

    /// <summary>MDM enrolment state.</summary>
    public const string MdmEnrollment = "management.enrollment";

    /// <summary>Every known health-check id (ordinal set).</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        DefenderRealtime,
        TamperProtection,
        SecureBoot,
        Tpm,
        BitLockerOsVolume,
        VbsDeviceGuard,
        LsaPpl,
        KernelDma,
        BuildSupport,
        LocalAdmins,
        JoinState,
        WindowsHello,
        FirewallProfiles,
        Rdp,
        SmbHardening,
        RemoteManagementServices,
        SudoDevMode,
        AppControlPolicy,
        DriverBlocklist,
        CoreIsolationHvci,
        EditionSupport,
        WinRe,
        NetworkExposure,
        EventLogSecuritySize,
        DefenderExclusions,
        LocalSecurityPolicy,
        ThirdPartyAv,
        MdmEnrollment,
    };
}
