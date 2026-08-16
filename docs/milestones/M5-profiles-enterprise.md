# M5 — Profiles, levels, all-users scope, MDM/GPO awareness, drift task

## Goal

Named and custom profiles (JSON, optional ES256 signature), per-category
level dials, all-users application, managed-setting reporting, scheduled
re-enforcement, Intune remediation templates.

## Preconditions

M4 certified. Read: research 07 §2 (MDM), §3 (all users), §5 (drift), §8
(profiles/signing); PLAN §5.4, D11.

## Profile format (`docs/profile-format.md`, schema `catalog/schema/profile.schema.json`)

```json
{
  "$schema": "https://github.com/RandyNorthrup/open-the-windows/catalog/schema/profile.schema.json",
  "schemaVersion": 1,
  "id": "enterprise-workstation",
  "name": "Enterprise Workstation",
  "description": "...",
  "levels": { "Privacy": "Strict", "Updates": "Balanced", "Security": "Strict", "Performance": "Basic", "Debloat": "Balanced", "Shell": "Basic" },
  "include": ["security.credentials.lsa-protection"],
  "exclude": ["debloat.apps.remove-feedback-hub"],
  "includeDraft": false,
  "scope": "AllUsers",
  "options": { "restorePoint": true, "restartExplorer": false, "allowAdvanced": true, "allowBreaking": false },
  "appliesTo": { "editions": [], "minBuild": null, "maxBuild": null, "architectures": [] }
}
```

Built-in profiles (embedded `catalog/profiles/*.json`): `home`, `power-user`,
`enterprise-workstation`, `developer`, `gamer`, `kiosk-shared`, `privacy-max`.
Built-ins never set `allowBreaking:true`.

Types: `Profile`, `ProfileLoader` (embedded + file + `--profile <path>`),
`ProfileResolver.Resolve(Profile, TweakCatalog, OperatingSystemFacts) →
IReadOnlyList<TweakDefinition>` (levels → `TweakCatalog.Select`, then include /
exclude, then applicability), `ProfileSignature` (detached `<file>.sig`
JSON: `{ "alg": "ES256", "keyId": "...", "signature": base64 }` over the
canonical UTF-8 bytes of the profile file; keys pinned in
`%ProgramData%\OpenTheWindows\trusted-keys\*.pem`; policy `RequireSignedProfiles`
under `HKLM\SOFTWARE\Policies\OpenTheWindows` — the product ships an ADMX
`admx/OpenTheWindows.admx` for it). CLI: `otw profile list|show|validate|sign
--key <pem>|verify`.

## All-users scope (`Windows/Users/*`)

`IUserHiveEnumerator`: enumerate `ProfileList` SIDs (skip system SIDs and
`.DEFAULT`), for each: loaded (`HKU\<SID>` exists) → write directly; not
loaded → `RegLoadKey` (SeBackup/SeRestore privileges enabled via
`AdjustTokenPrivileges`), write, flush, `RegUnLoadKey`, always in `finally`;
skip on sharing violation and report; Default profile hive
(`C:\Users\Default\NTUSER.DAT`) handled the same way; `UsrClass.dat` for
`Software\Classes` paths. Active Setup fallback: `HKLM\SOFTWARE\Microsoft\Active
Setup\Installed Components\{OTW-<profileId>}` with `StubPath = otw.exe
remediate --user-scope --profile <id>` and `Version` bump per apply. Never
during OOBE (`HKLM\SYSTEM\Setup\OOBEInProgress`).

## Managed detection & drift

- `IManagedSettingDetector` (M2) is used by plan/apply to mark `Managed`;
  GUI/CLI show "Managed by your organisation (MDM|GPO)".
- Drift task: `otw task install --profile <id> [--daily|--weekly] [--on-logon]
  [--on-build-change]` creates `\OpenTheWindows\Remediate` (SYSTEM, highest
  privileges, hidden) running `otw remediate --profile <id> --json --out
  %ProgramData%\OpenTheWindows\reports\last-remediate.json`; the task compares
  `CurrentBuild`/`UBR` with the last journal and re-applies after feature
  updates. `otw task remove`.
- `otw remediate` = scan then apply drift only; exit 0/2/3010.
- Intune templates: `docs/enterprise/intune-detect.ps1`,
  `intune-remediate.ps1` (wrap `otw scan`/`remediate` and map exit codes),
  `docs/enterprise.md` (deployment guide incl. re-signing and WDAC rules).

## Tests

- Profile schema/loader/resolver tests (levels, include/exclude, applicability,
  built-ins never Breaking); signature sign/verify/tamper/wrong-key/missing-sig
  with `RequireSignedProfiles`; hive enumeration with fakes; Active Setup
  registration; task XML golden; remediation exit codes.
- VM: sign+verify; all-users apply reaches a logged-off user and a new user;
  managed value (set a policy via `LGPO`-style import or Intune-simulated
  `PolicyManager` key on the VM) is reported and not overwritten; scheduled task
  runs and re-applies after a simulated build change (edit last journal's build).

## Gates / docs / acceptance

- All gates; docs: `docs/profile-format.md`, `docs/enterprise.md`, README,
  CHANGELOG, PLAN, `docs/certification/M5.md`.
- Acceptance: signed profile with wrong key rejected; managed value reported
  and untouched; all-users reaches logged-off + new user; task re-applies after
  build change; Intune templates return correct exit codes.
