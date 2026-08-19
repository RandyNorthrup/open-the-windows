using System.ComponentModel;
using System.Globalization;
using System.Management;
using System.Runtime.Versioning;
using System.Security;
using System.ServiceProcess;
using Microsoft.Win32;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows.Readers;

/// <summary>
/// Runs the read-only machine health checks (research 04 §5): Secure Boot, TPM,
/// Defender, BitLocker, VBS/HVCI/Credential Guard, LSA PPL, firewall, SMB, and
/// so on. Every check only reads; nothing here changes machine state. Checks
/// that need a source only an administrator can read report
/// <see cref="HealthStatus.Unknown"/> with an explanation rather than failing or
/// inventing a value.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsMachineHealthProbe : IMachineHealthProbe
{
    private const string CcsControl = @"SYSTEM\CurrentControlSet\Control";
    private const string CcsServices = @"SYSTEM\CurrentControlSet\Services";
    private const string CurrentVersion = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    private const string DefenderScope = @"\\.\root\Microsoft\Windows\Defender";
    private const string DeviceGuardScope = @"\\.\root\Microsoft\Windows\DeviceGuard";

    /// <inheritdoc />
    public IReadOnlyList<HealthCheckResult> Run() => [
        Guard(HealthCheckIds.DefenderRealtime, "Defender real-time protection", CheckDefenderRealtime),
        Guard(HealthCheckIds.TamperProtection, "Tamper Protection", CheckTamperProtection),
        Guard(HealthCheckIds.SecureBoot, "Secure Boot", CheckSecureBoot),
        Guard(HealthCheckIds.Tpm, "TPM present and ready", CheckTpm),
        Guard(HealthCheckIds.BitLockerOsVolume, "BitLocker on the OS volume", CheckBitLocker),
        Guard(HealthCheckIds.VbsDeviceGuard, "Virtualisation-based security", CheckVbs),
        Guard(HealthCheckIds.LsaPpl, "LSA protection (RunAsPPL)", CheckLsaPpl),
        Guard(HealthCheckIds.KernelDma, "Kernel DMA protection", CheckKernelDma),
        Guard(HealthCheckIds.BuildSupport, "Windows build in support", CheckBuildSupport),
        Guard(HealthCheckIds.LocalAdmins, "Local administrators", CheckLocalAdmins),
        Guard(HealthCheckIds.JoinState, "Device join state", CheckJoinState),
        Guard(HealthCheckIds.WindowsHello, "Windows Hello enrolment", CheckWindowsHello),
        Guard(HealthCheckIds.FirewallProfiles, "Firewall enabled on all profiles", CheckFirewall),
        Guard(HealthCheckIds.Rdp, "Remote Desktop exposure", CheckRdp),
        Guard(HealthCheckIds.SmbHardening, "SMB hardening", CheckSmb),
        Guard(HealthCheckIds.RemoteManagementServices, "Remote-management services", CheckRemoteServices),
        Guard(HealthCheckIds.SudoDevMode, "Sudo and Developer Mode", CheckSudoDevMode),
        Guard(HealthCheckIds.AppControlPolicy, "Smart App Control / WDAC", CheckAppControl),
        Guard(HealthCheckIds.DriverBlocklist, "Vulnerable-driver blocklist", CheckDriverBlocklist),
        Guard(HealthCheckIds.CoreIsolationHvci, "Core isolation (HVCI)", CheckCoreIsolation),
        Guard(HealthCheckIds.EditionSupport, "Supported Windows edition", CheckEdition),
        Guard(HealthCheckIds.WinRe, "Windows Recovery Environment", CheckWinRe),
        Guard(HealthCheckIds.NetworkExposure, "Host/port-proxy exposure", CheckNetworkExposure),
        Guard(HealthCheckIds.EventLogSecuritySize, "Security event-log size", CheckEventLog),
        Guard(HealthCheckIds.DefenderExclusions, "Defender exclusions", CheckDefenderExclusions),
        Guard(HealthCheckIds.LocalSecurityPolicy, "Local security policy snapshot", CheckLocalSecurityPolicy),
        Guard(HealthCheckIds.ThirdPartyAv, "Registered antivirus products", CheckThirdPartyAv),
        Guard(HealthCheckIds.MdmEnrollment, "MDM enrolment", CheckMdmEnrollment),
    ];

    private static HealthCheckResult Guard(string id, string title, Func<(HealthStatus Status, string Detail)> check)
    {
        try
        {
            (HealthStatus status, string detail) = check();
            return new HealthCheckResult(id, title, status, detail, null);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            return new HealthCheckResult(id, title, HealthStatus.Unknown, "Unavailable: " + ex.Message.Trim(), null);
        }
    }

    private static bool IsExpected(Exception ex) => ex is ManagementException
        or UnauthorizedAccessException or IOException or InvalidOperationException
        or Win32Exception or SecurityException or FormatException;

    // ----- checks -------------------------------------------------------------

    private static (HealthStatus, string) CheckDefenderRealtime() => WmiRead(
        DefenderScope, "SELECT * FROM MSFT_MpComputerStatus", status =>
        {
            if (status is null)
            {
                return (HealthStatus.Unknown, "Defender status unavailable.");
            }

            bool rtp = status["RealTimeProtectionEnabled"] is true;
            string mode = status["AMRunningMode"] as string ?? "unknown";
            string age = status["AntivirusSignatureAge"]?.ToString() ?? "?";
            string detail = string.Create(CultureInfo.InvariantCulture,
                $"Real-time protection {(rtp ? "on" : "off")}, mode {mode}, signature age {age} day(s).");
            return (rtp ? HealthStatus.Pass : HealthStatus.Fail, detail);
        });

    private static (HealthStatus, string) CheckTamperProtection() => WmiRead(
        DefenderScope, "SELECT * FROM MSFT_MpComputerStatus", status =>
        {
            if (status is null)
            {
                return (HealthStatus.Unknown, "Defender status unavailable.");
            }

            bool tamper = status["IsTamperProtected"] is true;
            return (tamper ? HealthStatus.Pass : HealthStatus.Warn,
                tamper ? "Tamper Protection is on." : "Tamper Protection is off.");
        });

    private static (HealthStatus, string) CheckSecureBoot()
    {
        if (HklmDword($@"{CcsControl}\SecureBoot\State", "UEFISecureBootEnabled") is not { } enabled)
        {
            return (HealthStatus.NotApplicable, "No Secure Boot state (legacy BIOS or not exposed).");
        }

        return enabled == 1
            ? (HealthStatus.Pass, "Secure Boot is enabled.")
            : (HealthStatus.Fail, "Secure Boot is disabled.");
    }

    private static (HealthStatus, string) CheckTpm() => WmiRead(
        @"\\.\root\CIMV2\Security\MicrosoftTpm", "SELECT * FROM Win32_Tpm", tpm =>
        {
            if (tpm is null)
            {
                return (HealthStatus.Fail, "No TPM detected.");
            }

            string spec = tpm["SpecVersion"] as string ?? "?";
            bool enabled = tpm["IsEnabled_InitialValue"] is true;
            bool activated = tpm["IsActivated_InitialValue"] is true;
            bool twoOh = spec.Contains("2.0", StringComparison.Ordinal);
            string detail = string.Create(CultureInfo.InvariantCulture,
                $"TPM spec {spec}, enabled {enabled}, activated {activated}.");
            return (twoOh && enabled && activated ? HealthStatus.Pass : HealthStatus.Warn, detail);
        });

    private static (HealthStatus, string) CheckBitLocker()
    {
        string systemDrive = (Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\").TrimEnd('\\');
        string query = string.Create(CultureInfo.InvariantCulture,
            $"SELECT ProtectionStatus, DriveLetter FROM Win32_EncryptableVolume WHERE DriveLetter='{systemDrive}'");
        return WmiRead(@"\\.\root\CIMV2\Security\MicrosoftVolumeEncryption", query, volume =>
        {
            if (volume is null)
            {
                return (HealthStatus.Unknown, "BitLocker status needs elevation or is unavailable.");
            }

            uint status = Convert.ToUInt32(volume["ProtectionStatus"] ?? 0u, CultureInfo.InvariantCulture);
            return status == 1
                ? (HealthStatus.Pass, $"OS volume {systemDrive} is BitLocker-protected.")
                : (HealthStatus.Warn, $"OS volume {systemDrive} protection status {status}.");
        });
    }

    private static (HealthStatus, string) CheckVbs() => WmiRead(
        DeviceGuardScope, "SELECT * FROM Win32_DeviceGuard", guard =>
        {
            if (guard is null)
            {
                return (HealthStatus.Unknown, "Device Guard status unavailable.");
            }

            uint vbs = Convert.ToUInt32(guard["VirtualizationBasedSecurityStatus"] ?? 0u, CultureInfo.InvariantCulture);
            List<int> running = ToIntList(guard["SecurityServicesRunning"]);
            string services = running.Count == 0 ? "none" : string.Join('+', running);
            string detail = string.Create(CultureInfo.InvariantCulture,
                $"VBS status {vbs} (2=running); services running {services} (1=CG,2=HVCI).");
            return (vbs == 2 ? HealthStatus.Pass : HealthStatus.Warn, detail);
        });

    private static (HealthStatus, string) CheckLsaPpl()
    {
        int ppl = HklmDword($@"{CcsControl}\Lsa", "RunAsPPL") ?? 0;
        return ppl is 1 or 2
            ? (HealthStatus.Pass, $"LSA runs as PPL (RunAsPPL={ppl}).")
            : (HealthStatus.Warn, "LSA is not running as a protected process.");
    }

    private static (HealthStatus, string) CheckKernelDma() => WmiRead(
        DeviceGuardScope, "SELECT * FROM Win32_DeviceGuard", guard =>
        {
            if (guard is null)
            {
                return (HealthStatus.Unknown, "Device Guard status unavailable.");
            }

            bool available = ToIntList(guard["AvailableSecurityProperties"]).Contains(3);
            return (available ? HealthStatus.Pass : HealthStatus.Warn,
                available ? "Kernel DMA protection is available." : "Kernel DMA protection is not reported available.");
        });

    private static (HealthStatus, string) CheckBuildSupport()
    {
        int build = ToInt(Hklm(CurrentVersion, "CurrentBuild"));
        string display = Hklm(CurrentVersion, "DisplayVersion") as string ?? "?";
        string detail = string.Create(CultureInfo.InvariantCulture, $"Build {build} ({display}).");
        return build >= OperatingSystemFacts.FirstWindows11Build
            ? (HealthStatus.Pass, detail)
            : (HealthStatus.Warn, detail + " Below the first Windows 11 build.");
    }

    private static (HealthStatus, string) CheckLocalAdmins()
    {
        string machine = Environment.MachineName;
        string query = string.Create(CultureInfo.InvariantCulture,
            $"SELECT PartComponent FROM Win32_GroupUser WHERE GroupComponent=\"Win32_Group.Domain='{machine}',Name='Administrators'\"");
        int count = WmiCount(@"\\.\root\CIMV2", query);
        string detail = string.Create(CultureInfo.InvariantCulture, $"{count} member(s) of the local Administrators group.");
        return count <= 3 ? (HealthStatus.Pass, detail) : (HealthStatus.Warn, detail);
    }

    private static (HealthStatus, string) CheckJoinState() => WmiRead(
        @"\\.\root\CIMV2", "SELECT PartOfDomain, Domain FROM Win32_ComputerSystem", cs =>
        {
            if (cs is null)
            {
                return (HealthStatus.Unknown, "Computer system information unavailable.");
            }

            bool domain = cs["PartOfDomain"] is true;
            string name = cs["Domain"] as string ?? "?";
            return (HealthStatus.NotApplicable,
                domain ? $"Domain-joined to {name}." : $"Workgroup member ({name}).");
        });

    private static (HealthStatus, string) CheckWindowsHello()
    {
        return (HealthStatus.NotApplicable, "Windows Hello enrolment is per-user and is not evaluated at machine scope.");
    }

    private static (HealthStatus, string) CheckFirewall()
    {
        const string Root = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy";
        (string Name, string Sub)[] profiles =
        [
            ("Domain", "DomainProfile"),
            ("Private", "StandardProfile"),
            ("Public", "PublicProfile"),
        ];

        var off = new List<string>();
        foreach ((string name, string sub) in profiles)
        {
            if ((HklmDword($@"{Root}\{sub}", "EnableFirewall") ?? 0) != 1)
            {
                off.Add(name);
            }
        }

        return off.Count == 0
            ? (HealthStatus.Pass, "Firewall is enabled on all profiles.")
            : (HealthStatus.Fail, "Firewall disabled on: " + string.Join(", ", off) + ".");
    }

    private static (HealthStatus, string) CheckRdp()
    {
        int deny = HklmDword($@"{CcsControl}\Terminal Server", "fDenyTSConnections") ?? 1;
        if (deny == 1)
        {
            return (HealthStatus.Pass, "Remote Desktop is disabled.");
        }

        int nla = HklmDword($@"{CcsControl}\Terminal Server\WinStations\RDP-Tcp", "UserAuthentication") ?? 0;
        return nla == 1
            ? (HealthStatus.Warn, "Remote Desktop is enabled with Network Level Authentication.")
            : (HealthStatus.Fail, "Remote Desktop is enabled without Network Level Authentication.");
    }

    private static (HealthStatus, string) CheckSmb()
    {
        int smb1Start = HklmDword($@"{CcsServices}\mrxsmb10", "Start") ?? 4;
        bool smb1Disabled = smb1Start == 4;
        int signing = HklmDword($@"{CcsServices}\LanmanServer\Parameters", "RequireSecuritySignature") ?? 0;
        string detail = string.Create(CultureInfo.InvariantCulture,
            $"SMBv1 {(smb1Disabled ? "disabled" : "enabled")}; server signing required={signing}.");
        return smb1Disabled ? (HealthStatus.Pass, detail) : (HealthStatus.Fail, detail);
    }

    private static (HealthStatus, string) CheckRemoteServices()
    {
        string[] names = ["WinRM", "RemoteRegistry", "Spooler"];
        var parts = new List<string>();
        foreach (string name in names)
        {
            parts.Add(name + "=" + DescribeService(name));
        }

        return (HealthStatus.NotApplicable, string.Join("; ", parts) + ".");
    }

    private static (HealthStatus, string) CheckSudoDevMode()
    {
        int sudo = HklmDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Sudo", "Enabled") ?? 0;
        int dev = HklmDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock", "AllowDevelopmentWithoutDevLicense") ?? 0;
        bool ok = sudo == 0 && dev == 0;
        string detail = string.Create(CultureInfo.InvariantCulture, $"Sudo enabled={sudo}, Developer Mode={dev}.");
        return ok ? (HealthStatus.Pass, detail) : (HealthStatus.Warn, detail);
    }

    private static (HealthStatus, string) CheckAppControl()
    {
        int state = HklmDword($@"{CcsControl}\CI\Policy", "VerifiedAndReputablePolicyState") ?? -1;
        string label = state switch
        {
            0 => "off",
            1 => "enforced",
            2 => "evaluation",
            _ => "unknown",
        };
        return (HealthStatus.NotApplicable, $"Smart App Control: {label}.");
    }

    private static (HealthStatus, string) CheckDriverBlocklist()
    {
        int enabled = HklmDword($@"{CcsControl}\CI\Config", "VulnerableDriverBlocklistEnable") ?? 0;
        return enabled == 1
            ? (HealthStatus.Pass, "Vulnerable-driver blocklist is enabled.")
            : (HealthStatus.Warn, "Vulnerable-driver blocklist is not enabled.");
    }

    private static (HealthStatus, string) CheckCoreIsolation()
    {
        int hvci = HklmDword($@"{CcsControl}\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled") ?? 0;
        return hvci == 1
            ? (HealthStatus.Pass, "Memory integrity (HVCI) is enabled.")
            : (HealthStatus.Warn, "Memory integrity (HVCI) is not enabled.");
    }

    private static (HealthStatus, string) CheckEdition()
    {
        string installation = Hklm(CurrentVersion, "InstallationType") as string ?? "?";
        string edition = Hklm(CurrentVersion, "EditionID") as string ?? "?";
        if (string.Equals(installation, "Server", StringComparison.OrdinalIgnoreCase))
        {
            return (HealthStatus.Fail, $"Windows Server ({edition}) is not supported.");
        }

        return (HealthStatus.Pass, $"Client edition {edition}.");
    }

    private static (HealthStatus, string) CheckWinRe()
    {
        return (HealthStatus.Unknown, "Windows RE status (reagentc) requires elevation; not read.");
    }

    private static (HealthStatus, string) CheckNetworkExposure()
    {
        int portProxies = 0;
        using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
        using (RegistryKey? tcp = baseKey.OpenSubKey($@"{CcsServices}\PortProxy\v4tov4\tcp", writable: false))
        {
            portProxies = tcp?.GetValueNames().Length ?? 0;
        }

        string hosts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
        int hostEntries = 0;
        if (File.Exists(hosts))
        {
            foreach (string line in File.ReadLines(hosts))
            {
                string trimmed = line.TrimStart();
                if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
                {
                    hostEntries++;
                }
            }
        }

        string detail = string.Create(CultureInfo.InvariantCulture,
            $"{portProxies} port-proxy rule(s); {hostEntries} active hosts-file entr(y/ies).");
        return portProxies == 0 ? (HealthStatus.Pass, detail) : (HealthStatus.Warn, detail);
    }

    private static (HealthStatus, string) CheckEventLog()
    {
        int maxSize = HklmDword($@"{CcsServices}\EventLog\Security", "MaxSize") ?? 0;
        const int MinBytes = 32 * 1024 * 1024;
        string detail = string.Create(CultureInfo.InvariantCulture, $"Security log MaxSize={maxSize} bytes.");
        return maxSize >= MinBytes ? (HealthStatus.Pass, detail) : (HealthStatus.Warn, detail);
    }

    private static (HealthStatus, string) CheckDefenderExclusions() => WmiRead(
        DefenderScope, "SELECT ExclusionPath FROM MSFT_MpPreference", pref =>
        {
            if (pref is null)
            {
                return (HealthStatus.Unknown, "Defender preferences unavailable.");
            }

            if (pref["ExclusionPath"] is not string[] paths)
            {
                return (HealthStatus.Pass, "No path exclusions (or not visible).");
            }

            return (paths.Length == 0 ? HealthStatus.Pass : HealthStatus.Warn,
                string.Create(CultureInfo.InvariantCulture, $"{paths.Length} path exclusion(s) reported."));
        });

    private static (HealthStatus, string) CheckLocalSecurityPolicy()
    {
        return (HealthStatus.Unknown, "Local security policy (secedit export) requires elevation; not read.");
    }

    private static (HealthStatus, string) CheckThirdPartyAv()
    {
        List<string> products = WmiStrings(@"\\.\root\SecurityCenter2", "SELECT displayName FROM AntiVirusProduct", "displayName");
        return products.Count == 0
            ? (HealthStatus.Warn, "No antivirus product registered with Security Center.")
            : (HealthStatus.Pass, "Registered antivirus: " + string.Join(", ", products) + ".");
    }

    private static (HealthStatus, string) CheckMdmEnrollment()
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using RegistryKey? enrollments = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Enrollments", writable: false);
        if (enrollments is null)
        {
            return (HealthStatus.NotApplicable, "No MDM enrolments.");
        }

        foreach (string subName in enrollments.GetSubKeyNames())
        {
            using RegistryKey? enrollment = enrollments.OpenSubKey(subName, writable: false);
            if (enrollment?.GetValue("EnrollmentState") is 1)
            {
                return (HealthStatus.NotApplicable, "Device is MDM-enrolled.");
            }
        }

        return (HealthStatus.NotApplicable, "No active MDM enrolment.");
    }

    // ----- helpers ------------------------------------------------------------

    private static string DescribeService(string name)
    {
        try
        {
            using var controller = new ServiceController(name);
            return string.Create(CultureInfo.InvariantCulture, $"{controller.StartType}/{controller.Status}");
        }
        catch (InvalidOperationException)
        {
            return "absent";
        }
    }

    private static object? Hklm(string subKey, string valueName)
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using RegistryKey? key = baseKey.OpenSubKey(subKey, writable: false);
        return key?.GetValue(valueName);
    }

    private static int? HklmDword(string subKey, string valueName) => Hklm(subKey, valueName) is int value ? value : null;

    private static int ToInt(object? value)
    {
        if (value is int i)
        {
            return i;
        }

        return int.TryParse(value as string, CultureInfo.InvariantCulture, out int parsed) ? parsed : 0;
    }

    private static List<int> ToIntList(object? value)
    {
        if (value is not System.Collections.IEnumerable sequence || value is string)
        {
            return [];
        }

        var items = new List<int>();
        foreach (object? item in sequence)
        {
            items.Add(Convert.ToInt32(item, CultureInfo.InvariantCulture));
        }

        return items;
    }

    private static T WmiRead<T>(string scope, string query, Func<ManagementBaseObject?, T> read)
    {
        using var searcher = new ManagementObjectSearcher(new ManagementScope(scope), new ObjectQuery(query));
        using ManagementObjectCollection results = searcher.Get();
        using ManagementBaseObject? first = results.Cast<ManagementBaseObject>().FirstOrDefault();
        return read(first);
    }

    private static int WmiCount(string scope, string query)
    {
        using var searcher = new ManagementObjectSearcher(new ManagementScope(scope), new ObjectQuery(query));
        using ManagementObjectCollection results = searcher.Get();
        return results.Count;
    }

    private static List<string> WmiStrings(string scope, string query, string property)
    {
        using var searcher = new ManagementObjectSearcher(new ManagementScope(scope), new ObjectQuery(query));
        using ManagementObjectCollection results = searcher.Get();
        var values = new List<string>();
        foreach (ManagementBaseObject item in results)
        {
            using (item)
            {
                if (item[property] is string text && !string.IsNullOrWhiteSpace(text))
                {
                    values.Add(text);
                }
            }
        }

        return values;
    }
}
