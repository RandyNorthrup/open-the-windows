# What Open the Windows refuses to do

Open the Windows runs elevated and changes operating-system state, so the product
is judged by what it *won't* do as much as by what it will. This page is the
public, permanent list of changes the app **refuses to make**, why, and what it
does instead. Nothing on this list can be applied — there is no catalogue entry
for it, and where a change could still be attempted (e.g. from a hand-written
catalogue override), the engine refuses it at plan time and writes an audit
event.

The rule behind every entry: *the app never disables the mechanisms that keep
Windows patched, defended, licensed and repairable.* Pausing, deferring and
pinning updates is a first-class feature (see
[docs/milestones/M4-catalogue-population.md](milestones/M4-catalogue-population.md)
and research 03); permanently breaking the update stack is not.

Sources: research [03 §1, §6, §7.3](research/03-windows-update-control.md),
[01 §17](research/01-telemetry-privacy-catalog.md),
[05 §7](research/05-performance-debloat-catalog.md); AGENTS.md non-negotiables;
PLAN.md D21.

## Windows Update stack

| Refused | Why | Instead |
| --- | --- | --- |
| Disable, delete, re-ACL or set `Start=4` on `WaaSMedicSvc`, `wuauserv`, `UsoSvc`, `DoSvc`, `BITS`, `TrustedInstaller`, `cryptsvc` | These run the update, servicing, Store, Defender-platform and .NET delivery paths. `WaaSMedicSvc` is a protected-process (PPL) service whose whole purpose is to repair exactly this damage; cumulative updates revert the change and leave the servicing stack broken (errors like `0x80070005`). | Detect tampering and offer to restore defaults; use the supported pause/defer/pin controls. |
| Disable, delete or re-ACL scheduled tasks under `\Microsoft\Windows\UpdateOrchestrator`, `\WindowsUpdate`, `\WaaSMedic`, `\InstallService` | Recreated by Medic; breaks deadline/reboot logic and update delivery. | Detect and report task health read-only. |
| `NoAutoUpdate=1`, `AUOptions=1` "kill switch" presets | Unbounded exposure; Medic and Microsoft nag campaigns fight it; users forget. | Offer `AUOptions` 3/4, deferral and pause instead. |
| `DisableWindowsUpdateAccess=1` | Microsoft: "Windows automatic updating is also disabled." | Use the documented UI-lockdown policies only in a managed profile. |
| `DoNotConnectToWindowsUpdateInternetLocations=1` outside a WSUS scenario | Microsoft documents that it can stop the Microsoft Store, Windows Update client policies and Delivery Optimization from working. | Only meaningful with WSUS, where the category is read-only anyway. |
| `DODownloadMode=100` | Documented to make some content fail to download. | Offer Delivery Optimization modes 0/1/99. |
| Faking a metered connection by taking ownership of `NetworkList\DefaultMediaCost` or editing `DusmSvc` profiles | ACL tampering that persists, is not a real pause, and throttles Store/OneDrive/Teams. | Deep-link to Settings › Network › Metered connection. |
| Multi-year "pause until 2051" registry dates as a one-click preset | Undocumented, reset by servicing, and leaves the device unpatched indefinitely. | 35-day pause by default; an explicit extended pause up to a configurable ceiling with a warning and an Event 4002. |
| `DisableWUfBSafeguards=1` outside an explicit IT-validation mode | Bypasses compatibility holds; Microsoft: only for IT validation, and it reverts after each feature update. | Advanced-only, behind a warning. |
| Pin `TargetReleaseVersion` to an end-of-service or non-upgrade (26H1) release | The device stops getting security updates until Microsoft force-migrates it. | The end-of-servicing calendar warns at 90/30 days and offers to clear an expired pin. |
| Hosts/firewall blocking of `*.windowsupdate.com`, `*.update.microsoft.com`, `*.delivery.mp.microsoft.com` | Breaks Store/Defender/Delivery Optimization; Defender flags it as `SettingsModifier:Win32/HostsFileHijack` and reverts it. | Use the documented policies, which stop the *sender*. |

## Microsoft Defender

