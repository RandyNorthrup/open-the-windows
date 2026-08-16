# M3 — Apply / verify / revert engine, journal, restore points, Event Log

## Goal

Transactional apply per PLAN §5.3 with journal-first semantics, per-kind
writers, GPO-aware policy writes, Explorer restart for the right session,
System Restore integration, Windows Event Log source, and CLI
`apply` / `revert` / `history` / `--what-if`.

## Preconditions

M2 certified. Read: research 07 §1 (policy writes), §3 (all-users — only the
interactive-user part is needed in M3), §4 (journal/rollback), §6 (event log),
§7 (safety rails), §9 (elevation/HKU target); research 03 §"refuse";
PLAN D12–D16, D21.

## Writer abstractions (Core/Abstractions)

```csharp
public interface IRegistryWriter
{
    // Policy kind: write via IGroupPolicyObject (Registry.pol) AND mirror; Preference: direct.
    void Write(RegistryHive hive, string? userSid, string path, string name, RegistryValueType type, JsonElement data, RegistryValueKind kind);
    void Delete(RegistryHive hive, string? userSid, string path, string name, RegistryValueKind kind);
}
public interface IServiceWriter { void SetStartType(string name, ServiceStartType startType); void Stop(string name, TimeSpan timeout); }
public interface IScheduledTaskWriter { void SetEnabled(string path, bool enabled); }
public interface IAppxWriter { void Remove(string packageFamilyName, string? userSid, AppxRemovalMode mode); void Reprovision(string packageFamilyName); }
public interface IOptionalFeatureWriter { void SetEnabled(string name, bool enabled); }
public interface IDefenderPreferenceWriter { void Set(string property, JsonElement value); }
public interface IPowerSettingWriter { void Set(Guid subgroup, Guid setting, uint ac, uint dc); }
public interface ICommandRunner { CommandResult Run(string executable, IReadOnlyList<string> args, TimeSpan timeout); } // the ONLY Process user; RS0030 suppressed locally with reason
public interface IRestorePointService { RestorePointResult Create(string description); } // verifies creation; reports Skipped24h, Disabled
public interface IExplorerRestarter { void RestartForSession(string userSid); }
public interface IAuditSink { void Write(AuditEvent e); }  // Event Log + JSONL implementations
public interface IPreflight { PreflightReport Run(); }      // elevated, supported OS, pending reboot, servicing in progress, OOBE, Safe Mode
```

## Journal (Core/Journal) — the safety core

- Location: `%ProgramData%\OpenTheWindows\journal\<yyyyMMdd-HHmmss>-<runId>.json`
  (ACL: Administrators + SYSTEM full, Users read; set on directory creation).
- Schema (`docs/journal-format.md`, source-generated `JournalJsonContext`):

```json
{
  "schemaVersion": 1,
  "runId": "guid",
  "startedAt": "2026-08-16T20:00:00Z",
  "appVersion": "0.2.0",
  "os": { "build": 26200, "revision": 9168, "editionId": "Professional" },
  "profile": "balanced",
  "operator": "WIN-11-VM\\Randy",
  "targetUser": { "sid": "S-1-5-21-...", "name": "..." },
  "restorePoint": { "requested": true, "created": false, "reason": "Skipped24h", "sequenceNumber": null },
  "entries": [
    {
      "id": "privacy.advertising-id.disable",
      "actions": [
        { "index": 0, "kind": "Registry", "target": "HKU\\S-1-5-21-...\\Software\\...\\AdvertisingInfo!Enabled",
          "prior": { "exists": true, "type": "Dword", "data": 1 },
          "desired": { "type": "Dword", "data": 0 },
          "appliedAt": "...", "verified": true, "result": "Applied" }
      ],
      "result": "Applied"
    }
  ],
  "result": "Completed",
  "requires": "None",
  "previousHash": "sha256-of-previous-journal-file-or-null",
  "hash": "sha256-of-this-document-without-hash-field"
}
```

- Rules: the journal file is written with every `prior` **before** the first
  write; each action's `appliedAt/verified/result` is flushed after that action;
  hash chain over the directory (previousHash = hash of the newest earlier
  journal); reverting a run produces a new journal with `revertOf: runId`.
- `IJournalStore { JournalRecord Begin(...); void Update(JournalRecord);
  IReadOnlyList<JournalSummary> List(); JournalRecord Load(Guid runId); }`.

## Engine (Core/Engine)

```csharp
public sealed class ApplyEngine
{
    PlanResult Plan(IEnumerable<TweakDefinition> entries, ApplyOptions options); // what-if: uses ScanEngine, orders by dependsOn, flags conflicts, managed, not applicable
    ApplyResult Apply(PlanResult plan, ApplyOptions options);                    // preflight → restore point (optional) → journal → apply in order → verify → rollback on failure
    RevertResult Revert(Guid runId, RevertOptions options);                      // reverse order, prior states, verify, new journal
}
public sealed record ApplyOptions(bool WhatIf, bool CreateRestorePoint, bool BreakGlass, bool AllowAdvanced, bool AllowBreaking, string ProfileName);
```

- Ordering: topological by `dependsOn`; conflicts ⇒ plan error (exit 4).
- Managed setting: skipped with `Managed` unless `BreakGlass`, which still
  applies but records `breakGlass:true` and emits Event 4001.
- Verify: re-read via the M2 readers; mismatch ⇒ action `Failed` ⇒ roll back
  every already-applied action of the run in reverse ⇒ `RolledBack`.
