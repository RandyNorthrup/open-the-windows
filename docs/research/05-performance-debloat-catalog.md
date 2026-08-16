# 05 - Performance, Debloat, Services, Scheduled Tasks and UI/UX Tweak Catalogue (Windows 11 23H2 / 24H2 / 25H2)

Research report for **Open The Windows** (C#/.NET, GUI + CLI). Compiled 2026-08-16.
Target OS: Windows 11 23H2 (22631), 24H2 (26100), 25H2 (26200). Editions: Home / Pro / Enterprise / Education (notes where behaviour differs).

Legend used throughout:

| Tag | Meaning |
|---|---|
| **KEEP** | Do not offer removal/disable at all (or only behind an "Advanced, I understand" gate). |
| **SAFE** | Safe to remove/disable for the vast majority of users; reversible; no hidden dependencies. |
| **CAUTION** | Removable, but has real dependencies or a re-install path; show a warning listing side effects. |
| **NEVER** | The app must refuse (system integrity, security, updates, licensing, or breaks other features). |
| Level: **Basic** | Default-on for everyone (pure UX/annoyance removal, no functional loss). |
| Level: **Balanced** | Default for the "recommended" profile; small functional trade-offs, all reversible. |
| Level: **Strict** | Opt-in; loses features some users want (Xbox, Phone Link, Widgets, OneDrive...). |
| Level: **Paranoid** | Opt-in with warning; may break Store/MSA/telemetry-dependent features. |
| Level: **Gamer** / **Developer** | Only in those profiles. |
| `[MS-DOC]` | Verified against learn.microsoft.com / support.microsoft.com / blogs.windows.com during this research (2026-08-16). |
| `[SRC]` | Verified against Raphire/Win11Debloat, ChrisTitusTech/winutil, Sophia Script or Andrew S Taylor sources (all read on 2026-08-16). |
| `[LOCAL]` | Verified on a local Windows 11 22H2 Pro machine (services/tasks dump on 2026-08-16); 24H2/25H2 defaults expected identical unless stated. |
| `[UNVERIFIED]` | Community knowledge that could not be confirmed against a primary source in this pass; treat as "needs a test-VM check before shipping". |

---

## 0. Executive summary and platform facts the app must respect

1. **Microsoft retired the "Provisioned apps installed with Windows client" page** (it now redirects to the generic "Overview of apps on Windows client devices"). The authoritative list is therefore what `Get-AppxProvisionedPackage -Online` returns on the target build; the catalogue below is assembled from that knowledge, the new 25H2 `RemoveDefaultMicrosoftStorePackages` policy list, and Raphire/Sophia/Taylor lists, then classified. `[MS-DOC]` `[SRC]`
2. **Windows 11 25H2 (Enterprise/Education/IoT Enterprise only) has a first-party policy to remove default Store packages**: `HKLM\SOFTWARE\Policies\Microsoft\Windows\Appx\RemoveDefaultMicrosoftStorePackages` (`Enabled`=1 plus one subkey per package family name). Its curated list (24 apps at 25H2 GA) is: Calculator, Camera, Feedback Hub, Microsoft 365 Copilot, Clipchamp, Microsoft Copilot, Microsoft News, Photos, Solitaire Collection, Sticky Notes, Microsoft Teams, To Do, MSN Weather, Notepad, Outlook for Windows, Paint, Quick Assist, Snipping Tool, Sound Recorder, Windows Media Player, Windows Terminal, Xbox Gaming App, Xbox Identity Provider, Xbox Speech to Text Overlay, Xbox TCUI. Apps removed this way return `0x80073D3F` on reinstall attempts while the policy is set. Microsoft flags Photos as "default handler for a common file type... we don't recommend removing this app". Not available on Home/Pro; the app should use it when present and fall back to `Remove-AppxProvisionedPackage` otherwise. `[MS-DOC Policy CSP ApplicationManagement]` `[SRC msendpointmgr, systemcenterdudes, patchmypc]`
3. **UCPD (User Choice Protection Driver, shipped with KB5034765, Feb 2024, and later)** blocks registry writes to `HKCU\...\Explorer\Advanced\TaskbarDa` (Widgets button) and to `UserChoice` keys for `http`/`https`/`.pdf` from well-known tools (powershell.exe, reg.exe...). Sophia Script works around it by copying `powershell.exe` to another name; the app must not assume a plain registry write will succeed on these values, must not attempt to set the default browser programmatically (unsupported), and should use the **policy** route for Widgets (`HKLM\SOFTWARE\Policies\Microsoft\Dsh\AllowNewsAndInterests=0`) or a Settings deep-link. `[SRC Sophia]` `[UNVERIFIED: exact UCPD allow-list]`
4. **Most CloudContent "consumer experience" policies are Enterprise/Education only** (`DisableWindowsConsumerFeatures`, `DisableSoftLanding`, `DisableWindowsSpotlightFeatures`, `DisableCloudOptimizedContent`, `DisableConsumerAccountStateContent`, `DisableSpotlightCollectionOnDesktop`, and Search `DoNotUseWebResults`). On Home/Pro the app must use the per-user `HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager` values instead. `[MS-DOC Policy CSP Experience/Search]`
5. **KB5072033 (Dec 9 2025, builds 26100.7462 / 26200.7462) moved `AppXSvc` (AppX Deployment Service) from Manual (trigger) to Automatic** "to improve reliability in some isolated scenarios". Do not treat Automatic as "user tampering", and do not set it back to Manual by default. `[SRC windowsforum summaries of the MS release note]` `[UNVERIFIED: exact release-note wording]`
6. **Feature updates (24H2 -> 25H2 etc.) re-provision inbox apps and reset some service/task/registry states.** The app needs a "re-apply after feature update" job (scheduled task or on-launch drift check). Outlook (new) specifically: after `Remove-AppxProvisionedPackage`, Windows 11 23H2 (March 2024 preview or later) and 24H2+ respect the deprovisioning; on Windows 10 use `HKLM\SOFTWARE\Microsoft\WindowsUpdate\Orchestrator\UScheduler_Oobe\BlockedOobeUpdaters` = REG_SZ `["MS_Outlook"]`; on Windows 11 additionally delete `...\UScheduler_Oobe\OutlookUpdate`. `[MS-DOC control-install]`
7. **Microsoft Edge cannot be uninstalled outside the EEA** (Windows makes Edge, Bing web search, Camera, Cortana and Photos uninstallable only in the EEA under the DMA). winutil's "dummy MicrosoftEdge.exe" trick and Raphire's `ForceRemoveEdge` are unsupported hacks that break Windows Sandbox's only browser and Store/WebView-dependent apps; the app should offer Edge *debloat policies* only. `[MS-DOC Windows Insider DMA blog]` `[SRC]`
8. **Copilot** is now a Store app (`Microsoft.Copilot`); `TurnOffWindowsCopilot` is deprecated and only affected the legacy sidebar. Microsoft's supported controls: uninstall the app, AppLocker rule for `MICROSOFT.COPILOT`, `RemoveMicrosoftCopilotApp` policy (Enterprise/Education, 24H2+), `SetCopilotHardwareKey` (remap the key), and `ShowCopilotButton=0` for the taskbar button. `[MS-DOC manage-windows-copilot, WindowsAI CSP]`
9. **Timer-resolution "optimizers" are placebo on Windows 11**: since Windows 10 2004 `timeBeginPeriod` is per-process, and since Windows 11 occluded/minimized processes do not get the higher resolution at all. `[MS-DOC timeBeginPeriod]`
10. Everything the app changes must be **snapshot-able and reversible** (registry export before write, service original start type, task original state, package re-registration path), because Windows Update reverts some settings and users will want an "Undo".

---

## 1. Inbox (provisioned) Appx packages on Windows 11 24H2 / 25H2

### 1.1 How provisioning works and the correct APIs

* **Provisioned package** = staged in the image, installed for every *new* user profile at first sign-in. **Installed package** = present in a given user's profile.
* `Remove-AppxPackage -Package <fullname>` removes for the current user; `-AllUsers` removes for all existing users. `Remove-AppxProvisionedPackage -Online -PackageName <fullname>` (or `-AllUsers`) prevents installation for future users. Both are needed for a clean removal (Microsoft's own Outlook guidance uses exactly this pair). `[MS-DOC control-install]`
* **C# equivalents** (`Windows.Management.Deployment.PackageManager`, WinRT, via CsWinRT / `net8.0-windows10.0.22621` TFM):
  * `PackageManager.FindPackagesForUser("", familyName)` / `FindProvisionedPackages()` to enumerate.
  * `RemovePackageAsync(fullName)` (current user) and `RemovePackageAsync(fullName, RemovalOptions.RemoveForAllUsers)` (needs admin).
  * `DeprovisionPackageForAllUsersAsync(packageFamilyName)` (Windows 10 2004+; needs admin) - the programmatic twin of `Remove-AppxProvisionedPackage`.
  * `ProvisionPackageForAllUsersAsync(familyName)` to re-provision an app that is still installed for someone; `RegisterPackageAsync(manifestUri, null, DeploymentOptions.DevelopmentMode)` to re-register from `C:\Program Files\WindowsApps\<pkg>\AppxManifest.xml` when the folder still exists; `AddPackageByUriAsync` for Store/appx bundles.
  * The DISM API (`DismRemoveProvisionedAppxPackage`) is unmanaged; PowerShell `Remove-AppxProvisionedPackage` hosted via `System.Management.Automation` is acceptable for the CLI backend.
* **Reinstall paths**: (a) `Get-AppxPackage -AllUsers <name> | % { Add-AppxPackage -DisableDevelopmentMode -Register "$($_.InstallLocation)\AppxManifest.xml" }` if the WindowsApps folder still exists; (b) Microsoft Store (`ms-windows-store://pdp/?ProductId=<id>`); (c) `winget install --id <StoreId> --source msstore`. Some packages (Store itself, XboxSpeechToTextOverlay, DesktopAppInstaller) are hard to bring back on a device without a working Store - hence NEVER/KEEP.
* Removal of *system* apps (`SignatureKind = System`; e.g. `MicrosoftWindows.Client.CBS`, `Microsoft.Windows.ShellExperienceHost`, `Microsoft.Windows.StartMenuExperienceHost`, `Microsoft.Windows.Search`, `Microsoft.UI.Xaml.CBS`, `MicrosoftWindows.Client.Core`, `Microsoft.SecHealthUI`, `Microsoft.LockApp`, `Microsoft.Win32WebViewHost`, `Microsoft.AAD.BrokerPlugin`, `Microsoft.AccountsControl`, `Microsoft.CredDialogHost`, `Microsoft.ECApp`, `Microsoft.XboxGameCallableUI`, `Windows.PrintDialog`, `Windows.CBSPreview`, `NcsiUwpApp`, `Microsoft.Windows.ContentDeliveryManager`, `Microsoft.Windows.CloudExperienceHost`, `Microsoft.Windows.PeopleExperienceHost`, `Microsoft.Windows.NarratorQuickStart`, `Microsoft.Windows.ParentalControls`, `Microsoft.Windows.PinningConfirmationDialog`, `Microsoft.Windows.XGpuEjectDialog`, `Microsoft.Windows.CapturePicker`, `Microsoft.Windows.FilePicker`, `Microsoft.Windows.AssignedAccessLockApp`, `Microsoft.Windows.CallingShellApp`, `Microsoft.Windows.OOBENetworkCaptivePortal`, `Microsoft.Windows.OOBENetworkConnectionFlow`, `Microsoft.Windows.PrintQueueActionCenter`, `Microsoft.Windows.Apprep.ChxApp` (SmartScreen), `Microsoft.Windows.AugLoop.CBS`, `MicrosoftWindows.UndockedDevKit`, `Microsoft.MicrosoftEdgeDevToolsClient`, `Microsoft.BioEnrollment`, `Microsoft.AsyncTextService`) is **NEVER** - they are not offered by Settings, and forced removal (`Remove-AppxPackage` fails with 0x80073CFA; tools use ACL/`EndOfLife` registry hacks) breaks the shell, sign-in, SmartScreen or Windows Security. The one exception commonly done by tools is `MicrosoftWindows.Client.CoreAI` / `Microsoft.Windows.AIHub` (winutil "Windows AI - Disable And Remove" uses the `AppxAllUserStore\EndOfLife` hack) - classify **Paranoid, Advanced-with-warning**, Copilot+ only.

### 1.2 Microsoft inbox apps - catalogue

Column "Comes back" answers: does a feature update / Windows Update / Store re-provision it after removal?

| Package name (family) | What it is | Verdict | Level | Comes back? | Dependencies / breakage if removed | Notes / source |
|---|---|---|---|---|---|---|
| `Microsoft.549981C3F5F10` (Cortana) | Cortana app (deprecated June 2023, no longer provisioned on 24H2+) | SAFE | Basic | No | None (Cortana search integration is gone) | Present only on upgraded 22H2/23H2 images. `[MS-DOC deprecated-features]` |
| `Microsoft.BingNews` / `Microsoft.News` | MSN / Microsoft Start News | SAFE | Basic | Feature update if provisioned; Store may re-offer | Widgets feed uses `MicrosoftWindows.Client.WebExperience`, not this app | Raphire default-remove. In 25H2 policy list. `[SRC]` |
| `Microsoft.BingWeather` | MSN Weather | SAFE | Basic | Feature update | Widgets weather is independent | In 25H2 policy list. `[MS-DOC]` |
| `Microsoft.BingSearch` | "Search" web app (24H2, new; hosts Bing web-results integration for Windows Search) | CAUTION | Balanced | Feature update / Store | Removing kills web results in Start search (which many users want) - it does **not** remove Search itself. In the EEA it is the officially uninstallable "Web Search from Microsoft Bing". | Raphire marks "optional". `[SRC]` `[MS-DOC DMA blog]` |
| `Microsoft.Copilot` (Store id 9NHT9RB2F4HD) | Consumer Copilot app | SAFE | Balanced | Yes - re-offered by Windows Update/Store, pinned again after feature update unless AppLocker / `RemoveMicrosoftCopilotApp` | None; Copilot key then opens Search / whatever `SetCopilotHardwareKey` says | Remove with `Remove-AppxPackage`; block re-install with AppLocker publisher rule `MICROSOFT.COPILOT`. `[MS-DOC manage-windows-copilot]` |
| `Microsoft.Windows.Ai.Copilot.Provider` | Copilot key/provider shim (23H2/24H2, some builds) | SAFE | Balanced | Yes (LCU) | None | Present on some builds only. `[UNVERIFIED]` |
| `Microsoft.MicrosoftOfficeHub` (Store id 9WZDNCRD29V9) | "Microsoft 365 Copilot" app (formerly Microsoft 365 / Office Hub) | SAFE | Balanced | Yes - Windows Update / Store re-offer; auto-installed for M365 desktop users | Copilot-key default target for Entra users; removing just makes the key fall back | `[MS-DOC manage-windows-copilot]` |
| `Microsoft.M365Companions` (People/Files/Calendar companions) | Microsoft 365 companion mini-apps (2025+, commercial devices) | SAFE | Balanced | Yes if tenant admin enables auto-install | None on consumer devices | Raphire "optional". `[SRC]` |
| `Microsoft.OutlookForWindows` | New Outlook (Windows 11 23H2+ preinstalled; pushed to Win10 Jan/Feb 2025) | SAFE | Balanced | 23H2 pre-March-2024 and Win10: yes via UScheduler_Oobe; 24H2+: no after de-provisioning | Mail/Calendar are gone (EOL Dec 31 2024), so removing Outlook leaves no inbox mail client | See 0.6 for the exact block keys. `[MS-DOC control-install]` |
| `Microsoft.windowscommunicationsapps` | Mail, Calendar, People (legacy) | SAFE | Basic | No (removed from image late 2024) | Mail/Calendar EOL; still on upgraded machines | `[MS-DOC control-install]` |
| `Microsoft.People` | People app | SAFE | Basic | No | Was a dependency of Mail/Calendar only | `[SRC Raphire]` |
| `MSTeams` (new Teams, `MSTeams_8wekyb3d8bbwe`) | Microsoft Teams (free / work) | SAFE | Balanced | Feature update re-provisions on consumer SKUs; Windows Update re-offers | Removes taskbar Chat/Teams; nothing else | Autostart: StartupTask (section 5). In 25H2 policy list. `[MS-DOC]` `[SRC]` |
| `MicrosoftTeams` (old "Chat"/Teams personal) | Old Teams personal (22H2/23H2) | SAFE | Basic | No | None | `[SRC]` |
| `Microsoft.MicrosoftSolitaireCollection` | Solitaire (ads/IAP) | SAFE | Basic | Feature update | None | 25H2 policy list. |
| `Microsoft.MicrosoftStickyNotes` | Sticky Notes | SAFE | Balanced | Feature update | Users may have notes (synced to OneNote/MSA) - warn about local unsynced notes | 25H2 policy list. |
| `Microsoft.Todos` | Microsoft To Do | SAFE | Balanced | Feature update | None | 25H2 policy list. |
| `Microsoft.WindowsFeedbackHub` | Feedback Hub | SAFE | Basic | Feature update | Insider feedback; Windows Update "give feedback" links | 25H2 policy list. |
| `Microsoft.GetHelp` | Get Help | CAUTION | Strict | Feature update | Windows 11 troubleshooters (Settings > System > Troubleshoot) launch Get Help; Windows Update / Activation "Get help" links break | Raphire marks **unsafe**. `[SRC]` |
| `Microsoft.Getstarted` (Tips) | Tips app (deprecated Nov 2023) | SAFE | Basic | Feature update until removed from image | Some "learn more" links | Cannot be uninstalled from Settings; PowerShell removal works. `[SRC Raphire]` `[MS-DOC deprecated-features]` |
| `Microsoft.PowerAutomateDesktop` (+ `...CopilotPlugin`) | Power Automate | SAFE | Basic | Feature update | None | `[SRC]` |
| `Microsoft.WindowsMaps` | Maps (deprecated Apr 2025, removed from Store Jul 2025) | SAFE | Basic | No | Offline maps for other apps (rare) | `[MS-DOC deprecated-features]` |
| `Microsoft.WindowsAlarms` | Clock (alarms, timers, focus sessions) | SAFE | Balanced | Feature update | Focus sessions entry in Settings/notification centre uses it | `[SRC]` |
| `Microsoft.WindowsCamera` | Camera | CAUTION | Strict | Feature update | Windows Hello enrolment does **not** need it, but Snipping Tool webcam/recording features, Teams "test camera" links, and the OOBE camera check use it; some Copilot+ Studio Effects entry points | 25H2 policy list; EEA-uninstallable. |
| `Microsoft.WindowsCalculator` | Calculator | CAUTION | Strict | Feature update | None functionally, but users expect it | 25H2 policy list; reinstall via Store. |
| `Microsoft.WindowsNotepad` | Notepad (Store) | CAUTION | Strict | Feature update | Default `.txt` handler; `notepad.exe` alias points to it (classic Notepad is the FoD `Microsoft.Windows.Notepad.System`) | 25H2 policy list. Disable AI features instead: `HKLM\SOFTWARE\Policies\WindowsNotepad\DisableAIFeatures=1`. `[SRC]` |
| `Microsoft.Paint` | Paint | CAUTION | Strict | Feature update | Default image editor; `mspaint.exe` alias | 25H2 policy list. Disable AI via `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint` (`DisableCocreator`, `DisableGenerativeFill`, `DisableImageCreator`, `DisableGenerativeErase`, `DisableRemoveBackground` = 1). `[MS-DOC WindowsAI CSP]` `[SRC]` |
| `Microsoft.ScreenSketch` | Snipping Tool | CAUTION | Strict | Feature update | PrtScn-to-Snipping-Tool, Win+Shift+S, screen recording | 25H2 policy list. Screen recorder policy `AllowScreenRecorder` (Ent/Edu 24H2+). |
| `Microsoft.WindowsSoundRecorder` | Sound Recorder | SAFE | Balanced | Feature update | None | 25H2 policy list. |
| `Microsoft.WindowsTerminal` | Windows Terminal | KEEP | - | Feature update | Default terminal host on Windows 11 (`DelegationConsole` / `DelegationTerminal`); winget/PowerShell UX; Raphire warns removing it kills the running script | 25H2 policy list allows it, but the app should refuse by default (Developer profile only, with a big warning). `[SRC]` |
| `Microsoft.WindowsStore` | Microsoft Store | NEVER | - | Feature update | Store, `winget --source msstore`, app updates for every inbox app, licence acquisition (`ClipSVC`), Xbox/Game Pass, some Win32 installers that pull MSIX dependencies | Raphire "unsafe - cannot be reinstalled easily". Reinstall requires `wsreset -i` or manual package chain. `[SRC]` |
| `Microsoft.StorePurchaseApp` | Store purchase/licensing UI | NEVER | - | - | Store purchases/subscriptions | Sophia excludes it. `[SRC]` |
| `Microsoft.DesktopAppInstaller` | App Installer = **winget** | NEVER | - | - | winget CLI, `.msix` double-click install, `ms-appinstaller:` | `[SRC]` |
| `Microsoft.SecHealthUI` | Windows Security UI | NEVER | - | - | Windows Security app (Defender UI) | Sysprep-blocking known issue on 24H2, so no tool should touch it. |
| `Microsoft.WindowsAppRuntime.*`, `Microsoft.VCLibs.*`, `Microsoft.NET.Native.*`, `Microsoft.UI.Xaml.2.x`, `Microsoft.Services.Store.Engagement`, `Microsoft.Advertising.Xaml` (framework packages) | Frameworks | NEVER | - | - | Every dependent app; Windows removes orphaned frameworks itself | Filter `IsFramework` / `PackageTypeFilter Framework` out of any UI. |
| `Microsoft.Windows.Photos` (+ `Microsoft.Photos.MediaEngineDLC`) | Photos | CAUTION | Strict | Feature update | **Default image viewer** for JPG/PNG/HEIC etc.; Microsoft's own 25H2 policy text: "default handler... we don't recommend removing this app" | Offer only with "install an alternative viewer first" warning. Legacy Windows Photo Viewer is not registered on Win11. `[MS-DOC]` |
| `Microsoft.ZuneMusic` | Media Player (also plays video) | CAUTION | Strict | Feature update | Default music/video handler after Films & TV removal | 25H2 policy list. |
| `Microsoft.ZuneVideo` | Films & TV / Movies & TV | SAFE | Balanced | No (dropped from new images 2024/25; Media Player supersedes) | Video default falls to Media Player | `[SRC Raphire]` |
| `Clipchamp.Clipchamp` | Clipchamp video editor | SAFE | Basic | Feature update | None | 25H2 policy list. |
| `Microsoft.Xbox.TCUI` | Xbox Title Callable UI (achievements/party overlay APIs) | CAUTION | Strict | Feature update | Raphire: "seems to be required for Microsoft Store, photos and certain games" - Xbox Live sign-in flows in games and Store game-purchase dialogs | Bundle all Xbox items into one "Gaming" toggle with a warning. `[SRC]` |
| `Microsoft.XboxGamingOverlay` | Xbox Game Bar | CAUTION | Strict / Gamer-keep | Feature update; Store | Win+G, Game DVR capture, controller bar, `ms-gamebar:` links (removing without also neutralising the protocol produces "ms-gamebar" popups); some games call the overlay | Prefer disabling DVR (section 6) over removal. Raphire's `Disable_Game_Bar_Integration.reg` points `ms-gamebar` / `ms-gamebarservices` at systray.exe. `[SRC]` |
| `Microsoft.XboxGameOverlay` | Game Bar plugin (legacy) | SAFE | Balanced | Feature update | Same as above | `[SRC]` |
| `Microsoft.XboxIdentityProvider` | Xbox Live sign-in provider | CAUTION | Strict | Feature update | **Breaks Game Pass, Xbox app, Minecraft, Forza, any Xbox Live-enabled title, and Store game purchases** | Raphire "unsafe"; Sophia unchecked. `[SRC]` |
| `Microsoft.XboxSpeechToTextOverlay` | Xbox speech-to-text | CAUTION | Strict | Hard to reinstall | Accessibility in some games | Raphire: "cannot be reinstalled easily". `[SRC]` |
| `Microsoft.GamingApp` | Xbox app (Game Pass client) | CAUTION | Strict | Feature update; Store | Game Pass PC installs go through it and `GamingServices` | 25H2 policy list. `[SRC]` |
| `Microsoft.GamingServices` (+ `GamingServicesNet` services) | Gaming Services (installed on first Xbox-app use) | CAUTION | Strict | Store re-installs on demand | Game Pass installs, Xbox app | Sophia unchecked. `[SRC]` |
| `Microsoft.YourPhone` | Phone Link | SAFE | Strict | Feature update | Cross-device: phone photos in Snipping Tool/File Explorer, Start "mobile device" panel; also see `MicrosoftWindows.CrossDevice` | Autostart via StartupTask; Start integration `HKCU\...\Start\Companions\Microsoft.YourPhone_8wekyb3d8bbwe\IsEnabled=0`. `[SRC]` |
| `MicrosoftWindows.CrossDevice` | Cross Device Experience Host (24H2) | CAUTION | Strict | LCU / Store | Phone Link continuation (Explorer "Mobile devices", phone as webcam, drag-tray sharing); Sophia excludes it from its removal UI | Prefer disabling in Settings > Bluetooth & devices > Mobile devices. `[SRC]` |
| `MicrosoftWindows.Client.WebExperience` | Windows Web Experience Pack = **Widgets** board/host | CAUTION | Strict | Store updates it; feature update re-provisions | Removing kills Widgets and lock-screen widgets; on 24H2 the Store may silently re-install it; safer to use policy `AllowNewsAndInterests=0` | winutil removes it + `Microsoft.WidgetsPlatformRuntime`. `[SRC]` `[MS-DOC NewsAndInterests CSP]` |
| `Microsoft.WidgetsPlatformRuntime` | Widgets platform runtime | CAUTION | Strict | Store | Widgets, third-party widget providers | `[SRC]` |
| `Microsoft.StartExperiencesApp` | Widgets "My Feed" / Start experiences (24H2+) | SAFE | Balanced | Store | Widgets feed | Raphire "optional". `[SRC]` |
| `Microsoft.MicrosoftEdge.Stable` | Microsoft Edge (MSIX registration of the Win32 browser) | NEVER (outside EEA) | - | Always (Windows Update / EdgeUpdate) | Windows Sandbox browser, WebView2 fallbacks, Store/Widgets/Copilot/Outlook-new WebView surfaces, `microsoft-edge:` protocol | Removal unsupported except in the EEA (Settings shows Uninstall there). Offer debloat policies only (1.4). `[MS-DOC DMA]` `[SRC]` |
| `Microsoft.Edge.GameAssist` | Edge Game Assist (in-game browser overlay, 2025) | SAFE | Balanced | Edge update | Game Bar "Game Assist" widget | Taylor list. `[SRC]` |
| `Microsoft.MicrosoftEdgeDevToolsClient` | Edge DevTools client (system) | NEVER | - | - | System app | - |
| `Microsoft.ApplicationCompatibilityEnhancements` | App-compat shims package | KEEP | - | LCU | Compat fixes for Win32 apps | Sophia excludes. `[SRC]` |
| `Microsoft.WebMediaExtensions`, `Microsoft.HEIFImageExtension`, `Microsoft.HEVCVideoExtension(s)`, `Microsoft.VP9VideoExtensions`, `Microsoft.WebpImageExtension`, `Microsoft.RawImageExtension`, `Microsoft.AV1VideoExtension`, `Microsoft.AVCEncoderVideoExtension`, `Microsoft.MPEG2VideoExtension`, `Microsoft.DolbyAudioExtensions` (OEM) | Codec/format extensions | KEEP | - | Store | Photos/Media Player/Explorer thumbnails for HEIC/HEIF/WebP/RAW/AV1/HEVC; some are OEM-licensed and cannot be re-obtained free | Sophia excludes all. Taylor removes AV1/MPEG2 (not recommended). `[SRC]` |
| `Microsoft.MixedReality.Portal` | Mixed Reality Portal | SAFE | Basic | No (removed 24H2) | WMR headsets (feature removed anyway) | `[MS-DOC removed-features]` |
| `MicrosoftCorporationII.QuickAssist` | Quick Assist (Store app since build 22572) | CAUTION | Strict | Feature update; Store | Remote help for/with family & helpdesk; the FoD `App.Support.QuickAssist` no longer exists on Win11 | Raphire default-remove (many admins remove for scam-prevention). 25H2 policy list. `[MS-DOC FoD]` `[SRC]` |
| `Microsoft.RemoteDesktop` | Remote Desktop (Store client, being replaced by "Windows App") | SAFE | Balanced | Store | Only if user uses AVD/W365/RDS | `[SRC]` |
| `MicrosoftCorporationII.MicrosoftFamily` | Family Safety | SAFE | Balanced | Feature update | Family Safety features for child accounts | Keep on devices with child accounts. `[SRC]` |
| `Microsoft.MicrosoftJournal` | Journal (pen) | SAFE | Basic | OEM/Store | None | `[SRC]` |
| `Microsoft.Whiteboard` | Whiteboard | SAFE | Basic | OEM/Store | None | `[SRC]` |
| `7EE7776C.LinkedInforWindows` | LinkedIn | SAFE | Basic | Store/CDM | None | In 25H2 policy dynamic-list examples. `[SRC]` |
| `Microsoft.Windows.DevHome` | Dev Home (retirement announced; removed from Store 2025) | SAFE | Basic | No | Dev Drive/WSL settings pages moved into Settings | Sophia unchecked ("Windows Advanced Settings"). `[SRC]` |
| `Microsoft.Windows.NarratorQuickStart` | Narrator QuickStart (system) | NEVER | - | - | Accessibility onboarding | System app. |
| Windows Backup app | Windows Backup (Settings-integrated app on 23H2+) | KEEP | - | LCU | Not offered for uninstall by Settings; system-registered | Instead disable nag: `HKCU\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.BackupReminder\Enabled=0` and account notifications (`Start_AccountNotifications=0`). Package family name `[UNVERIFIED]`. `[SRC Raphire]` |
| `Microsoft.OneDriveSync` (Store/MSIX OneDrive) | OneDrive Store version (rare; the Win32 per-user OneDrive is the normal one) | SAFE | Strict | Windows Update / feature update re-installs `OneDriveSetup.exe` per user | KFM (Desktop/Documents/Pictures redirected to OneDrive) - see 1.5 | `[SRC]` |
| `Microsoft.PCManager` | Microsoft PC Manager (bundled on some 2025 devices/regions) | SAFE | Basic | Store | None | Raphire default-remove. `[SRC]` |
| `Microsoft.Windows.AIHub`, `MicrosoftWindows.Client.CoreAI` (Copilot+ AI components), Recall/Semantic search/Click to Do system packages | Copilot+ on-device AI experiences | CAUTION | Paranoid | LCU | Recall/Click to Do/Semantic search; some are system packages that resist removal | Prefer policies: Recall via `Disable-WindowsOptionalFeature -FeatureName Recall` + `AllowRecallEnablement=0`; Click to Do `DisableClickToDo=1`; AI service `WSAIFabricSvc` -> Manual/Disabled. `[MS-DOC WindowsAI CSP]` `[SRC]` |
| `Microsoft.MSPaint` (Paint 3D) | Paint 3D (removed from Store Nov 2024) | SAFE | Basic | No | None | `[MS-DOC removed-features]` |
| `Microsoft.Microsoft3DViewer` | 3D Viewer (deprecated Feb 2026, Store removal Jul 1 2026) | SAFE | Basic | No | `.glb/.3mf` viewer | `[MS-DOC deprecated-features]` |
| `Microsoft.3DBuilder`, `Microsoft.Print3D`, `Microsoft.Office.OneNote` (UWP), `Microsoft.Office.Sway`, `Microsoft.SkypeApp`, `Microsoft.Messaging`, `Microsoft.OneConnect`, `Microsoft.BingFinance/Sports/Travel/FoodAndDrink/HealthAndFitness/Translator`, `Microsoft.NetworkSpeedTest`, `Microsoft.MicrosoftPowerBIForWindows`, `Microsoft.XboxApp` (Console Companion), `Microsoft.Wallet`, `Microsoft.Office.Todo.List`, `Microsoft.Office.Lens`, `Microsoft.SysinternalsSuite`, `Microsoft.HoloCamera/HoloItemPlayerApp/HoloShell` | Legacy/discontinued Windows 10-era or OEM-added Microsoft apps | SAFE | Basic | No | None | Kept in the removal list for upgraded machines. `[SRC Raphire/Taylor]` |

**Counts (Microsoft packages catalogued above): 71 table rows covering ~125 distinct package families -> SAFE 39 rows, CAUTION 20, KEEP 4 (rows; ~15 packages incl. all codec/framework families), NEVER 8 (rows; ~45 packages incl. all system apps listed in 1.1).** Third-party OEM/Store-promoted stubs below add 47 more entries plus ~30 OEM hub packages, all SAFE.

### 1.3 Third-party OEM / Store-promoted stubs (all SAFE, Level Basic)

These are either fully installed by the OEM or are **Start-menu stubs** ("Spotify", "TikTok", "Instagram", "Netflix", "Disney+", "Prime Video", "Candy Crush", "LinkedIn", "WhatsApp", "Facebook", "Messenger", "ESPN"...) created by **ContentDeliveryManager** at first sign-in; the stub is a pin plus a deferred Store install. Package IDs (from Raphire `Apps.json`, all default-selected): `ACGMediaPlayer, ActiproSoftwareLLC, AdobeSystemsIncorporated.AdobePhotoshopExpress, Amazon.com.Amazon, AmazonVideo.PrimeVideo, Asphalt8Airborne, AutodeskSketchBook, CaesarsSlotsFreeCasino, COOKINGFEVER, CyberLinkMediaSuiteEssentials, DisneyMagicKingdoms, Disney.37853FC22B2CE, DrawboardPDF, Duolingo-LearnLanguagesforFree, EclipseManager, FACEBOOK.FACEBOOK, FarmVille2CountryEscape, Flipboard, HiddenCity, HULULLC.HULUPLUS, iHeartRadio, Facebook.Instagram, king.com.BubbleWitch3Saga, king.com.CandyCrushSaga, king.com.CandyCrushSodaSaga, LinkedInforWindows, MarchofEmpires, 4DF9E0F8.Netflix, NYTCrossword, OneCalendar, PandoraMediaInc, PhototasticCollage, PicsArt-PhotoStudio, PolarrPhotoEditorAcademicEdition, flaregamesGmbH.RoyalRevolt, Sidia.LiveWallpaper, SlingTV, SpotifyAB.SpotifyMusic, BytedancePte.Ltd.TikTok, TuneInRadio, WinZipUniversal, 5A894077.McAfeeSecurity, C27EB4BA.DropboxOEM, E0469640.SmartAppearance, MirametrixInc.GlancebyMirametrix, RealtimeboardInc.RealtimeBoard` plus OEM hubs (`AD2F1837.HP*` / `AD2F1837.myHP`, `E046963F.LenovoCompanion`, `LenovoCompanyLimited.LenovoVantageService`, `DellInc.DellSupportAssistforPCs`, `DellInc.DellDigitalDelivery`, `DellInc.DellMobileConnect`, `LGElectronics.LGMonitorApp`, `AppUp.IntelGraphicsExperience`, `DolbyLaboratories.*`, `NVIDIACorp.NVIDIAControlPanel`, `RealtekSemiconductorCorp.RealtekAudioControl`, `SynapticsIncorporated.*`, `ELANMicroelectronicsCorpo.*`, `AdvancedMicroDevicesInc-2.AMDRadeonSoftware`). `[SRC Raphire, Taylor, Sophia]`

* **Do not default-remove driver control panels** (Intel Graphics Command Center, NVIDIA Control Panel, Realtek Audio Console, Dolby Access, AMD Radeon Software, Synaptics/ELAN touchpad): Sophia explicitly excludes them because the driver installs/needs the Store package. Show them under "OEM utilities", unchecked. `[SRC Sophia]`
* Windows Update can **silently re-install OEM companion apps via device metadata** (LG monitor app, Alienware, Razer): block with `HKLM\SOFTWARE\Policies\Microsoft\Windows\Device Metadata\PreventDeviceMetadataFromNetwork=1` (device metadata is deprecated as of May 2025 anyway). `[SRC Raphire/winutil]` `[MS-DOC deprecated-features]`

**Stop the stubs at the source (per user, `HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager`, all DWORD = 0):** `ContentDeliveryAllowed`, `OemPreInstalledAppsEnabled`, `PreInstalledAppsEnabled`, `PreInstalledAppsEverEnabled`, `SilentInstalledAppsEnabled`, `SubscribedContentEnabled`, `SystemPaneSuggestionsEnabled`, `SoftLandingEnabled`, `RotatingLockScreenEnabled`, `RotatingLockScreenOverlayEnabled`, `FeatureManagementEnabled`, `SubscribedContent-310093Enabled` (welcome experience), `-338387Enabled` (lock-screen tips), `-338388Enabled` (Start suggestions), `-338389Enabled` (tips), `-338393Enabled` / `-353694Enabled` / `-353696Enabled` / `-353698Enabled` (Settings suggestions), `-314559Enabled`, `-314563Enabled` (My People); delete `HKCU\...\ContentDeliveryManager\Subscriptions` and `SuggestedApps` subkeys. Machine-wide (Enterprise/Education only): `HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent\DisableWindowsConsumerFeatures=1`. Also write these into the **Default user hive** (load `C:\Users\Default\NTUSER.DAT` under `HKU\DefaultUserTmp`; `HKU\.DEFAULT` is *not* the default profile) so new profiles do not get stubs. `[SRC Raphire Disable_Windows_Suggestions.reg]` `[MS-DOC Experience CSP]`

### 1.4 Edge, Copilot, Teams, Widgets, Cortana, Recall, Dev Home - specific guidance

* **Edge**: outside the EEA removal is unsupported (see 0.7). Supported alternatives the app should implement (all `HKLM\SOFTWARE\Policies\Microsoft\Edge`, DWORD unless stated): `StartupBoostEnabled=0`, `BackgroundModeEnabled=0` (no msedge.exe at sign-in / after close), `HubsSidebarEnabled=0`, `ShowRecommendationsEnabled=0`, `EdgeCollectionsEnabled=0`, `EdgeShoppingAssistantEnabled=0`, `NewTabPageContentEnabled=0`, `NewTabPageHideDefaultTopSites=1`, `SpotlightExperiencesAndRecommendationsEnabled=0`, `ShowMicrosoftRewards=0`, `MicrosoftEdgeInsiderPromotionEnabled=0`, `WebWidgetAllowed=0`, `PersonalizationReportingEnabled=0`, `DiagnosticData=0`, `UserFeedbackAllowed=0`, `AlternateErrorPagesEnabled=0`, `DefaultBrowserSettingEnabled=0`, `DefaultBrowserSettingsCampaignEnabled=0`, `WalletDonationEnabled=0`, `ShowAcrobatSubscriptionButton=0`, `TabServicesEnabled=0`, `ConfigureDoNotTrack=1`, `HideFirstRunExperience=1`, `EdgeAssetDeliveryServiceEnabled=0`, `NewTabPageBingChatEnabled=0`, `CopilotPageContext=0`, `CopilotCDPPageContext=0`, `EdgeEntraCopilotPageContext=0`, `EdgeHistoryAISearchEnabled=0`, `ComposeInlineEnabled=0`, `GenAILocalFoundationalModelSettings=1`; `HKLM\SOFTWARE\Policies\Microsoft\EdgeUpdate\CreateDesktopShortcutDefault=0`. Edge services `edgeupdate` (Automatic Delayed) / `edgeupdatem` (Manual) may be set to Manual, not Disabled (Edge must still update - security). **Programmatic default-browser change is not supported** (UserChoice hash + UCPD); the app should deep-link `ms-settings:defaultapps` and let the user click. SetUserFTA-style hash forgery must be documented as unsupported and not shipped. `[SRC winutil/Raphire]` `[MS-DOC Edge policy reference]`
* **Copilot**: remove `Microsoft.Copilot` (per user + provisioned), `ShowCopilotButton=0` (`HKCU\...\Explorer\Advanced`), remap key via `HKCU\SOFTWARE\Policies\Microsoft\Windows\CopilotKey\SetCopilotHardwareKey` (AUMID string) or deep-link `ms-settings:personalization-textinput-copilot-hardwarekey`; block reinstall with an AppLocker packaged-app rule (publisher `CN=MICROSOFT CORPORATION, O=MICROSOFT CORPORATION, L=REDMOND, S=WASHINGTON, C=US`, package `MICROSOFT.COPILOT`, any version). `TurnOffWindowsCopilot` (HKCU/HKLM `SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot`) is harmless to set for 22H2/23H2 upgrade scenarios but deprecated. `RemoveMicrosoftCopilotApp` (Ent/Edu 24H2+) only removes when M365 Copilot is also present and the app is unused for 28 days. `[MS-DOC]`
* **Teams**: remove `MSTeams` (+ old `MicrosoftTeams`); Chat icon policy `HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Chat\ChatIcon=3` is deprecated (23H2 removed the Chat button); `TaskbarMn=0` (HKCU Explorer\Advanced) hides the button on 22H2; block auto-install of consumer Teams: `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Communications\ConfigureChatAutoInstall=0` `[UNVERIFIED but widely used]`. Autostart: disable the Teams StartupTask (section 5).
* **Widgets**: policy `HKLM\SOFTWARE\Policies\Microsoft\Dsh\AllowNewsAndInterests=0` ("applies to the entire widgets experience, including content on the taskbar"; Pro/Ent/Edu, 21H2+). Insider-preview policies `DisableWidgetsBoard`, `DisableWidgetsOnLockScreen` (same key). Home: `TaskbarDa=0` (blocked by UCPD from scripts; do it via Settings deep-link `ms-settings:taskbar`, or remove the package). Package removal (`MicrosoftWindows.Client.WebExperience`, `Microsoft.WidgetsPlatformRuntime`) works but on 24H2 the Store can silently re-install the Web Experience Pack. `[MS-DOC NewsAndInterests CSP]` `[SRC Sophia/winutil]`
* **Cortana**: gone; leftover policies `AllowCortana=0`, `CortanaConsent=0` (`HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search`) are harmless.
* **Recall / Click to Do (24H2 Copilot+ only)**: `Disable-WindowsOptionalFeature -Online -FeatureName Recall` (DISM `/Disable-Feature /FeatureName:Recall`), `HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsAI\AllowRecallEnablement=0` (removes the bits + snapshots, needs restart; Pro+), `DisableAIDataAnalysis=1` (HKLM + HKCU), `TurnOffSavingSnapshots=1` (older name, keep for compat), `DisableClickToDo=1` (HKLM + HKCU). Recall is *off by default on managed devices*. `[MS-DOC WindowsAI CSP]` `[SRC Raphire]`
* **Dev Home**: retired; safe removal. **Mail/Calendar**: EOL. **Maps**: deprecated. **Tips**: deprecated. **Steps Recorder**: deprecated FoD. **WordPad**: removed 24H2. **Mixed Reality**: removed 24H2. **Location History**: removed Mar 2025. **Suggested actions**: deprecated Dec 2024. `[MS-DOC deprecated/removed]`

### 1.5 OneDrive

* Ships as a per-user Win32 app installed at first sign-in from `%SystemRoot%\System32\OneDriveSetup.exe` (older builds `SysWOW64`; 24H2 also stages `%LocalAppData%\Microsoft\OneDrive\`). Uninstall: `OneDriveSetup.exe /uninstall` (winutil), or `winget uninstall Microsoft.OneDrive`, then remove `HKCU\...\Run\OneDrive`, the Explorer namespace pin (`HKCR\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}\System.IsPinnedToNameSpaceTree=0`), leftover `%LocalAppData%\Microsoft\OneDrive`, `%ProgramData%\Microsoft OneDrive`, the scheduled task `OneDrive Standalone Update Task-S-1-5-21-*`, and set `OneSyncSvc` to Manual. **Before uninstall the app must check `HKCU\Software\Microsoft\OneDrive\Accounts\Personal|Business1\UserEmail` and refuse/warn if signed in - Known Folder Move means Desktop/Documents/Pictures may live only in the cloud** (Sophia does exactly this check). winutil protects the folder with `icacls /deny Administrators:(D,DC)` during uninstall. Policies: `HKLM\SOFTWARE\Policies\Microsoft\Windows\OneDrive\DisableFileSyncNGSC=1` (blocks OneDrive entirely - also blocks Office/Teams sync features; Strict), `KFMBlockOptIn=1` (stop KFM nag). Windows Update/feature updates re-run OneDriveSetup for new users; the app needs a re-check. `[SRC winutil/Sophia/Raphire]`

---
## 2. Optional features (`Enable-/Disable-WindowsOptionalFeature`) and capabilities (`Get-WindowsCapability`)

| Feature / capability name | What | Default on 24H2 | Verdict | Level | Notes / source |
|---|---|---|---|---|---|
| `Recall` (optional feature) | Windows Recall (Copilot+ only) | Present on Copilot+ 24H2+; disabled on managed devices | SAFE to disable | Balanced (Paranoid to also policy-block) | `Disable-WindowsOptionalFeature -Online -FeatureName Recall`; pair with `AllowRecallEnablement=0`. `[MS-DOC]` |
| `MicrosoftWindowsPowerShellV2Root` / `MicrosoftWindowsPowerShellV2` | Windows PowerShell 2.0 engine | **Removed from 24H2 as of Aug 2025 (Server 2025 Sep 2025)**; on 23H2 present, disabled by default on clean installs | SAFE to disable | Balanced (security: downgrade-attack surface) | On builds where it exists, disabling is a hardening win. `[MS-DOC removed-features]` |
| `WMIC~~~~` (capability) | WMIC command-line tool | Not preinstalled on 24H2 (FoD, disabled by default); preinstalled on 22H2/23H2 | SAFE to remove | Balanced | Old scripts/installers that shell out to `wmic` break. `[MS-DOC FoD]` |
| `VBSCRIPT~~~~` (capability, 24H2+) | VBScript engine | Preinstalled FoD on 24H2 (still on by default), scheduled to become disabled-by-default | CAUTION | Paranoid | Removing breaks legacy `.vbs`, some installers, older Office bridges, some OEM tools. `[MS-DOC deprecated-features]` |
| `Microsoft.Windows.WordPad~~~~` | WordPad | Removed 24H2 (FoD on 22H2/23H2) | SAFE | Basic | `[MS-DOC]` |
| `App.StepsRecorder~~~~0.0.1.0` | Steps Recorder | Deprecated Nov 2023; may still be present | SAFE | Balanced | Sophia default-remove. `[MS-DOC]` `[SRC]` |
| `Browser.InternetExplorer~~~~0.0.11.0` | IE11 binaries (IE mode in Edge) | Preinstalled | CAUTION | Strict | Removing breaks **Edge IE mode** and some legacy LOB apps; MS lists it under "preinstalled - don't remove". Sophia default-removes it. `[MS-DOC FoD]` `[SRC]` |
| `Media.WindowsMediaPlayer~~~~0.0.12.0` | Windows Media Player Legacy | Preinstalled | SAFE | Balanced | Sophia note: removing also removes "Multimedia settings" from advanced Power Options. `[SRC]` |
| `MathRecognizer~~~~0.0.1.0` | Math input recognizer | Preinstalled | SAFE | Balanced | Only pen math input. `[MS-DOC FoD]` |
| `Print.Fax.Scan~~~~0.0.1.0` | Windows Fax and Scan | **Not preinstalled after 22H2** | SAFE | Basic | `[MS-DOC FoD]` |
| `XPS.Viewer~~~~0.0.1.0` | XPS Viewer | Not preinstalled | SAFE | Basic | `[MS-DOC]` |
| `Printing-XPSServices-Features` (feature) | Microsoft XPS Document Writer | Enabled | SAFE | Balanced | Sophia default-disable. `[SRC]` |
| `Printing-PrintToPDFServices-Features` | Microsoft Print to PDF | Enabled | KEEP | - | Users rely on it. |
| `Microsoft.Windows.PowerShell.ISE~~~~0.0.1.0` | PowerShell ISE | Preinstalled | SAFE | Strict (Developer keep) | `[MS-DOC]` |
| `OpenSSH.Client~~~~0.0.1.0` | OpenSSH client | Preinstalled | KEEP | - | Git/SSH workflows. `[MS-DOC]` |
| `OpenSSH.Server~~~~0.0.1.0` | OpenSSH server | Not installed | KEEP off | - | Security. |
| `Microsoft.Windows.Notepad.System~~~~0.0.1.0` | Classic Notepad fallback | Preinstalled | KEEP | - | Fallback when Store Notepad removed. `[MS-DOC]` |
| `Microsoft.Windows.MSPaint~~~~` / `Microsoft.Windows.SnippingTool~~~~` | Legacy Paint / Snipping FoDs | Not available where Store versions are preinstalled | n/a | - | `[MS-DOC]` |
| `Hello.Face.*` | Windows Hello Face drivers/recognizer | Preinstalled on devices with IR cameras | CAUTION | Strict | Removing kills face sign-in; only if no IR camera. |
| `Tools.DeveloperMode.Core~~~~0.0.1.0` | Device Portal/SSH for dev mode | Not installed | KEEP off unless Developer | Developer | Auto-installed when Developer Mode is enabled. `[MS-DOC]` |
| `Microsoft.Windows.Sense.Client~~~~` | Defender for Endpoint SENSE client (24H2 FoD) | Preinstalled on Pro+/Ent | NEVER | - | "Once this FOD is installed... it cannot be uninstalled" per MS. `[MS-DOC FoD]` |
| `SMB1Protocol` (+ `-Client/-Server/-Deprecation`) | SMBv1 | Disabled/removed by default (auto-removed after 15 days unused) | SAFE (ensure disabled) | Basic (security) | Verify state; never enable. |
| `SmbDirect` | SMB Direct (RDMA) | Enabled | KEEP | - | Harmless. |
| `WorkFolders-Client` | Work Folders | Enabled | SAFE | Balanced | Only enterprise Work Folders users; also service `workfolderssvc`. |
| `TelnetClient` | Telnet client | Disabled | KEEP off | - | Security. |
| `TFTP` | TFTP client | Disabled | KEEP off | - | - |
| `SimpleTCP` | Simple TCP/IP services | Disabled | KEEP off | - | - |
| `LPDPrintService`, `Printing-Foundation-LPRPortMonitor` | LPD/LPR printing (deprecated) | Disabled | KEEP off | - | `[MS-DOC deprecated]` |
| `Printing-Foundation-InternetPrinting-Client` | Internet Printing Client (IPP over HTTP) | Enabled | CAUTION | Paranoid | Some network printers use IPP; security baselines historically recommend disabling on hardened clients. |
| `MSMQ-Container` (+ subfeatures) | Message Queuing | Disabled | KEEP off | - | - |
| `LegacyComponents` / `DirectPlay` | DirectPlay | Disabled | SAFE (ensure disabled) | Basic | Old games may auto-prompt to enable. Sophia default-disable. `[SRC]` |
| `MediaPlayback` | Media Features (Media Player, playback infrastructure) | Enabled | CAUTION | Paranoid | Disabling breaks Media Player, media thumbnails, apps that use Media Foundation; Sophia offers it but warns. `[SRC]` |
| `WindowsMediaPlayer` (feature) | Legacy WMP (feature form) | Enabled | SAFE | Balanced | Same as the capability above. |
| `Windows-Defender-Default-Definitions` | Defender default definitions | Enabled | NEVER | - | - |
| `Windows-Defender-ApplicationGuard` | MDAG | Removed 24H2 | n/a | - | `[MS-DOC removed]` |
| `Windows-Identity-Foundation` | WIF 3.5 | Disabled | KEEP off | - | - |
| `NetFx3` | .NET Framework 3.5 | Disabled (on-demand) | KEEP off unless needed | - | Some old apps need it; install on demand. `[MS-DOC FoD]` |
| `NetFx4-AdvSrvs`, `WCF-Services45`, `WCF-TCP-PortSharing45` | .NET 4.x advanced services | Enabled | KEEP | - | Removing breaks apps. |
| `Microsoft-Hyper-V-All` (+ `-Hypervisor`, `-Services`, `-Management-*`) | Hyper-V (Pro+) | Disabled | KEEP as-is | Developer | Enable only for VMs; disabling when enabled loses VMs' host. |
| `HypervisorPlatform` | Windows Hypervisor Platform (VirtualBox/Android emulators) | Disabled | KEEP as-is | Developer | - |
| `VirtualMachinePlatform` | VMP (needed by WSL2, Sandbox, WSA) | Disabled unless WSL/Sandbox enabled | KEEP as-is | - | Turning off breaks WSL2. Gaming: MS says VMP/Memory Integrity can cost performance; still a security trade-off (section 7). |
| `Microsoft-Windows-Subsystem-Linux` | WSL (legacy feature; new WSL is the Store/MSIX app `MicrosoftCorporationII.WindowsSubsystemForLinux`) | Disabled | KEEP as-is | Developer | Sophia excludes the WSL package from removal. `[SRC]` |
| `Containers-DisposableClientVM` | Windows Sandbox (Pro+) | Disabled | KEEP as-is | - | Needs Edge present. |
| `Client-ProjFS` | Projected File System (VFS for Git) | Disabled | Developer only | - | - |
| `Microsoft-RemoteDesktopConnection` | mstsc | Enabled | KEEP | - | - |
| `SearchEngine-Client-Package` | Windows Search | Enabled | NEVER disable | - | Start search relies on it. |
| `IIS-*`, `Windows-Identity-Foundation`, `DataCenterBridging`, `MultiPoint-Connector` | Off by default | Disabled | leave | - | Do not offer. |
| `Microsoft.Windows.StorageManagement~~~~`, `Rsat.*`, `Tools.Graphics.DirectX~~~~`, `Msix.PackagingTool.Driver~~~~`, `Network.Irda~~~~`, `Windows.Desktop.EMS-SAC.Tools~~~~`, `Accessibility.Braille~~~~`, `Print.Management.Console~~~~`, `App.WirelessDisplay.Connect~~~~`, `Microsoft.WebDriver~~~~`, `RasCMAK.Client~~~~`, `RIP.Listener~~~~`, `SNMP.Client~~~~` | Not-preinstalled FoDs | Not installed | n/a | - | Do not offer "remove"; optionally offer "install" in Developer profile (RSAT, WirelessDisplay). `[MS-DOC FoD]` |
| `Microsoft.Windows.Wifi.Client.<Vendor>.*`, `Microsoft.Windows.Ethernet.Client.<Vendor>.*` | Inbox network driver FoDs | Preinstalled | KEEP | - | Removable per MS to save disk, but breaks networking after a hardware change. `[MS-DOC FoD]` |
| `Language.*`, `Microsoft.Wallpapers.Extended~~~~`, `DirectX.Configuration.Database~~~~`, `OneCoreUAP.OneSync~~~~` | Language / wallpaper / DirectX DB / OneSync FoDs | Preinstalled | KEEP | - | MS: "Don't remove these FODs from the Windows image". `[MS-DOC]` |

Total optional features/capabilities catalogued: 50 rows (~75 feature/capability names).

---

## 3. Services

### 3.1 Ground rules the app must encode

* Default startup types below are `[LOCAL]` from a Windows 11 22H2 Pro dump plus the Microsoft "Security guidelines for system services" reference (Server 2016, but the same binaries), corrected for known Windows 11 differences. "Manual" here includes "Manual (Trigger Start)"; **a Manual/trigger service costs nothing while idle - setting it to Disabled buys no performance and only removes a capability**. The only services that consume resources continuously are the Automatic ones actually running.
* Windows Update **resets** some startup types (e.g. `wuauserv`, `UsoSvc`, `WaaSMedicSvc` are self-healing via WaaSMedic; feature updates reset `DiagTrack`, `WSearch`, `SysMain`, `Spooler` to defaults). The app must re-check after updates and must never fight WaaSMedic.
* Several services are **protected** (PPL/`sc sdset` denied): `WaaSMedicSvc`, `WdNisSvc`, `WinDefend`, `Sense`, `MDCoreSvc`, `SecurityHealthService`, `wscsvc`, `SgrmBroker`, `TrustedInstaller`, `StateRepository` and `TextInputManagementService` return *Access denied* when tools try to change them (winutil issues #775/#867). Do not attempt; do not "take ownership" of their registry keys.
* Per-user services (`CDPUserSvc_xxxxx`, `OneSyncSvc_xxxxx`, `WpnUserService_xxxxx`, `cbdhsvc_xxxxx`, `BcastDVRUserService_xxxxx`, `UnistoreSvc`, `UserDataSvc`, `PimIndexMaintenanceSvc`, `MessagingService`, `PrintWorkflowUserSvc`, `DevicesFlowUserSvc`, `CaptureService`, `ConsentUxUserSvc`, `CredentialEnrollmentManagerUserSvc`, `DeviceAssociationBrokerSvc`, `DevicePickerUserSvc`, `BluetoothUserService`, `NPSMSvc`, `PenService`, `UdkUserSvc`, `P9RdrService`, `webthreatdefusersvc`, `CloudBackupRestoreSvc`, `AarSvc`) are instantiated per session from a template key without the suffix - change the **template** key (`HKLM\SYSTEM\CurrentControlSet\Services\<name>` `Start`) and expect the change on next sign-in; `Set-Service` on the suffixed instance often fails.
* Recommendation vocabulary: **NEVER** (refuse), **KEEP** (leave at default; do not offer), **MANUAL-OK** (may be set to Manual; near-zero benefit), **DISABLE-OK** (may be disabled with the listed side effects), **DISABLE-RECOMMENDED** (privacy/security win, no functional loss for most).

### 3.2 Service table (219 rows, ~260 service names)

| Service | Display name / purpose | Default (Win11 24H2) | Recommendation | Rationale / side effects |
|---|---|---|---|---|
| `AJRouter` | AllJoyn Router | Manual (trigger) - removed in 24H2 | DISABLE-OK / gone | AllJoyn retired Oct 2024. `[MS-DOC removed]` |
| `ALG` | Application Layer Gateway | Manual | KEEP | Only for ICS/legacy FTP; harmless. |
| `AppIDSvc` | Application Identity (AppLocker) | Manual (trigger) | NEVER | AppLocker/SmartScreen policy evaluation. |
| `Appinfo` | Application Information (UAC elevation) | Manual (trigger) | NEVER | UAC prompts. `[MS-DOC]` |
| `AppMgmt` | Application Management (GPO software install) | Manual | KEEP | Domain software deployment. |
| `AppReadiness` | App Readiness | Manual | NEVER | First-sign-in app provisioning; MS "Do not disable". `[MS-DOC]` |
| `AppXSvc` | AppX Deployment Service | Manual (trigger) -> **Automatic since KB5072033 (Dec 2025)** | NEVER | Store/MSIX install & updates; new default per MS. |
| `AssignedAccessManagerSvc` | Kiosk mode manager | Manual (trigger) | KEEP | Kiosk/assigned access; trigger only. |
| `AudioEndpointBuilder`, `Audiosrv` | Windows Audio | Automatic | NEVER | Sound. |
| `autotimesvc` | Cellular Time | Manual (trigger) | KEEP | Only WWAN devices. |
| `AxInstSV` | ActiveX Installer | Manual | DISABLE-OK | Legacy IE ActiveX; MS "OK to disable". `[MS-DOC]` |
| `BcastDVRUserService_*` | Game DVR and Broadcast (per-user) | Manual | DISABLE-OK (Gamer profile keep) | Only Game Bar capture; disable DVR by policy instead. |
| `BDESVC` | BitLocker Drive Encryption Service | Manual (trigger) | NEVER | BitLocker unlock/manage. |
| `BFE` | Base Filtering Engine | Automatic | NEVER | Firewall/IPsec/VPN foundation. |
| `BITS` | Background Intelligent Transfer | Manual (delayed) | NEVER | Windows Update, Store, Defender defs. |
| `BluetoothUserService_*`, `bthserv`, `BTAGService`, `BthAvctpSvc`, `BthHFSrv` | Bluetooth stack | Manual (trigger) | DISABLE-OK only if no Bluetooth radio | Breaks all BT; trigger-start = free otherwise. |
| `BrokerInfrastructure` | Background Tasks Infrastructure | Automatic | NEVER | UWP background tasks, Start, notifications. |
| `camsvc` | Capability Access Manager | Manual (trigger) | NEVER | Camera/mic/location consent for apps. |
| `CaptureService_*` | Screen capture (Snipping/GameBar) | Manual | KEEP | Trigger. |
| `cbdhsvc_*` | Clipboard User Service | Automatic (per user) | NEVER (KEEP) | **Clipboard history (Win+V) and cloud clipboard**; older winutil set it Manual and broke Win+V. `[SRC]` |
| `CDPSvc` | Connected Devices Platform | Automatic (delayed, trigger) | KEEP (DISABLE-OK Paranoid) | Nearby Share, Phone Link, cross-device resume, "Continue on PC"; MS "No guidance". Prefer per-feature toggles (`CdpSessionUserAuthzPolicy`). |
| `CDPUserSvc_*` | Connected Devices Platform User | Automatic | KEEP (DISABLE-OK Paranoid) | Same; MS "OK to disable" on Server; on 24H2 disabling breaks Phone Link/Cross Device. |
| `CertPropSvc` | Certificate Propagation | Manual (trigger) | KEEP | Smart-card certs. |
| `ClipSVC` | Client License Service | Manual (trigger) | NEVER | Store app licensing; disabling breaks Store apps launching. |
| `cloudidsvc` | Microsoft Cloud Identity | Manual | KEEP | Entra/MSA. |
| `COMSysApp` | COM+ System Application | Manual | KEEP | - |
| `ConsentUxUserSvc_*` | Consent UX | Manual | KEEP | Permission prompts. |
| `CoreMessagingRegistrar` | CoreMessaging | Automatic | NEVER | Shell/WinUI IPC. |
| `CredentialEnrollmentManagerUserSvc_*` | Credential Enrollment Manager | Manual | KEEP | Windows Hello. |
| `CryptSvc` | Cryptographic Services | Automatic | NEVER | Certs, WU, signing. |
| `CscService` | Offline Files | Manual (trigger) - Disabled on 24H2 clean installs `[UNVERIFIED]` | DISABLE-OK | winutil disables; only Work Folders/Offline Files users need it. |
| `DcomLaunch` | DCOM Server Process Launcher | Automatic | NEVER | Everything. |
| `dcsvc` | Declared Configuration | Manual (delayed, trigger) | KEEP | MDM. |
| `defragsvc` | Optimize drives | Manual | KEEP | Scheduled TRIM/optimize. |
| `DeviceAssociationService`, `DeviceInstall`, `DsmSvc`, `DsSvc`, `DevQueryBroker`, `DevicePickerUserSvc_*`, `DevicesFlowUserSvc_*`, `DeviceAssociationBrokerSvc_*` | Device pairing / PnP | Manual (trigger) (DAS: Automatic trigger) | NEVER | Plug and Play, pairing. |
| `Dhcp` | DHCP Client | Automatic | NEVER | Networking. |
| `diagnosticshub.standardcollector.service` | Diagnostics Hub Standard Collector | Manual | DISABLE-OK | Visual Studio profiling only. |
| `diagsvc` | Diagnostic Execution Service | Manual (trigger) | KEEP | Troubleshooters. |
| `DiagTrack` | Connected User Experiences and Telemetry | Automatic | **DISABLE-OK** (Balanced) | Telemetry upload. Side effects: Windows Update for Business reports, Windows Insider flighting, some Feedback Hub features, Update Compliance/Intune "device health". winutil/Raphire/Sophia all disable. Prefer `AllowTelemetry` policy + service Disabled. `[SRC]` |
| `DialogBlockingService` | Dialog Blocking | Disabled | KEEP | Already disabled. |
| `DispBrokerDesktopSvc` | Display Policy Service | Automatic | NEVER | Display config, night light, HDR. |
| `DisplayEnhancementService` | Display Enhancement | Manual (trigger) | KEEP | Brightness/adaptive on laptops. |
| `DmEnrollmentSvc` | Device Management Enrollment | Manual | KEEP | Intune enrolment. |
| `dmwappushservice` | WAP Push (dmwappushsvc) | Manual (delayed, trigger) | KEEP (DISABLE-OK Paranoid) | MS: required for Intune/MDM and *also* used by DiagTrack; disabling breaks MDM enrolment. Raphire/winutil no longer disable it. `[MS-DOC]` |
| `Dnscache` | DNS Client | Automatic (trigger) | NEVER | Name resolution; cannot be stopped anyway. |
| `DoSvc` | Delivery Optimization | Automatic (delayed, trigger) | NEVER (configure DODownloadMode instead) | Windows Update, Store, Defender downloads go through it; disabling stalls updates. Use `DODownloadMode=1` (LAN) or `0`/`99` (HTTP only). `[MS-DOC DO CSP]` |
| `dot3svc` | Wired AutoConfig | Manual | KEEP | 802.1X. |
| `DPS` | Diagnostic Policy Service | Automatic | KEEP | Troubleshooters, network diagnostics, reliability monitor; disabling gives no perf. |
| `DusmSvc` | Data Usage | Automatic | KEEP (MANUAL-OK) | Settings > Data usage, metered-connection logic. |
| `EapHost` | Extensible Authentication Protocol | Manual | NEVER | Wi-Fi WPA2/3-Enterprise, VPN. |
| `edgeupdate`, `edgeupdatem`, `MicrosoftEdgeElevationService` | Edge updater / elevation | Automatic (delayed) / Manual / Manual | MANUAL-OK (not Disabled) | Edge must still update for security; Manual keeps scheduled-task-driven updates. |
| `EFS` | Encrypting File System | Manual (trigger) | NEVER | EFS-encrypted files/PDE. |
| `embeddedmode` | Embedded Mode | Manual (trigger) | KEEP | - |
| `EntAppSvc` | Enterprise App Management | Manual | KEEP | MDM app install. |
| `EventLog` | Windows Event Log | Automatic | NEVER | - |
| `EventSystem` | COM+ Event System | Automatic | NEVER | SENS, network awareness. |
| `Fax` | Fax | Manual (only if Fax & Scan installed) | DISABLE-OK | Not present on 23H2+ clean installs. |
| `fdPHost`, `FDResPub` | Function Discovery Provider Host / Resource Publication | Manual (FDResPub trigger) | KEEP (DISABLE-OK Paranoid) | Network discovery of PCs/printers/media servers; disabling makes this PC invisible on the LAN and breaks WSD printers. |
| `fhsvc` | File History | Manual (trigger) | KEEP | File History backups. |
| `FontCache`, `FontCache3.0.0.0` | Windows Font Cache | Automatic / Manual | NEVER | UI text rendering perf. |
| `FrameServer`, `FrameServerMonitor` | Windows Camera Frame Server | Manual (trigger) | KEEP | Camera in apps. |
| `GameInputSvc`, `GameInputRedistService` | GameInput | Manual (trigger) / Automatic (redist) | KEEP (Gamer) | Controllers in newer games. |
| `GamingServices`, `GamingServicesNet` | Gaming Services | Automatic (trigger) | KEEP (DISABLE-OK if Xbox app removed) | Game Pass installs. |
| `gpsvc` | Group Policy Client | Automatic (trigger) | NEVER | Policy processing (even local). |
| `GraphicsPerfSvc` | GraphicsPerfSvc | Manual (trigger) | KEEP | GPU perf counters. |
| `hidserv` | Human Interface Device Service | Manual (trigger) | NEVER | Media keys, HID buttons. |
| `HvHost`, `vmcompute`, `vmms`, `hns` | Hyper-V host / compute / networking | Manual (trigger) / Automatic if enabled | KEEP | Only when Hyper-V/WSL2/Sandbox/Docker are used; MS "Do not disable" (Application Guard, WDAG). `[MS-DOC]` |
| `vmicguestinterface`, `vmicheartbeat`, `vmickvpexchange`, `vmicrdv`, `vmicshutdown`, `vmictimesync`, `vmicvmsession`, `vmicvss` | Hyper-V guest integration | Manual (trigger) | KEEP (harmless on physical) | Only start inside a Hyper-V VM; MS "Do not disable". `[MS-DOC]` |
| `icssvc` | Windows Mobile Hotspot | Manual (trigger) | DISABLE-OK | Mobile hotspot feature. |
| `IKEEXT` | IKE and AuthIP IPsec Keying | Manual (trigger) | NEVER | IKEv2/L2TP VPN. |
| `InstallService` | Microsoft Store Install Service | Manual | NEVER | Store/inbox app updates. |
| `InventorySvc` | Inventory and Compatibility Appraisal | Manual | KEEP (DISABLE-OK Paranoid) | Feeds Windows Update compat checks (Appraiser); disabling can block feature-update offers. |
| `iphlpsvc` | IP Helper | Automatic | KEEP (DISABLE-OK if no IPv6 transition/Teredo/ISATAP/6to4 needed) | Also hosts port-proxy (`netsh interface portproxy`) and IPv6 transition; WSL2/Docker port forwarding uses it. Disable is Paranoid-only. |
| `IpxlatCfgSvc` | IP Translation Configuration | Manual (trigger) | KEEP | 464XLAT on cellular. |
| `KeyIso` | CNG Key Isolation | Manual (trigger) | NEVER | Certificates, TPM keys, VPN. |
| `KtmRm` | KtmRm for DTC | Manual (trigger) | KEEP | - |
| `LanmanServer` | Server (SMB) | Automatic (trigger) | KEEP (DISABLE-OK Paranoid on isolated clients) | File/printer sharing, admin shares, some backup software. |
| `LanmanWorkstation` | Workstation (SMB client) | Automatic | NEVER | Network shares, domain, printers. |
| `lfsvc` | Geolocation Service | Manual (trigger) | DISABLE-OK (Strict) | Breaks location for apps, Maps, Find My Device, weather widget location, **automatic time zone** (`tzautoupdate`), Wi-Fi-based location. Prefer the location consent toggle. `[MS-DOC]` `[SRC winutil]` |
| `LicenseManager` | Windows License Manager | Manual (trigger) | NEVER | Store/Windows licensing. |
| `lltdsvc` | Link-Layer Topology Discovery Mapper | Manual | DISABLE-OK | Network map only. `[MS-DOC]` |
| `lmhosts` | TCP/IP NetBIOS Helper | Manual (trigger) | KEEP (DISABLE-OK if NetBIOS not needed) | Legacy name resolution. |
| `LSM` | Local Session Manager | Automatic (protected) | NEVER | Sessions. |
| `LxpSvc` | Language Experience Service | Manual | KEEP | Language packs. |
| `LxssManager` / `WSLService` | WSL | Manual / Automatic if WSL installed | KEEP | WSL. |
| `MapsBroker` | Downloaded Maps Manager | Automatic (delayed) | DISABLE-OK (Balanced) | Only offline maps; MS "OK to disable"; Maps app deprecated. winutil sets Manual. `[MS-DOC]` `[SRC]` |
| `McpManagementService` | MCP Management (Windows Update for Business deployment service) | Manual | KEEP | - |
| `MDCoreSvc` | Microsoft Defender Core | Automatic (protected) | NEVER | Defender. |
| `MessagingService_*` | Messaging (SMS via Phone) | Manual (trigger) | DISABLE-OK | Only Phone Link SMS. |
| `MicrosoftEdgeElevationService` | Edge elevation | Manual | KEEP | - |
| `MixedRealityOpenXRSvc`, `SharedRealitySvc`, `spectrum`, `perceptionsimulation` | Mixed Reality | Manual (trigger) | DISABLE-OK / gone in 24H2 | WMR removed in 24H2. |
| `mpssvc` | Windows Defender Firewall | Automatic | NEVER | Firewall. |
| `MSDTC` | Distributed Transaction Coordinator | Manual (delayed) | KEEP | Some SQL/LOB apps. |
| `MSiSCSI` | iSCSI Initiator | Manual | KEEP | MS "Do not disable" (used more than expected). `[MS-DOC]` |
| `msiserver` | Windows Installer | Manual | NEVER | MSI installs. |
| `MsKeyboardFilter` | Keyboard Filter | Disabled | KEEP | Kiosk feature. |
| `NaturalAuthentication` | Natural Authentication (Dynamic Lock, Hello companion) | Manual (trigger) | KEEP | Dynamic lock. |
| `NcaSvc` | Network Connectivity Assistant | Manual (trigger) | KEEP | DirectAccess only (deprecated). |
| `NcbService` | Network Connection Broker | Manual (trigger) | KEEP | UWP apps receive network notifications (Teams, Mail); MS "OK to disable" but breaks push for apps. |
| `NcdAutoSetup` | Network Connected Devices Auto-Setup | Manual (trigger) | DISABLE-OK | Auto-setup of network devices (printers/TVs) - privacy win. |
| `Netlogon` | Netlogon | Manual (Automatic when domain-joined) | NEVER | Domain auth. |
| `Netman` | Network Connections | Manual | NEVER | Adapter management UI. |
| `netprofm` | Network List Service | Manual | NEVER | Network profiles (public/private). |
| `NetSetupSvc` | Network Setup | Manual (trigger) | NEVER | Adapter driver install. |
| `NetTcpPortSharing` | Net.Tcp Port Sharing | Disabled | KEEP | Already disabled. |
| `NgcSvc`, `NgcCtnrSvc` | Microsoft Passport / Container | Manual (trigger) | NEVER | **Windows Hello PIN/biometrics sign-in**. |
| `NlaSvc` | Network Location Awareness | Manual (trigger, effectively always running) | NEVER | Network profile detection; disabling breaks firewall profiles/"No internet" detection. |
| `nsi` | Network Store Interface | Automatic | NEVER | Networking. |
| `OneSyncSvc_*` | Sync Host | Automatic (per user) | MANUAL-OK (after Mail/Calendar removal) | Legacy Mail/Calendar/People sync; deprecated since 1809; still used by some Store apps' account sync. |
| `p2pimsvc`, `p2psvc`, `PNRPsvc`, `PNRPAutoReg` | Peer Networking / PNRP | Manual | DISABLE-OK | PNRP APIs removed 24H2; only HomeGroup/legacy. `[MS-DOC removed]` |
| `PcaSvc` | Program Compatibility Assistant | Automatic (delayed, trigger) | DISABLE-OK (Balanced) | Loses "this app might not have installed correctly" prompts and compat-mode auto-application; MS "OK to disable". Also feeds Windows telemetry on app installs. `[MS-DOC]` |
| `PeerDistSvc` | BranchCache | Manual | DISABLE-OK | Enterprise BranchCache only. |
| `PenService_*` | Pen Service | Manual (trigger) | KEEP | Pen devices. |
| `PerfHost` | Performance Counter DLL Host | Manual | KEEP | 32-bit perf counters. |
| `PhoneSvc` | Phone Service | Manual (trigger) | DISABLE-OK (Strict) | Phone Link calls, VoIP apps (Teams calling uses it for dialer integration). `[MS-DOC]` |
| `PimIndexMaintenanceSvc_*` | Contact Data | Manual | DISABLE-OK | Contacts indexing for People/Mail (gone). `[MS-DOC]` |
| `pla` | Performance Logs & Alerts | Manual | KEEP | perfmon data collectors. |
| `PlugPlay` | Plug and Play | Manual (effectively always) | NEVER | - |
| `PolicyAgent` | IPsec Policy Agent | Manual (trigger) | NEVER | IPsec/VPN/DirectAccess. |
| `Power` | Power | Automatic | NEVER | - |
| `PrintNotify`, `PrintWorkflowUserSvc_*` | Printer Extensions / Print Workflow | Manual (trigger) | KEEP | Printing UI. |
| `ProfSvc` | User Profile Service | Automatic | NEVER | Sign-in. |
| `PushToInstall` | Windows PushToInstall (remote Store install from web) | Manual (trigger) | DISABLE-OK (privacy) | Lose "install from microsoft.com to this device". |
| `QWAVE` | Quality Windows Audio Video Experience | Manual | DISABLE-OK | qWave QoS; MS "OK to disable"; harmless. `[MS-DOC]` |
| `RasAuto`, `RasMan` | Remote Access Auto Connection / Connection Manager | Manual / Automatic (24H2: Automatic) | NEVER | **All VPN connections** (built-in, and many third-party clients rely on RasMan). |
| `RemoteAccess` | Routing and Remote Access | Disabled | KEEP | Already disabled. |
| `RemoteRegistry` | Remote Registry | Disabled (trigger) | KEEP disabled (DISABLE-RECOMMENDED if found enabled) | Security. |
| `RetailDemo` | Retail Demo Service | Manual | DISABLE-OK | Only retail demo mode. |
| `RmSvc` | Radio Management (airplane mode) | Manual | KEEP | Airplane mode / radio toggles. MS "OK to disable" on Server only. |
| `RpcEptMapper`, `RpcSs` | RPC | Automatic | NEVER | Everything. |
| `RpcLocator` | RPC Locator | Manual | DISABLE-OK | Legacy; unused. |
| `SamSs` | Security Accounts Manager | Automatic | NEVER | Sign-in. |
| `SCardSvr`, `ScDeviceEnum`, `SCPolicySvc` | Smart Card | Manual (trigger) | KEEP (DISABLE-OK if no smart cards) | Smart-card sign-in, YubiKey PIV. |
| `Schedule` | Task Scheduler | Automatic | NEVER | - |
| `SDRSVC` | Windows Backup (legacy Backup & Restore) | Manual | DISABLE-OK | System Image Backup deprecated. |
| `seclogon` | Secondary Logon | Manual | KEEP | `runas`, some installers, game launchers. |
| `SecurityHealthService` | Windows Security Health | Manual (trigger; protected) | NEVER | Windows Security app + notifications. |
| `SEMgrSvc` | Payments and NFC/SE Manager | Manual (trigger) | DISABLE-OK if no NFC | NFC payments/tap-to-pay. |
| `Sense`, `WdNisSvc`, `WinDefend`, `WdBoot`, `WdFilter`, `WdNisDrv` | Defender for Endpoint sensor / Defender AV | Manual / Manual / Automatic (all protected) | NEVER | Security. |
| `SENS` | System Event Notification | Automatic | NEVER | Sign-in/network events. |
| `SensorDataService`, `SensorService`, `SensrSvc` | Sensors | Manual (trigger) | DISABLE-OK on desktops (Strict) | Auto-rotate, ambient light brightness, location via sensors, some Hello. |
| `SessionEnv`, `TermService`, `UmRdpService` | Remote Desktop | Manual | KEEP (DISABLE-OK if RDP unused - security) | MS "Do not disable" on Server; on client fine to disable if RDP not used, but Settings toggle already controls listening. |
| `SgrmBroker` | System Guard Runtime Monitor Broker | Automatic (delayed) / removed 24H2 | NEVER | Security. |
| `SharedAccess` | Internet Connection Sharing | Manual (trigger) | KEEP (DISABLE-OK if hotspot/Miracast unused) | Mobile hotspot, Miracast; winutil disables. `[MS-DOC]` |
| `ShellHWDetection` | Shell Hardware Detection | Automatic | KEEP | AutoPlay, drive letters UI, media insertion. |
| `shpamsvc` | Shared PC Account Manager | Disabled | KEEP | - |
| `smphost` | Storage Spaces SMP | Manual | KEEP | MS "Do not disable" (storage management APIs). `[MS-DOC]` |
| `SmsRouter` | Microsoft Windows SMS Router | Manual (trigger) | DISABLE-OK | Only cellular SMS. |
| `SNMPTrap` | SNMP Trap | Manual | DISABLE-OK | Rare. |
| `Spooler` | Print Spooler | Automatic | KEEP (DISABLE-OK if no printers - security win, PrintNightmare class) | Also required for Print to PDF/XPS/OneNote printers and printer install. |
| `sppsvc` | Software Protection | Automatic (delayed, trigger) | NEVER | Activation. |
| `SSDPSRV`, `upnphost` | SSDP Discovery / UPnP Device Host | Manual | DISABLE-OK (Strict) | Breaks DLNA/cast-to-device discovery, some smart-TV/printer discovery, Xbox console streaming; MS "OK to disable". `[MS-DOC]` |
| `ssh-agent` | OpenSSH Authentication Agent | Disabled | KEEP (Developer may enable) | - |
| `SstpSvc` | SSTP | Manual | NEVER | SSTP VPN; MS "Do not disable". |
| `StateRepository` | State Repository | Automatic (protected) | NEVER | Start menu/app model; access denied anyway. |
| `StiSvc` | Windows Image Acquisition | Automatic (trigger) | KEEP (DISABLE-OK if no scanner/camera) | Scanners, WIA cameras. |
| `StorSvc` | Storage Service | Automatic (delayed, trigger) | NEVER | Storage Sense, USB storage enumeration in Settings, Store install location; winutil sets Manual - **reported to break external drive detection / Settings > Storage** `[UNVERIFIED]`. |
| `svsvc` | Spot Verifier | Manual (trigger) | KEEP | NTFS self-healing. |
| `swprv`, `VSS` | Shadow Copy provider / VSS | Manual | NEVER | System Restore, backups. |
| `SysMain` | SysMain (Superfetch/ReadyBoost/memory compression assist) | Automatic | KEEP (DISABLE-OK only for troubleshooting 100 % disk on HDD) | On SSD it is cheap; MS "No guidance"; disabling gives no measurable gain on modern hardware. `[MS-DOC]` |
| `SystemEventsBroker` | System Events Broker | Automatic (trigger) | NEVER | Task Scheduler/broker; MS "Do not disable". |
| `TabletInputService` | Touch Keyboard and Handwriting Panel | Manual (trigger) | NEVER | Touch keyboard, emoji panel (Win+.), IME, clipboard UI on 24H2; MS "Do not disable" with Desktop Experience. `[MS-DOC]` |
| `TapiSrv` | Telephony | Manual | KEEP | Modems, some VoIP/fax; MS "Do not disable" (RRAS). |
| `TextInputManagementService` | Text Input Management | Automatic (trigger; protected) | NEVER | Typing/IME; access denied. |
| `Themes` | Themes | Automatic | NEVER | Visual styles; MS "Do not disable". |
| `TieringEngineService` | Storage Tiers Management | Manual | KEEP | Storage Spaces tiers. |
| `TimeBrokerSvc` | Time Broker | Manual (trigger) | NEVER | UWP background tasks/timers; MS "Do not disable". |
| `TokenBroker` | Web Account Manager | Manual | NEVER | MSA/Entra sign-in for apps, Store, OneDrive. |
| `TrkWks` | Distributed Link Tracking Client | Automatic | DISABLE-OK (marginal) | Shortcut target tracking across moves; disabling gives near-zero benefit; MS "No guidance". |
| `TroubleshootingSvc` | Recommended Troubleshooting | Manual | DISABLE-OK (privacy) | Auto-troubleshooters. |
| `TrustedInstaller` | Windows Modules Installer | Manual | NEVER | Servicing/updates. |
| `tzautoupdate` | Auto Time Zone Updater | Disabled (trigger) - enabled by the "Set time zone automatically" toggle | KEEP | Needs `lfsvc`. |
| `UdkUserSvc_*` | Udk User Service (shell) | Manual | KEEP | Shell. |
| `UevAgentService` | UE-V | Disabled | KEEP | MDOP EOL Apr 2026. |
| `uhssvc` | Microsoft Update Health Service | Disabled/Manual | KEEP | Update remediation (KB4023057). |
| `UnistoreSvc_*`, `UserDataSvc_*` | User Data Storage / Access | Manual | DISABLE-OK | Mail/People/Calendar data (gone). MS "OK to disable". |
| `UserManager` | User Manager | Automatic (trigger) | NEVER | Sign-in. |
| `UsoSvc` | Update Orchestrator | Automatic (delayed) | NEVER | Windows Update; MS "Do not disable"; WaaSMedic re-enables. |
| `VacSvc` | Volumetric Audio Compositor | Manual | DISABLE-OK / gone | Mixed Reality audio. |
| `VaultSvc` | Credential Manager | Manual (trigger) | NEVER | Saved credentials, RDP/Wi-Fi/browser passwords. |
| `vds` | Virtual Disk | Manual | KEEP | Disk Management. |
| `W32Time` | Windows Time | Manual (delayed, trigger) | NEVER | Time sync (breaks TLS/auth if drift). |
| `WaaSMedicSvc` | Windows Update Medic | Manual (protected) | NEVER | Self-heals WU; access denied. |
| `WalletService` | WalletService | Manual | DISABLE-OK | Wallet (dead); MS "OK to disable". |
| `WarpJITSvc` | WARP JIT | Manual (trigger) | KEEP | Software GPU fallback in sandboxed apps. |
| `wbengine` | Block Level Backup Engine | Manual | DISABLE-OK | Legacy backup. |
| `WbioSrvc` | Windows Biometric | Manual (trigger) | NEVER | **Windows Hello fingerprint/face**. |
| `Wcmsvc` | Windows Connection Manager | Automatic (trigger) | NEVER | Wi-Fi/WWAN auto-connect, metered logic. |
| `wcncsvc` | Windows Connect Now | Manual | DISABLE-OK | WPS push-button config. |
| `WdiServiceHost`, `WdiSystemHost` | Diagnostic Service/System Host | Manual (trigger) | KEEP | Troubleshooters, boot performance diagnostics; harmless. |
| `WebClient` | WebClient (WebDAV) | Manual (trigger) | DISABLE-RECOMMENDED (Balanced) | Deprecated Nov 2023; security win (WebDAV NTLM leak vectors); loses `\\server@SSL\` UNC to WebDAV, SharePoint "Open with Explorer". `[MS-DOC deprecated]` |
| `Wecsvc` | Windows Event Collector | Manual | KEEP | WEF collectors; MS "Do not disable". |
| `WEPHOSTSVC` | Windows Encryption Provider Host | Manual (trigger) | KEEP | - |
| `wercplsupport` | Problem Reports Control Panel Support | Manual | DISABLE-OK | Only the CP applet. |
| `WerSvc` | Windows Error Reporting | Manual (trigger) | DISABLE-OK (Balanced, privacy) | Loses crash dumps to MS and "check online for a solution"; MS "Do not disable" (needed by ISVs for crash data) - so keep at Manual with `Disabled` policy `HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting\Disabled=1` instead of service Disabled. Note winutil disables `wermgr` which is *not* a service. `[MS-DOC]` `[SRC]` |
| `WFDSConMgrSvc` | Wi-Fi Direct Services Connection Manager | Manual (trigger) | DISABLE-OK | Miracast/Wi-Fi Direct. |
| `WiaRpc` | Still Image Acquisition Events | Manual | KEEP | Scanner buttons. |
| `WinHttpAutoProxySvc` | WinHTTP Web Proxy Auto-Discovery | Manual (trigger) | NEVER (use `DisableWpad` registry) | Everything using WinHTTP depends on it; MS "Do not disable"; disable WPAD via `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings\WinHttp\DisableWpad=1` instead. `[MS-DOC]` |
| `Winmgmt` | WMI | Automatic | NEVER | Management/monitoring. |
| `WinRM` | Windows Remote Management | Manual (delayed) | KEEP (ensure not Automatic on clients) | Remote PowerShell; MS "Do not disable" (Server). |
| `wisvc` | Windows Insider Service | Manual (trigger) | DISABLE-OK | Only Insider flighting. `[MS-DOC]` |
| `WlanSvc` | WLAN AutoConfig | Automatic | NEVER | Wi-Fi. |
| `wlidsvc` | Microsoft Account Sign-in Assistant | Manual (trigger) | NEVER if MSA used (KEEP) | MSA sign-in, Store, OneDrive, Xbox, Office activation. `[MS-DOC]` |
| `wlpasvc` | Local Profile Assistant | Manual (trigger) | KEEP | eSIM. |
| `WManSvc` | Windows Management Service | Manual | KEEP | Provisioning packages. |
| `wmiApSrv` | WMI Performance Adapter | Manual | KEEP | Perf counters via WMI. |
| `WMPNetworkSvc` | Windows Media Player Network Sharing | Manual (only with WMP) | DISABLE-OK | DLNA server. |
| `workfolderssvc` | Work Folders | Manual | DISABLE-OK | Enterprise Work Folders. |
| `WpcMonSvc` | Parental Controls | Manual | DISABLE-OK if no child accounts | Family Safety. |
| `WPDBusEnum` | Portable Device Enumerator | Manual (trigger) | KEEP | Phones/cameras via MTP. |
| `WpnService` | Windows Push Notifications System | Automatic | NEVER (KEEP) | **All toast notifications, WNS push, Action Center**; older tools set Manual and broke notifications. |
| `WpnUserService_*` | Windows Push Notifications User | Automatic | NEVER (KEEP) | Same. |
| `WSAIFabricSvc` | Windows AI Fabric (Copilot+ on-device models) | Automatic (Copilot+ 24H2) | MANUAL-OK (Balanced) / Disabled (Strict) | Recall/Click to Do/on-device AI; Raphire sets Manual, winutil Disabled. `[SRC]` |
| `wscsvc` | Security Center | Automatic (delayed; protected) | NEVER | Windows Security status. |
| `WSearch` | Windows Search | Automatic (delayed) | KEEP (NEVER by default; DISABLE-OK Paranoid with warning) | Start-menu/Explorer/Outlook search; disabling breaks Start typing results and Outlook classic search. Offer index-scope reduction instead. |
| `wuauserv` | Windows Update | Manual (trigger) | NEVER | Updates. |
| `wudfsvc` | User-mode Driver Framework | Manual (trigger) | NEVER | UMDF drivers (USB, sensors, biometrics). |
| `WwanSvc` | WWAN AutoConfig | Manual (trigger) | KEEP | Cellular. |
| `XblAuthManager`, `XblGameSave`, `XboxNetApiSvc`, `XboxGipSvc` | Xbox Live Auth / Game Save / Networking / Accessory Management | Manual (trigger) | DISABLE-OK (Strict; Gamer keep) | Breaks Xbox app, Game Pass, cloud saves for Xbox-Live titles, Xbox controller firmware/accessory app; MS "Should be disabled" on **Server** only. `[MS-DOC]` |
| Third-party updaters: `GoogleUpdaterService*`/`gupdate`/`gupdatem`, `AdobeARMservice`, `AdobeUpdateService`, `Steam Client Service`, `EABackgroundService`, `EpicOnlineServices`, `NvContainerLocalSystem`, `RzActionSvc`, `LGHUBUpdaterService`, `ClickToRunSvc` (Office), `TeamViewer`, `AnyDesk` | Vendor services | Automatic/Manual | MANUAL-OK (show in a "third-party" section, never Disabled by default) | Setting updaters to Manual keeps on-demand function; disabling `ClickToRunSvc` breaks Office; disabling anti-cheat services breaks games. |

**Counts: 219 service rows (~260 service names incl. per-user templates and Hyper-V/vendor groups)** -> NEVER 83, KEEP 87, MANUAL-OK 4, DISABLE-OK 45 (of which DISABLE-RECOMMENDED: `WebClient`, and `RemoteRegistry` if found enabled). Only **DiagTrack, MapsBroker, PcaSvc, WebClient, RetailDemo, wisvc, lfsvc (Strict), Xbox quartet (Strict), WSAIFabricSvc, TroubleshootingSvc, PushToInstall, NcdAutoSetup, RemoteRegistry** are worth exposing as toggles; the rest of the DISABLE-OK set is trigger-start and yields no measurable benefit.

---
## 4. Scheduled tasks

Rules: `\Microsoft\Windows\UpdateOrchestrator\*`, `\Microsoft\Windows\WindowsUpdate\*`, `\Microsoft\Windows\WaaSMedic\*`, `\Microsoft\Windows\Windows Defender\*`, `\Microsoft\Windows\Servicing\*`, `\Microsoft\Windows\InstallService\*`, `\Microsoft\Windows\AppxDeploymentClient\*`, `\Microsoft\Windows\LanguageComponentsInstaller\*`, `\Microsoft\Windows\SoftwareProtectionPlatform\*`, `\Microsoft\Windows\TPM\*`, `\Microsoft\Windows\BitLocker\*`, `\Microsoft\Windows\Registry\RegIdleBackup`, `\Microsoft\Windows\SystemRestore\SR`, `\Microsoft\Windows\Defrag\ScheduledDefrag`, `\Microsoft\Windows\.NET Framework\*` are **NEVER**. UpdateOrchestrator and WaaSMedic tasks are ACL-protected (SYSTEM only) and get recreated; attempts to disable them are the classic cause of "Windows Update broken" reports. `[SRC winutil issue #1098]`

Task names verified `[LOCAL]` on 22H2 (all exist on 24H2 unless noted). "Real effect" is what actually stops when the task is disabled.

| Task path | Purpose | Default | Verdict | Level | Real effect of disabling |
|---|---|---|---|---|---|
| `\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser` | Runs `CompatTelRunner.exe`; app-compat inventory for feature-update eligibility + telemetry | Enabled | DISABLE-OK | Balanced | Stops the periodic CPU/disk spike; **may prevent feature-update offers on unmanaged PCs** and Windows Update "compatibility hold" evaluation; winutil/Raphire/Sophia disable it. Re-enabled by feature updates. |
| `...\Application Experience\ProgramDataUpdater` | Collects program telemetry (CEIP) | Enabled | DISABLE-OK | Balanced | Telemetry only. |
| `...\Application Experience\StartupAppTask` | Scans startup entries, raises "too many startup apps" toast | Enabled | DISABLE-OK | Basic | Loses the toast only. |
| `...\Application Experience\PcaPatchDbTask` | Updates PCA patch database | Enabled | DISABLE-OK | Balanced | Loses PCA compat DB updates. |
| `...\Application Experience\MareBackup` | Win32 app inventory for "app backup/restore" (Windows Backup) | Enabled | DISABLE-OK | Balanced | Loses Windows Backup app-list restore. |
| `...\Application Experience\SdbinstMergeDbTask`, `PcaWallpaperAppDetect` | Compat DB merge / wallpaper app detection | Enabled | KEEP | - | Servicing of shims; harmless. |
| `\Microsoft\Windows\Autochk\Proxy` | Collects autochk SQM data (CEIP) | Enabled | DISABLE-OK | Basic | Telemetry only. |
| `\Microsoft\Windows\Customer Experience Improvement Program\Consolidator`, `UsbCeip` | CEIP uploads | Enabled | DISABLE-OK | Basic | Telemetry only. |
| `\Microsoft\Windows\CloudExperienceHost\CreateObjectTask` | Creates CloudExperienceHost broker (OOBE, MSA sign-in flows, Windows Hello enrolment) | Enabled | KEEP | - | Disabling breaks MSA/Entra add-account and OOBE flows; often wrongly listed as telemetry. |
| `\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector` | Uploads disk diagnostics (CEIP) | Enabled | DISABLE-OK | Basic | Telemetry only. |
| `\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticResolver` | Shows S.M.A.R.T. failure warnings | Enabled | KEEP | - | Disabling hides "your disk is failing" warnings. |
| `\Microsoft\Windows\DiskFootprint\Diagnostics` | Disk usage diagnostics | Enabled | DISABLE-OK | Balanced | Minor. |
| `\Microsoft\Windows\DiskFootprint\StorageSense` | Runs Storage Sense | Enabled | KEEP | - | Disables Storage Sense cleanup. |
| `\Microsoft\Windows\Feedback\Siuf\DmClient`, `DmClientOnScenarioDownload` | Feedback (SIUF) prompts/uploads | Enabled | DISABLE-OK | Basic | Loses "how likely are you to recommend Windows" prompts; pair with `NumberOfSIUFInPeriod=0`. |
| `\Microsoft\Windows\Windows Error Reporting\QueueReporting` | Uploads queued WER reports | Enabled | DISABLE-OK | Balanced | Reports stay local. |
| `\Microsoft\Windows\Maps\MapsToastTask`, `MapsUpdateTask` | Maps toasts / offline map updates | Enabled | DISABLE-OK | Basic | Maps deprecated. |
| `\Microsoft\Windows\Power Efficiency Diagnostics\AnalyzeSystem` | Power efficiency diagnostics | Enabled | DISABLE-OK | Balanced | Loses `powercfg /energy`-style background analysis. |
| `\Microsoft\Windows\Shell\FamilySafetyMonitor`, `FamilySafetyRefreshTask` | Family Safety enforcement | Enabled | DISABLE-OK only if no child accounts | Strict | Breaks Family Safety limits. |
| `\Microsoft\Windows\Shell\IndexerAutomaticMaintenance` | Search index maintenance | Enabled | KEEP | - | Index health. |
| `\Microsoft\Windows\Shell\CreateObjectTask`, `UpdateUserPictureTask`, `ThemesSyncedImageDownload` | Shell brokers / account picture / theme images | Enabled | KEEP | - | Shell. |
| `\Microsoft\Windows\Device Information\Device`, `Device User` | Device inventory (`devicecensus.exe`) | Enabled | DISABLE-OK | Balanced | Telemetry only. |
| `\Microsoft\Windows\PI\Sqm-Tasks` | Platform inventory SQM | Enabled (older) | DISABLE-OK | Basic | Telemetry. |
| `\Microsoft\Windows\NetTrace\GatherNetworkInfo` | Network trace on request | Enabled | DISABLE-OK | Balanced | Only support diagnostics. |
| `\Microsoft\Windows\Speech\SpeechModelDownload` | Downloads speech models | Enabled | DISABLE-OK | Balanced | Voice typing/Narrator model updates. |
| `\Microsoft\Windows\Servicing\StartComponentCleanup` | Component store (WinSxS) cleanup | Enabled | NEVER | - | Disk bloat grows. |
| `\Microsoft\Windows\Defrag\ScheduledDefrag` | Weekly optimize (TRIM on SSD, defrag on HDD) | Enabled | NEVER | - | SSD retrim stops. |
| `\Microsoft\Windows\Chkdsk\ProactiveScan`, `SyspartRepair` | Online NTFS health | Enabled | NEVER | - | - |
| `\Microsoft\Windows\Wininet\CacheTask` | WinINet cache maintenance | Enabled | KEEP | - | Harmless. |
| `\Microsoft\Windows\Windows Filtering Platform\BfeOnServiceStartTypeChange` | Firewall integrity | Enabled | NEVER | - | - |
| `\Microsoft\Windows\Work Folders\*` | Work Folders sync | Enabled | DISABLE-OK | Balanced | Only enterprise. |
| `\Microsoft\Windows\XblGameSave\XblGameSaveTask` | Xbox cloud save sync | Enabled | DISABLE-OK (Strict; Gamer keep) | Strict | Cloud saves for Xbox-Live titles. `[MS-DOC Server guidance]` |
| `\Microsoft\Windows\.NET Framework\.NET Framework NGEN v4.0.30319*` | Native image compilation | Enabled (Critical variants disabled) | NEVER | - | .NET app start-up gets slower. |
| `\Microsoft\Windows\UpdateOrchestrator\*`, `\WindowsUpdate\Scheduled Start`, `\WaaSMedic\*`, `\InstallService\*` | Windows Update pipeline | Enabled | NEVER | - | Protected; breaking them = no updates. |
| `\Microsoft\Windows\Windows Defender\Windows Defender Cache Maintenance / Cleanup / Scheduled Scan / Verification` | Defender maintenance | Enabled | NEVER | - | Security. |
| `\Microsoft\Windows\Flighting\FeatureConfig\*`, `Flighting\OneSettings\RefreshCache` | Feature-flag & OneSettings refresh | Enabled | KEEP | - | Disabling breaks controlled-feature-rollout config and some app settings sync; not telemetry. |
| `\Microsoft\Windows\Diagnosis\Scheduled`, `RecommendedTroubleshootingScanner` | Recommended troubleshooting | Enabled | DISABLE-OK | Balanced | Loses auto-troubleshooter recommendations. |
| `\Microsoft\Windows\DUSM\dusmtask` | Data usage | Enabled | KEEP | - | Data usage stats. |
| `\Microsoft\Windows\Location\Notifications`, `WindowsActionDialog` | Location UI | Enabled | KEEP | - | Location prompts. |
| `\Microsoft\Windows\MemoryDiagnostic\*`, `\Multimedia\SystemSoundsService`, `\Plug and Play\*`, `\Printing\*`, `\RecoveryEnvironment\VerifyWinRE`, `\Time Synchronization\*`, `\Time Zone\SynchronizeTimeZone`, `\Registry\RegIdleBackup`, `\SystemRestore\SR`, `\StateRepository\MaintenanceTasks`, `\Sysmain\*`, `\Data Integrity Scan\*`, `\BrokerInfrastructure\*`, `\capabilityaccessmanager\*`, `\ApplicationData\*`, `\AppListBackup\*` (Windows Backup app list), `\Input\*`, `\International\*`, `\Kernel\La57Cleanup`, `\MUI\LPRemove`, `\NlaSvc\WiFiTask`, `\WCM\WiFiTask`, `\WlanSvc\CDSSync`, `\WDI\ResolutionHost`, `\WindowsColorSystem\Calibration Loader`, `\Task Manager\Interactive`, `\TextServicesFramework\MsCtfMonitor`, `\USB\Usb-Notifications`, `\UPnP\UPnPHostConfig`, `\Subscription\*`, `\License Manager\*`, `\EDP\*`, `\ExploitGuard\*`, `\Management\*`, `\Workplace Join\*`, `\CertificateServicesClient\*`, `\CloudRestore\*`, `\ConsentUX\*`, `\Clip\ClipESUConsumer`, `\DirectX\*`, `\Bluetooth\UninstallDeviceTask`, `\SpacePort\*`, `\Storage Tiers Management\*`, `\Offline Files\*`, `\FileHistory\*`, `\File Classification Infrastructure\*`, `\SharedPC\*`, `\Active Directory Rights Management Services Client\*`, `\AppID\*`, `\Mobile Broadband Accounts\*`, `\WwanSvc\*`, `\UNP\RunUpdateNotificationMgr`, `\Maintenance\WinSAT`, `\DiskCleanup\SilentCleanup` | System housekeeping | Various | KEEP | - | No benefit from disabling; several are protected. |
| `\Microsoft\Windows\Windows Media Sharing\UpdateLibrary` | WMP library sharing | Enabled (only with WMP) | DISABLE-OK | Basic | DLNA library. |
| `\Microsoft\Windows\RemoteAssistance\RemoteAssistanceTask` | Remote Assistance firewall/scoping | Enabled | DISABLE-OK | Balanced | Only if RA unused. |
| `\Microsoft\Windows\SettingSync\*` | Settings sync backup | Enabled | KEEP | - | Sync. |
| `\Microsoft\Windows\User Profile Service\HiveUploadTask` | Enterprise roaming | Enabled | KEEP | - | - |
| `\Microsoft\Windows\WS\WSTask` (older) | Store licensing | Enabled | KEEP | - | - |
| `\Microsoft\Windows\Ras\MobilityManager` | RAS mobility | Enabled | KEEP | - | VPN roaming. |
| `\Microsoft\XblGameSave\XblGameSaveTaskLogon` (older) | Xbox save on logon | Enabled | DISABLE-OK (Strict) | Strict | Same as above. |
| **Third-party** `\OneDrive Standalone Update Task-S-1-5-21-*`, `\OneDrive Reporting Task-S-1-5-21-*` | OneDrive updater/telemetry | Enabled | KEEP if OneDrive used; remove with OneDrive | Balanced | Reporting task DISABLE-OK. |
| `\MicrosoftEdgeUpdateTaskMachineCore{GUID}`, `\MicrosoftEdgeUpdateTaskMachineUA{GUID}`, `\MicrosoftEdgeUpdateBrowserReplacementTask` | Edge updater | Enabled | KEEP (security updates) | - | If disabled, Edge stops updating unless service triggers. |
| `\GoogleUpdateTaskMachineCore/UA`, `\GoogleUpdaterTaskSystem*` | Chrome updater | Enabled | KEEP | - | Same reasoning. |
| `\Adobe Acrobat Update Task`, `\AdobeGCInvoker-1.0`, `\CCleaner*`, `\NvTmRep*`, `\NVIDIA GeForce Experience SelfUpdate*`, `\Intel*`, `\Dropbox*`, `\Brave*` | Vendor updaters/telemetry | Enabled | Show in "third-party" list; DISABLE-OK for telemetry ones (`AdobeGCInvoker`, `NvTmRep`, `NvProfileUpdater`) | Balanced | Loses vendor auto-update if the core update task is disabled. |

Total scheduled-task rows: 50 (covering ~130 task paths incl. the housekeeping group; about 20 individually toggle-worthy; ~15 DISABLE-OK telemetry/CEIP; rest KEEP/NEVER).

---

## 5. Startup management

| Surface | Mechanism / location | Notes for the app |
|---|---|---|
| Run / RunOnce keys | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`, `RunOnce`; `HKLM\SOFTWARE\WOW6432Node\...\Run`; `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, `RunOnce`; policy variants `...\Policies\Explorer\Run` | Enumerate all; show publisher/signature; never delete silently - **disable** via StartupApproved. |
| Enable/disable state (what Task Manager/Settings toggle) | `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run` (per-user Run), `...\StartupApproved\Run32` (HKLM Run entries), `...\StartupApproved\StartupFolder`; also `HKLM\...\Explorer\StartupApproved\Run` / `Run32` for machine scope. Value = REG_BINARY 12 bytes: `02 00 00 00 00 00 00 00 00 00 00 00` = enabled; `03 00 00 00` + 8-byte FILETIME = disabled (`06 ...` = enabled-by-user, `07 ...` disabled variant on some builds). | Write the same format Task Manager writes; do not remove Run values. `[UNVERIFIED: 06/07 semantics]` |
| Startup folders | `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup`, `%ProgramData%\Microsoft\Windows\Start Menu\Programs\StartUp` | Toggle via StartupApproved\StartupFolder. |
| Scheduled-task autostarts | Tasks with `LogonTrigger`/`BootTrigger` (vendor updaters, OneDrive, Teams classic) | List separately; `Disable-ScheduledTask`. |
| Packaged (UWP/MSIX) StartupTask | `HKCU\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData\<PackageFamilyName>\<TaskId>` value `State` DWORD (`0` Disabled, `1` DisabledByUser, `2` Enabled, `3` EnabledByPolicy, `4` DisabledByPolicy - matches the WinRT `StartupTaskState` enum). Known ids: `MSTeams_8wekyb3d8bbwe\TeamsTfwStartupTask`, `Microsoft.YourPhone_8wekyb3d8bbwe\...StartupTask` (Phone Link), `Microsoft.Copilot_8wekyb3d8bbwe\...`, `Microsoft.OutlookForWindows_8wekyb3d8bbwe\...`, `MicrosoftWindows.CrossDevice_cw5n1h2txyewy\...`, `Microsoft.XboxGamingOverlay_8wekyb3d8bbwe\...` | Task Manager toggles this; the WinRT API `Windows.ApplicationModel.StartupTask` can only be used by the app itself. `[UNVERIFIED: exact TaskId strings]` |
| Services (Automatic) | see section 3 | - |
| Explorer shell extensions / `ShellServiceObjectDelayLoad`, `Winlogon\Userinit`/`Shell` | Autoruns territory | Show read-only; not a debloat target. |
| Edge preload/StartupBoost | Policies `StartupBoostEnabled=0`, `BackgroundModeEnabled=0` (1.4) | Also `Software\Policies\Microsoft\Edge\PreLaunch...` legacy. |
| Teams autostart | StartupTask above (new Teams); classic Teams: HKCU Run `com.squirrel.Teams.Teams` | - |
| OneDrive autostart | HKCU Run `OneDrive` (`OneDrive.exe /background`) | Toggle via StartupApproved. |
| Phone Link / Copilot / Cross Device | StartupTask entries | Same. |
| "Restart apps after sign-in" | `HKCU\Software\Microsoft\Windows NT\CurrentVersion\Winlogon\RestartApps=0` (default 0 in Win11) | Sophia `SaveRestartableApps`. `[SRC]` |
| Xbox Game Bar / Game DVR | `HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR\AppCaptureEnabled=0`; `HKCU\System\GameConfigStore\GameDVR_Enabled=0`; policy `HKLM\SOFTWARE\Policies\Microsoft\Windows\GameDVR\AllowGameDVR=0`; controller-opens-Game-Bar `HKCU\SOFTWARE\Microsoft\GameBar\UseNexusForGameBarEnabled=0` | Real (measurable) for gaming: background capture buffer off. `[SRC Raphire]` `[MS-DOC AllowGameDVR]` |
| Game Mode | `HKCU\Software\Microsoft\GameBar\AutoGameModeEnabled=1` (default on), `AllowAutoGameMode=1` | Leave on. `[SRC winutil]` |
| Windows Spotlight/lock screen | 6.x | - |

---

## 6. Real, measurable performance and UX tweaks (catalogue)

Columns: id | name | mechanism (exact) | scope | editions/versions | reversible | side effects | measurable benefit | level | source. Scope U = per-user (HKCU), M = machine (HKLM/system). "Measurable" = Yes (benchmarkable / removes CPU, IO or latency), UX (perceived responsiveness / annoyance removal), No (placebo).

### 6.1 Visual effects and animations

| id | name | mechanism | scope | ed/ver | rev | side effects | meas. | level | source |
|---|---|---|---|---|---|---|---|---|---|
| VFX-01 | Visual effects: custom "best performance minus fonts" | `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects\VisualFXSetting=3` (custom; 1=best appearance, 2=best performance) plus `HKCU\Control Panel\Desktop\UserPreferencesMask` = `90 12 03 80 10 00 00 00` (winutil "best performance") or `90 12 07 80 10 00 00 00` (Raphire: keeps smooth fonts + thumbnails), `MinAnimate=0` (`Control Panel\Desktop\WindowMetrics`), `TaskbarAnimations=0`, `ListviewAlphaSelect=0`, `ListviewShadow=0` (`Explorer\Advanced`), `DragFullWindows=0`, `MenuShowDelay=200..0` (`Control Panel\Desktop`), `EnableAeroPeek=0` (`HKCU\Software\Microsoft\Windows\DWM`) | U | all | yes (restore mask/values) | Less eye candy; `DragFullWindows=0` shows outline drag; keep font smoothing bit or ClearType turns off | UX (real on low-end / VMs / RDP; negligible on modern GPUs) | Balanced (default off) | `[SRC winutil/Raphire]` |
| VFX-02 | Transparency off | `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\EnableTransparency=0` | U | all | yes | Acrylic/Mica gone | UX/small GPU on iGPU laptops | Balanced | `[SRC]` |
| VFX-03 | Animations off (Settings > Accessibility) | `UserPreferencesMask` (`HKCU\Control Panel\Desktop`, bit 0x02 of byte 0 = animations) + `HKCU\Control Panel\Desktop\WindowMetrics\MinAnimate=0`; the Settings > Accessibility > Visual effects > Animation effects toggle writes the same mask - prefer the mask so the toggle stays in sync | U | all | yes | - | UX | Balanced | `[SRC Raphire Disable_Animations.reg]` |
| VFX-04 | Menu show delay | `HKCU\Control Panel\Desktop\MenuShowDelay="0"` (default 400) | U | all | yes | Menus pop instantly (some users find hover menus twitchy; 100-200 is a good compromise) | UX | Basic | `[SRC winutil]` |
| VFX-05 | Aero Shake off | `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\DisallowShaking=1` (Win11 default already off via Settings "Title bar window shake") | U | all | yes | - | UX | Basic | `[SRC Sophia]` |
| VFX-06 | Snap layouts flyout / snap assist | `EnableSnapAssistFlyout=0`, `EnableSnapBar=0` (top-of-screen), `SnapAssist=0` (suggestions), `WindowArrangementActive="0"` in `Control Panel\Desktop` disables snapping entirely | U | 22H2+ | yes | Lose snap features | UX | Basic (per preference) | `[SRC Raphire]` |
| VFX-07 | Lock-screen acrylic blur | policy `HKLM\SOFTWARE\Policies\Microsoft\Windows\System\DisableAcrylicBackgroundOnLogon=1` | M | Pro+ (works on Home via registry `[UNVERIFIED]`) | yes | - | UX | Basic | `[SRC winutil]` |
| VFX-08 | Windows Ink Workspace off | policy `HKLM\SOFTWARE\Policies\Microsoft\WindowsInkWorkspace\AllowWindowsInkWorkspace=0` (0 off, 1 on-no-lockscreen, 2 on) | M | Pro+ | yes | Pen menu gone | UX | Strict | `[MS-DOC WindowsInkWorkspace CSP]` |

### 6.2 Power

| id | name | mechanism | scope | ed/ver | rev | side effects | meas. | level | source |
|---|---|---|---|---|---|---|---|---|---|
| PWR-01 | Power plan: Balanced (default) vs High performance | `powercfg /setactive SCHEME_MIN` (High perf `8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c`), `SCHEME_BALANCED` `381b4222-f694-41f0-9685-ff5bb260df2e`, `SCHEME_MAX` power saver `a1841308-3541-4fab-bc81-f71556f20b4a`; on Modern Standby devices only Balanced exists and Windows 11 uses **power mode overlays** (Settings > Power > Power mode; `powercfg /overlaysetactive` alias `OVERLAY_SCHEME_MAX`=Best performance) | M | all; laptops = overlays | yes | High perf: min processor state 100 %, no core parking, no USB selective suspend, higher idle power/heat/fan noise; on laptops kills battery | Marginal on desktops (mostly benefits latency-sensitive workloads; modern CPUs ramp in ms); harmful on laptops | Gamer/Developer only, desktop only, default = Balanced/"Best performance" overlay | `[MS-DOC powercfg]` `[SRC Sophia notes "not recommended for laptops"]` |
| PWR-02 | Ultimate Performance plan | `powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61` then `/setactive` | M | Pro for Workstations natively; duplicable elsewhere; **hidden on battery-powered systems by design** | yes (delete scheme) | Same as high perf plus removes micro-latencies from power management; higher idle power | Marginal (MS: for workstations reducing micro-latencies) | Gamer/Developer, desktop only, off by default | `[MS-DOC Windows Insider blog build 17101]` `[SRC winutil "NOT FOR LAPTOPS"]` |
| PWR-03 | Fast Startup (hiberboot) | `HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power\HiberbootEnabled=0` (needs hibernate on to work) | M | all | yes | Off: slower cold boot (seconds on SSD), **on: dual-boot NTFS locking issues, some drivers/network adapters misbehave, "shutdown" does not fully reset kernel, Wake-on-LAN quirks**; updates that need shutdown are unaffected | UX trade-off | Balanced (default: leave; offer "disable for dual boot / troubleshooting") | `[SRC Raphire/winutil]` |
| PWR-04 | Hibernation off / reduced hiberfile | `powercfg /hibernate off` (also disables Fast Startup); `powercfg /hibernate /type reduced` (hiberfile ~40 % of RAM instead of ~75 %, keeps Fast Startup, removes Hibernate option); `powercfg /hibernate /size 50` | M | all | yes | Off: no hibernate/hybrid sleep, no fast startup, frees `hiberfil.sys` (RAM x 0.4-0.75); laptops lose "critical battery -> hibernate" | Disk space (real); no speed gain | Balanced = reduced on desktops; Strict = off on desktops only; never on laptops | `[MS-DOC powercfg]` |
| PWR-05 | USB selective suspend | `powercfg /setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb50f7e73c 0` (disable, AC only) | M | all | yes | Higher idle power; fixes flaky USB audio/mice wake issues | Yes for USB dropouts; not a general perf tweak | Gamer/Developer (troubleshooting) | `[MS-DOC powercfg]` |
| PWR-06 | PCIe Link State Power Management off | `powercfg /setacvalueindex SCHEME_CURRENT 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 0` | M | all | yes | Higher idle power; can fix NVMe/GPU stutter on some boards | Marginal | Gamer (desktop) | `[MS-DOC powercfg]` |
| PWR-07 | Processor min state / core parking | `powercfg` `SUB_PROCESSOR` `893dee8e-...` PROCTHROTTLEMIN; core parking `0cc5b647-c1df-4637-891a-dec35c318583` | M | all | yes | Higher idle power | Marginal (Windows 11 already unparks fast) | Gamer desktop only; not default | `[MS-DOC powercfg]` |
| PWR-08 | Modern Standby network connectivity off (laptops) | `HKLM\SOFTWARE\Policies\Microsoft\Power\PowerSettings\f15576e8-98b7-4186-b944-eafa664402d9\ACSettingIndex=0`, `DCSettingIndex=0` | M | S0ix devices | yes | No background sync/mail/notifications during sleep; better battery, cooler bag | Yes (battery) | Balanced (laptops) | `[SRC Raphire/winutil]` |
| PWR-09 | Energy Saver always on (24H2) | Settings > Power > Energy saver (registry `HKLM\SYSTEM\CurrentControlSet\Control\Power\...` overlay `[UNVERIFIED]`); use `powercfg /setdcvalueindex ... SUB_ENERGYSAVER ESBATTTHRESHOLD` | M | 24H2+ | yes | Dimmer screen, throttled background | Yes (battery), negative for perf | Not a perf tweak; leave to user | `[MS-DOC 24H2 what's new energy saver API]` |

### 6.3 Storage, memory, file system

| id | name | mechanism | scope | ed/ver | rev | side effects | meas. | level | source |
|---|---|---|---|---|---|---|---|---|---|
| STO-01 | Storage Sense on with schedule | `HKCU\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy` values: `01`=1 (on), `2048`=cadence (0 low-space, 1 daily, 7 weekly, 30 monthly), `04`=1 delete temp files, `08`=1 empty Recycle Bin, `256`=days (0/1/14/30/60), `32`=1 Downloads cleanup (careful), `512`=days, `1024`... ; policy `HKLM\SOFTWARE\Policies\Microsoft\Windows\StorageSense\AllowStorageSenseGlobal=1`, `ConfigStorageSenseGlobalCadence`, `ConfigStorageSenseRecycleBinCleanupThreshold`, `ConfigStorageSenseDownloadsCleanupThreshold` (Pro+) | U / M | all | yes | Downloads cleanup can delete wanted files - default 0 (never) | Disk (real) | Balanced (on, weekly, Recycle Bin 30 d, Downloads never) | `[SRC Raphire/winutil (they disable; we recommend on)]` `[MS-DOC Storage CSP]` |
| STO-02 | TRIM verify | `fsutil behavior query DisableDeleteNotify` -> NTFS 0 = TRIM on (default) | M | all | n/a | - | Yes (SSD longevity/perf) - **check-only, never disable** | Basic (health check) | `[MS-DOC fsutil]` |
| STO-03 | Optimize Drives weekly (retrim) | task `\Microsoft\Windows\Defrag\ScheduledDefrag` enabled + `defrag /C /O` on demand | M | all | n/a | - | Yes | Basic (check-only) | `[MS-DOC]` |
| STO-04 | Reserved Storage off | `DISM /Online /Set-ReservedStorageState /State:Disabled` (needs no pending updates) | M | 1903+ | yes | Frees ~7 GB; **feature updates may fail on low space**; MS keeps it on for reliability | Disk only | Strict (small SSD only, warn) | `[SRC winutil]` |
| STO-05 | Component store cleanup / Disk Cleanup | `Dism /Online /Cleanup-Image /StartComponentCleanup` (optionally `/ResetBase` - blocks uninstalling updates), `cleanmgr /sagerun:n`, delete `%TEMP%`, `C:\Windows\Temp`, DO cache | M | all | partial | `/ResetBase` prevents update rollback | Disk | Balanced (without ResetBase) | `[SRC winutil]` |
| STO-06 | NTFS last-access update | `fsutil behavior set disablelastaccess 1` (Win11 default is 2 = system-managed, disabled on >128 GB volumes) | M | all | yes | Backup/DLP tools using atime; already effectively off | Marginal/none | Not offered (check-only) | `[MS-DOC fsutil]` |
| STO-07 | 8.3 short-name creation | `fsutil behavior set disable8dot3 1` (default 2 = per-volume; system volume keeps 8.3) | M | all | yes (does not strip existing) | Legacy 16-bit/installer scripts using short paths (some MSIs) break | Marginal (dir-create on huge folders) | Not offered by default (Developer, warn) | `[MS-DOC fsutil]` |
| STO-08 | Long paths | `HKLM\SYSTEM\CurrentControlSet\Control\FileSystem\LongPathsEnabled=1` | M | all | yes | Only manifest-opted apps benefit; Explorer still limited | UX for developers/git | Developer (default on) | `[SRC winutil/Sophia]` |
| STO-09 | Page file: leave "System managed" | Do **not** set fixed/none. Explain: kernel uses pagefile for crash dumps, commit charge, memory compression store; SSD wear is negligible | M | all | - | Fixed small/none -> OOM crashes, no dumps | No | Not offered (info only) | rationale |
| STO-10 | Memory compression | `Get-MMAgent`; leave `MemoryCompression` enabled | M | all | - | Disabling increases paging | No | Not offered | rationale |
| STO-11 | Explorer thumbnail cache size / rebuild | delete `%LocalAppData%\Microsoft\Windows\Explorer\thumbcache_*.db` (Sophia `ThumbnailCacheRemoval`) | U | all | n/a | Regenerates | UX (fix corrupt thumbnails) | Tool action | `[SRC Sophia]` |
| STO-12 | Windows Search index scope reduction | Settings > Privacy & security > Searching Windows: Classic vs Enhanced (`HKLM\SOFTWARE\Microsoft\Windows Search\Gather\Windows\SystemIndex\EnableFindMyFiles` 0 = Classic default, 1 = Enhanced); exclude folders via COM `ISearchCrawlScopeManager` (`Microsoft.Search.Interop`) or Control Panel `srchadmin.dll`; policy `AllowFindMyFiles=0` (Pro+) blocks Enhanced | M | all | yes | Enhanced indexes whole drives = more IO/CPU; Classic is default | Yes (IO on big drives) | Balanced (ensure Classic; expose exclusions) - **do not disable WSearch** | `[MS-DOC Search CSP AllowFindMyFiles]` |
| STO-13 | Search indexer respect power / backoff | leave `DisableBackoff` unset (0) | M | all | - | - | - | check-only | `[MS-DOC Search CSP]` |

### 6.4 Background activity, telemetry-adjacent perf, network

| id | name | mechanism | scope | ed/ver | rev | side effects | meas. | level | source |
|---|---|---|---|---|---|---|---|---|---|
| BG-01 | Background apps global off | `HKCU\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications\GlobalUserDisabled=1` (Win11 has no UI for global; per-app in Settings); policy `HKLM\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy\LetAppsRunInBackground=2` (Pro+; 0 user control, 1 force allow, 2 force deny) | U / M | all | yes | **Kills notifications/live updates from Store apps (Teams, WhatsApp, Mail, Phone Link), Widgets refresh, Clock alarms while closed** | Yes (CPU/battery on busy machines); UX cost | Strict (Paranoid via policy) - never Basic | `[SRC winutil]` |
| BG-02 | Delivery Optimization LAN-only / HTTP-only | `HKLM\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization\DODownloadMode=1` (LAN peers) or `0` (HTTP only) or `99` (no cloud); user setting `HKU\S-1-5-20\Software\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Settings\DownloadMode=0` (Raphire) | M | Pro+ policy; HKU works on Home | yes | `0/99` = no LAN peer benefit; `100` bypass deprecated | Yes (upload bandwidth) | Balanced = 1 (LAN), Strict = 0 | `[MS-DOC DO CSP]` `[SRC]` |
| BG-03 | Windows Update: not first for CFR features | `HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings\IsContinuousInnovationOptedIn=0` | M | 22H2+ | yes | Non-security features arrive later | UX/stability | Balanced | `[SRC Raphire]` |
| BG-04 | No auto-restart while signed in | `HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU\NoAutoRebootWithLoggedOnUsers=1` | M | Pro+ (Home honours registry `[UNVERIFIED]`) | yes | Updates may wait longer | UX | Balanced | `[SRC Raphire]` `[MS-DOC Update CSP]` |
| BG-05 | Diagnostic data minimum + DiagTrack off | `HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection\AllowTelemetry=0` (Ent/Edu = Security; Pro/Home floor is 1 = Required); service `DiagTrack` Disabled; task set in section 4 | M | all | yes | Loses Insider, WUfB reports; Defender sample submission separate | Yes (CompatTelRunner/DiagTrack CPU & IO) | Balanced | `[SRC winutil/Raphire]` |
| BG-06 | Program Compatibility Assistant off | service `PcaSvc` Disabled or policy `HKLM\SOFTWARE\Policies\Microsoft\Windows\AppCompat\DisablePCA=1` | M | all | yes | No "this program might not have installed correctly" | Marginal | Balanced | `[MS-DOC services guidance]` |
| BG-07 | Windows Error Reporting off | policy `HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting\Disabled=1` (+ `DontSendAdditionalData=1`), task `QueueReporting` disabled; keep service Manual | M | all | yes | No crash reports; app vendors lose telemetry | Marginal (privacy) | Balanced | `[MS-DOC]` |
| BG-08 | Nearby sharing / cross-device off | `HKCU\Software\Microsoft\Windows\CurrentVersion\CDP\CdpSessionUserAuthzPolicy=0`, `NearShareChannelUserAuthzPolicy=0`, `RomeSdkChannelUserAuthzPolicy=0` (shared experiences), `DragTrayEnabled=0` (24H2 drag tray) | U | all | yes | No Nearby Share / continue-on-PC | Marginal | Balanced (privacy) | `[SRC Raphire]` `[UNVERIFIED value names for authz]` |
| BG-09 | Activity history / Timeline off | `HKLM\SOFTWARE\Policies\Microsoft\Windows\System\EnableActivityFeed=0`, `PublishUserActivities=0`, `UploadUserActivities=0` | M | all | yes | Timeline gone (already retired in Win11) | Marginal | Basic | `[SRC winutil]` |
| BG-10 | WPAD off | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings\WinHttp\DisableWpad=1` (not the service!) | M | all | yes | Corporate auto-proxy breaks | Marginal (security/perf: no WPAD lookup) | Strict | `[MS-DOC services guidance]` |
| BG-11 | Nagle / TCP tuning | leave; see 7 | - | - | - | - | No | not offered | - |
| BG-12 | Windows Spotlight/lock screen/desktop, tips, suggestions | see 1.3 CDM values + `HKCU\Software\Policies\Microsoft\Windows\CloudContent\DisableSpotlightCollectionOnDesktop=1`, `DisableWindowsSpotlightFeatures=1` (policy Ent/Edu; HKCU variant works `[UNVERIFIED]`); `RotatingLockScreenEnabled=0`, `RotatingLockScreenOverlayEnabled=0`, `SubscribedContent-338387Enabled=0` | U | all | yes | Static lock screen | UX + small network | Basic | `[SRC Raphire]` |
| BG-13 | "Get even more out of Windows" / SCOOBE | `HKCU\Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement\ScoobeSystemSettingEnabled=0` | U | all | yes | - | UX | Basic | `[SRC Raphire]` |
| BG-14 | Windows welcome experience / tips / Settings suggestions | CDM `-310093`, `-338389`, `SoftLandingEnabled`, `-338393/-353694/-353696/-353698` = 0; policy `HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent\DisableSoftLanding=1` (Ent/Edu) | U | all | yes | - | UX | Basic | `[SRC]` `[MS-DOC Experience CSP]` |
| BG-15 | Sync provider (OneDrive) ads in Explorer | `HKCU\...\Explorer\Advanced\ShowSyncProviderNotifications=0` | U | all | yes | - | UX | Basic | `[SRC]` |
| BG-16 | Account-related notifications in Start / Windows Backup nag / "Suggested" toasts | `HKCU\...\Explorer\Advanced\Start_AccountNotifications=0`; `HKCU\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.BackupReminder\Enabled=0`; `...\Windows.SystemToast.Suggested\Enabled=0`; `HKCU\Software\Microsoft\Windows\CurrentVersion\SystemSettings\AccountNotifications\EnableAccountNotifications=0`; Phone Link suggestions `HKCU\Software\Microsoft\Windows\CurrentVersion\Mobility\OptedIn=0` | U | 23H2+ | yes | - | UX | Basic | `[SRC Raphire]` |
| BG-17 | Settings Home page & M365 ads | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer\SettingsPageVisibility="hide:home"` (also `hide:aicomponents` for AI page); `HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent\DisableConsumerAccountStateContent=1` (Ent/Edu) | M | 22H2+ | yes | Settings opens on System | UX | Basic (Home page hide) | `[SRC Raphire/winutil]` |
| BG-18 | Search highlights off | `HKCU\Software\Microsoft\Windows\CurrentVersion\SearchSettings\IsDynamicSearchBoxEnabled=0`; policy `HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search\EnableDynamicContentInWSB=0` (Pro+) | U/M | all | yes | Static search icon | UX + no daily content download | Basic | `[MS-DOC Search CSP]` `[SRC]` |
| BG-19 | Bing web results in Start search off | `HKCU\Software\Policies\Microsoft\Windows\Explorer\DisableSearchBoxSuggestions=1` (works Home/Pro; also hides Explorer search-box suggestions), Ent/Edu: `HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search\ConnectedSearchUseWeb=0`; search history `HKCU\...\SearchSettings\IsDeviceSearchHistoryEnabled=0`; Store app suggestions in search: no policy - winutil denies ACL on `store.db` (hack; not recommended) | U | all | yes | No web answers in Start | UX + latency (Start search is faster offline) | Balanced | `[SRC Raphire]` `[MS-DOC Search CSP]` |
| BG-20 | Device companion auto-install off | `HKLM\SOFTWARE\Policies\Microsoft\Windows\Device Metadata\PreventDeviceMetadataFromNetwork=1` | M | all | yes | No OEM device icons/apps | UX | Basic | `[SRC]` |
| BG-21 | Reduce Widgets/News network | policy `AllowNewsAndInterests=0` | M | Pro+ | yes | Widgets gone | Yes (background fetch, RAM of Widgets.exe/WebExperience) | Balanced | `[MS-DOC]` |
| BG-22 | Windows Sandbox / Hyper-V / VBS | leave; see 7 (HVCI trade-off) | - | - | - | - | - | - | - |

### 6.5 Taskbar, Start, Explorer, input (UX)

| id | name | mechanism | scope | ed/ver | rev | side effects | meas. | level | source |
|---|---|---|---|---|---|---|---|---|---|
| UX-01 | Widgets button off | see BG-21; per-user `TaskbarDa=0` (UCPD-blocked from scripts) | U/M | 21H2+ | yes | - | UX | Basic | `[MS-DOC]` `[SRC Sophia]` |
| UX-02 | Chat/Teams button off | `TaskbarMn=0` (22H2/23H2; button removed in 23H2 KB5031455+) | U | 22H2 | yes | - | UX | Basic | `[SRC]` |
| UX-03 | Copilot button off | `HKCU\...\Explorer\Advanced\ShowCopilotButton=0` | U | 23H2+ | yes | - | UX | Basic | `[SRC]` |
| UX-04 | Search box style | `HKCU\Software\Microsoft\Windows\CurrentVersion\Search\SearchboxTaskbarMode` 0 hidden, 1 icon, 2 box, 3 icon+label; policy `ConfigureSearchOnTaskbarMode` (24H2, Pro+) | U/M | 22H2+ | yes | - | UX | Basic | `[MS-DOC Search CSP]` `[SRC]` |
| UX-05 | Task View button | `ShowTaskViewButton=0`; policy `HideTaskViewButton` (22H2+) | U/M | all | yes | - | UX | Basic | `[MS-DOC Start CSP]` |
| UX-06 | Taskbar left alignment | `TaskbarAl=0` | U | 21H2+ | yes | - | UX | Basic | `[SRC]` |
| UX-07 | Taskbar combine / labels | `TaskbarGlomLevel` 0 always, 1 when full, 2 never; secondary `MMTaskbarGlomLevel`; `MMTaskbarMode` 0 all/1 main+active/2 active | U | 23H2+ (never combine) | yes | - | UX | Basic | `[SRC Raphire]` |
| UX-08 | End task in taskbar context menu | `HKCU\...\Explorer\Advanced\TaskbarDeveloperSettings\TaskbarEndTask=1` | U | 23H2+ | yes | - | UX | Basic | `[SRC]` |
| UX-09 | Last active click | `HKCU\...\Explorer\Advanced\LastActiveClick=1` | U | all | yes | - | UX | Basic | `[SRC]` |
| UX-10 | Seconds in clock | `ShowSecondsInSystemClock=1` (Settings toggle in 22H2+) | U | 22H2+ | yes | Small extra CPU/battery (MS warns in Settings) | UX (negative perf, tiny) | Preference | `[SRC Sophia]` |
| UX-11 | Start recommendations off | Home/Pro: `Start_IrisRecommendations=0`, `Start_TrackDocs=0`, `Start_TrackProgs=0` (`Explorer\Advanced`), `HKCU\Software\Microsoft\Windows\CurrentVersion\Start\ShowRecentList=0`, `ShowFrequentList=0`; policy `HideRecommendedSection=1` (`HKCU` or `HKLM` `\Software\Policies\Microsoft\Windows\Explorer`; CSP doc lists Pro/Ent/Edu 22H2+; early builds honoured it only on Ent/Edu/SE - `[UNVERIFIED on Home]`); winutil also sets `HKLM\SOFTWARE\Microsoft\PolicyManager\current\device\Start\HideRecommendedSection` + `Education\IsEducationEnvironment` (hack) | U/M | 22H2+ | yes | "More pins" layout: `HKCU\...\Explorer\Advanced\Start_Layout=1` (more pins) / 2 (more recommendations) | UX | Basic | `[MS-DOC Start CSP]` `[SRC Sophia/winutil/Raphire]` |
| UX-12 | Start "All apps" view (25H2) | `HKCU\...\Explorer\Advanced\Start_AllAppsView`? Raphire ships `Start_AllApps_{Category,Grid,List}.reg` (26200+) `[UNVERIFIED value]`; policy `HideCategoryView` (Insider) | U | 25H2 | yes | - | UX | Preference | `[SRC Raphire]` |
| UX-13 | Phone Link panel in Start off | `HKCU\Software\Microsoft\Windows\CurrentVersion\Start\Companions\Microsoft.YourPhone_8wekyb3d8bbwe\IsEnabled=0` | U | 23H2+ | yes | - | UX | Basic | `[SRC]` |
| UX-14 | Show file extensions / hidden files / launch to This PC / compact view / checkboxes | `HideFileExt=0`, `Hidden=1` (+`ShowSuperHidden=1` for protected OS files - not default), `LaunchTo=1` (This PC; 2 Downloads `[UNVERIFIED]`, Home default), `UseCompactMode=1`, `AutoCheckSelect=0` (all `HKCU\...\Explorer\Advanced`) | U | all | yes | Extensions on = security win (double-extension malware) | UX/security | Basic (extensions on) | `[SRC]` |
| UX-15 | Recent files / frequent folders in Quick Access & Home | `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\ShowRecent=0`, `ShowFrequent=0`, `ShowCloudFilesInQuickAccess=0` (Office/OneDrive files) | U | all | yes | Home page becomes sparse | UX/privacy | Balanced | `[SRC Sophia]` |
| UX-16 | Hide Home / Gallery / OneDrive / Network from nav pane | `HKCU\Software\Classes\CLSID\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}\System.IsPinnedToNameSpaceTree=0` (Home), `{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}` (Gallery), `{018D5C66-4533-4307-9B53-224DE2ED1FE6}` (OneDrive), 3D Objects legacy `{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}` | U | 22H2+ | yes | - | UX | Preference | `[SRC Raphire]` |
| UX-17 | Classic (Windows 10) context menu | create key `HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32` with empty default value; restart Explorer. Undo = delete key | U | 21H2+ (works on 24H2/25H2) | yes | Loses new-menu items (Open in Terminal, Share, tabs "Compress to" 7z/TAR are still under classic "Compress to"), Copilot/Snipping actions; undocumented hack (Sophia dropped it) | UX | Preference (Basic-visible, off by default) | `[SRC Raphire/winutil]` |
| UX-18 | Verbose status messages | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System\VerboseStatus=1` | M | all | yes | - | UX (diagnostics) | Developer | `[SRC winutil]` |
| UX-19 | Detailed BSoD | `HKLM\SYSTEM\CurrentControlSet\Control\CrashControl\DisplayParameters=1` (+`DisableEmoticon=1`) | M | all | yes | - | UX (diagnostics) | Developer | `[SRC winutil/Sophia]` |
| UX-20 | Mouse acceleration off ("Enhance pointer precision") | `HKCU\Control Panel\Mouse\MouseSpeed="0"`, `MouseThreshold1="0"`, `MouseThreshold2="0"` + `SystemParametersInfo(SPI_SETMOUSE)` to apply live | U | all | yes | Raw 1:1 mouse; some users prefer accel | Yes (input consistency; real for gaming/CAD) | Gamer default on; Preference elsewhere | `[SRC Raphire/winutil]` |
| UX-21 | Keyboard repeat delay/rate | `HKCU\Control Panel\Keyboard\KeyboardDelay=0` (0-3), `KeyboardSpeed=31` | U | all | yes | - | UX | Preference | `[SRC winutil]` |
| UX-22 | Sticky Keys shortcut off (5x Shift) | `HKCU\Control Panel\Accessibility\StickyKeys\Flags="506"` (default 510; clears HOTKEYACTIVE); also `ToggleKeys\Flags="58"`, `Keyboard Response\Flags="122"` (Filter Keys) `[UNVERIFIED exact]` | U | all | yes | Accessibility users lose shortcut | UX (real for gamers) | Gamer default; Preference | `[SRC Raphire]` |
| UX-23 | Num Lock at boot | `HKU\.DEFAULT\Control Panel\Keyboard\InitialKeyboardIndicators="2"` (+ HKCU; some builds need `2147483650`) - Fast Startup can override | U/M | all | yes | - | UX | Preference | `[SRC winutil]` |
| UX-24 | PrtScn opens Snipping Tool | `HKCU\Control Panel\Keyboard\PrintScreenKeyForSnippingEnabled=1/0` (default 1 since 23H2) | U | 22H2+ | yes | - | UX | Preference | `[SRC Sophia]` |
| UX-25 | Dark mode | `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme=0`, `SystemUsesLightTheme=0` | U | all | yes | - | UX (OLED battery) | Preference | `[SRC]` |
| UX-26 | Notifications off (all) / Focus | `HKCU\Software\Microsoft\Windows\CurrentVersion\PushNotifications\ToastEnabled=0` (kills all toasts incl. calendar/alarms) | U | all | yes | Loses all app notifications | UX | Strict (per-app is better) | `[SRC Raphire]` |
| UX-27 | Autoplay off | `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers\DisableAutoplay=1`; policy `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer\NoDriveTypeAutoRun=255` | U/M | all | yes | No auto-open prompts | Security/UX | Balanced | `[SRC Sophia]` |
| UX-28 | USB "notify me if there are issues" | `HKCU\Software\Microsoft\Shell\USB\NotifyOnUsbErrors=0` | U | all | yes | Loses USB error toasts | UX | Preference | `[UNVERIFIED]` |
| UX-29 | Bluetooth Swift Pair notifications | Settings > Bluetooth > "Show notifications to connect using Swift Pair" (`HKCU\Software\Microsoft\Windows\CurrentVersion\Bluetooth\QuickPair`? `[UNVERIFIED]`) | U | all | yes | - | UX | Preference | - |
| UX-30 | Dynamic Lighting off (24H2) | `HKCU\Software\Microsoft\Lighting\AmbientLightingEnabled=0`, `ControlledByForegroundApp=0` | U | 24H2 | yes | RGB devices revert to vendor software | UX (small CPU) | Preference | `[UNVERIFIED value names]` |
| UX-31 | Windows Studio Effects / Voice Access / Live Captions | Copilot+/NPU features; toggled in Settings; no debloat action | - | - | - | - | - | leave | - |
| UX-32 | Explorer folder-type auto discovery off | winutil deletes `Bags`/`BagMRU` and sets `Bags\AllFolders\Shell\FolderType=NotSpecified` | U | all | partial | **Disables Explorer grouping/typed views**; heavy-handed | UX (faster large folders) | Not offered by default (Developer, warn) | `[SRC winutil]` |
| UX-33 | Default terminal = Windows Terminal | `HKCU\Console\%%Startup\DelegationConsole={2EACA947-7F5F-4CFA-BA87-8F7FBEEFBE69}`, `DelegationTerminal={E12CFF52-A866-4C77-9A90-F570A7AA2C6B}` | U | 22H2+ | yes | - | UX | Developer | `[SRC Sophia DefaultTerminalApp]` |
| UX-34 | Developer Mode | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock\AllowDevelopmentWithoutDevLicense=1` (+ `AllowAllTrustedApps=1`) | M | all | yes | Sideloading of any signed MSIX, symlink without admin, Device Portal FoD; **security trade-off** | UX (dev) | Developer only, warn | `[MS-DOC ApplicationManagement CSP]` |
| UX-35 | Copilot key remap | `SetCopilotHardwareKey` (AUMID) / Settings deep-link | U | 22H2+ | yes | - | UX | Basic (when key present) | `[MS-DOC]` |
| UX-36 | Snipping Tool / Notepad / Paint AI features off | `HKLM\SOFTWARE\Policies\WindowsNotepad\DisableAIFeatures=1`; Paint policies (1.2); Snipping `AllowScreenRecorder` | M | 24H2+ | yes | - | UX/privacy | Balanced | `[MS-DOC WindowsAI CSP]` `[SRC]` |
| UX-37 | Lock screen off (straight to sign-in) | policy `HKLM\SOFTWARE\Policies\Microsoft\Windows\Personalization\NoLockScreen=1` (Ent/Edu documented; works on Pro `[UNVERIFIED]`) | M | Pro+ | yes | Loses lock-screen widgets/spotlight | UX | Preference | `[SRC winutil]` |
| UX-38 | Explorer "Show more options" for OneDrive/Share/Include-in-library removal | HKCR shell handlers `{7AD84985-87B4-4a16-BE58-8B72A5B390F7}` (Include in library) etc. via `HKCU\Software\Classes\CLSID\...\` "-" trick (Raphire `.reg` files) | U | all | yes | - | UX | Preference | `[SRC Raphire]` |

### 6.6 Gaming-specific (real)

| id | name | mechanism | scope | ed/ver | rev | side effects | meas. | level | source |
|---|---|---|---|---|---|---|---|---|---|
| GM-01 | Game DVR / background recording off | see section 5 (AppCaptureEnabled, GameDVR_Enabled, AllowGameDVR) | U/M | all | yes | No "record last 30 s" | Yes (a few % FPS / VRAM on capture-enabled systems) | Gamer default; Balanced elsewhere | `[SRC]` `[MS-DOC]` |
| GM-02 | Game Mode on (default) | `AutoGameModeEnabled=1` | U | all | yes | - | Small but real (background task throttling, WU deferral while gaming) | Keep on | `[SRC winutil]` |
| GM-03 | Hardware-accelerated GPU scheduling (HAGS) | `HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\HwSchMode=2` on / 1 off; Windows enables by default on supported GPUs (WDDM 2.7+) | M | 2004+ | yes | Depends on driver; DLSS 3 frame-gen requires HAGS on | Marginal; leave to GPU vendor default | Not toggled by default (Gamer: expose with "leave default") | `[SRC Sophia GPUScheduling]` |
| GM-04 | Windowed games optimizations / Auto HDR / VRR | Settings > Display > Graphics; `HKCU\Software\Microsoft\DirectX\UserGpuPreferences\DirectXUserGlobalSettings="SwapEffectUpgradeEnable=1;VRROptimizeEnable=1;"` | U | 22H2+ | yes | - | Real latency win for windowed DX10/11 games; leave enabled | Keep on | `[UNVERIFIED value string]` |
| GM-05 | Fullscreen optimizations per app | per-exe `HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers\<exe>` = `~ DISABLEDXMAXIMIZEDWINDOWEDMODE`; global winutil `GameConfigStore\GameDVR_DXGIHonorFSEWindowsCompatible=1`, `GameDVR_FSEBehaviorMode=2`, `GameDVR_HonorUserFSEBehaviorMode=1`, `GameDVR_EFSEFeatureFlags=0` | U | all | yes | Global disable loses colour management/HDR in FSE (winutil warning); MS says FSO generally improves | Per-game only (some titles benefit) | Gamer advanced, per app | `[SRC winutil]` |
| GM-06 | Xbox Game Bar popups when controller connects | `UseNexusForGameBarEnabled=0`; `ms-gamebar` protocol redirect (Raphire) | U | all | yes | - | UX | Gamer/Balanced | `[SRC Raphire]` |
| GM-07 | Mouse acceleration off | UX-20 | - | - | - | - | Yes | Gamer | - |
| GM-08 | Power plan High performance / PCIe / USB suspend (desktop) | PWR-01/05/06 | - | - | - | - | Marginal | Gamer desktop | - |
| GM-09 | Memory Integrity (HVCI) / VBS off | Settings > Windows Security > Core isolation, or `HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity\Enabled=0`, VBS `...\DeviceGuard\EnableVirtualizationBasedSecurity=0`; VMP feature off | M | all | yes (reboot) | **Security trade-off** (kernel exploit mitigation, credential guard); MS: memory integrity/VMP "can impact gaming performance" and recommends re-enabling after gaming | Yes: typically 0-10 % (up to ~15 % in a few titles/older CPUs) | Advanced-with-warning only; never default; Gamer opt-in with explicit re-enable reminder | `[MS-DOC support article "Options to optimize gaming performance in Windows 11" - paraphrased, `[UNVERIFIED fetch]`]` |
| GM-10 | Defender exclusions for game folders | `Add-MpPreference -ExclusionPath` | M | all | yes | Security trade-off; small load-time gains | Marginal | Not offered by default | rationale |
| GM-11 | Multi-plane overlay (MPO) off | `HKLM\SOFTWARE\Microsoft\Windows\Dwm\OverlayTestMode=5` | M | all | yes | Fixes flicker/black screens on some GPU/driver combos (NVIDIA advice 2022); **not** a perf tweak; can hurt power/perf | No (troubleshooting only) | Advanced troubleshooting | `[SRC winutil toggle]` |
| GM-12 | DirectStorage / GPU decompression | no toggle; requires NVMe + DX12 GPU | - | - | - | - | - | leave | - |

---
## 7. Snake-oil and harmful tweaks - the exclusion list

Verdicts: **EXCLUDE** = the app must not ship it (placebo or harmful); **ADVANCED-WITH-WARNING** = real effect but with a security/stability trade-off - allowed only behind an explicit advanced gate with a plain-language warning and one-click undo; **CHECK-ONLY** = show status, never change.

| # | Tweak (as commonly marketed) | What it actually does | Why placebo / harmful | Evidence | Verdict |
|---|---|---|---|---|---|
| 1 | Timer resolution "optimizers" (ISLC/TimerResolution.exe/`bcdedit` tricks to force 0.5 ms globally) | Calls `timeBeginPeriod`/`NtSetTimerResolution` in a background process | Since Windows 10 2004 the request is **per-process**; since Windows 11 minimized/occluded processes are not granted it at all; a background "resolution service" cannot change other processes' resolution. Costs power. | MS `timeBeginPeriod` remarks | EXCLUDE |
| 2 | `bcdedit /set useplatformclock true` / "disable HPET for FPS" / `disabledynamictick yes` / `tscsyncpolicy Enhanced` | Changes kernel timekeeping source/tick behaviour | `useplatformclock` is a debugging aid that forces the *slower* HPET; results are anecdotal, hardware-specific, often negative; dynamic tick change trades power for nothing measurable; a wrong TSC policy can cause stutter or timing bugs. Modifying BCD also risks boot issues on BitLocker devices (recovery key prompt). | bcdedit reference (debugging options); no MS perf claim | EXCLUDE |
| 3 | `HKLM\SYSTEM\...\Multimedia\SystemProfile\NetworkThrottlingIndex=0xFFFFFFFF` | Disables the MMCSS network throttling (10 packets/ms) that applies **only while an MMCSS multimedia thread is registered** | Games/apps not using MMCSS audio/video tasks are unaffected; historical KB948066 workaround for Vista streaming glitches; no measurable latency benefit today | MMCSS doc (SystemResponsiveness/Tasks); KB948066 (historic) | EXCLUDE (allow only as documented "MMCSS audio glitch workaround", Advanced) |
| 4 | `SystemResponsiveness=0` ("gaming responsiveness") | Sets the CPU % reserved for *low-priority* work under MMCSS to 0; values <10 are clamped to 20 by the driver | Values below 10 are clamped to 20 - **setting 0 does nothing different from default 20**; 100 disables MMCSS (worse audio) | MMCSS doc: "Values below 10 and above 100 are clamped to 20" | EXCLUDE |
| 5 | MMCSS `Tasks\Games` `GPU Priority=8`, `Priority=6`, `Scheduling Category=High`, `SFIO Priority=High` | Edits per-task MMCSS parameters | GPU Priority "is not yet used", SFIO Priority "is not used" (MS doc); "High" category is reserved for Pro Audio and can starve the UI; games rarely register with MMCSS "Games" | MMCSS doc | EXCLUDE |
| 6 | Nagle off (`TcpAckFrequency=1`, `TCPNoDelay=1` per interface) | Disables delayed ACK / Nagle | Only helps specific chatty TCP apps (some MMOs/RDP), can *increase* packet rate and CPU; not a general improvement; modern games use UDP. | MS KB328890 (per-app fix) | ADVANCED-WITH-WARNING (per-app troubleshooting only), never default |
| 7 | `Win32PrioritySeparation` = 0x26/0x28/0x2A "gaming" | Foreground quantum boost tuning | Client default (2 = short, variable, 3:1) is already foreground-biased; changes are within noise and can hurt background tasks (encoding, streaming) | MS "Windows Internals"/perf tuning docs | EXCLUDE (CHECK-ONLY) |
| 8 | `LargeSystemCache=1` | File cache favours system cache over working sets | Server file-server setting; on client it starves apps of memory, worsens paging | MS docs (memory management, server tuning) | EXCLUDE (harmful) |
| 9 | `DisablePagingExecutive=1` | Keeps kernel-mode drivers/kernel paged pool in RAM | Only meaningful for kernel debugging; modern systems already rarely page kernel; wastes RAM | MS debugging docs | EXCLUDE |
| 10 | `ClearPageFileAtShutdown=1` marketed as "perf/privacy" | Zeroes pagefile at shutdown | Slows shutdown by minutes on big pagefiles; only a marginal security control (already covered by BitLocker/PDE) | MS security baseline notes | EXCLUDE from perf; may exist under a separate security profile as CHECK-ONLY |
| 11 | Disable / shrink / move page file, "no page file with 32 GB RAM" | Removes system-managed pagefile | Breaks crash dumps, causes commit-limit OOM crashes in games/creative apps that reserve address space, disables memory compression's backing; SSD wear argument is obsolete | MS pagefile guidance | EXCLUDE (CHECK-ONLY: warn if user set none) |
| 12 | Disable memory compression / SysMain / Superfetch / Prefetch (`EnablePrefetcher=0`, `EnableSuperfetch=0`) | Turns off memory management heuristics | Windows already adapts to SSDs (E7 blog); disabling SysMain on SSD gives no gain and can slow app launch; `EnableSuperfetch` value is ignored on Win10+/11 (`SysMain` service controls it) | Engineering Windows 7 SSD blog; MS SysMain "No guidance" | EXCLUDE (SysMain: ADVANCED for HDD "100 % disk" troubleshooting only) |
| 13 | Registry "cleaners"/"defragmenters" | Delete "orphan" keys | Microsoft does not support registry cleaners; no measurable perf gain; well-documented breakage | MS KB2563254 "Microsoft support policy for the use of registry cleaning utilities" | EXCLUDE (never bundle/advertise) |
| 14 | RAM "optimizers"/"boosters", `EmptyStandbyList` on a timer | Trim working sets / flush standby list | Forces re-reads from disk; the standby list *is* the cache; only masks a leak; can stutter games | MS memory manager docs; Windows Internals | EXCLUDE (a manual "clear standby list" button for a known-leaking driver is Advanced at most) |
| 15 | `SvcHostSplitThresholdInKB` = huge value ("fewer svchost processes") | Re-groups services into shared svchost like <3.5 GB systems | MS split them for **reliability, security isolation and diagnostics**; merging saves a few MB and reintroduces shared-failure domains; older winutil shipped it and later stopped | MS "Service host grouping in Windows 10" | EXCLUDE (harmful) |
| 16 | "IRQ8Priority=1" / IRQ priority tweaks | Legacy NT4 registry | Not read by modern kernels | No MS reference exists post-NT4 | EXCLUDE (myth) |
| 17 | MSI mode / interrupt affinity via registry (`MessageSignaledInterruptProperties`, `Interrupt Management\Affinity Policy`) | Forces MSI/affinity per device | Real but hardware-specific; wrong values cause no-boot/BSOD; drivers already choose MSI when supported | MS driver docs | ADVANCED-WITH-WARNING (expert tool territory; not in a debloat app) |
| 18 | CPU affinity/priority "unpark all cores", core-parking registry hacks (`ValueMax` edits under `0cc5b647...`) | Edits hidden power settings | Balanced already unparks under load; hidden-attribute editing can leave the plan corrupt | powercfg docs | EXCLUDE (use documented powercfg indices only, PWR-07) |
| 19 | Ultimate Performance on laptops | Max-perf overlay/plan on battery devices | MS deliberately hides it on battery systems; kills battery, heat, and can throttle harder | MS Insider blog 17101 | EXCLUDE on laptops (desktop: ADVANCED, PWR-02) |
| 20 | Disable Windows Defender / real-time protection / tamper protection permanently | Registry `DisableAntiSpyware` (ignored since 2020), tamper hacks | Unsupported; leaves device unprotected; `DisableAntiSpyware` no longer works | MS Defender docs | EXCLUDE (NEVER) |
| 21 | Disable UAC (`EnableLUA=0`) | Removes elevation prompts | Breaks Store apps, Edge, sandboxing; unsupported | MS UAC docs | EXCLUDE (NEVER) |
| 22 | Disable Windows Update / WaaSMedic / UsoSvc / block via hosts | Stops updates | Security; WaaSMedic self-heals; hosts blocking breaks Store/Defender/activation | MS services guidance | EXCLUDE (offer pause/defer policies only) |
| 23 | Telemetry via `hosts` file blocklists (huge lists) | DNS-sinks MS endpoints | Breaks Store, Xbox, Office activation, Defender cloud, Windows Update, Copilot; also *ignored* for some hard-coded endpoints; Defender flags hosts tampering | Community breakage reports; MS endpoint docs | EXCLUDE (use documented policies) |
| 24 | Disable Windows Search service to "save CPU" | `WSearch` Disabled | Start menu typing, Explorer search, Outlook classic search break; the indexer already backs off under load | MS Search CSP; user reports | EXCLUDE as default (Paranoid ADVANCED with warning); offer scope reduction |
| 25 | Disable `WpnService`/`WpnUserService`/`cbdhsvc`/`TabletInputService`/`TextInputManagementService`/`StateRepository`/`CDPSvc` "for performance" | Kills notifications, clipboard history, touch keyboard/emoji, Start | Documented breakage; older winutil lists did this and generated bug reports | winutil issues; MS "Do not disable" | EXCLUDE (NEVER list, section 3) |
| 26 | Disable `Themes` / `DWM` / "classic theme" | Turn off composition | DWM cannot be disabled on Win8+; Themes off breaks accessibility themes | MS services guidance | EXCLUDE |
| 27 | Disable HVCI/VBS/VMP **by default** for FPS | Removes kernel exploit mitigations | Real perf effect but security regression; MS advises re-enabling after gaming | MS gaming-performance support article | ADVANCED-WITH-WARNING (GM-09), never default, never silent |
| 28 | Defender exclusions for whole drives/game folders by default | Removes scanning | Security hole for negligible gain | MS Defender exclusion guidance | ADVANCED-WITH-WARNING at most; not in profiles |
| 29 | Global "disable fullscreen optimizations" | `GameDVR_FSEBehaviorMode` etc. | MS: FSO improves most titles; global disable loses HDR/colour management in FSE (winutil's own warning) | DirectX team blog on FSO (2019); winutil description | ADVANCED per-app (GM-05); global EXCLUDE |
| 30 | HAGS force on/off for everyone | `HwSchMode` | Vendor-driver dependent; can reduce or increase perf; DLSS FG needs it on | GPU vendor guidance | CHECK-ONLY / expose with "vendor default" note |
| 31 | MPO disable (`OverlayTestMode=5`) as "FPS boost" | Disables DWM overlays | Troubleshooting only (flicker); can *reduce* efficiency/perf | NVIDIA support note 2022 | ADVANCED troubleshooting; not perf |
| 32 | "Disable Hyper-V/Sandbox to speed up" | Removes optional features | If not enabled they cost nothing; if enabled the user needs them | - | EXCLUDE (VBS is the real cost, see 27) |
| 33 | `fsutil behavior set disablelastaccess 1`, `disable8dot3 1`, `memoryusage 2`, `mftzone` | NTFS knobs | Last-access is already system-managed off; 8.3 breaks legacy installers; memoryusage 2 can reduce pool for others; mftzone irrelevant on modern volumes | fsutil doc | STO-06/07 CHECK-ONLY / Developer with warning |
| 34 | Disable Delivery Optimization service / BITS / "Windows Update Medic" | Service disable | Breaks updates/Store; use `DODownloadMode` policy | DO CSP | EXCLUDE (policy instead) |
| 35 | Force `TaskbarDa`/`UserChoice` writes by copying `powershell.exe`, disabling UCPD driver (`sc config UCPD start=disabled`) | Bypasses User Choice Protection | Bypassing a security driver; Windows re-enables it via a scheduled task; violates MS policy | Sophia comments; UCPD behaviour | EXCLUDE (use policies/Settings deep-links) |
| 36 | Force-remove Edge (winutil `RemoveEdge` dummy-exe trick, Raphire `ForceRemoveEdge`) | Unlocks Edge uninstaller | Unsupported outside EEA; breaks Sandbox, WebView2 fallbacks, Store surfaces; Edge returns via WU | MS DMA blog; tool warnings | EXCLUDE (offer policies) |
| 37 | Remove Microsoft Store / App Installer / SecHealthUI / system apps via `EndOfLife`/ACL hacks | Deletes protected packages | Breaks app updates, winget, Windows Security UI, sign-in | 1.1 | EXCLUDE (NEVER) |
| 38 | Set every service to Manual/Disabled ("300-service lists") | Mass startup-type changes | Trigger-start services cost nothing; mass changes cause hidden breakage months later; WU resets many | winutil's own retreat to 5 services in 2025 | EXCLUDE (curated list only, section 3) |
| 39 | Disable all scheduled tasks under `\Microsoft\Windows\*` including UpdateOrchestrator/Defender/Servicing | Mass task disable | Breaks updates, TRIM, WinSxS cleanup, Defender maintenance | winutil issue #1098 | EXCLUDE (curated, section 4) |
| 40 | Disable `iphlpsvc`/IPv6 ("IPv6 slows internet") | `DisabledComponents=0xFF` | MS does not recommend disabling IPv6 (breaks HomeGroup-era features, Teredo, some VPNs, DirectAccess, WSL2 port proxy); "prefer IPv4" (0x20) is the only supported variant | MS "Guidance for configuring IPv6" | EXCLUDE (offer "prefer IPv4" as Advanced only) |
| 41 | "Debloat" by deleting `WindowsApps` folders, `SystemApps` folders, or `takeown` on `C:\Windows` | File deletion | Corrupts servicing store; SFC/DISM failures; unbootable after feature update | - | EXCLUDE |
| 42 | Disabling `Spooler` on every machine | Service off | Fine only if no printing (incl. PDF/XPS/OneNote printers) - not a perf tweak | MS services guidance | Balanced-with-check (offer only when no printers detected) |
| 43 | `AllowTelemetry=0` on Home/Pro expecting "off" | Policy value 0 | On Home/Pro the floor is 1 (Required); 0 is treated as 1 - not harmful, but the UI must not claim "off" | MS diagnostic-data docs | Keep, but truthful labelling |
| 44 | Disable `DPS`/`WdiSystemHost`/`WdiServiceHost` "diagnostics = telemetry" | Kills local diagnostics | These are local troubleshooting; no upload; disabling removes network troubleshooter, reliability monitor | MS services guidance | EXCLUDE |
| 45 | GPU "Prefer maximum performance", "Threaded optimization", NVIDIA/AMD control-panel registry pokes | Vendor driver settings via registry | Undocumented, driver-version-specific keys | - | EXCLUDE (out of scope) |
| 46 | "Reduce Windows 11 latency" by disabling `Windows Filtering Platform`/BFE/firewall | Security removal | Firewall/VPN dependency; no latency change | - | EXCLUDE (NEVER) |
| 47 | Registry `AutoEndTasks=1`, `HungAppTimeout=1000`, `WaitToKillAppTimeout=2000`, `WaitToKillServiceTimeout=2000` "faster shutdown" | Shortens shutdown grace periods | Real but risks data loss for apps saving on close; MS minimum recommendations; marginal seconds | MS docs | ADVANCED (Developer) with warning; not default |
| 48 | Disable `Windows Error Reporting` service by setting service Disabled | - | MS "Do not disable" (ISVs need it); use policy instead (BG-07) | MS services guidance | Use policy, not service |
| 49 | Disable Windows Sandbox/Hyper-V *service* set (`vmcompute`, `HvHost`, `vmic*`) on physical machines | Manual triggers | Already idle; MS "Do not disable" (Application Guard, WSL2, Sandbox) | MS services guidance | EXCLUDE |
| 50 | Windows 11 "Windowed Optimizations" off / DirectStorage off "for stability" | - | Real latency benefits; no evidence of harm | MS DirectX blog | EXCLUDE (leave enabled) |

---

## 8. Implementation notes for the C#/.NET app

1. **Elevation model**: everything in HKLM/services/tasks/DISM/packages-for-all-users needs admin. Per-user (HKCU) UX tweaks should run un-elevated *as the target user* (elevated processes launched via UAC on a different admin account write to the wrong HKCU). Provide `--user <SID>` for the CLI and load hives (`RegLoadKey`) for other/default profiles.
2. **Registry**: use `Microsoft.Win32.RegistryKey` with `RegistryView.Registry64`; snapshot old value + type before writes; treat `ERROR_ACCESS_DENIED` on `TaskbarDa`/`UserChoice` as *expected* (UCPD) and route to Settings deep-links; broadcast `WM_SETTINGCHANGE` and call `SystemParametersInfo` (mouse, animations) or restart Explorer only when required (context menu, taskbar alignment).
3. **Services**: `ServiceController` for state; `ChangeServiceConfig` (advapi32 P/Invoke) or `sc.exe`/`Set-Service` for start type; per-user template keys need registry writes; skip protected services gracefully; store original `Start` value.
4. **Scheduled tasks**: `TaskScheduler` COM (`Microsoft.Win32.TaskScheduler` NuGet or `schtasks.exe`); protected tasks (UpdateOrchestrator/WaaSMedic) must be filtered out, not attempted.
5. **Appx**: WinRT `PackageManager` (see 1.1) with a PowerShell fallback; always show `SignatureKind`, `IsFramework`, dependents (`FindPackages().Where(p => p.Dependencies.Any(...))`) and the "comes back" flag; write an `AppxAllowList.json` that the drift job re-applies after feature updates; log Store product ids for reinstall.
6. **DISM**: PowerShell `Enable-/Disable-WindowsOptionalFeature`, `Add-/Remove-WindowsCapability` (needs internet for `Add`); or `dism.exe` with `/NoRestart` and parse exit codes (3010 = reboot).
7. **Power**: shell out to `powercfg` (aliases `SCHEME_*`, `SUB_*`, setting GUIDs) and detect battery (`SystemInformation.PowerStatus` / WMI `Win32_Battery`, `Win32_ComputerSystem.PCSystemType==2`) to hide laptop-unsafe options.
8. **Edition/version gating**: read `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion` (`CurrentBuild`, `UBR`, `DisplayVersion`, `EditionID`) and `Get-WindowsEdition`; hide Ent/Edu-only policies on Home/Pro (or show as "no effect on this edition"); detect Copilot+ (`Get-CimInstance Win32_Processor`/NPU presence via `HKLM\SYSTEM\CurrentControlSet\Control\WindowsAI`? `[UNVERIFIED]`) before showing Recall/Click to Do items; detect EEA (`HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Control Panel\... GeoID` / `Get-WinHomeLocation`) before showing Edge/Bing removal.
9. **Restore point** before batch apply (`Checkpoint-Computer` / `SystemRestore` WMI; ensure System Protection is enabled) and an export of touched registry keys/services/tasks to a JSON undo file.
10. **Drift/reapply job**: a scheduled task (`\OpenTheWindows\Reapply`) after sign-in and after `Windows Update` events (`Microsoft-Windows-WindowsUpdateClient` 19/43) that re-checks provisioned apps, service start types and CDM values; must be opt-in and visible.

---

## 9. Sources (accessed 2026-08-16)

Microsoft documentation
* Overview of apps on Windows client devices (provisioned-apps page now redirects here) - https://learn.microsoft.com/en-us/windows/application-management/overview-windows-apps
* Get-AppxProvisionedPackage - https://learn.microsoft.com/en-us/powershell/module/dism/get-appxprovisionedpackage
* Policy CSP - ApplicationManagement (RemoveDefaultMicrosoftStorePackages, AllowGameDVR, BlockNonAdminUserInstall) - https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-applicationmanagement
* Control installing and using new Outlook (BlockedOobeUpdaters, UScheduler_Oobe) - https://learn.microsoft.com/en-us/microsoft-365-apps/outlook/get-started/control-install
* Updated Windows and Microsoft 365 Copilot Chat experience (Copilot removal, AppLocker, key remap) - https://learn.microsoft.com/en-us/windows/client-management/manage-windows-copilot
* Policy CSP - WindowsAI (Recall, Click to Do, Copilot key, TurnOffWindowsCopilot deprecated, RemoveMicrosoftCopilotApp) - https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-windowsai
* Policy CSP - Experience (CloudContent editions, ConfigureChatIcon deprecated) - https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-experience
* Policy CSP - NewsAndInterests (AllowNewsAndInterests, DisableWidgetsBoard) - https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-newsandinterests
* Policy CSP - Start (HideRecommendedSection, HideTaskViewButton, ConfigureStartPins) - https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-start
* Policy CSP - Search (AllowSearchHighlights, DoNotUseWebResults, ConfigureSearchOnTaskbarMode, AllowFindMyFiles) - https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-search
* Policy CSP - DeliveryOptimization (DODownloadMode) - https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-deliveryoptimization
* Deprecated features for Windows client - https://learn.microsoft.com/en-us/windows/whats-new/deprecated-features
* Features and functionality removed in Windows client - https://learn.microsoft.com/en-us/windows/whats-new/removed-features
* What's new in Windows 11, version 24H2 for IT pros - https://learn.microsoft.com/en-us/windows/whats-new/whats-new-windows-11-version-24h2
* Available Features on Demand (capability names, WMIC/WordPad/Fax status) - https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/features-on-demand-non-language-fod
* Security guidelines for system services in Windows Server 2016 (service defaults/recommendations) - https://learn.microsoft.com/en-us/windows-server/security/windows-services/security-guidelines-for-disabling-system-services-in-windows-server
* Service host grouping in Windows 10 (SvcHostSplitThresholdInKB) - https://learn.microsoft.com/en-us/windows/application-management/svchost-service-refactoring
* Multimedia Class Scheduler Service (SystemResponsiveness clamping, task values) - https://learn.microsoft.com/en-us/windows/win32/procthread/multimedia-class-scheduler-service
* timeBeginPeriod (per-process timer resolution; Windows 11 occluded behaviour) - https://learn.microsoft.com/en-us/windows/win32/api/timeapi/nf-timeapi-timebeginperiod
* fsutil behavior (DisableDeleteNotify, disablelastaccess, disable8dot3) - https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/fsutil-behavior
* Powercfg command-line options (/hibernate /type reduced, schemes, overlays) - https://learn.microsoft.com/en-us/windows-hardware/design/device-experiences/powercfg-command-line-options
* Windows Insider blog - Previewing changes in Windows to comply with the DMA in the EEA (Edge/Bing/Camera/Photos uninstall) - https://blogs.windows.com/windows-insider/2023/11/16/previewing-changes-in-windows-to-comply-with-the-digital-markets-act-in-the-european-economic-area/
* Windows Insider blog - build 17101 (Ultimate Performance plan; not on battery systems) - https://blogs.windows.com/windows-insider/2018/02/07/announcing-windows-10-insider-preview-build-17101-fast-17604-skip-ahead/
* Options to optimize gaming performance in Windows 11 (Memory integrity/VMP) - https://support.microsoft.com/en-us/windows/options-to-optimize-gaming-performance-in-windows-11-a255f612-2949-4373-a566-ff6f3f474613 (fetch redirected during research - content cited from prior knowledge, marked UNVERIFIED)
* Microsoft support policy for the use of registry cleaning utilities (KB2563254) - https://support.microsoft.com/kb/2563254 (historic KB)
* Microsoft Edge policy reference - https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies

Third-party / community sources
* Raphire/Win11Debloat - `Config/Apps.json`, `Config/Features.json`, `Regfiles/*.reg` (MIT) - https://github.com/Raphire/Win11Debloat
* ChrisTitusTech/winutil - `config/tweaks.json` (main, 2026-08) and tag 25.01.11 (historic 282-service list) (MIT) - https://github.com/ChrisTitusTech/winutil ; PR #3376 (services cleanup) - https://github.com/ChrisTitusTech/winutil/pull/3376 ; issues #775, #867, #1098, #1922, #3278, #3876
* farag2/Sophia-Script-for-Windows - `src/Sophia_Script_for_Windows_11/Module/Sophia.psm1` (MIT) - https://github.com/farag2/Sophia-Script-for-Windows
* andrew-s-taylor/public - `De-Bloat/RemoveBloat.ps1` - https://github.com/andrew-s-taylor/public
* MSEndpointMgr - How to remove built-in apps for Windows 11 25H2 - https://msendpointmgr.com/2025/10/29/how-to-remove-built-in-apps-for-windows-11-25h2/
* System Center Dudes - Remove default Microsoft Store packages using Intune (24-app list) - https://www.systemcenterdudes.com/remove-default-microsoft-store-packages-using-intune/
* Patch My PC - Windows 11 25H2 Remove Default Microsoft Store Packages - https://patchmypc.com/blog/remove-default-microsoft-store-app-packages-windows11-25h2/
* Windows IT Pro Blog - Dynamically remove apps from managed Windows 11 devices - https://techcommunity.microsoft.com/blog/windows-itpro-blog/dynamically-remove-apps-from-managed-windows-11-devices/4516291
* CybersecKyle - Remove preinstalled Microsoft Store apps in Windows 11 (24H2 & 25H2) - https://www.kylereddoch.me/blog/remove-preinstalled-microsoft-store-apps-in-windows-11-24h2-and-25h2/
* WindowsForum - KB5072033 AppXSVC startup change threads - https://windowsforum.com/threads/kb5072033-appxsvc-auto-start-brings-reliability-gains-and-performance-tradeoffs.395264
* Local verification: Windows 11 22H2 Pro (22621) service and scheduled-task inventory dumped 2026-08-16.