| Refused | Why | Instead |
| --- | --- | --- |
| Block Defender security-intelligence (signature) updates — `Windows Defender\Signature Updates`, removing `MicrosoftUpdateServer`/`MMPC` from the fallback order, or `MpUpdate`-style disabling | Leaves the antivirus stale within days; these updates flow even during an update pause by design. The engine refuses any write under this subtree. | Show signature age on the dashboard; a repair flow runs `Update-MpSignature`. |
| Disable real-time protection / Defender AV (`DisableRealtimeMonitoring`, `DisableAntiSpyware`, killing `WinDefend`, `SecurityHealthService`, `wscsvc`) | Ignored/undone by Tamper Protection; leaves the device unprotected and breaks the Windows Security app and third-party AV registration. `DisableAntiSpyware` has been ignored since 2020. | Only expose the data-flow knobs (`SubmitSamplesConsent`, MAPS reporting) with a warning, plus enhanced-notification and MRT-reporting opt-outs. |
| Disable Automatic Root Certificates Update (`DisableRootAutoUpdate`) | TLS trust breaks progressively as new roots are not installed. | Never. |

## Security services and identity

| Refused | Why | Instead |
| --- | --- | --- |
| Disable protected services: `TrustedInstaller`, `SecurityHealthService`, `WinDefend`, `wscsvc`, `EventLog`, `Schedule`, `Winmgmt` | Breaks servicing, the Security app, logging, tasks and WMI (Intune/SCCM inventory). | Not offered. |
| Disable `wlidsvc`, `LicenseManager`, `ClipSVC`, `TokenBroker` | Feature updates stop being offered; Store/app licensing, activation and subscription features break. | Not offered on a consumer/pro tool. |
| Disable UAC (`EnableLUA=0`) | Breaks Store apps, Edge and app sandboxing; unsupported. | Not offered. |
| Registry ACL/ownership tricks on `HKLM\SOFTWARE\Policies` or `Microsoft\PolicyManager` to "make settings stick" | Breaks Group Policy/MDM management and future servicing; not revertible by normal tools. | Write policy through the Local GPO (`Registry.pol`) and respect existing management. |

## Apps and the shell

| Refused | Why | Instead |
| --- | --- | --- |
| Remove Microsoft Edge / WebView2 (dummy-exe or `--force-uninstall` tricks) | Unsupported outside the EEA; WebView2 and many inbox apps depend on it; Windows Update reinstalls it; leaves orphaned tasks/services. | Set the Edge privacy policies; hide it as the default browser. |
| Remove protected Appx packages: Store, App Installer (winget), `SecHealthUI`, `Microsoft.UI.Xaml`, `Microsoft.VCLibs`, `WindowsAppRuntime`, `XboxIdentityProvider`, `MicrosoftWindows.Client.*`, `ShellExperienceHost`, `StartMenuExperienceHost`, `AAD.BrokerPlugin`, `AccountsControl`, `LockApp` | Start menu, Settings, sign-in, Store, winget, Windows Security and auth break; some cannot be reinstalled without an in-place upgrade. | The debloat catalogue ships a hard deny list and only removes vetted consumer apps. |
| Delete `WindowsApps`/`SystemApps` folders or `takeown` on `C:\Windows` | Corrupts the servicing store; SFC/DISM failures; unbootable after a feature update. | Never. |
| Fight the User Choice Protection Driver (UCPD) by copying `powershell.exe` or `sc config UCPD start=disabled` | Bypasses a security driver that Windows re-enables via a scheduled task; violates Microsoft policy. | Use documented policies and Settings deep-links; verify writes by reading back. |

## Networking and telemetry

| Refused | Why | Instead |
| --- | --- | --- |
| Hosts-file / DNS-sinkhole blocking of Microsoft domains | Update, Store, Defender cloud, activation, sign-in and OneSettings share hostnames/CDNs with telemetry; wildcards over `microsoft.com` break TLS CRL/OCSP; Defender reverts hosts hijacks; endpoint lists rot. | Use the documented telemetry policies (they stop the sender); optionally per-executable outbound firewall rules. |
| `AllowTelemetry=0` on Home/Pro sold as "telemetry off" | On Home/Pro the floor is Required (1); 0 behaves as 1. Claiming "off" is dishonest. | Set Required and label the real effective level per edition. |
| Disable `DiagTrack` on managed devices, or `AllowTelemetry=0` while relying on Intune/WUfB/Autopatch/MDE reports | Those reports go dark; Microsoft asks for the service in MDE troubleshooting. | Detect management and cap at "Required, service running." |

Anything on this page that is requested anyway (for example, through a
hand-authored catalogue override) is refused at plan time, recorded in the
Windows Event Log as a refusal, and never partially applied. Placebo and
low-value "optimisations" that are merely useless rather than dangerous are
listed separately in [not-included.md](not-included.md).
