# M2 — Detection engine (`scan`), health checks, reports

Status: **DONE** (2026-08-16). Certification: `docs/certification/M2.md`.

## Goal

Read-only state readers for every action kind, a drift report against a set
of entries, 28 read-only health checks (research 04 §5), report exporters, and
CLI `otw scan` / `otw health`. Still no writes to the machine.

## Preconditions

M1 certified. Read: research 07 §2 (MDM/GPO detection), §10 (native API
table); research 04 §5 (health checks); PLAN §5.3, §5.5.

## Abstractions (Core/Abstractions; one interface per file; fakes in TestSupport)

```csharp
public interface IRegistryReader
{
    // hive: LocalMachine, or User resolved to HKEY_USERS\<sid> by the caller
    RegistryValueSnapshot Read(RegistryHive hive, string? userSid, string path, string name);
}
public sealed record RegistryValueSnapshot(bool Exists, RegistryValueType? Type, JsonElement? Data);

public interface IServiceReader { ServiceSnapshot? Read(string name); }
public sealed record ServiceSnapshot(string Name, ServiceStartType StartType, bool IsRunning, bool IsProtectedProcess);

public interface IScheduledTaskReader { ScheduledTaskSnapshot? Read(string path); }
public sealed record ScheduledTaskSnapshot(string Path, bool Enabled, bool Exists);

public interface IAppxReader { AppxSnapshot Read(string packageFamilyName, string? userSid); }
public sealed record AppxSnapshot(bool InstalledForUser, bool InstalledForAnyUser, bool Provisioned, bool Deprovisioned);

public interface IOptionalFeatureReader { bool? IsEnabled(string featureName); } // null = not present
public interface IDefenderPreferenceReader { JsonElement? Read(string property); }
public interface IPowerSettingReader { (uint Ac, uint Dc)? Read(Guid subgroup, Guid setting); }

public interface IInteractiveUserResolver { InteractiveUser? Resolve(); }   // WTS session user, never process user
public sealed record InteractiveUser(string Sid, string UserName, bool HiveLoaded);

public interface IManagedSettingDetector
{
    ManagedState Detect(RegistryHive hive, string path, string name); // MDM PolicyManager, GPO history/RSOP
}
public enum ManagedState { NotManaged, Mdm, GroupPolicy }

public interface IMachineHealthProbe { IReadOnlyList<HealthCheckResult> Run(); }
public sealed record HealthCheckResult(string Id, string Title, HealthStatus Status, string Detail, Uri? Reference);
public enum HealthStatus { Pass, Warn, Fail, NotApplicable, Unknown }
```

Windows implementations (`OpenTheWindows.Windows/Readers/*`): registry via
`Microsoft.Win32.Registry` (`Registry.Users` + SID for user hives — never
`CurrentUser`), services via `System.ServiceProcess.ServiceController` +
`sc qprotection` equivalent (`QueryServiceConfig2` / `SERVICE_LAUNCH_PROTECTED_INFO`
via CsWin32), tasks via `TaskScheduler` NuGet 2.12.2, Appx via WinRT
`Windows.Management.Deployment.PackageManager` (+ `Deprovisioned` key
`HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Deprovisioned`),
features via DISM API (`Microsoft.Dism` 3.x or DismApi P/Invoke — pick one,
pin, record in PLAN §7.1), Defender via WMI `root\Microsoft\Windows\Defender`
`MSFT_MpPreference` (System.Management 10.0.11), power via `PowerReadACValueIndex`
/ `PowerReadDCValueIndex` (CsWin32), interactive user via
`WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, WTS_CURRENT_SESSION, WTSUserName)`
plus `LookupAccountName` → SID (CsWin32), managed detection per research 07 §2.

## Engine (Core/Engine)

