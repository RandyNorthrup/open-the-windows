# 07 — Enterprise / Safety Architecture: applying changes safely, reversibly, for all users, without fighting MDM/GPO

**Project:** Open The Windows (OTW) — open-source, enterprise-grade Windows 11 privacy / hardening / debloat / tweak app (C#/.NET; Core library + CLI + GUI)
**Research date:** 2026-08-16 (all "as of" statements refer to this date)
**Target OS:** Windows 11 23H2 / 24H2 / 25H2 (x64 and ARM64)
**Status:** Engineering research — informs the Core library's *apply engine*, *state model*, *elevation model*, *audit* and *profile* subsystems. This is deliberately **not** a settings catalogue (see `03-*`, `04-*`, `05-*`, `06-*`).
**Confidence markers:** statements are backed by the Sources list; items marked **UNVERIFIED** are practitioner consensus or my own inference that I could not confirm against learn.microsoft.com during this pass and should be validated on a test VM before code is written against them.

> How to read this document: Section 0 is a one-page decision record. Sections 1–10 follow the research brief (policy vs preference; MDM coexistence; all-users; transactions/rollback; drift/remediate; audit; safety rails; profiles/catalogue; elevation & the HKCU-target bug; native-API table + diagnostics). Section 11 is the "top pitfalls" list, Section 12 is Sources.

---

## 0. Decision record (recommended architecture)

| # | Area | Decision | Why (short) |
|---|------|----------|-------------|
| D1 | **Policy writes** | Every catalogue action that targets a `...\Policies\...` path is a **`policy` action**, executed by a `PolicyWriter` that (a) writes the value into the **Local GPO `Registry.pol`** through `IGroupPolicyObject` (gpedit.dll COM; falls back to writing the PReg file directly if COM fails), (b) **mirrors** the same value into the live registry immediately, and (c) triggers `RefreshPolicyEx`. On Home (no GP client-side extensions) step (a) is skipped and only (b) is done, with the catalogue flagging which policies are known to be ignored on Home. | Survives `gpupdate /force`, is visible/editable in gpedit, does not "fight" the GP engine; immediate mirror gives instant effect on Home and avoids waiting for refresh. |
| D2 | **Managed-by-org detection** | Before any policy write, resolve **who owns the setting**: MDM (`HKLM\SOFTWARE\Microsoft\PolicyManager\current\device\<Area>\<Policy>` + `_WinningProvider`), domain/local GPO (RSOP `root\rsop\computer` `RSOP_RegistryPolicySetting`, GP History key), or nobody. If MDM/domain-GPO owns it: show **"Managed by your organisation"**, mark tweak *Not applicable*, refuse unless `--break-glass` is set, and even then warn that the next policy refresh will revert it. | MDM wins over GP for Policy CSP when `MDMWinsOverGP=1`; domain GPO refresh overwrites local values; overriding is futile and creates a "flapping" state that confuses auditors. |
| D3 | **All-users** | Three explicit scopes in the catalogue: `user` (target interactive user), `all-users` (interactive user + every local profile in `ProfileList` whose hive is *not* loaded, via `RegLoadKey`, + Default profile), `machine`. Logged-on/loaded hives other than the interactive user's are **not** loaded from disk; those users get a **queued apply** via a per-user logon mechanism (Active Setup StubPath → `otw.exe apply --user-scope --queued`), which is also what makes *future* users converge. Never touch hives during OOBE, on roaming/FSLogix profiles, or when `RegLoadKey` says the file is locked. | Loading an in-use hive corrupts it; Active Setup is the supported once-per-user hook; Default hive covers new users. |
| D4 | **Rollback** | No KTM. **Journal-first**: for each tweak, snapshot prior state (registry value/type/data or `absent`; service start type + status; task enabled state; package presence; …) into `%ProgramData%\OpenTheWindows\journal\<utc-timestamp>-<runId>.json` (append-only, SHA-256 hash-chained entries, ACL SYSTEM+Administrators), then apply in dependency order, verify, and roll back in reverse on failure. Optional **System Restore point** (`SRSetRestorePoint`, with the 24-hour throttle disclosed and optionally overridden), plus a `.reg`-style human-readable export of every touched key. | KTM is a dead end (TxF deprecation notice; PowerShell removed transactions); a journal gives per-tweak revert, "revert to prior" vs "revert to default", and audit evidence. |
| D5 | **Elevation** | GUI: **`requireAdministrator`** manifest (single elevated process). CLI: `asInvoker` + self-check (scan works unelevated; apply requires elevation and says so with exit code 5). Per-user writes **never** use `HKEY_CURRENT_USER` blindly: resolve the **target user SID** from the interactive session (`WTSQuerySessionInformation(WTS_CURRENT_SESSION, WTSUserName/WTSDomainName)` → `LookupAccountName` → SID) and write to `HKU\<SID>` and `HKU\<SID>_Classes`. If token user ≠ session user (UAC "over-the-shoulder" elevation with a different admin), say so in the UI. | Classic bug: elevated-with-another-admin tools write HKCU into the *admin's* hive. |
| D6 | **Drift / remediate** | CLI verbs `scan` (exit **0** compliant / **1** drift / **2** error), `remediate` (idempotent apply of only non-compliant items; exit **0** ok / **2** error / **3010** ok-but-reboot-required), `--json` on stdout, human text on stderr. A scheduled task (`\OpenTheWindows\Remediate`) at startup + logon + weekly + after build/UBR change re-applies the pinned profile. Detection/remediation `.ps1` wrappers ship for Intune Remediations (detect exit 1 ⇒ remediation runs). | Feature updates re-provision apps / reset services & tasks; Intune's contract is exit-code based; drift monitoring is a differentiator (see `02-*`). |
| D7 | **Audit** | Windows Event Log: custom log **`OpenTheWindows`** with source `OTW` (created at install/first elevated run; `System.Diagnostics.EventLog` package), structured IDs (1000-series apply, 2000-series scan, 3000-series rollback, 4000-series safety/pre-flight, 9000-series errors); an **`EventSource`** ETW provider (`OpenTheWindows-Core`) for tracing; and a local **JSONL audit log** with size-based rotation. Compliance report = machine identity + OS build + profile id/hash + per-tweak desired/actual/compliant + timestamps + app version + SHA-256 of canonical JSON (+ optional org-key signature). | Event Log is what SIEM/SOC teams collect; ETW for support diagnostics; JSONL for offline evidence. |
| D8 | **Profiles & catalogue** | Data-driven **catalogue** (JSON, versioned schema) with code-backed **action types** (`registry`, `policy`, `service`, `task`, `appx`, `feature`, `capability`, `powercfg`, `defender-preference`, `firewall`, `secedit`, `auditpol`, `script` (last resort)). **Profiles** are JSON (`id`, `version`, `appliesTo`, per-category levels, per-tweak overrides). Optional **detached signature**: ECDSA P-256 / SHA-256 built into .NET (Ed25519 via library if wanted), org public keys pinned under `%ProgramData%\OpenTheWindows\trust\`; an org can *require* signed profiles through OTW's own policy key. | Testable, diffable, reviewable tweaks; enterprises need tamper-evident configuration. |

---

## 1. Policy writes vs preference writes

### 1.1 The two kinds of registry locations

* **Preference keys** — e.g. `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\ShowTaskViewButton`. Owned by the feature; the Settings UI reads and writes them; nothing "enforces" them.
* **Policy keys** — the four trees the Group Policy registry client-side extension (CSE) manages: `HKLM\SOFTWARE\Policies`, `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies`, `HKCU\Software\Policies`, `HKCU\Software\Microsoft\Windows\CurrentVersion\Policies`. Components check these first; if present the UI control is typically greyed out ("Some settings are managed by your organization"). ADMX files describe how each policy maps into these keys.

Key behaviours to design around:

1. **A raw write to a policy key works immediately** on any edition — components read the registry, they do not ask "who wrote this". This is why every debloat script "works". But:
2. **On a domain- or MDM-managed machine, the same value can be overwritten** at the next refresh if a GPO/MDM policy manages that value (foreground at boot/logon; background every 90 min ± 30 min by default; `gpupdate`). The registry CSE writes the values from applied GPOs' `Registry.pol` files, and *removes* values it previously applied from GPOs that no longer apply (it keeps a cached copy of applied GPO files under `%ProgramData%\Microsoft\Group Policy\History\<GPO GUID>\` and records the applied GPO list under `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Group Policy\History\{35378EAC-683F-11D2-A89A-00C04FBBCFA2}` — one subkey per GPO in application order, with `DisplayName`, `DSPath`, `FileSysPath`, `GPOName`) — the `History` layout is documented in older MS material and by practitioners; **the ProgramData cache path is UNVERIFIED** in current docs.
3. By default the registry CSE only re-applies a GPO whose version changed; the ADMX policy *"Configure registry policy processing → Process even if the Group Policy objects have not changed"* forces re-application on every refresh. So a manual value under a domain-managed path may *appear* to stick until someone edits the GPO or runs `gpupdate /force` — the worst kind of intermittent bug.
4. **Values in the policy trees that no GPO manages are left alone by the CSE.** That is why "raw policy writes" survive on unmanaged Pro machines: nothing owns them, so nothing reverts them.

### 1.2 How Local Group Policy stores settings

* `%SystemRoot%\System32\GroupPolicy\gpt.ini` — `[General] Version=<n>` (32-bit; low 16 bits = machine version, high 16 bits = user version; the editor bumps it on Save). `Machine\Registry.pol`, `User\Registry.pol`; also `Machine\Microsoft\Windows NT\SecEdit\GptTmpl.inf` (security template) and `Machine\Microsoft\Windows NT\Audit\audit.csv` (advanced audit).
* Per-user / per-group local GPOs (MLGPO): `%SystemRoot%\System32\GroupPolicyUsers\<SID>\User\Registry.pol`.
* `gpupdate [/force]` = `RefreshPolicyEx(bMachine, RP_FORCE)` in userenv.dll — you can call it directly (`RefreshPolicyEx(TRUE, RP_FORCE)` for machine, `FALSE` for the calling user).
* The Local GPO editor loads `Registry.pol` into a temporary registry key (under `HKCU\Software\Microsoft\Windows\CurrentVersion\Group Policy Objects\...` — "populated only when a Group Policy editor is open" per Aaron Margosis) and serialises it back on Save.

### 1.3 `Registry.pol` binary format (PReg) — official spec

From *Registry Policy File Format* (learn.microsoft.com, archived):

* Header: `REGFILE_SIGNATURE = 0x67655250` (ASCII **`PReg`**) then `REGISTRY_FILE_VERSION = 1` (both DWORD LE). Bytes: `50 52 65 67 01 00 00 00`.
* Body: repeated **`[key;value;type;size;data]`**, where the brackets and semicolons are literal UTF-16LE characters, `key` and `value` are NUL-terminated UTF-16LE strings, `type` is a DWORD (REG_SZ=1, REG_EXPAND_SZ=2, REG_BINARY=3, REG_DWORD=4, REG_MULTI_SZ=7, REG_QWORD=11), `size` is a DWORD byte length, and `data` is `size` bytes (strings NUL-terminated UTF-16LE; MULTI_SZ double-NUL).
* `key` omits the root (`HKLM`/`HKCU`) — the file's location decides.
* Special value names: `**DeleteValues` (semicolon list), `**Del.<valuename>` (delete one value), `**DelVals` (delete all values in key), `**DeleteKeys` (semicolon list of subkeys), `**SecureKey` (=1 admins/system full, users read; =0 reset). If value/type/size/data are missing or zero, only the key is created.
* Anecdotal but important (LGPO blog comments): setting a policy to *Not Configured* means **removing the entry from `Registry.pol`**, whereas writing `**Del.<name>` means *managed removal* (re-deleted at every refresh). OTW's `policy` action therefore needs three states: `enabled/value`, `disabled/value`, `not-configured` (remove from .pol + remove mirrored value if OTW wrote it).

**Reference implementations:** *Policy Plus* (Fleex255, VB.NET, **CC-BY-4.0** — attribution required if code is ported; last push 2025-12) has a complete PReg reader/writer and ADMX parser and can target Local GPO, per-user GPOs, .pol files, live registry and offline hives; it also states that per-user local GPOs "have no effect on these limited editions of Windows" (Home) and that `RefreshPolicyEx` "has reduced functionality on editions without full Group Policy infrastructure". LGPO.exe's `/parse` and `/r … /w …` produce a text form ("LGPO text") that is convenient for tests.

**Recommendation:** implement OTW's own `PRegFile` (read/merge/write) in C# — ~200 lines, unit-testable with golden files produced by `LGPO.exe /parse` on a dev box. Do **not** overwrite an existing `Registry.pol`; always *load → merge (replace same key/value) → write*, preserving other entries; take a copy into the journal first; and bump `gpt.ini` Version if writing the file directly (IGroupPolicyObject::Save does this for you).

### 1.4 `IGroupPolicyObject` from C# (preferred write path)

* COM class `CLSID_GroupPolicyObject = {EA502722-A23D-11d1-A7D3-0000F87571E3}` (gpedit.dll); `IID_IGroupPolicyObject = {EA502723-A23D-11d1-A7D3-0000F87571E3}`; `IID_IGroupPolicyObject2 = {7E37D5E7-263D-45CF-842B-96A95C63E46C}` (adds `OpenLocalMachineGPOForPrincipal(sid, flags)` for MLGPO and `GetRegistryKeyPath`).
* Flow: `OpenLocalMachineGPO(GPO_OPEN_LOAD_REGISTRY = 1)` → `GetRegistryKey(GPO_SECTION_MACHINE = 2 | GPO_SECTION_USER = 1, out HKEY)` → write values under that HKEY with normal `RegSetValueEx` (paths relative to the root, e.g. `SOFTWARE\Policies\Microsoft\Windows\DataCollection`) → `Save(bMachine, bAdd = TRUE, REGISTRY_EXTENSION_GUID = {35378EAC-683F-11D2-A89A-00C04FBBCFA2}, pGuid = <snap-in GUID>)`. The docs: *"The Save method saves the specified registry policy settings to disk and updates the revision number of the GPO. If the GPO is to be processed by the snap-in that processes .pol files, you must specify the REGISTRY_EXTENSION_GUID value."* Public samples pass the Administrative Templates snap-in GUID (`{0F6B957E-509E-11D1-A7CC-0000F87571E3}` machine / `{0F6B957D-509E-11D1-A7CC-0000F87571E3}` user) or simply any GUID (the sjitech sample uses `Guid.NewGuid()`); the value lands in `gpt.ini`'s `gPCMachineExtensionNames` list. Practitioner samples run this on an **STA thread**; do the same.
* `Save` "automatically triggers a policy refresh when the user or computer portion of the local GPO is enabled or disabled"; otherwise call `RefreshPolicyEx` yourself.
* Not available when gpedit.dll's dependencies are missing (Home) — COM activation fails → fall back to raw registry (and, optionally, still write the PReg file for forward-compat).

Minimal C# shape (interop declarations abbreviated; see the sjitech sample for the full vtable order):

```csharp
[ComImport, Guid("EA502722-A23D-11d1-A7D3-0000F87571E3")] class GroupPolicyObject { }
[ComImport, Guid("EA502723-A23D-11d1-A7D3-0000F87571E3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IGroupPolicyObject {
  uint New(string domain, string display, uint flags);
  uint OpenDSGPO(string path, uint flags);
  uint OpenLocalMachineGPO(uint flags);
  uint OpenRemoteMachineGPO(string computer, uint flags);
  uint Save([MarshalAs(UnmanagedType.Bool)] bool machine, [MarshalAs(UnmanagedType.Bool)] bool add,
            [MarshalAs(UnmanagedType.LPStruct)] Guid extension, [MarshalAs(UnmanagedType.LPStruct)] Guid app);
  uint Delete(); uint GetName(StringBuilder n, int max); uint GetDisplayName(StringBuilder n, int max);
  uint SetDisplayName(string n); uint GetPath(StringBuilder p, int max); uint GetDSPath(uint s, StringBuilder p, int max);
  uint GetFileSysPath(uint s, StringBuilder p, int max);
  uint GetRegistryKey(uint section, out IntPtr hKey);   // wrap with RegistryKey.FromHandle(new SafeRegistryHandle(hKey, true))
  uint GetOptions(out uint o); uint SetOptions(uint o, uint mask); uint GetType(out IntPtr t);
  uint GetMachineName(StringBuilder n, int max); uint GetPropertySheetPages(out IntPtr p);
}
static readonly Guid RegistryExtension = new("35378EAC-683F-11D2-A89A-00C04FBBCFA2");
static readonly Guid AdmTemplatesSnapinMachine = new("0F6B957E-509E-11D1-A7CC-0000F87571E3");
```

### 1.5 LGPO.exe — can we bundle it? No.

* LGPO.exe ships in the **Microsoft Security Compliance Toolkit** (download id 55319) under Microsoft Software License Terms; the tool's author answered *"Can this executable be redistributed in a commercial product?"* with **"No. Please link back to this site."** (blog comment, June 2016). Treat as **not redistributable**.
* It is fine to **invoke it if present** (user-supplied path or a well-known folder; verify the Authenticode signature is Microsoft's before executing). Useful for enterprise workflows: `LGPO.exe /m|/u path\registry.pol`, `/s GptTmpl.inf`, `/ac audit.csv`, `/t lgpo.txt`, `/b backupdir` (backup), `/parse /m file.pol` (to LGPO text on **stdout**; banners/progress go to **stderr**). Note LGPO is x86 and can be redirected to `SysWOW64\GroupPolicy` if such a folder exists (documented bug in comments) — run it with `Wow64DisableWow64FsRedirection` semantics avoided, i.e. simply don't rely on it as the primary path.

### 1.6 ADMX-backed policies vs raw registry

* An ADMX-backed policy is a *named* setting whose enabled/disabled/element states map to specific registry writes/deletes; the same registry value can be reached raw. OTW's catalogue should carry, for each `policy` action, the ADMX `class/category/policyName` when one exists (from `%SystemRoot%\PolicyDefinitions`) so the UI can say "this is *Computer Configuration → Administrative Templates → … → Allow Telemetry*", so RSOP/MDM ownership can be resolved, and so a future export-to-GPO-backup is possible.
* Some settings have **no ADMX** and only exist as raw values (or as MDM CSP nodes) — flag those `policyKind: raw`.

### 1.7 Windows Home — what is honoured?

* Home lacks `gpedit.msc` and the GP client-side extension packages (people re-add them with `DISM /Add-Package` from `Microsoft-Windows-GroupPolicy-ClientTools-Package` / `-ClientExtensions-Package`; practitioners report that even then `Registry.pol` is *not* processed into the registry on Home — "no GPO handler"). Treat **`Registry.pol` on Home as inert** (**UNVERIFIED** against official docs, but consistent across PolicyPlus, woshub and forum reports).
* The **registry policy trees themselves are honoured on Home** for the majority of components (Explorer, Edge, Defender, Windows Search, Privacy/DataCollection, …), because the consuming component reads the registry directly. Exceptions are policies whose *feature* is not present or is Pro+-only (BitLocker, AppLocker, Windows Update for Business deferrals appear under `Update` CSP as Pro+, many `Policy CSP` nodes list "Pro/Enterprise/Education" only). The Policy CSP reference marks per-policy edition applicability (e.g. `ControlPolicyConflict/MDMWinsOverGP`: Pro, Enterprise, Education, IoT Enterprise — not Home). Use those edition markers when populating each catalogue entry's `appliesTo.editions`, and default to `effectOnHome: unknown` until verified on a Home VM.
* Practical rule for OTW on Home: write policy values directly (mirror step only), require reboot/logoff for anything that a service reads at start-up, and mark the tweak `verifiedOn: [Home 24H2]` only after a real test.

---

## 2. MDM / Intune coexistence

### 2.1 How MDM policies land in the registry

* Policy CSP writes to **`HKLM\SOFTWARE\Microsoft\PolicyManager\current\device\<Area>\<Policy>`** (device scope; user scope under `...\PolicyManager\current\<UserSID>\<Area>`), plus for each set policy the values **`<Policy>_ProviderSet`** (=1) and **`<Policy>_WinningProvider`** (the enrolment GUID) — and per-provider copies under **`HKLM\SOFTWARE\Microsoft\PolicyManager\providers\<EnrollmentGUID>\default\Device\<Area>\<Policy>`**. ADMX-backed CSP policies additionally get written into the classic `...\Policies\...` locations by the ADMX-ingestion machinery.
* **`ControlPolicyConflict/MDMWinsOverGP`** (`./Device/Vendor/MSFT/Policy/Config/ControlPolicyConflict/MDMWinsOverGP`, registry mirror `HKLM\SOFTWARE\Microsoft\PolicyManager\current\device\ControlPolicyConflict\MDMWinsOverGP` = 1): *"If set to 1 then any MDM policy that's set that has an equivalent GP policy will result in GP service blocking the setting of the policy by GP MMC."* It **only applies to policies in Policy CSP** ("does not apply to other MDM settings with equivalent GP settings that are defined in other CSPs such as the Defender CSP"); "the policy should be set at every sync" and — critically for OTW — *"Any values set by scripts/user outside of GP that conflict with MDM are removed."* Default 0; not supported on Home. The MDM Diagnostic Report lists blocked GP settings.
* Windows Update: policies from MDM appear under `PolicyManager\current\device\Update`; classic under `HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate`. **Windows Autopatch / WUfB deployment service** registration is server-side (Graph/Intune); on the client the Autopatch client broker leaves artefacts under `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate` (per patchmypc; exact value names **UNVERIFIED**). Detection rule for OTW: *MDM-enrolled AND any `Update` area policy present in `PolicyManager\current\device`* ⇒ treat all Windows-Update tweaks as **managed** and refuse.

### 2.2 Detecting enrolment / join state (from an elevated app, no SYSTEM needed)

| Signal | How | Notes |
|---|---|---|
| Domain join | `NetGetJoinInformation()` → `NetSetupDomainName`; or WMI `Win32_ComputerSystem.PartOfDomain/Domain` | Cheap, no elevation |
| Entra join / hybrid / registered | parse `dsregcmd /status` (Device State: `AzureAdJoined`, `EnterpriseJoined`, `DomainJoined`, `DomainName`; Tenant Details: `MdmUrl`, `MdmTouUrl`, `MdmComplianceUrl`; User State: `WorkplaceJoined`) | Run **as the interactive user** for User State/SSO State ("must run in a user context"); *"The presence of MDM URLs doesn't guarantee that the device is managed by an MDM."* No documented API equivalent — string-parse defensively (English labels; the format is stable but unversioned) |
| MDM enrolment | enumerate `HKLM\SOFTWARE\Microsoft\Enrollments\<GUID>` (skip the well-known non-MDM GUIDs); look at `EnrollmentState`, `EnrollmentType`, `ProviderID` (`MS DM Server` = Intune), `UPN`, `DiscoveryServiceFullURL`; plus `HKLM\SOFTWARE\Microsoft\Enrollments\<GUID>\DMClient\MS DM Server` | Registry layout is widely documented by practitioners, not by MS; **UNVERIFIED** enum values (`EnrollmentState`=1 "enrolled" is the common reading). Presence of an `Enrollments` subkey ≠ managed |
| MDM (authoritative) | WMI Bridge `root\cimv2\mdm\dmmap`, classes `MDM_DevDetail_Ext01`, `MDM_Policy_Result01_*` | *"For all device settings, the WMI Bridge client must be executed under local system user"* — so only usable from a SYSTEM helper/service (e.g. the OTW scheduled task), not from the elevated GUI |
| Effective policy source for a value | WMI `root\rsop\computer` (`RSOP_RegistryPolicySetting`: `registryKey`, `valueName`, `value`, `GPOID`, `precedence`, `deleted`) and `root\rsop\user\<SID-with-underscores>`; `gpresult /r` / `/x` for humans; GP History key (§1.1) | RSOP namespaces are populated by the GP engine at refresh; empty on Home. Query with `SELECT * FROM RSOP_RegistryPolicySetting WHERE registryKey='...' AND valueName='...'` |
| MDM owns a value | `PolicyManager\current\device\<Area>\<Policy>_WinningProvider` exists and ≠ empty | Requires the catalogue's CSP mapping (`Area/Policy`) — Policy CSP docs list `GPRegistryMappedName`/`ADMXMapped` per policy |

### 2.3 Behaviour when a setting is managed

1. **Resolve owner** at scan time (cache per run): `mdm`, `domain-gpo`, `local-gpo` (OTW's own writes or someone else's), `none`.
2. UI: badge **"Managed by your organisation"** + owner + (for GPO) GPO display name; tweak state shows *desired vs actual* but the toggle is disabled. CLI: `managedBy` field in JSON; `scan` counts it as `not-applicable`, not `drift`.
3. `--break-glass` (CLI) / hidden Advanced switch (GUI): allowed only when explicitly set **and** the tweak's `risk` ≠ Breaking; log Event 4010 "Override of managed setting", write the value raw, and record in journal `overrideOfManaged: true` so `scan` later reports it as expected-to-flap. Never touch `PolicyManager` keys or `Enrollments`.
4. Never modify **`MDMWinsOverGP`**, never disable **`gpsvc`**, **`dmwappushservice`**, **`IntuneManagementExtension`**, and never remove `Group Policy` scheduled tasks — these are enterprise-critical and their removal is a common debloat foot-gun.

---

## 3. Applying HKCU settings for ALL users

### 3.1 The three scopes

* **Current (interactive) user** — the user who owns the interactive session (§9 explains why this is *not* always the process token user).
* **All existing local profiles** — enumerate `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\<SID>` (`ProfileImagePath` REG_EXPAND_SZ; skip `S-1-5-18/19/20` and any SID whose path is under `%SystemRoot%`; skip `.bak` keys); each has `<ProfileImagePath>\NTUSER.DAT` and `<ProfileImagePath>\AppData\Local\Microsoft\Windows\UsrClass.dat`.
* **Future users** — `ProfileList\Default` (REG_EXPAND_SZ, normally `C:\Users\Default`) → `Default\NTUSER.DAT` (and its `UsrClass.dat`), plus a per-user first-logon hook.

### 3.2 Techniques and their constraints

**A. Live hive of a logged-on user** — `HKU\<SID>` (and `HKU\<SID>_Classes` for `HKCU\Software\Classes`, which is a *separate hive*, `UsrClass.dat`) is already loaded; write there. Fire `SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, …)` / `SHChangeNotify(SHCNE_ASSOCCHANGED)` afterwards; the elevated process runs in the same session, so broadcasts reach that user's windows (UIPI blocks low→high, not high→low).

**B. Offline hive of a logged-off user** — `RegLoadKey(HKEY_USERS, "OTW_<SID>", path)` requires **`SeBackupPrivilege` and `SeRestorePrivilege` enabled** on the token (`AdjustTokenPrivileges`; both are present-but-disabled in an elevated admin token). Then write under `HKU\OTW_<SID>\...`, `RegFlushKey`, dispose *every* `RegistryKey` handle (an open handle makes `RegUnLoadKey` fail with `ERROR_ACCESS_DENIED`), then `RegUnLoadKey`. If the hive is in use, `RegLoadKey` fails with a sharing violation — treat that as "user is logged on or profile loaded by a service; queue instead". Additional guards: refuse if the profile is roaming (`ProfileList\<SID>\CentralProfile` set) or the path is on a VHD/FSLogix mount, if `Sid` is a temporary profile (`State` flag 0x800 — **UNVERIFIED** flag meaning; verify), or during OOBE. Also load `UsrClass.dat` the same way (`HKU\OTW_<SID>_Classes`) for `Software\Classes` tweaks (e.g. classic context menu CLSID `{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32`).

**C. Default profile** — same as B against `C:\Users\Default\NTUSER.DAT` (only affects users created *after* the write; Windows copies Default → new profile at first logon).

**D. Active Setup** — `HKLM\SOFTWARE\Microsoft\Active Setup\Installed Components\<GUID-or-name>` with `StubPath` (command), `Version` (`"1,0,0,0"` — four comma-separated numbers), optional `IsInstalled=1`, default value = display name. At logon, before the desktop/Run keys, Windows compares against `HKCU\SOFTWARE\Microsoft\Active Setup\Installed Components\<same>`; runs `StubPath` synchronously **in the user's context** if missing or `Version` is lower, then copies the version to HKCU. Increase `Version` to re-run. Caveats: blocking (no timeout — a hung stub blocks logon), requires explorer as shell (published apps/RDS may skip it), 32-bit processes see `Wow6432Node`. This is *the* supported once-per-user hook and is what OTW should use for **queued per-user applies** and for **new users**: `StubPath = "C:\Program Files\OpenTheWindows\otw.exe" apply --user-scope --queued --quiet` (fast, non-interactive, exits 0 always).

**E. `RunOnce` (per user)** — `HKU\<SID>\Software\Microsoft\Windows\CurrentVersion\RunOnce` runs after shell start, once, is deleted before it runs (so a failure is lost) and is skipped in Safe Mode unless prefixed `*`. Fine as a secondary "refresh explorer" trigger, not as the primary mechanism.

**F. Logon scheduled task in user context** — Task with trigger *At log on (any user)*, principal **`GroupId = BUILTIN\Users`, `LogonType = Group`, `RunLevel = LUA`** (PowerShell: `New-ScheduledTaskPrincipal -GroupId "BUILTIN\Users" -RunLevel Limited`) runs `otw.exe apply --user-scope` as *whoever* logs on. Advantages over Active Setup: asynchronous, has a timeout, logs to Task Scheduler. Disadvantage: some hardening profiles disable Task Scheduler-dependent things; and it runs at *every* logon (make it idempotent and quick, ≤ 2 s scan).

**G. Per-user policies via machine scope** — many "user" policies have a machine (`HKLM\SOFTWARE\Policies\...`) twin that wins regardless of user; when a catalogue entry has both, prefer the machine policy for `all-users` scope. (Beware: it also applies to admins/service accounts.)

### 3.3 Recommended approach

1. `scope: user` → technique **A** against the *resolved interactive user SID* (never `HKEY_CURRENT_USER`, see §9).
2. `scope: all-users` → A for the interactive user; **B** for every profile whose hive is *not loaded* (probe `HKU\<SID>` existence first; then attempt `RegLoadKey`, and on sharing violation fall through); **C** for Default; and register **D** (Active Setup, bump `Version` on every profile change) so loaded-but-not-interactive users (fast-user-switching, RDP) and future users converge at next logon. Record every hive touched in the journal with its `SID`, `path`, `technique`.
3. `explorer-restart` requirement is only satisfied for the interactive user; others get it at logon.
4. Never during **OOBE** (`OOBEComplete()` returns FALSE), never in **Windows Sandbox / WDAG** (`WDAGUtilityAccount`), never for roaming/mandatory/FSLogix profiles; warn on **non-persistent VDI** (`dsregcmd` "Virtual Desktop: YES" metadata) that per-profile changes will not persist.

---

## 4. Transactional apply and rollback

### 4.1 Registry transactions (KTM) — do not build on them

* `RegCreateKeyTransacted` / `RegOpenKeyTransacted` / `RegDeleteKeyTransacted` (advapi32; need a `CreateTransaction` handle) still exist and their reference pages carry **no deprecation banner** (checked 2026-08). But: TxF (the file half of KTM) is officially flagged *"may not be available in future versions of Microsoft Windows"* with an *Alternatives* page; PowerShell 6+ removed transacted registry provider support; only registry ops on the *transacted handle* are transacted (subkeys/other handles are not; a non-transacted op on the key rolls the transaction back); and none of OTW's other actions (services, tasks, AppX, DISM, powercfg, firewall, LSA) are transactional anyway. Google Project Zero also documented non-atomic outcomes in KTM registry transactions. **Conclusion:** no benefit for a multi-subsystem apply; use a journal.

### 4.2 Journal-first design (pragmatic transactionality)

```
%ProgramData%\OpenTheWindows\
  journal\2026-08-16T12-03-11Z-3f9c.json      # one run = one file, append-only entries
  journal\latest.json                          # pointer + hash of last file
  backups\2026-08-16T12-03-11Z-3f9c\*.reg      # human-readable exports of touched keys
  backups\...\Registry.pol.machine.before      # copies of LGPO files before merge
  state\applied.json                           # current desired-state (profile id/version/hash)
```

Per-run file: header `{schema, runId, appVersion, catalogVersion, profileId/profileHash, os {build, ubr, displayVersion, edition, arch}, machine {name, sid, entraDeviceId?}, actor {tokenUser, sessionUser, isSystem}, dryRun}` then a list of **entries** written *before* each mutation:

```json
{ "seq": 12, "ts": "…", "tweakId": "telemetry.allow-telemetry", "action": "policy",
  "target": { "hive": "HKLM", "key": "SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection", "value": "AllowTelemetry", "scope": "machine", "sid": null },
  "prior": { "present": true, "type": "REG_DWORD", "data": 3 },
  "desired": { "type": "REG_DWORD", "data": 0 },
  "policyFile": { "path": "…\\Machine\\Registry.pol", "priorEntry": null },
  "result": "applied|failed|skipped|rolledBack", "error": null,
  "prevHash": "sha256…", "hash": "sha256(prevHash + canonical(entry-without-hash))" }
```

Rules:

* **Snapshot before mutate; write the entry to disk (flush) before mutating.** Prior state records `present:false` distinctly from a default value — reverting must be able to *delete* what OTW created.
* Service entries store `startType`, `delayedAutoStart`, `status`, `triggers?`; task entries store `enabled` (+ XML export for safety); AppX entries store `packageFullName`, `isProvisioned`, `installedForSids`; feature entries store `state`; powercfg stores scheme GUID + AC/DC index; firewall stores rule XML/`MSFT_NetFirewallRule` properties; LSA/audit store the queried prior policy.
* **Apply order:** topologically sort by `dependsOn`, then by action type (policy/registry first, then services, tasks, appx, features, reboot-required last). **Verify** by re-reading (`detect`) after each action; on failure of a `required` action, **roll back in reverse** the entries of that tweak (or the whole run, per `--atomic`), marking each `rolledBack`.
* Two revert modes: `revert --run <id>` (exact prior state from journal) and `revert --tweak <id> --to-default` (catalogue's `revert` recipe / Windows default). Journals are never deleted by OTW; rotation is by count/age with a floor of the last N runs.
* **Tamper-evidence:** hash chain across entries and across files (`latest.json`); ACL journal folder to `SYSTEM:F, Administrators:F, Users:R`. Not tamper-*proof* (an admin can rewrite) — that is what the Event Log copy is for.
* **Human-readable backup:** for each touched key, export via **`RegSaveKeyEx`** (binary hive; needs `SeBackupPrivilege`; use `REG_LATEST_FORMAT`) *and/or* `reg.exe export <key> <file> /y` (text `.reg`, UTF-16LE, "Windows Registry Editor Version 5.00"). `.reg` is what a helpdesk can double-click; `RegSaveKey` is exact. Both are subtree-only — that is enough per tweak. For a whole-hive safety net, VSS-snapshot the system volume (`Win32_ShadowCopy.Create` / `IVssBackupComponents`) — the registry docs explicitly say backup/restore of hives should use VSS.

### 4.3 System Restore — useful, but set expectations

* Two APIs, same engine: `SRSetRestorePoint(RESTOREPOINTINFO{dwEventType=BEGIN_SYSTEM_CHANGE, dwRestorePointType=MODIFY_SETTINGS(12), szDescription}, out STATEMGRSTATUS)` from **srclient.dll** (load with `LoadLibrary`/`GetProcAddress`, never link statically — per docs), then `END_SYSTEM_CHANGE` with the returned `llSequenceNumber`; or WMI `root\default:SystemRestore.CreateRestorePoint(Description, RestorePointType, EventType)` (types: `MODIFY_SETTINGS=12`, `BEGIN_SYSTEM_CHANGE=100`, `END_SYSTEM_CHANGE=101`; nested variants 102/103). PowerShell's `Checkpoint-Computer` wraps the same.
* **24-hour throttle:** *"If the key does not exist (default) and any restore points have been created in the last 24 hours, Windows skips creating this new restore point"* — and **returns success** (`nStatus = ERROR_SUCCESS`, `llSequenceNumber` = the earlier point). Override: `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore\SystemRestorePointCreationFrequency` (DWORD minutes; `0` = never skip). OTW must (a) *verify* creation by enumerating `SystemRestore` instances (`SequenceNumber`, `CreationTime`) before/after, and (b) tell the user when a point was *not* created; optionally set the frequency key to 0 temporarily (record + restore it in the journal).
* **System Protection is frequently OFF** on Windows 11 installs; enabling requires `SystemRestore.Enable("C:\")` (root\default) or `Enable-ComputerRestore -Drive "C:\"`, plus shadow storage (`vssadmin resize shadowstorage /for=C: /on=C: /maxsize=5%` or `Win32_ShadowStorage`). Restore point creation can also be blocked by policy (`HKLM\SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore\DisableSR`), by low disk, or on VMs with tiny disks. Never *require* SR; offer it.
* **Scope reality:** SR restores registry hives, system files, drivers, installed programs snapshot — **not user documents** and not per-user AppX state consistently; it also does not undo *policy* refreshes coming from a domain. Wording in the UI: "Restore point = coarse safety net for the OS; OTW's journal is the precise undo."
* Last resort docs to link: *Reset this PC* (keep files) and WinRE (Startup Repair / System Restore / Command Prompt with `reg load` for offline hive edits). OTW should ship a **"recovery card"** (`otw recover --print`) that lists exactly which offline steps undo its Breaking-tier changes.

---

## 5. Re-apply and drift

### 5.1 What feature updates undo

Practitioner reality (see `02-*` §5): feature updates (23H2 → 24H2 → 25H2, and "enablement package" updates) **re-provision** removed in-box apps (Outlook (new), Copilot, Teams, Dev Home, Widgets), **re-enable** in-box scheduled tasks and reset service start types for services Setup owns, reset some Explorer/shell state (pins, icon-promotion list), can drop custom scheduled tasks (reported on 10→11 upgrades), and re-register AppX packages. Values under `HKLM\SOFTWARE\Policies` are generally *migrated* (policy trees are in Setup's migration set) — but "generally" is not "always"; the only robust answer is **detect + re-apply**.

Mitigations worth building in:

* **Deprovisioned marker:** when removing a provisioned AppX package, also create `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Deprovisioned\<PackageFamilyName>` — Windows honours it to *not* re-provision on upgrade (widely used by Win11Debloat/Sophia; **UNVERIFIED** in official docs).
* **Upgrade detection:** persist `{CurrentBuild, UBR, DisplayVersion, EditionID}` (from `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion`) in `state\applied.json`; on every scheduled run compare and, if `CurrentBuild` changed, run a full `remediate` and raise Event 2100 "OS upgraded from X to Y; re-applied profile". (`HKLM\SYSTEM\Setup\Source OS (Updated on …)` subkeys are another tell.) Do **not** rely on a specific Setup event ID for "upgrade complete" — **UNVERIFIED**; the build compare is deterministic.
* **`SetupConfig.ini` PostOOBE hook:** for WU-delivered feature updates, `%SystemDrive%\Users\Default\AppData\Local\Microsoft\Windows\WSUS\SetupConfig.ini` with `[SetupConfig]` `PostOOBE=<path to cmd>` (and `PostRollback=`) makes Setup run a script after the upgrade completes; enterprises already use it (4sysops, MSEndpointMgr). Optional OTW feature: `otw install-upgrade-hook` writes a `PostOOBE` script that runs `otw remediate --quiet` (guarded: keep other keys in the INI intact).

### 5.2 Scheduled re-apply

Task `\OpenTheWindows\Remediate` (SYSTEM, highest privileges, hidden, `StartWhenAvailable`, 10-minute execution limit): triggers *At startup* (+ 2 min delay), *At log on of any user* (per-user part runs through a second task with the `BUILTIN\Users` principal, see §3.2 F), and *Weekly*. An event-based trigger for "upgrade completed" is not needed and no stable Setup event ID for it was confirmed (**UNVERIFIED**) — the startup run does the build/UBR compare (§5.1) and escalates to a full re-apply when the build changed. The task must honour the same pre-flight rails as the CLI, in particular refusing to run while a Windows Update install is in progress (§7).

### 5.3 CLI contract (drift + Intune Remediations)

* `otw scan [--profile <id|path>] [--json] [--user-scope]` → **exit 0** all compliant/not-applicable, **1** at least one drift, **2** error (pre-flight failed, catalogue invalid, not elevated when required). stdout: single JSON document (`{summary:{compliant,drift,notApplicable,error}, items:[{id, desired, actual, state, managedBy, requires}]}`); stderr: human text. Keep a `--brief` mode ≤ 2 KB because **Intune caps script output at 2,048 characters**.
* `otw remediate [--profile …] [--json] [--only <ids>]` → idempotent: applies only items in `drift`; **exit 0** all applied, **2** error, **3010** applied but reboot required (Win32-app convention; document it). Never reboots by itself (Intune: *"Don't put reboot commands in detection or remediations scripts"*).
* **Intune Remediations semantics** (learn.microsoft.com): a package = detection script + optional remediation script; *"A remediation script only runs if the detection script uses exit code `exit 1`"* — any other exit code (including 0 and empty output) = issue not found; scripts run as SYSTEM unless "Run this script using the logged-on credentials"; 64-bit PowerShell optional; UTF-8 without BOM if signature check is enforced; recur every 24 h by default (or hourly/daily/once schedules); requires Entra-joined/hybrid + Intune-enrolled Pro/Enterprise/Education (or co-managed) and Windows E3/E5/A3/A5/VDA licensing. Ship `Detect-OTW.ps1` (runs `otw scan --brief`; exits 1 on scan exit 1, else 0; prints the summary line) and `Remediate-OTW.ps1` (runs `otw remediate --brief`; exits 0 unless otw returned 2). Same wrappers work for ConfigMgr Configuration Items and other RMMs.

---

## 6. Audit and logging

### 6.1 Windows Event Log

* Use the **`System.Diagnostics.EventLog`** NuGet package (Windows-only, .NET 6+). Create the log/source once, elevated: `EventLog.CreateEventSource(new EventSourceCreationData("OTW", "OpenTheWindows"))`. Docs constraints: *"you must have administrative privileges"*; source names must be unique on the machine and must not equal an existing log name; **the first 8 characters of a new log name must differ from every existing log's first 8** (`OpenTheW` — check no clash); the log file is created on first write; *"An event log source should not be created and immediately used… There is a latency time"* — create at install / first elevated run, and if the source was ever remapped to another log **a computer restart is required**. There is no need to restart the EventLog service for a brand-new source, but be tolerant: on `Write` failure fall back to the JSONL log and retry the source next run.
* Message file: the .NET package registers its own generic message DLL (`System.Diagnostics.EventLog.Messages.dll`) so Event Viewer renders the text without "The description for Event ID … cannot be found" (**UNVERIFIED** for every TFM — validate on the CI VM).
* **Structured IDs (proposal):** 1000 apply started (profile id/hash, dry-run flag) · 1001 tweak applied · 1002 tweak failed · 1003 tweak skipped (managed/not-applicable) · 1004 apply finished (counts) · 1010 reboot required · 2000 scan started · 2001 drift detected (per tweak) · 2002 scan finished (summary) · 2100 OS build changed, re-apply · 3000 rollback started · 3001 tweak rolled back · 3002 rollback failed · 4000 pre-flight failed (reason) · 4010 break-glass override of managed setting · 4020 restore point created / 4021 skipped · 5000 profile imported/verified (key id) · 5001 profile signature invalid · 9000 unhandled error. Use `EventLogEntryType` Information/Warning/Error accordingly and put the JSON payload in the message body (≤ 31 KB).

### 6.2 ETW / EventSource

* `System.Diagnostics.Tracing.EventSource` (`[EventSource(Name="OpenTheWindows-Core")]`) gives self-describing ETW events with **no registration** (capturable with `logman`, WPR, PerfView, `dotnet-trace`) — use it for verbose diagnostics (each API call, timings, hive load/unload). **Windows Event Log *channels* (Admin/Operational) via EventSource still require a static manifest registered with `wevtutil im` and the older `Microsoft.Diagnostics.Tracing.EventSource` NuGet build step** — extra installer complexity for little gain when we already have the classic log; skip channels.
* Recommendation: **EventSource for tracing + classic Event Log for admin-facing events + JSONL for evidence.** Add `otw diag collect` that bundles the JSONL, the last journals, `dsregcmd /status`, `gpresult /x`, and an ETW trace into a zip.

### 6.3 JSONL audit log & compliance report

* `%ProgramData%\OpenTheWindows\logs\audit-YYYYMMDD.jsonl`, one JSON object per line, size/day-based rotation, retention configurable (default 90 days), same ACL as the journal.
* **Compliance report** (`otw report --json|--html`): `reportVersion`, `generatedAt`, `appVersion`, `catalogVersion`, `profile {id, version, sha256, signer?}`, `machine {name, domain/tenant, entraDeviceId?, mdmEnrolled, isVm}`, `os {build, ubr, displayVersion, edition, arch}`, `items[] {id, category, level, desired, actual, compliant, managedBy, lastAppliedRunId}`, `summary`, `contentHash` (SHA-256 of the canonical JSON without the hash field). An HMAC adds nothing without a shared secret; a **signature with the org's private key is meaningful only if signing happens off-box** (e.g. the collector signs on receipt). On-box, `contentHash` + Event Log correlation is the honest claim.

---

## 7. Safety rails

### 7.1 Pre-flight checks (all must pass before *apply*; `scan` runs with a subset)

| Check | Implementation | On failure |
|---|---|---|
| Elevated | `GetTokenInformation(TokenElevation)` / `WindowsPrincipal.IsInRole(Administrator)` | exit 5 "needs elevation" |
| Windows 11 client, supported build | `RtlGetVersion` (not `Environment.OSVersion` unless manifested) — `MajorVersion=10, BuildNumber ≥ 22631`; `wProductType == VER_NT_WORKSTATION` (or `Win32_OperatingSystem.ProductType == 1`) | refuse on Server SKU; warn on unknown newer build (`--allow-unknown-build`) |
| Architecture | `RuntimeInformation.OSArchitecture` (x64 / Arm64) | Windows 11 has no 32-bit SKU: **ship x64 + ARM64 builds** (Copilot+ PCs are ARM64); do not ship x86; native interop (DISM, WinRT) is arch-native |
| Edition | `HKLM\…\CurrentVersion\EditionID` / `GetProductInfo` | drives `appliesTo.editions`; Home ⇒ policy path degrade (§1.7) |
| OOBE complete | `OOBEComplete(out bool)` (kernel32) | refuse (Autopilot/OOBE phase) unless `--during-oobe` (for provisioning packages) |
| Safe Mode | `GetSystemMetrics(SM_CLEANBOOT) != 0` | refuse (services/AppX APIs unreliable) |
| Windows Sandbox / WDAG | user == `WDAGUtilityAccount`, `CExecSvc` present | allow (nothing persists) but warn |
| VM | `Win32_ComputerSystem.HypervisorPresent` / model | informational |
| Pending reboot | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending`, `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired`, `HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\PendingFileRenameOperations`, plus WUA COM `Microsoft.Update.SystemInfo` → `ISystemInformation.RebootRequired` | warn; refuse for `feature`/`appx` actions (`--ignore-pending-reboot` override) |
| Windows Update installing | WUA COM `Microsoft.Update.Installer` → `IUpdateInstaller.IsBusy`; heuristics: `TiWorker.exe`/`TrustedInstaller` CPU-active, `wuauserv` state | refuse/pause; retry later |
| Another OTW instance | `Mutex("Global\\OpenTheWindows.Apply")` | refuse |
| Disk space | `GetDiskFreeSpaceEx` on system drive ≥ 2 GB | warn; disable restore point |
| AC power (laptops) | `GetSystemPowerStatus().ACLineStatus` | warn for long runs (feature/appx) |
| Managed device | §2 detection | switch to "managed mode" (respect owners) |

### 7.2 Dry-run, explain, confirmations

* `--what-if` computes the diff (desired vs actual per action, including hive targets and Registry.pol merges) with **zero writes** — implement by running the same planner with a `NoOpExecutor`; the GUI's "Review changes" page is the same object.
* `otw explain <tweakId>` prints description, rationale, sources, risk, exact actions (key/value/data), revert recipe, requires (reboot/logoff/explorer), managed-by resolution and verified-on builds.
* **Breaking-tier** tweaks require typed confirmation of the tweak id (`--confirm-breaking <id>` in CLI, typed text in GUI) and are excluded from `remediate` unless the profile pins them explicitly.

### 7.3 Restart requirements and a safe Explorer restart

* Each catalogue entry declares `requires: none | explorer | logoff | reboot`; the run aggregates and reports (exit 3010). Never auto-reboot; offer `shutdown /r /t 60` via a button.
* **Explorer restart, correctly:** (1) enumerate `explorer.exe` processes **in the target user's session only** (`Process.SessionId`), (2) ask them to exit gracefully — there is no documented API; tools post the undocumented "Exit Explorer" message to `Shell_TrayWnd` (**UNVERIFIED/undocumented**, may change) — so in practice `Process.Kill()` after `CloseMainWindow()` fails; (3) wait ~2 s: Winlogon's `AutoRestartShell=1` (`HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon`) usually respawns the shell for the interactive user by itself; (4) only if no `explorer.exe` reappears in that session, start it — **from an elevated process this is subtle**: Explorer refuses to run elevated as the shell and re-launches itself unelevated through the on-demand scheduled task **`CreateExplorerShellUnelevatedTask`** (Raymond Chen: "Explorer needs to run non-elevated because it needs to be available to other applications which aren't necessarily elevated"), which **depends on the Task Scheduler service** — a service some hardening profiles break — and `explorer.exe /nouaccheck` bypasses that and leaves you with an *elevated shell* (security regression). Robust path: obtain the interactive user's **unelevated primary token** (from SYSTEM: `WTSQueryUserToken`; from an admin: duplicate the token of an existing user process such as `sihost.exe`/`RuntimeBroker.exe` in that session, `DuplicateTokenEx(TOKEN_ALL_ACCESS, SecurityImpersonation, TokenPrimary)`) and `CreateProcessAsUserW`/`CreateProcessWithTokenW(explorer.exe)`; fall back to `Process.Start("explorer.exe")` and let Windows bounce it. Report the 23H2/24H2 nuance in docs: shell restarts on 24H2 have been reported to fail intermittently in the wild (taskbar not returning); OTW should verify by polling for a `Shell_TrayWnd` window and offer "sign out" as the safe alternative.
* **Service stop timeouts:** `ServiceController.Stop()` + `WaitForStatus(Stopped, TimeSpan.FromSeconds(30))`; never `taskkill` a service host; if a dependent service blocks, list it and let the user decide.
* **Protected / never-touch services:** detect with `QueryServiceConfig2(SERVICE_CONFIG_LAUNCH_PROTECTED)` → `SERVICE_LAUNCH_PROTECTED_INFO.dwLaunchProtected` (`0 NONE, 1 WINDOWS, 2 WINDOWS_LIGHT, 3 ANTIMALWARE_LIGHT`) — `sc qprotection <svc>` shows the same. Refuse `service` actions on any protected service (WinDefend = ANTIMALWARE LIGHT, Sense, MsSecFlt, WdNisSvc, sgrmbroker…). Maintain a static deny-list too: `RpcSs, RpcEptMapper, DcomLaunch, LSM, EventLog, Schedule, ProfSvc, UserManager, Winmgmt, CryptSvc, TrustedInstaller, gpsvc, wscsvc, SecurityHealthService, BFE, MpsSvc, WinDefend, Sense, wuauserv (policy-only), UsoSvc, WaaSMedicSvc, BITS, DoSvc (policy-only), Power, PlugPlay, SamSs, Netlogon (domain), dmwappushservice (MDM), IntuneManagementExtension`. Service ACL/ownership hacks are out of scope by design (see `02-*` §5).

---

## 8. Configuration profiles and the data-driven catalogue

### 8.1 Catalogue entry schema (data, not code)

```jsonc
{
  "$schema": "https://openthewindows.dev/schemas/catalog-entry.v1.json",
  "id": "privacy.telemetry.allow-telemetry",          // stable, dotted, lowercase
  "title": "Diagnostic data: Security/Off (Enterprise) or Required (Pro/Home)",
  "description": "...", "rationale": "...",
  "category": "privacy", "level": "balanced",          // Off/Basic/Balanced/Strict/Paranoid (see 02-*)
  "risk": "safe",                                       // safe|caution|advanced|breaking (+ "irreversible": false)
  "sources": [{ "title": "Policy CSP: System/AllowTelemetry", "url": "https://learn.microsoft.com/…", "accessed": "2026-08-16" }],
  "appliesTo": { "editions": ["Pro","Enterprise","Education","Home"], "minBuild": 22631, "maxBuild": null, "arch": ["x64","arm64"], "effectOnHome": "partial" },
  "scope": "machine",                                   // machine|user|all-users
  "managedBy": { "csp": "System/AllowTelemetry", "admx": "DataCollection.admx:AllowTelemetry", "gpPath": "Computer Configuration/…" },
  "actions": [
    { "type": "policy", "hive": "HKLM", "key": "SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection",
      "value": "AllowTelemetry", "regType": "REG_DWORD", "data": 0, "notConfigured": "delete" }
  ],
  "detect": [ { "type": "registry", "hive": "HKLM", "key": "…", "value": "AllowTelemetry", "expect": { "equals": 0 } } ],
  "revert": "derived",                                  // "derived" = journal/prior; or explicit action list
  "requires": "none",                                    // none|explorer|logoff|reboot
  "dependsOn": [], "conflictsWith": ["privacy.telemetry.diagtrack-service-disable"],
  "tests": { "verifiedOn": ["Pro 24H2 26100.4349", "Home 24H2 26100.4349"], "lastVerified": "2026-08-01" }
}
```

Action types are **code-backed executors** with a common interface (`Plan(ctx) → IReadOnlyList<Op>`, `Snapshot`, `Apply`, `Verify`, `Revert`): `registry`, `policy` (§1), `service`, `task`, `appx`, `feature`, `capability`, `powercfg`, `defender-preference`, `firewall`, `secedit`, `auditpol`, `file` (hosts is intentionally *not* provided; see `02-*`), `script` (PowerShell, gated behind `allowScripts` and signature). Every executor must be **idempotent** and must implement `Detect` without side effects. The catalogue is validated in CI against the JSON schema and against a "no raw script for anything an executor can do" lint.

### 8.2 Profile schema

```jsonc
{ "$schema": "…/profile.v1.json", "id": "contoso.workstation", "name": "Contoso Workstation", "version": "2026.08.1",
  "catalogMinVersion": "1.4.0",
  "appliesTo": { "editions": ["Pro","Enterprise"], "minBuild": 22631 },
  "levels": { "privacy": "strict", "hardening": "balanced", "debloat": "basic", "ui": "off" },
  "overrides": { "privacy.telemetry.allow-telemetry": { "enabled": true, "data": 1 }, "debloat.appx.remove-outlook-new": { "enabled": false } },
  "customTweaks": [ /* inline catalogue entries, same schema, ids prefixed "custom." */ ],
  "options": { "createRestorePoint": true, "allUsers": true, "breakGlassAllowed": false, "requireSigned": true },
  "signature": null }
```

* **Versioning/migration:** `schemaVersion` inside; loader supports N-1 with in-memory migration; export always writes the current schema; unknown fields are preserved on round-trip.
* **Import/export:** `otw profile export --id … --out file.json`, `otw profile import file.json [--verify-with key.pub]`; GUI drag-and-drop with a diff view against the current profile.
* **Signing:** detached `file.json.sig` = `{ "alg": "ES256", "keyId": "<sha256 of SPKI>", "signedAt": "...", "hash": "sha256:<canonical json>", "sig": "<base64 DER>" }`. Use .NET's built-in **`ECDsa` P-256 / SHA-256** (no dependency; keys via `ECDsa.Create()`, `ExportSubjectPublicKeyInfo`/`ImportFromPem`). Ed25519 is *not* a first-class standalone class in .NET 10 (`System.Security.Cryptography.Ed25519` doc page returns 404; only composite ML-DSA-with-Ed25519 exists) — offer `EdDSA` only via a library (NSec/BouncyCastle) if minisign compatibility is requested. Trust store: `%ProgramData%\OpenTheWindows\trust\*.pem` (Administrators-writable only), listed in the GUI with fingerprints. Enforcement: `HKLM\SOFTWARE\Policies\OpenTheWindows\RequireSignedProfiles=1` (OTW ships its **own ADMX** so orgs can push it by GPO/Intune) — unsigned or unverifiable profiles are then refused with Event 5001. Canonicalise JSON before hashing (RFC 8785-style key ordering, no whitespace, UTF-8) so signatures survive pretty-printing.

---

## 9. Elevation model and the "wrong HKCU" bug

### 9.1 Process model

* **GUI:** single process, manifest `requestedExecutionLevel level="requireAdministrator"`. Standard for this class of tool (Winaero, O&O, Sophia, winutil all elevate whole-process); a split broker (COM elevation moniker or a SYSTEM service) is more secure in theory but adds an installer, an IPC surface and its own attack surface — defer to a later milestone; design the Core so the executor layer can be hosted in a broker later.
* **CLI:** `asInvoker`; `scan`/`explain`/`report` run unelevated (read HKLM, services, tasks); mutating verbs check elevation and exit 5 with a clear message. Optional `--elevate` relaunches via `ShellExecuteEx(runas)` and streams output back through a named pipe.
* **Scheduled remediation** runs as **SYSTEM** — the only context in which `WTSQueryUserToken` and the MDM WMI Bridge work; per-user parts run through the `BUILTIN\Users` logon task (§3.2 F) or by impersonating the console user's token.

### 9.2 The classic bug and the fix

*Symptom:* user `alice` (standard user) runs the tool; UAC prompts and `bob` (admin) authenticates. The elevated process's token is **bob's**; `HKEY_CURRENT_USER` maps to **`HKU\<bob SID>`**; every "current user" tweak lands in bob's profile — bob is not even logged on interactively, so alice sees nothing and bob gets surprised later. Same for `%APPDATA%`, `SHGetKnownFolderPath`, and `Environment.GetFolderPath`. Even with same-user elevation, `HKCU\Software\Classes` in an elevated process ignores per-user COM registrations (elevated processes only see `HKLM\Software\Classes` for COM activation — MS "Elevated Permissions and the Vista SP1 Registry").

*Fix (deterministic):*

1. **Determine the target user** = the user who owns the *session*, not the token: `WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, WTS_CURRENT_SESSION, WTSUserName / WTSDomainName)` (works for an elevated admin; no SYSTEM needed), `LookupAccountName` → SID. Cross-check with the owner of `explorer.exe`/`sihost.exe` in `ProcessIdToSessionId(GetCurrentProcessId())`. When running as SYSTEM (scheduled task): `WTSGetActiveConsoleSessionId()` / `WTSEnumerateSessions` → active session → `WTSQueryUserToken` (docs: *"the calling application must be running within the context of the LocalSystem account and have the SE_TCB_NAME privilege"*) → `GetTokenInformation(TokenUser)`.
2. **Compare** with the process token user; if different, show a banner: "You elevated as *CONTOSO\bob*; per-user settings will be applied to the signed-in user *CONTOSO\alice*."
3. **Write to `HKEY_USERS\<targetSID>`** and **`HKEY_USERS\<targetSID>_Classes`** — both are loaded while the user is logged on. Never open `Registry.CurrentUser` in the Core; add an analyzer/lint that forbids `Registry.CurrentUser`, `HKEY_CURRENT_USER` and `Environment.SpecialFolder.ApplicationData` in `Core/`.
4. **Per-user file paths** (e.g. `%LOCALAPPDATA%` for UsrClass or start-layout files): `SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, hUserToken)` with the *user's* token (from step 1; for an admin process, duplicate a token from a user-session process with `PROCESS_QUERY_LIMITED_INFORMATION` + `TOKEN_DUPLICATE|TOKEN_QUERY`), or `ProfileList\<SID>\ProfileImagePath`.
5. **User-context helper processes** (explorer restart, `RunOnce`, notifications): launch with the *user's* unelevated token (`CreateProcessAsUserW` from SYSTEM; `CreateProcessWithTokenW` from an admin — requires `SeImpersonatePrivilege`, which admins hold), never with the elevated token.
6. **Fast user switching / RDP:** if more than one interactive session exists, `scope: user` targets the session that launched the GUI; `scope: all-users` covers the rest via §3.

---

## 10. Native APIs instead of `powershell.exe`, and diagnostics

### 10.1 Capability → preferred native API → fallback

| Capability | Preferred native API (from C#) | Fallback | Namespace / package / notes |
|---|---|---|---|
| Registry (live) | `Microsoft.Win32.Registry` + P/Invoke `RegLoadKey/RegUnLoadKey/RegSaveKeyEx`, `RegOpenUserClassesRoot` | `reg.exe` | Enable `SeBackup/SeRestore` via `AdjustTokenPrivileges` (Vanara.PInvoke or hand-written) |
| Local GPO policy | `IGroupPolicyObject` COM (gpedit.dll) + own PReg writer; `RefreshPolicyEx` (userenv.dll) | LGPO.exe if present; raw registry (Home) | §1 |
| RSOP / effective policy | WMI `root\rsop\computer`, `root\rsop\user\<SID>` (`RSOP_RegistryPolicySetting`) | `gpresult /x` | `Microsoft.Management.Infrastructure` (MMI) — prefer over `System.Management` |
| MDM state | registry `PolicyManager`, `Enrollments`; `dsregcmd /status` (parse) | WMI Bridge `root\cimv2\mdm\dmmap` (SYSTEM only) | §2 |
| Domain join | `NetGetJoinInformation` (netapi32) | `Win32_ComputerSystem` | — |
| Services | `ServiceController` + `QueryServiceConfig2/ChangeServiceConfig2` P/Invoke (`SERVICE_CONFIG_DELAYED_AUTO_START_INFO`, `SERVICE_CONFIG_LAUNCH_PROTECTED`, `SERVICE_CONFIG_TRIGGER_INFO`) | `sc.exe` | `System.ServiceProcess.ServiceController` package |
| Scheduled tasks | Task Scheduler 2.0 COM (`Schedule.Service` / `ITaskService`) — via **TaskScheduler NuGet (dahall, MIT)** or COM interop | `schtasks.exe` | Enable/disable, export XML for journal, register `\OpenTheWindows\*` tasks |
| AppX / provisioned apps | WinRT `Windows.Management.Deployment.PackageManager` (`FindPackagesForUser`, `RemovePackageAsync(…, RemovalOptions.RemoveForAllUsers)`, `DeprovisionPackageForAllUsersAsync`) via CsWinRT (`Microsoft.Windows.SDK.NET.Ref`, TFM `net8.0-windows10.0.19041+`) | `Get-AppxPackage`/`Remove-AppxProvisionedPackage` in `powershell.exe` | Provisioned enumeration also via DISM API `DismGetProvisionedAppxPackages` (Microsoft.Dism) |
| Optional features / capabilities | DISM API (`dismapi.dll`) via **Microsoft.Dism (jeffkl/ManagedDism, MIT, actively updated 2024-12)**: `DismApi.Initialize`, `OpenOnlineSession`, `GetFeatures`, `EnableFeature`/`DisableFeature`, `AddCapability`/`RemoveCapability` | `Get/Enable/Disable-WindowsOptionalFeature`, `dism.exe` | DISM API is arch-native (x64/ARM64), requires elevation; long ops — run on a worker with progress callback |
| Defender preferences | WMI `root\Microsoft\Windows\Defender` `MSFT_MpPreference` (Get + `Set` static method with the same parameter names as `Set-MpPreference`); `MSFT_MpComputerStatus` for state | `Set-MpPreference` | Tamper Protection blocks many settings (returns error) — detect via `MSFT_MpComputerStatus.IsTamperProtected` and mark managed |
| Firewall | COM `HNetCfg.FwPolicy2` (`INetFwPolicy2`, `INetFwRules`) or WMI `root\StandardCimv2` `MSFT_NetFirewallRule` / `MSFT_NetFirewallProfile` | `netsh advfirewall`, `New-NetFirewallRule` | Prefer WMI (matches PowerShell semantics; supports groups/profiles) |
| Password/lockout policy | `NetUserModalsSet` (netapi32, `USER_MODALS_INFO_0/3`) | `net accounts`, `secedit /configure` | — |
| User rights / LSA options | `LsaOpenPolicy`, `LsaAddAccountRights`/`LsaRemoveAccountRights`, `LsaEnumerateAccountRights` (advapi32) | `secedit /configure /cfg GptTmpl.inf` | Many "Security Options" are just registry values (`HKLM\SYSTEM\CurrentControlSet\Control\Lsa\…`) — use `registry`/`policy` actions for those |
| Audit policy | `AuditSetSystemPolicy` / `AuditQuerySystemPolicy` (advapi32; needs `SeSecurityPrivilege`) | `auditpol.exe /set` | Subcategory GUIDs from `AuditLookupCategoryGuidFromCategoryId` |
| Power | `PowerGetActiveScheme`, `PowerSetActiveScheme`, `PowerWriteACValueIndex`/`PowerWriteDCValueIndex`, `PowerReadACValueIndex` (powrprof.dll); hibernation on/off via `CallNtPowerInformation(SystemReserveHiberFile, &BOOLEAN)` | `powercfg.exe` (`/setacvalueindex`, `/hibernate off`) | Journal the active scheme GUID + prior AC/DC index per setting GUID; Modern Standby machines expose fewer settings |
| NTFS behaviours | registry `HKLM\SYSTEM\CurrentControlSet\Control\FileSystem\NtfsDisableLastAccessUpdate`, `NtfsDisable8dot3NameCreation` | `fsutil behavior set` | — |
| DNS over HTTPS | registry `HKLM\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters\DohWellKnownServers` + interface `DohFlags`; policy `…\Policies\Microsoft\Windows NT\DNSClient\DoHPolicy` | `netsh dns add encryption` / `Set-DnsClientDohServerAddress` | WMI `root\StandardCimv2` `MSFT_DNSClientDohServerAddress` also available |
| SMB | WMI `root\Microsoft\Windows\SMB` `MSFT_SmbServerConfiguration` (`Get`/`Set` methods), `MSFT_SmbClientConfiguration`; SMB1 via DISM feature `SMB1Protocol` | `Set-SmbServerConfiguration` | Some values are registry (`LanmanServer\Parameters`) |
| BitLocker | WMI `root\cimv2\Security\MicrosoftVolumeEncryption` `Win32_EncryptableVolume` (`GetProtectionStatus`, `GetEncryptionMethod`, `ProtectKeyWithTPM`, …) | `manage-bde`, `Get-BitLockerVolume` | Read-only in OTW by default (Breaking-tier to change) |
| TPM | WMI `root\cimv2\Security\MicrosoftTpm` `Win32_Tpm` (`IsEnabled`, `IsActivated`, `IsOwned`, `SpecVersion`); `Tbsi_GetDeviceInfo` (tbs.dll) | `Get-Tpm` | Read-only |
| Secure Boot | registry `HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\State\UEFISecureBootEnabled`; `GetFirmwareEnvironmentVariableEx("SecureBoot", EFI_GLOBAL_VARIABLE)` (needs `SeSystemEnvironmentPrivilege`) | `Confirm-SecureBootUEFI` | Read-only |
| Device Guard / VBS / HVCI | WMI `root\Microsoft\Windows\DeviceGuard` `Win32_DeviceGuard` (`SecurityServicesRunning`, `VirtualizationBasedSecurityStatus`); registry `HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity` | `msinfo32` | Read + policy only |
| Windows Update state | WUA COM `Microsoft.Update.Session/Installer/SystemInfo` (`IUpdateInstaller.IsBusy`, `ISystemInformation.RebootRequired`) | `usoclient`, `Get-WindowsUpdateLog` (do not) | Read-only |
| System Restore | `SRSetRestorePoint` (srclient.dll, dynamic load); WMI `root\default` `SystemRestore` (`CreateRestorePoint`, `Enable`, `Disable`, enumerate) | `Checkpoint-Computer` | §4.3 |
| Event Log | `System.Diagnostics.EventLog` (package); `EventSource` for ETW | `wevtutil` | §6 |
| Sessions/users | `WTSEnumerateSessions`, `WTSQuerySessionInformation`, `WTSQueryUserToken` (SYSTEM), `LookupAccountName` | `query user`, `whoami` | §9 |
| PowerShell (last resort) | **`Microsoft.PowerShell.SDK`** in-proc hosting (`PowerShell.Create()`; ships pwsh 7 runtime — heavy, ~100 MB) or `System.Management.Automation` from Windows PowerShell 5.1 (`powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand`) | — | Prefer spawning `powershell.exe` with `-EncodedCommand`, a timeout, and `ConvertTo-Json` output over embedding the SDK; use only for cmdlets without a practical native path (e.g. `Set-ProcessMitigation`) and record the exact command in the journal |

### 10.2 Windows Event IDs / diagnostics that show a tweak's effect

* **Group Policy:** `Microsoft-Windows-GroupPolicy/Operational` — 4016/5016 (CSE processing start/end, incl. the Registry CSE), 7016 (CSE processing failed — the ID seen in LGPO blog reports), 1500/1502 (System log: policy processed with/without changes); exact ID semantics beyond 7016 are **UNVERIFIED** here — confirm against the GP operational log on a test VM; `gpresult /h` for humans.
* **MDM:** `Microsoft-Windows-DeviceManagement-Enterprise-Diagnostics-Provider/Admin` — policy set/apply events (practitioners cite 813/814 for policy values; **UNVERIFIED** semantics), `MDM Diagnostic Report` (Settings → Access work or school → Advanced Diagnostic Report) lists blocked GP settings.
* **Services:** System log, source Service Control Manager — 7040 (start type changed), 7036 (state changed), 7045 (new service), 7000/7001 (start failures/dependency).
* **Scheduled tasks:** `Microsoft-Windows-TaskScheduler/Operational` (must be enabled) — 106 (registered), 140 (updated), 141 (deleted), 200/201 (action started/completed), 101 (launch failed).
* **Defender:** `Microsoft-Windows-Windows Defender/Operational` — 5001 (real-time protection disabled), 5004 (RTP config changed), 5007 (configuration changed), 5010/5012 (scanning disabled), 1116/1117 (detection/action).
* **AppX:** `Microsoft-Windows-AppXDeployment-Server/Operational` — 400/401 (deployment started/succeeded), 404 (failed); `Microsoft-Windows-AppxPackaging/Operational`.
* **Optional features / CBS:** `Setup` log — 1/2/3/4 (package install initiated/succeeded/failed/pending reboot); `%WINDIR%\Logs\CBS\CBS.log`, `%WINDIR%\Logs\DISM\dism.log`.
* **Firewall:** Security log 4946/4947/4948 (rule added/modified/deleted; needs "Audit MPSSVC Rule-Level Policy Change") and 4950 (setting changed).
* **Registry auditing:** Security 4657 (value modified) requires a SACL on the key + "Audit Registry" — optional forensic mode for OTW's own writes.
* **System Restore:** Application log source "System Restore" (creation/failure events; **UNVERIFIED IDs**); enumerate `SystemRestore` WMI instances instead.
* **Setup/upgrade:** compare build/UBR (§5.1); `%WINDIR%\Panther\setupact.log`.

---

## 11. Top pitfalls (what most tweak tools get wrong)

1. **Writing HKCU from an elevated process** — lands in the elevating admin's hive (or is ignored on same-user elevation for `Software\Classes` COM lookups). Fix: resolve the session user's SID and write `HKU\<SID>` / `HKU\<SID>_Classes` (§9).
2. **Fighting the policy engine** — raw writes to `...\Policies\...` on domain/MDM machines are silently reverted (or worse: only reverted when the GPO version bumps), and `MDMWinsOverGP` actively removes "values set by scripts". Fix: resolve owner, badge as managed, write local policy through the LGPO plumbing so it survives `gpupdate` (§1–2).
3. **Loading a hive that is in use / leaking handles** — corrupts profiles or leaves `RegUnLoadKey` failing; touching hives during OOBE/roaming/VDI. Fix: probe `HKU\<SID>`, treat sharing violations as "queue via Active Setup", dispose all handles, never during OOBE (§3).
4. **Trusting System Restore** — silently *not* created inside the 24-hour window (API still returns success), often disabled, does not cover user data. Fix: verify creation, disclose, keep the journal as the real undo (§4).
5. **Explorer restarts from an elevated context** — spawn an elevated shell (`/nouaccheck`), rely on Task Scheduler being alive for `CreateExplorerShellUnelevatedTask`, kill explorer in the wrong session, or race `AutoRestartShell`. Fix: session-scoped kill, wait for auto-restart, launch with the user's unelevated token (§7.3).
6. **Disabling protected/critical services** or the update stack (WinDefend, gpsvc, Schedule, wuauserv/UsoSvc/WaaSMedic) — Fix: PPL detection + deny-list; Windows Update only via documented policies (§7.3).
7. **Not planning for feature updates** — apps re-provisioned, tasks re-enabled; users think "the tool didn't work". Fix: deprovisioned marker, build-change detection, scheduled `remediate`, Intune detect/remediate wrappers (§5).
8. **Bundling LGPO.exe** — Microsoft says no. Fix: own PReg writer + `IGroupPolicyObject`; invoke LGPO only if the customer supplies it (§1.5).
9. **Registering an Event Log source and writing immediately / colliding on the 8-char log name** — Fix: create at install, tolerate latency, name check (§6.1).
10. **Home edition assumptions** — `Registry.pol` is inert, `gpedit` absent, some policies simply do nothing; and MDM `MDMWinsOverGP` isn't even available. Fix: per-entry `effectOnHome`, mirror-only path, verified-on evidence (§1.7).

---

## 12. Sources (accessed 2026-08-16)

**Group Policy storage, PReg, COM, refresh**
- Registry Policy File Format — https://learn.microsoft.com/en-us/previous-versions/windows/desktop/policy/registry-policy-file-format
- IGroupPolicyObject interface — https://learn.microsoft.com/en-us/windows/win32/api/gpedit/nn-gpedit-igrouppolicyobject
- IGroupPolicyObject::OpenLocalMachineGPO — https://learn.microsoft.com/en-us/windows/win32/api/gpedit/nf-gpedit-igrouppolicyobject-openlocalmachinegpo
- IGroupPolicyObject::Save (REGISTRY_EXTENSION_GUID, revision bump, auto-refresh note) — https://learn.microsoft.com/en-us/windows/win32/api/gpedit/nf-gpedit-igrouppolicyobject-save
- RefreshPolicyEx (userenv.h) — https://learn.microsoft.com/en-us/windows/win32/api/userenv/nf-userenv-refreshpolicyex
- C# IGroupPolicyObject2 sample (sjitech) — https://github.com/sjitech/powershell-local-group-policy-reg/blob/master/IGroupPolicyObject2.cs
- Pete Batard, "Programmatically setting and applying Local Group Policies on Windows" — https://pete.akeo.ie/2011/03/porgramatically-setting-and-applying.html
- LGPO.exe v1.0 blog + comments (redistribution: "No. Please link back to this site."; stdout/stderr; SysWOW64 redirection) — https://learn.microsoft.com/en-us/archive/blogs/secguide/lgpo-exe-local-group-policy-object-utility-v1-0
- Microsoft Security Compliance Toolkit guide — https://learn.microsoft.com/en-us/windows/security/operating-system-security/device-management/windows-security-configuration-framework/security-compliance-toolkit-10 ; download — https://www.microsoft.com/en-us/download/details.aspx?id=55319
- Policy Plus (Fleex255) README / repo (CC-BY-4.0 per GitHub API; Home-edition notes; RefreshPolicyEx reduced functionality) — https://github.com/Fleex255/PolicyPlus
- Group Policy History registry key {35378EAC-…} (Registry CSE) — https://www.serverbrain.org/network-security-2003/analyzing-group-policy-using-the-registry.html ; ADMX "Configure registry policy processing" — https://www.windows-security.org/6dc1df957d5fe20af799b12796339b35/configure-registry-policy-processing
- Enabling gpedit on Home (ClientTools/ClientExtensions packages; "no GPO handler applies registry.pol") — https://woshub.com/group-policy-editor-gpedit-msc-for-windows-10-home/

**MDM / Intune**
- ControlPolicyConflict Policy CSP (MDMWinsOverGP) — https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-controlpolicyconflict
- Policies in Policy CSP supported by Group Policy — https://learn.microsoft.com/en-us/windows/client-management/mdm/policies-in-policy-csp-supported-by-group-policy
- Understanding ADMX-backed policies — https://learn.microsoft.com/en-us/windows/client-management/understanding-admx-backed-policies
- Using PowerShell scripting with the WMI Bridge Provider (must run as local system; root\cimv2\mdm\dmmap) — https://learn.microsoft.com/en-us/windows/client-management/using-powershell-scripting-with-the-wmi-bridge-provider
- Troubleshoot devices by using dsregcmd — https://learn.microsoft.com/en-us/entra/identity/devices/troubleshoot-device-dsregcmd
- Diagnose MDM enrollment failures — https://learn.microsoft.com/en-us/windows/client-management/mdm-diagnose-enrollment
- HKLM\Software\Microsoft\Enrollments explained (practitioner) — https://en.ittrip.xyz/windows/enrollments-registry-check
- MDM wins over GPO / PolicyManager registry (_ProviderSet/_WinningProvider) — https://www.anoopcnair.com/mdm-wins-over-gpo-group-policy-intune-policy/ ; https://www.anoopcnair.com/windows-10-intune-mdm-support-help-1/
- RSOP_RegistryPolicySetting class — https://learn.microsoft.com/en-us/previous-versions/windows/desktop/policy/rsop-registrypolicysetting ; RSoP WMI classes — https://learn.microsoft.com/en-us/previous-versions/windows/desktop/policy/rsop-wmi-classes
- Windows Autopatch FAQ — https://learn.microsoft.com/en-us/windows/deployment/windows-autopatch/overview/windows-autopatch-faq ; WUfB DS / Autopatch deep dive (client artefacts) — https://patchmypc.com/blog/windows-11-feature-updates-deep-dive/
- Use Remediations to detect and fix support issues (Intune) — https://learn.microsoft.com/en-us/intune/device-management/tools/deploy-remediations

**All-users / hives / Active Setup / sessions**
- RegLoadKey privileges (SE_BACKUP_NAME/SE_RESTORE_NAME; profile-not-loaded failure modes) — https://learn.microsoft.com/en-us/archive/blogs/alejacma/win32_process-create-fails-if-user-profile-is-not-loaded
- RegCreateKeyTransacted (REG_OPTION_BACKUP_RESTORE; VSS note; transaction semantics) — https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regcreatekeytransacteda
- RegOpenUserClassesRoot — https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regopenuserclassesroot
- Helge Klein, "Active Setup Explained" — https://helgeklein.com/blog/active-setup-explained/
- Enumerating profiles from ProfileList (Scripting Guy) — https://devblogs.microsoft.com/scripting/use-powershell-to-find-user-profiles-on-a-computer/ ; PDQ, modify registry for all users — https://www.pdq.com/blog/modifying-the-registry-users-powershell/
- WTSQueryUserToken (LocalSystem + SE_TCB_NAME) — https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsqueryusertoken
- Elevated permissions and the Vista SP1 registry (elevated processes and per-user HKCR) — https://learn.microsoft.com/en-us/archive/blogs/msdn/ben/elevated-permissions-and-the-vista-sp1-registry
- Raymond Chen, "What is the CreateExplorerShellUnelevatedTask scheduled task?" — https://devblogs.microsoft.com/oldnewthing/20220524-00/?p=106682 ; running Explorer elevated / `/nouaccheck` — https://woshub.com/how-to-run-file-explorer-elevated/
- OOBEComplete (kernel32) — https://learn.microsoft.com/en-us/windows/win32/api/oobenotification/nf-oobenotification-oobecomplete

**Transactions, backup, System Restore**
- Alternatives to using Transactional NTFS (deprecation) — https://learn.microsoft.com/en-us/windows/win32/fileio/deprecation-of-txf ; Kernel Transaction Manager portal — https://learn.microsoft.com/en-us/windows/win32/ktm/kernel-transaction-manager-portal
- Project Zero: KTM registry transactions may have non-atomic outcomes — https://project-zero.issues.chromium.org/issues/42451576
- Calling SRSetRestorePoint (24-hour rule, SystemRestorePointCreationFrequency, ScopeSnapshots) — https://learn.microsoft.com/en-us/windows/win32/sr/calling-srsetrestorepoint
- SRSetRestorePointW — https://learn.microsoft.com/en-us/windows/win32/api/srrestoreptapi/nf-srrestoreptapi-srsetrestorepointw ; Using System Restore (LoadLibrary SrClient.dll) — https://learn.microsoft.com/en-us/windows/win32/sr/using-system-restore
- SystemRestore.CreateRestorePoint (root\default; types/event codes) — https://learn.microsoft.com/en-us/windows/win32/sr/createrestorepoint-systemrestore
- Change System Restore point creation frequency (registry) — https://www.ninjaone.com/blog/change-system-restore-point-creation-frequency-in-windows/

**Drift / feature updates**
- SetupConfig.ini with PostOOBE/PostRollback (4sysops) — https://4sysops.com/archives/upgrade-from-windows-10-to-windows-11-with-setupconfigini-and-intune/ ; A Square Dozen — https://www.asquaredozen.com/2019/08/25/windows-10-feature-updates-using-setupconfig-ini-to-manage-feature-updates-in-the-enterprise/ ; MSEndpointMgr custom actions — https://msendpointmgr.com/2021/04/12/running-custom-actions-during-a-windows-10-feature-update-with-configuration-manager/
- Upgrade to Windows 11 deletes scheduled tasks (Q&A) — https://learn.microsoft.com/en-gb/answers/questions/1696089/upgrade-to-windows-11-deletes-all-scheduled-tasks
- Surviving a feature update without losing pins (shell state reset) — https://automatalabs.ca/blog/survive-windows-11-feature-update-without-losing-pinned-apps/

**Audit / logging**
- EventLog.CreateEventSource (admin rights, 8-char rule, latency, restart on remap) — https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.eventlog.createeventsource
- EventSourceCreationData.LogName — https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.eventsourcecreationdata.logname
- EventSource (.NET) — https://learn.microsoft.com/en-us/dotnet/core/diagnostics/eventsource ; EventSource NuGet + Windows Event Log channels (wevtutil static registration) — https://devblogs.microsoft.com/dotnet/announcing-the-eventsource-nuget-package-write-to-the-windows-event-log/ ; dotnet/runtime #27002 (EventSource not emitting to Event Log on .NET Core) — https://github.com/dotnet/runtime/issues/27002

**Safety rails**
- Pending reboot registry keys (Scripting Blog) — https://devblogs.microsoft.com/scripting/determine-pending-reboot-statuspowershell-style-part-1/ ; Adam the Automator — https://adamtheautomator.com/pending-reboot-registry/
- ISystemInformation::RebootRequired — https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-isysteminformation-get_rebootrequired ; IUpdateInstaller::IsBusy — https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdateinstaller-get_isbusy
- SERVICE_LAUNCH_PROTECTED_INFO — https://learn.microsoft.com/en-us/windows/win32/api/winsvc/ns-winsvc-service_launch_protected_info ; QueryServiceConfig2 — https://learn.microsoft.com/en-us/windows/win32/api/winsvc/nf-winsvc-queryserviceconfig2a
- Detecting PPL services/processes (gist) — https://gist.github.com/jonny-jhnson/6b9e87f5a428f31d41ffc8c1ee05a999 ; why admins cannot stop WinDefend — https://www.tenforums.com/antivirus-firewalls-system-security/148379-can-anyone-explain-why-admin-cant-kill-windefend-service.html
- Windows Sandbox detection (wsb-detect; capa rule) — https://github.com/fengjixuchui/wsb-detect ; https://github.com/mandiant/capa-rules/blob/master/anti-analysis/anti-vm/vm-detection/check-for-windows-sandbox-via-registry.yml
- Explorer.exe crash/taskbar issue and KB5074105 (24H2-era shell restarts) — https://www.windowslatest.com/2026/01/31/microsoft-says-latest-windows-11-issue-crashes-explorer-exe-makes-taskbar-disappear-but-a-fix-is-rolling-out/ ; AutoRestartShell — https://www.visualautomation.com/comprod/secure6/auto_res.htm

**Native APIs / packages**
- ManagedDism (Microsoft.Dism NuGet, MIT) — https://github.com/jeffkl/ManagedDism
- .NET 10 CompositeMLDsaAlgorithm.MLDsa44WithEd25519 (evidence that standalone Ed25519 is not exposed) — https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.compositemldsaalgorithm.mldsa44withed25519 ; dotnet/runtime Ed25519 API proposal — https://github.com/dotnet/runtime/issues/63174 ; ECDsa class — https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.ecdsa
- Policy CSP overview — https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-configuration-service-provider
- Companion research: `docs/research/02-landscape-and-enterprise-requirements.md` (breakage catalogue, licensing constraints, level model)
