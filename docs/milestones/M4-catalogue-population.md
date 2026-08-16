# M4 — Catalogue population and VM verification

## Goal

Turn research 01/03/04/05 into ≥ 300 catalogue entries with sources, levels,
risk, `appliesTo`, side effects and `verifiedOn`; implement the Windows
Update guardrails; publish the refusal and not-included lists.

## Preconditions

M3 certified (apply/revert works). Read: research 01 (tables per section),
03 (§ master table + refusals + EOS), 04 (§4.A–4.Q + §3 levels), 05 (§1
Appx, §2 features, §3 services, §4 tasks, §6 tweaks, §7 snake oil);
`docs/catalog-format.md`.

## Work packages (each is a PR; each PR runs the VM verification for its entries)

| WP | Files | Source | Target count | Notes |
| ---- | ------- | -------- | -------------- | ------- |
| 4.1 privacy | `catalog/privacy/*.json` | research 01 §§ diagnostic data, ads, activity, input, search, cloud content, Copilot/Recall, Edge, tasks/services | ≥ 90 | policy vs preference exactly as the research row says; user-scope rows use `User` hive |
| 4.2 updates | `catalog/updates/*.json` | research 03 master table | ≥ 25 | includes pause presets (7/14/21/28/35 days) as `Registry` actions on `HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings` (times computed by the engine — see guardrails), defer/target/AU/DO/preview/optional content, driver exclusion, MSRT opt-out; **none** of the refusal list |
| 4.3 security | `catalog/security/*.json` | research 04 §4.A–4.Q | ≥ 120 | tags `baseline`, `stig`, `cis-<id>`, `acsc`; ASR rules as `DefenderPreference` actions (`AttackSurfaceReductionRules_Ids`/`_Actions`); audit policy via `Command auditpol.exe`; every Strict/Paranoid item has sideEffects |
| 4.4 performance | `catalog/performance/*.json` | research 05 §3 (DISABLE-OK/MANUAL-OK only), §4, §6 (measurable only) | ≥ 40 | services only from the DISABLE-OK list; nothing from §7 snake oil |
| 4.5 debloat | `catalog/debloat/*.json` | research 05 §1 (SAFE + CAUTION), §2 | ≥ 60 | one entry per package family; `reinstallHint` mandatory; CAUTION rows are Strict with side effects; NEVER/KEEP rows are not entries |
| 4.6 shell | `catalog/shell/*.json` | research 05 §6 UX rows, 01 lock screen/Start rows | ≥ 30 | ExplorerRestart where the research says so |
| 4.7 docs | `docs/refusals.md`, `docs/not-included.md` | research 03 refusals, 05 §7 | — | each item: what, why not, what to do instead |

## Windows Update guardrails (code, `Core/Updates`)

- `PauseCalculator(TimeProvider)`: pause N days ⇒ writes
  `PauseUpdatesStartTime`, `PauseUpdatesExpiryTime`, `PauseFeatureUpdatesStartTime/EndTime`,
  `PauseQualityUpdatesStartTime/EndTime` (ISO-8601 UTC, exact format from
  research 03 §Pause). Default cap 35 days; `--extended` up to a ceiling
  (default 365, configurable) only at RiskTier Advanced with a warning and a
  Windows Event 4002; `otw updates resume` clears all six values and triggers
  a scan (`UsoClient StartScan` via `Command`, allow-listed addition
  `usoclient.exe` — add to allow-list with a validator test).
- `EndOfServicingCalendar` (data file `catalog/updates/eos.json`: version,
  edition family, EOS date; sources: Microsoft lifecycle pages) with
  `otw updates status` warning when a target release is < 90 days from EOS.
- Defender signature updates: a test that asserts no catalogue entry writes
  `HKLM\SOFTWARE\Policies\Microsoft\Windows Defender\Signature Updates` or
  disables `MpUpdate`; the engine refuses those paths.

## Verification procedure (per entry, on the lab VM 25H2 and one 24H2 image)

1. `otw scan --profile <level> --include-draft` before → note state.
2. `otw apply` the entry alone (`--only <id>` option added in this milestone).
3. Verify with the entry's own detection **and** an independent read
   (`reg query`, `Get-Service`, `Get-ScheduledTask`, `Get-AppxPackage`).
4. Observe the documented effect where visible (screenshot/`Get-WinEvent`).
5. `otw revert last` → independent read equals step 1.
6. Record `verifiedOn: { build, edition, date, evidence: "docs/certification/M4/<id>.md" }`
   and set `status: Verified`. Home-edition rows: verify on a Home image or
   leave `Draft` with `appliesTo.editions` restricted.

## Tests

- Catalogue tests extended: total ≥ 300; every category ≥ its target; every
  security entry with tag `stig` or `baseline` has a `managedBy` or a
  Group Policy path; no entry references a `ProtectedServices` name; no entry
  writes under `Windows Defender\Signature Updates`; every `Verified` entry has
  evidence file existing in repo; every `Appx` entry has a `reinstallHint`.
- `PauseCalculatorTests` with fixed clock (format, cap, extended ceiling,
  resume clears); `EndOfServicingCalendarTests`.

## Gates / docs / security / performance

- All gates + `catalog` gate; new `catalog-coverage` assertions inside tests.
- Docs: `docs/refusals.md`, `docs/not-included.md`, README feature list
  updated to what is Verified, CHANGELOG, PLAN, `docs/certification/M4.md`
  (verification matrix build × edition × entry).
- Security: security-category review checklist in the PR template.
- Performance: entries with `measurableBenefit` unknown stay out of Basic/Balanced.

## Acceptance criteria

1. ≥ 300 entries, 100% schema-valid, `otw catalog validate` exit 0.
2. 100% of Basic/Balanced entries `Verified` on 25H2 with evidence files.
3. Windows Update pause presets + resume + EOS warnings work on the VM.
4. `docs/refusals.md` and `docs/not-included.md` published and linked from README.