```csharp
public sealed record DesiredState(TweakDefinition Entry, ITweakAction Action);
public enum ComplianceState { Compliant, Drift, NotApplicable, Managed, Unknown }
public sealed record ActionObservation(ITweakAction Action, ComplianceState State, string Actual, string Desired, ManagedState Managed);
public sealed record TweakObservation(TweakId Id, ComplianceState State, IReadOnlyList<ActionObservation> Actions);
public sealed record ScanReport(DateTimeOffset At, OperatingSystemFacts Os, string ProfileName, IReadOnlyList<TweakObservation> Results)
{ int DriftCount; int CompliantCount; bool IsCompliant; }

public sealed class ScanEngine(IRegistryReader, IServiceReader, IScheduledTaskReader, IAppxReader,
    IOptionalFeatureReader, IDefenderPreferenceReader, IPowerSettingReader,
    IInteractiveUserResolver, IManagedSettingDetector, IOperatingSystemInfo, TimeProvider)
{
    ScanReport Scan(IEnumerable<TweakDefinition> entries, string profileName);
}
```

Semantics per kind: Registry — Compliant iff Exists && Type matches && Data
equals `Value` (Dword compare numeric, Sz ordinal, MultiSz sequence, Binary
bytes); Service — start type equal (StopIfRunning is not part of compliance);
ScheduledTask — enabled equal, missing task ⇒ NotApplicable; Appx — Compliant
iff not installed for target scope (and deprovisioned when mode says so);
OptionalFeature — enabled equal, absent ⇒ NotApplicable; DefenderPreference —
JSON-equal; PowerSetting — both AC/DC equal; Command — always Unknown
(cannot detect) unless the entry provides a registry/other action too.
Entry state = worst of its actions (Drift > Managed > Unknown > NotApplicable
> Compliant); `AppliesTo` mismatch ⇒ NotApplicable without reading.

## Reports (Core/Reports)

`IReportWriter { string Extension; void Write(ScanReport, Stream) }` with
`JsonReportWriter` (source-generated), `CsvReportWriter`, `HtmlReportWriter`
(single self-contained file, no external assets), `SarifReportWriter`
(SARIF 2.1.0; one rule per tweak id; result level from risk). Golden-file
tests via string comparison of a fixed report (TimeProvider fixed).

## CLI

- `otw scan --profile <name|path> [--catalog-dir] [--json|--csv|--html|--sarif] [--out <file>]`
  — for M2, `--profile` accepts a built-in named level set (`basic`,
  `balanced`, `strict`, `paranoid` = every category at that level, Verified
  entries only, `--include-draft` to include drafts) or a JSON profile file
  (format defined in M5; M2 accepts only the named levels). Exit 0 compliant,
  1 drift, 2 error, 3 unsupported OS.
- `otw health [--json]` — the 28 health checks; exit 0 always unless error.

## Tests

- Unit: `ScanEngineTests` per kind × {compliant, drift, absent, managed, not
  applicable}; worst-state aggregation; TimeProvider stamped.
- Report golden files (`Verify.XunitV3` or plain string fixtures).
- Windows integration (`OTW_INTEGRATION=1`, elevated, VM): read known values
  (e.g. `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\CurrentBuild`),
  a known service (`Spooler`), a known task (`\Microsoft\Windows\Defrag\ScheduledDefrag`),
  interactive-user resolution returns the SSH session user's SID, managed
  detection returns NotManaged on the un-managed VM.
- CLI tests with a fake reader set.

## Gates / docs / security / performance

- All gates; add Stryker.NET (pin, PLAN §7.1) as a report-only step
  `pwsh build/quality.ps1 -Only mutation` (threshold enforced from M3).
- README: `scan`, `health`; docs `docs/reports.md` (formats); CHANGELOG; PLAN.
- Security: readers project has a test asserting no write APIs are referenced
  (reflection over `OpenTheWindows.Windows.Readers` for `SetValue`,
  `Start`, `Stop`, `Remove`, `Add`, `Delete`) — a cheap architectural guard.
- Performance: scan of the built-in catalogue on the VM < 5 s (record time).

## Acceptance criteria

1. On the VM: `otw scan --profile balanced --include-draft` exits 1 (drift) before any apply and lists the drifting ids.
2. Change one registry value manually on the VM; the report flips that entry to Compliant.
3. `otw health` prints 28 checks with real values (Secure Boot, TPM, Defender, BitLocker, etc.).
4. HTML/JSON/CSV/SARIF files validate (SARIF against schema; JSON parses; CSV row count).
5. Certification record `docs/certification/M2.md`.