- Refusals enforced in code (not only catalogue): `ProtectedServices`,
  `CommandAction.AllowedExecutables`; an attempt throws `RefusedOperationException` and is journaled.
- Requires aggregation = max over applied entries; exit 3010 when Reboot.
- Explorer restart: only when an applied entry `Requires == ExplorerRestart` and
  `--restart-explorer` (default on for GUI, off for CLI unattended); restart in
  the interactive user's session (research 07 §7).

## Windows implementations (`OpenTheWindows.Windows/Writers/*`)

- Registry policy path: `IGroupPolicyObject` (CLSID `{EA502722-A23D-11d1-A7D3-0000F87571E3}`,
  `OpenLocalMachineGPO(GPO_OPEN_LOAD_REGISTRY)` / user GPO for user policies,
  `GetRegistryKey`, write, `Save(machine/user, TRUE, REGISTRY_EXTENSION_GUID
  {35378EAC-683F-11D2-A89A-00C04FBBCFA2}, OTW snap-in GUID)`), on an STA
  thread; then mirror into the live registry; then `RefreshPolicyEx`. Home
  edition (no GP engine): mirror only and record `policyEngine:false`.
- Registry hives for `User`: `Registry.Users.OpenSubKey(sid + "\\" + path,
  writable: true)`; if the hive is not loaded ⇒ error `UserHiveNotLoaded`
  (all-users hive loading is M5).
- Services: `ChangeServiceConfig` (CsWin32) — refuse if `IsProtectedProcess`.
- Tasks: TaskScheduler NuGet `Enabled` property.
- Appx: `PackageManager.RemovePackageAsync(fullName, RemovalOptions)` per user /
  `DeprovisionPackageForAllUsersAsync`; write `Deprovisioned` marker key.
- Features: DISM API enable/disable (no restart forced; report `Requires`).
- Defender: `MSFT_MpPreference` `Set` method via WMI.
- Power: `PowerWriteACValueIndex` / `PowerWriteDCValueIndex` + `PowerSetActiveScheme`.
- Restore point: `SRSetRestorePointW` (srclient.dll) with `BEGIN_SYSTEM_CHANGE`
  / `END_SYSTEM_CHANGE`; then verify via WMI `SystemRestore` enumeration that a
  new sequence number exists; report `Skipped24h` when not; never override the
  frequency key without `--restore-point-force`.
- Event Log: log `OpenTheWindows`, source `OTW` (create at first elevated run
  or by the M7 installer); IDs: 1000 apply started, 1001 tweak applied, 1002
  tweak failed, 1003 run rolled back, 1004 revert started, 1005 revert done,
  2000 scan drift, 3000 rollback, 4000 refusal, 4001 break-glass, 9000 error.
  Also JSONL audit `%ProgramData%\OpenTheWindows\audit\otw-YYYYMM.jsonl` (rotate monthly).

## CLI

- `otw apply --profile <name|file> [--include-draft] [--what-if] [--restore-point|--no-restore-point]
  [--break-glass] [--allow-advanced] [--allow-breaking] [--yes] [--json]`
  — Breaking entries require `--allow-breaking` **and** interactive typed
  confirmation of the id unless `--yes` (unattended). Exit 0 / 3010 / 2 / 3 / 4 / 5.
- `otw revert <runId|last> [--what-if] [--json]`; `otw history [--json]`
  (lists journals: runId, time, profile, result, entry count).
- `otw scan` unchanged.

## Tests

- Engine unit tests with an in-memory `FakeOperatingSystem` implementing all
  reader/writer interfaces: ordering, conflict error, managed skip vs
  break-glass, verify-fail ⇒ full rollback (fault injection on action N of M),
  journal written before first write (assert ordering via recorded call
  sequence), hash chain valid, revert restores exact prior incl. "absent",
  requires aggregation, refusal exceptions for protected service / disallowed
  executable, restore-point Skipped24h surfaced.
- Windows integration on the VM (`OTW_INTEGRATION=1`, elevated): apply the
  Balanced privacy set → scan Compliant → revert → scan equals pre-state;
  policy write survives `gpupdate /force`; user-hive write lands in the SSH
  session user's `HKU\<SID>`; Event Log entries present (`Get-WinEvent -LogName OpenTheWindows`).
- Coverage thresholds raised to 90/80; Stryker threshold ≥ 70 on
  `OpenTheWindows.Core.Engine` and `Journal` (fail below).

## Gates / docs / security / performance

- All gates; `mutation` gate now enforcing.
- Docs: `docs/journal-format.md`, `docs/event-log.md`, README commands,
  CHANGELOG, PLAN §7.2 (new gates proven), `docs/certification/M3.md`.
- Security: journal directory ACL test; no write without journal (fault
  injection); `Process` used only in `WindowsCommandRunner` (grep test);
  break-glass audit event.
- Performance: 100 registry actions apply < 3 s on VM; journal write cost
  linear (test with 1 000 actions in-memory).

## Acceptance criteria

1. VM: apply/scan/revert/scan cycle byte-identical per journal for the 24 built-in entries that are
   applicable (drafts included via `--include-draft` on the lab VM only).
2. Forced failure mid-run rolls back completely (verified by scan = pre-state).
3. `gpupdate /force` does not remove OTW-written policy values.
4. `Get-WinEvent -LogName OpenTheWindows` shows 1000/1001/…/1005 events for the runs.
5. Restore point behaviour reported truthfully (created vs Skipped24h vs Disabled).
