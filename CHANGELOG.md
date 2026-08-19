# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/). Entries record what happened, not
what was planned (plans live in `PLAN.md`).

## [Unreleased]

### Added

- M6 (GUI, part 3 — review and apply): every category page now has a
  **Review & apply…** action that opens a modal flow over the existing apply
  engine. The flow previews the run as a what-if plan (per entry: what the run
  would do, its risk, and each action with its current compliance), gates every
  Breaking entry behind typing its id to confirm, then applies the run
  transactionally **on a background thread** so the UI never freezes, and shows
  the per-entry outcome with the aggregate restart requirement. If the run
  fails the engine rolls it back and the flow reports that the machine is
  unchanged. When a change needs Explorer restarted the flow offers to do it
  (during the run or afterwards). The atomic apply is not interrupted
  mid-transaction; cancellation is available before it starts.
- M6 (GUI, part 2 — category pages): a shared `CategoryPageViewModel` now backs
  the **Privacy**, **Security**, **Performance**, **Debloat** and **Shell** pages.
  Each has a level dial (Off / Basic / Balanced / Strict / Paranoid) that
  pre-selects the entries a profile at that level would apply, a search box over
  id / title / description / rationale / tags, and risk and draft filters that
  narrow the visible list without changing the selection. Every entry is a
  `TweakItemViewModel` with a selection checkbox and risk / restart / draft /
  "verified on" badges (colour plus text, never colour alone); the selected row
  opens a detail pane with the description, rationale, side effects, the
  human-readable actions, source links and verification history. Selection is
  cumulative and survives filtering. (Updates has its own page, added later.)
- M6 (GUI, part 1 — shell, navigation and service layer): the WPF app is now a
  multi-page shell instead of the single doctor window. A left navigation list
  drives a content host through an `INavigationService` that resolves page view
  models from the container by key; `IDialogService` (message / confirm / typed
  confirmation, the last for Breaking tweaks) and `IDispatcherService` (UI-thread
  marshalling) complete the App service layer, each with a fake for view-model
  tests. The shell ships the **Dashboard** (system health, reusing the doctor
  report, plus quick actions) and **About** (identity, licence, non-affiliation
  notice) pages; further pages are added in later parts. A `Strings.resx`
  localisation baseline (en-US) backs shared display text, and a
  `BoolToYesNoConverter` fixes the M0 defect where `CanApply` surfaced as
  "True"/"False".
- M5 (profiles, part 1 — the profile format): named, data-defined profiles in
  `OpenTheWindows.Core.Profiles`. A `Profile` is a level dial per category plus
  explicit `include`/`exclude` overrides, an application `Scope`, apply
  `ProfileOptions` and machine applicability, described by
  `catalog/schema/profile.schema.json` and loaded by `ProfileLoader` (embedded
  built-ins, an operator file, or raw text; schema-validated before
  deserialization). `ProfileResolver.Resolve` turns a profile into the ordered,
  deduplicated set of catalogue entries it selects on a given machine: level
  dials first (dropping Advanced/Breaking entries unless the options allow them),
  then explicit includes (which bypass the level and risk gates but never pull in
  a Deprecated entry), then excludes (which always win), then per-entry
  applicability. `ProfileValidator` adds the catalogue-aware checks the schema
  cannot express (include/exclude ids must exist and not collide or reference
  retired entries; a built-in profile can never apply a Breaking-risk entry).
  Seven built-in profiles ship embedded (`home`, `power-user`,
  `enterprise-workstation`, `developer`, `gamer`, `kiosk-shared`, `privacy-max`),
  none of which allows or resolves to a Breaking entry.
- M5 (profiles, part 2 — the `otw profile` CLI): `otw profile list` shows the
  built-in profiles, `otw profile show <id|path>` prints one profile (a built-in
  id or a profile file), and `otw profile validate <id|path>` validates it
  against the schema and the catalogue. All read-only; `--json` on each; exit 0
  when valid, 4 on invalid input.
- M5 (profiles, part 4 — profiles drive scan/apply): `otw scan --profile` and
  `otw apply --profile` now accept, besides a level name (`basic`..`paranoid`), a
  **built-in profile id** (`home`, `enterprise-workstation`, …) or a **path to a
  profile `.json` file**. A named profile is resolved through `ProfileResolver`,
  so it selects exactly the entries its per-category dials, include/exclude, risk
  gate and Draft setting call for on this machine. Level names keep their previous
  uniform-level behaviour and the CLI `--include-draft` flag. (Consuming a
  profile's apply `options` — restore point, restart Explorer, all-users scope —
  is deferred to the all-users work package.)
- M5 (profiles, part 3 — profile signing): `ProfileSignature` signs and verifies
  profiles with detached **ES256** (ECDSA P-256 / SHA-256) signatures (decision
  D11) over a canonical form of the JSON, so a signature survives re-formatting;
  the signer is identified by a `keyId` (hex SHA-256 of its SubjectPublicKeyInfo).
  `otw profile sign <file> --key <pem>` writes `<file>.sig`; `otw profile verify
  <file> [--key <pem>]` checks it against a given public key or the machine trust
  store (`%ProgramData%\OpenTheWindows\trusted-keys\*.pem`).
- M5 (profiles, part 5 — `RequireSignedProfiles` policy): a machine can require
  that profiles supplied as files be signed by a trusted key before `otw scan` or
  `otw apply` will use them. `ProfilePolicy` reads
  `HKLM\SOFTWARE\Policies\OpenTheWindows\RequireSignedProfiles` (REG_DWORD) and
  `ProfileTrust` verifies a file's `<file>.sig` against the machine trust store;
  when the policy is on, an unsigned file, an unparseable signature, or a
  signature from an untrusted key is refused. Built-in profile ids and level
  names are never gated (they ship inside the product, not as files). The product
  ships a Group Policy administrative template (`admx/OpenTheWindows.admx` +
  `admx/en-US/OpenTheWindows.adml`) for the setting, and a full format reference
  in `docs/profile-format.md`. The shared trust-store verification now lives in
  `ProfileTrust`, which `otw profile verify` also uses.
- M5 (profiles, part 6 — `apply` honours a profile's options): when
  `otw apply --profile` resolves a real profile (a built-in id or a file), that
  profile's `options` now seed the run — `restorePoint`, `restartExplorer`,
  `allowAdvanced` and `allowBreaking` become the defaults, and the command's
  flags still override (`--no-restore-point` turns the point off; the
  `--allow-advanced` / `--allow-breaking` / `--restart-explorer` switches turn a
  behaviour on). This closes a gap where a profile that selected Advanced entries
  (via `allowAdvanced: true`) still had them *blocked* at apply time unless the
  operator repeated `--allow-advanced`. A profile's `scope` is still not consumed
  (all-users application is a later work package). `CommandSupport.TrySelectEntries`
  now also yields the resolved `Profile` so the command can read its options.
- M5 (profiles, part 7 — `otw remediate`): the unattended re-enforcement command
  the drift scheduled task and the Intune remediation script call. It scans, then
  applies only the drifted entries of a profile, with no interactive confirmation,
  and never break-glasses a managed setting. `--out <file>` writes the stable JSON
  report (creating its directory) — e.g. the task's `last-remediate.json` —
  otherwise `--json` writes it to stdout. Same `--profile` / `--include-draft` /
  `--no-restore-point` / `--allow-advanced` / `--allow-breaking` /
  `--restart-explorer` / `--what-if` options and the same exit contract as `apply`
  (0 applied/compliant, 2 error, 3 unsupported, 4 invalid input, 5 elevation
  required, 3010 reboot). The shared apply plumbing (option-building from a
  profile, exit-code mapping, JSON/text rendering) was factored into
  `ApplyReporting`, which `apply` now also uses.
- M5 (profiles, part 8 — the drift scheduled task): `otw task install --profile
  <id> [--daily|--weekly|--on-logon]` registers `\OpenTheWindows\Remediate`, a
  SYSTEM / highest-privileges / hidden task that runs `otw remediate` for the
  profile on the chosen schedule and writes its report to
  `%ProgramData%\OpenTheWindows\reports\last-remediate.json`; `otw task remove`
  deletes it. Both need elevation. A new Core `IScheduledTaskInstaller` /
  `ScheduledTaskSpec` / `TaskTrigger` abstraction with the deterministic
  `RemediationTask` builder (asserted directly in tests) describes the task; the
  Windows `WindowsScheduledTaskInstaller` registers it through the Task Scheduler
  2.0 API. Build-change re-application (compare the current OS build/UBR against
  the last journal) is a follow-up.
- M5 (profiles, part 9 — enterprise deployment): `docs/enterprise.md`, the
  managed-fleet deployment guide (exit-code contract, signed profiles and the
  `RequireSignedProfiles` policy/ADMX, WDAC allow-listing of the unsigned
  `otw.exe` by file hash or a re-signed publisher rule, auditing), plus the
  paired Intune Remediations scripts `docs/enterprise/intune-detect.ps1`
  (wraps `otw scan`) and `docs/enterprise/intune-remediate.ps1` (wraps
  `otw remediate`). Each script is standalone — Intune uploads detection and
  remediation separately — resolves `otw.exe` from `$OtwPath`, then PATH, then
  `%ProgramFiles%\OpenTheWindows`, and maps the Open The Windows exit codes onto
  Intune's detection contract (0 healthy / non-zero remediate) and remediation
  contract (0 success / non-zero failure). Their shared bootstrap is excluded
  from the duplication gate (PLAN §7.3).
- M5 (profiles, part 10 — build-change detection): `otw remediate
  --if-build-changed` compares the running OS build/UBR against the most recent
  journal (`Core.Engine.BuildChange`) and exits 0 without changes when the build
  is unchanged; with no prior run to compare against it applies (the safe,
  reversible default). `otw task install --on-build-change` registers a
  boot-triggered `\OpenTheWindows\Remediate` task whose `remediate` runs with
  `--if-build-changed`, so a profile is re-applied only after a feature update
  (which reboots). New `TaskTrigger.BuildChange` maps to a Task Scheduler
  `BootTrigger`; `RemediationTask.Build` adds the flag for that trigger; the CLI
  reaches the journal store through a new lazy `CliServices.CreateJournalStore`.
- M5 (profiles, part 11 — all-users foundation): the read-only user-hive
  enumerator that all-users application will target. New Core
  `IUserHiveEnumerator` / `UserProfile`; the Windows `WindowsUserHiveEnumerator`
  reads `HKLM\…\ProfileList`, keeps only real accounts (local/domain `S-1-5-21-…`
  and Azure AD `S-1-12-1-…`), skips the service accounts and `.DEFAULT`, and
  reports whether each `HKEY_USERS\<sid>` hive is loaded. New `otw users [--json]`
  lists them (the hives an all-users apply would touch); new lazy
  `CliServices.CreateUserHiveEnumerator`. This is the first, read-only slice of
  the all-users scope work; loading unloaded hives and applying across them is the
  next slice.
- M5 (profiles, part 12 — journal records a per-action SID): `JournalAction` now
  carries the `Sid` the action wrote under (`HKU\<sid>` for a user-hive registry
  action or a per-user Appx removal, absent for a machine-scope action, via new
  `ActionApplier.EffectiveSid`), and apply, rollback and revert now drive each
  action off its own `Sid` rather than one run-level SID. Behaviour is unchanged
  for today's single-user runs (every action's SID is that one user), but the
  journal and the transactional core can now represent and correctly revert an
  entry applied across several hives — the schema groundwork for all-users apply.
  `docs/journal-format.md` documents the field.
- M5 (profiles, part 13 — user-hive loader): `Core.Abstractions.IUserHiveLoader`
  and the Windows `WindowsUserHiveLoader`, which loads and unloads a user's
  `NTUSER.DAT` with `RegLoadKey`/`RegUnLoadKey` (enabling the backup and restore
  privileges via `AdjustTokenPrivileges`) so an all-users apply can reach the hive
  of a user who is not logged on. The hive mounts under `HKEY_USERS\<sid>`, so the
  existing per-user registry writer targets it unchanged. `Load`/`Unload` are
  explicit (the caller unloads in a `finally`) rather than `IDisposable`, so unload
  failure surfaces to the engine's audit rather than a throwing `Dispose`.
  VM-verified on 26200.9168: loads and unloads the Default profile hive.
- M5 (profiles, part 14 — all-users apply): a profile with `scope: AllUsers` now
  applies each user-scoped entry to every real user hive on the machine, not just
  the interactive user. `ApplyOptions` gains `Scope` (default `Machine`, so all
  existing runs are unchanged); `apply` and `remediate` pass the resolved profile's
  scope. The engine enumerates the target users, loads the hives of any who are not
  logged on (unloading them in a `finally`, auditing an unload failure rather than
  failing the run), and — building on the per-action SID — journals one action per
  hive. A user-scoped entry is planned even when the interactive user is already
  compliant (another user's hive may differ), and each hive is decided independently
  by reading it (compliant hives skipped, drifted ones applied and revertible per
  hive). `ApplyServices` gains the enumerator and loader, wired in
  `WindowsApplyEngineFactory`. VM-verified on 26200.9168: a real `scope: AllUsers`
  apply through the production factory writes a user-scoped value to the current
  user's hive and reverts it.
- M5 (profiles, part 15 — Active Setup logon fallback + `remediate --user-scope`):
  an all-users apply now also registers an Active Setup component
  (`HKLM\SOFTWARE\Microsoft\Active Setup\Installed Components\{OTW-<profileId>}`)
  whose `StubPath` is `otw remediate --user-scope --profile <id>` and whose
  `Version` is bumped per apply, so users who were logged off — or created later —
  are re-enforced in their own session at their next sign-in (the standard per-user
  provisioning mechanism, skipped while OOBE is in progress). The new
  `remediate --user-scope` mode applies only a profile's user-scope entries to the
  calling user's own hive **without elevation**: writing your own `HKU\<SID>` needs
  no administrator token, so pre-flight now treats elevation as required by scope
  (`Scope.User` is exempt, `Machine`/`AllUsers` still require it) and the run is
  filtered to entries whose every action is user-scoped, so a non-elevated run can
  never attempt a machine write. The profile id is validated to kebab-case before it
  is placed in the registry key, so an untrusted id cannot inject a key path. New
  `IActiveSetupRegistrar` / `WindowsActiveSetupRegistrar` and the pure
  `ActiveSetupFallback` builder; `otw task remove` now also clears the logon
  fallback so removing a profile's re-enforcement footprint is one command.
  VM-verified on 26200.9168: the registrar writes the component, bumps its version
  on a second apply, and `RemoveAll` deletes it.
- M5 (profiles, part 16 — all-users revert reloads offline hives): reverting an
  all-users apply now reloads the hives of users who logged off since the run
  (the SIDs recorded on the journalled actions, matched to the enumerator's
  offline profiles), restores each, and unloads them again — so an all-users
  apply stays reversible even after a user signs out, symmetric with the apply
  path. VM-verified on 26200.9168 end to end: an all-users apply reaches a
  genuinely offline user's hive (a synthetic ProfileList profile), writes it, and
  a revert reloads and restores it to absent. Also VM-verified this milestone: a
  Group-Policy-managed value (owned by the Local GPO's Registry.pol) is reported
  Managed and left untouched by apply — the product never fights Group Policy.
- M5 (profiles, part 17 — profiles never resolve to a conflict): fixed a defect
  where five of the seven built-in profiles could not apply at all (`apply` /
  `remediate --profile <id>` exited 4) because mutually-exclusive update presets
  (`defer-30-days` / `defer-90-days` / `pin-to-25h2`, and the Delivery Optimization
  modes) were all tagged `level: Balanced`, so any profile dialing Updates to
  Balanced selected all of them and the engine rejected the run. `ProfileResolver`
  now resolves conflicts within the selected set deterministically — an explicitly
  included entry wins, else the higher level (the more aggressive choice the dial
  reached), else the earlier in catalogue order — so every profile, built-in or
  custom, always yields an applicable plan. The catalogue was re-levelled so the
  single feature-update default at Balanced is a 30-day deferral (the 90-day defer
  and the 25H2 pin are opt-in at Strict), and the Delivery Optimization mode is
  chosen per-profile by an explicit `include` (LAN-only for enterprise-workstation
  and kiosk-shared, HTTP-only for privacy-max, developer and power-user). New tests
  assert no built-in resolves to a conflicting pair and that the Balanced profiles
  get exactly these defaults. VM-verified on 26200.9168: all seven built-in
  profiles now plan cleanly (`apply --what-if` exit 0).
- M4 (Windows Update guardrails): the safe, reversible update controls
  (`OpenTheWindows.Core.Updates`). `PauseCalculator` computes the six Settings-app
  pause values (`PauseUpdatesStartTime`/`ExpiryTime`,
  `PauseFeatureUpdatesStartTime`/`EndTime`, `PauseQualityUpdatesStartTime`/`EndTime`)
  as ISO-8601 UTC strings (research 03 §5.1), capping the pause at Microsoft's
  35 days by default and up to a configurable ceiling (default 365) with an
  opt-in extended pause. `EndOfServicingCalendar` loads an embedded
  `catalog/updates/eos.json` snapshot (research 03 §4.2) and answers days-to-end
  -of-servicing per version and servicing track, warning within 90 days.
  `UpdateControlService` runs pause / resume / status through the transactional
  apply engine so every change is journaled and reversible: pause applies the six
  values (and writes a Windows Event 4002 when extended); resume reverts every
  active Open the Windows pause and triggers a fresh scan (`usoclient StartScan`);
  status reports the running version, its end-of-servicing warning, the pause
  state and any `TargetReleaseVersion` pin. New CLI `otw updates pause`,
  `otw updates resume` and `otw updates status`, plus a `--only <id>` selector on
  `scan` and `apply` for per-entry verification. Wired through the Windows
  adapters by `WindowsUpdateControlFactory`.
- Defender safety boundary (`OpenTheWindows.Core.Engine.DefenderSafety`): the
  engine refuses, at plan time, any registry write under
  `Windows Defender\Signature Updates` and any Defender preference that would
  disable a protection or signature updates — recorded as an audited refusal and
  never partially applied. `usoclient.exe` was added to the command allow-list
  (with a validator test) for the resume scan.
- Published safety documentation: [docs/refusals.md](docs/refusals.md) (what the
  app refuses to do to Windows Update, Defender, security services, apps and the
  network, and what it does instead) and
  [docs/not-included.md](docs/not-included.md) (the placebo / low-value
  "optimisation" tweaks it deliberately leaves out), both linked from the README.
- Windows Update catalogue entries (`catalog/updates/windows-update-controls.json`,
  research 03): feature-update deferral presets (30 / 90 / 180 / 365 days), a
  quality-update 7-day deferral, a 25H2 feature-release pin, Temporary Enterprise
  Feature Control, Automatic Updates "notify before install", Delivery
  Optimization HTTP-only and Simple modes, the MSRT infection-report opt-out,
  Microsoft Store app auto-update off, active hours, quality/feature restart
  deadlines, notification-level control, optional-content user choice and hiding
  the organisation name in notifications. Deferral presets and the release pin
  conflict-guard each other; all are Draft pending VM verification. The catalogue
  now ships 47 entries.
- Security hardening catalogue (WP 4.3, research 04 §4.A–§4.Q): 147 new Security
  entries across seven domain files under `catalog/security/` — credential and
  identity protection (`credential-identity.json`: VBS / memory integrity /
  kernel-shadow-stacks / Credential Guard, WDigest, LM/NTLM session security,
  PKU2U, credential delegation, MSA restriction), UAC and logon/session/lock
  (`uac-logon.json`), network / firewall / remote (`network-remote.json`: SMBv1
  removal, LLMNR/mDNS/NetBIOS/WPAD, anonymous restrictions, firewall profiles and
  logging, Remote Assistance / WinRM / RDP hardening, Point-and-Print), Microsoft
  Defender and all 18 applicable Attack Surface Reduction rules
  (`defender-asr.json`, modelled as paired `AttackSurfaceReductionRules_Ids` /
  `_Actions` preferences with audit-first Balanced and Block Strict variants),
  exploit protection and application control (`exploit-appcontrol.json`: SEHOP,
  SmartScreen, Mark-of-the-Web, Windows Script Host, App Installer protocol,
  Sudo), logging / audit policy / removable media / TLS
  (`audit-media-crypto.json`: event-log sizing, nine `auditpol.exe` advanced-audit
  subcategories, PowerShell module logging and transcription, removable-storage
  and DMA device-class controls, TLS 1.0/1.1 and SSL 3.0 disablement, FIPS mode),
  and disk-encryption / boot / 24H2–25H2 feature controls (`boot-features.json`:
  BitLocker XTS-AES-256 and TPM+PIN policy, Early-Launch Antimalware, Recall and
  Sudo). Every entry is transcribed from the research mechanism tables with exact
  registry paths, values, `valueKind`, edition and build gating, `managedBy`
  (Group Policy / CSP) and provenance tags (`baseline` / `stig-…` / `cis` /
  `acsc`); chk-only health checks, domain-only settings and no-fallback refusals
  were excluded, and each entry was adversarially re-verified against its research
  section for value, hex-conversion, hive, type and ASR-GUID fidelity. All Draft;
  the catalogue now ships 194 entries (153 Security).
- Telemetry / privacy catalogue (WP 4.1, research 01 §1–§16): 98 new Privacy
  entries across six domain files under `catalog/privacy/` — diagnostic-data core
  / CEIP / Windows Error Reporting (`telemetry-core.json`), advertising ID and
  app-launch tracking, account and notification nags, cloud content, Windows
  Spotlight and widgets (`content-suggestions.json`), search / Bing / Cortana,
  Copilot / Click to Do / generative-AI features and activity history
  (`search-ai-activity.json`), input personalization and app-permission consent
  (`input-permissions.json`, per-user `ConsentStore` and machine `AppPrivacy`
  policies), settings-sync / clipboard / Find My Device / Store / consumer
  features (`sync-store-defender.json`), and Microsoft Edge telemetry plus the
  telemetry-relevant scheduled tasks (`edge-tasks.json`). Each row follows the
  research policy-vs-preference distinction (`POL:` → `valueKind: Policy`,
  `PREF:` → `Preference`), applies per-user settings at scope `User` (HKCU) and
  machine settings at scope `Machine`, and skips every row the research rates
  **Never** (security regressions such as disabling SmartScreen or a Defender
  protection) or **n/a** / informational. Community-verified consumer toggles
  whose exact key Microsoft does not document are tagged `unverified-ms` with a
  provenance note. A full-catalogue action-target collision scan then removed 12
  privacy entries and 3 security entries that duplicated existing
  perf / debloat / shell / updates / privacy controls (e.g. DiagTrack, Start
  suggestions, News-and-Interests, Recall, removable-drive BitLocker), and each
  file was adversarially re-verified against its research section. All Draft; the
  catalogue now ships 289 entries (107 Privacy, 150 Security, 22 Updates).
- Performance, debloat and shell catalogues (WP 4.4 / 4.5 / 4.6, research 05):
  143 new entries transcribed from the performance/debloat research tables.
  Debloat (`catalog/debloat/`): Microsoft inbox-app removals (`Appx`,
  `AllUsersAndDeprovision`, every `packageFamilyName` a verified
  `_8wekyb3d8bbwe` — no guessed publisher ids), Copilot / Teams / Dev Home /
  Cortana component removals and the device-metadata-reinstall block, and
  optional-feature / capability removals (Recall, PowerShell 2.0, WMIC, VBScript,
  WordPad, Internet Explorer, WMP legacy, XPS, Work Folders, Internet Printing).
  Performance (`catalog/performance/`): disable-able services (only
  DISABLE-OK / DISABLE-RECOMMENDED non-protected services, per-user services
  written via their template `Start` key), telemetry-adjacent scheduled tasks,
  real gaming tweaks (HAGS, Game DVR policy), and measurable visual / power /
  storage tweaks (`powercfg` and `fsutil` command actions, `PowerSetting` AC/DC
  toggles). Shell (`catalog/shell/`): taskbar, Start and Explorer UX toggles.
  The snake-oil / harmful set (research 05 §7 — timer-resolution hacks, Nagle /
  network-throttling knobs, `LargeSystemCache`, MSI-mode, registry cleaners) and
  every `NEVER` / `KEEP` / `SignatureKind = System` / protected-service row were
  excluded; third-party OEM stubs whose publisher id the research does not state
  were skipped rather than guessed. A full-catalogue collision scan removed nine
  cross-category duplicates (Cortana / Copilot / Dev Home / Teams authored twice,
  Delivery-Optimization and Start-tracking keys owned by other categories) and
  paired the two Windows-Ink-Workspace entries with `conflictsWith`; each file
  was adversarially re-verified (zero discrepancies). All Draft; the catalogue
  now ships 432 entries — Security 150, Privacy 107, Performance 61, Debloat
  61, Shell 31, Updates 22 — with a `BuiltInCatalogTests` theory asserting every
  category's M4 minimum.
- Three more Windows Update client-policy entries (research 03): get updates for
  other Microsoft products (`AllowMUUpdateService`), an 18-hour active-hours range
  (`SetActiveHoursMaxRange` / `ActiveHoursMaxRange`) and an auto-download /
  scheduled-daily-install mode (`AUOptions` = 4), the last paired with
  notify-before-install by `conflictsWith`. Updates reaches 25 and **all six
  catalogue categories now meet their M4 per-category minimums** (435 entries
  total: Security 150, Privacy 107, Performance 61, Debloat 61, Shell 31,
  Updates 25).
- M4 catalogue invariant tests (`BuiltInCatalogTests`): the built-in catalogue
  ships ≥ 300 entries and meets every per-category minimum; every security entry
  tagged `baseline` or `stig` declares a Policy CSP or Group Policy path; no entry
  reconfigures a protected service; every `Appx` removal carries a `reinstallHint`;
  and every `Verified` entry points at an evidence file that exists in the repo.
  (SEHOP, a registry-only mitigation with no Group Policy surface, now carries only
  its specific `stig-wn11-00-000150` provenance tag, not the generic `stig` tag.) `OpenTheWindows.Core.Engine.ApplyEngine`
  plans a run (dependency-ordered, flagging conflicts, managed settings, risk
  gating and not-applicable entries), then applies it journal-first — every prior
  state is written to the journal before the first machine change, each action is
  verified by re-reading, and any failure rolls back every already-applied action
  in reverse. `Revert` replays a prior run's captured state from the journal
  alone. `ActionApplier` executes all eight action kinds (capture / apply / verify
  / restore); code-level refusals (protected and protected-process services,
  disallowed executables) are enforced regardless of the catalogue. New writer
  interfaces in `Core.Abstractions` (registry, service, scheduled task, Appx,
  optional feature, Defender preference, power, command runner, restore point,
  Explorer restart, audit sink, pre-flight) and a hash-chained journal
  (`Core.Journal`, `docs/journal-format.md`). Value comparison is shared between
  scan and apply-verify (`StateComparison`). Windows adapters: `WindowsRegistryWriter`
  (policy values written through the Local GPO via COM `IGroupPolicyObject` and
  mirrored to the live hive so they survive `gpupdate`; preference and user-hive
  values written directly), `WindowsServiceWriter` (`ChangeServiceConfig`; refuses
  PPL and protected services — protected-process detection now populated in
  `WindowsServiceReader` via `QueryServiceConfig2`), `WindowsScheduledTaskWriter`,
  `WindowsAppxWriter`, `WindowsOptionalFeatureWriter` (drives the in-box `dism.exe`
  through the guarded command runner — deviation recorded in PLAN §7.1),
  `WindowsDefenderPreferenceWriter`, `WindowsPowerSettingWriter`, `WindowsCommandRunner`
  (the single sanctioned `Process` host), `WindowsJournalStore` (ProgramData with a
  restrictive ACL and hash chain), `WindowsRestorePointService` (`SRSetRestorePointW`,
  truthful Created/Skipped24h/Disabled), `WindowsAuditSink` (Windows Event Log +
  monthly JSONL, `docs/event-log.md`), `WindowsPreflight` and `WindowsExplorerRestarter`.
  New CLI: `otw apply`, `otw revert`, `otw history`. New package
  `System.Diagnostics.EventLog` 10.0.11. Coverage thresholds raised to 90 line /
  80 branch, with the OS-mutating writer/interop namespaces scoped out of coverage
  (verified by the `OTW_INTEGRATION` VM tests, not units).
- M2 (in progress): read-only detection engine core. `OpenTheWindows.Core.Abstractions`
  gains per-kind reader interfaces (registry, service, scheduled task, Appx,
  optional feature, Defender preference, power) plus interactive-user resolution,
  managed-setting detection and machine health probing, each with a snapshot
  record. `OpenTheWindows.Core.Engine.ScanEngine` compares each entry against the
  machine (per-kind compliance semantics, worst-of-actions aggregation,
  applicability short-circuit, interactive-user hive targeting) and produces a
  `ScanReport`. Consolidated `FakeReaders`/`FixedTimeProvider` test doubles and 28
  `ScanEngineTests`. First Windows reader: `WindowsRegistryReader` (64-bit view,
  `HKEY_USERS\<sid>` for the user hive — never `HKCU`; values materialised as JSON
  via `Utf8JsonWriter` so the trimmable project stays reflection-free), and
  `WindowsServiceReader` (start type incl. delayed auto-start from the registry,
  and run state, via `ServiceController`; new package
  `System.ServiceProcess.ServiceController` 10.0.11), and
  `WindowsScheduledTaskReader` (enabled state via the Task Scheduler 2.0 API;
  new package `TaskScheduler` 2.12.2; a missing task reads as `null`, which the
  engine treats as not-applicable), and `WindowsAppxReader` (per-user, all-users,
  provisioned and deprovisioned state via WinRT
  `Windows.Management.Deployment.PackageManager` plus the `Deprovisioned`
  registry key; the all-users / provisioned enumeration needs elevation and
  degrades to a documented user-scoped lower bound when denied, never a silent
  success), and `WindowsOptionalFeatureReader` (enabled state via WMI
  `Win32_OptionalFeature` — read-only and unelevated, chosen over the spec's
  DISM suggestion because a DISM online session needs elevation and can modify
  the image; deviation recorded in PLAN §7.1; new package `System.Management`
  10.0.11), and `WindowsDefenderPreferenceReader` (a named `MSFT_MpPreference`
  property read from WMI and materialised to JSON with `Utf8JsonWriter`;
  Defender absent or property unknown ⇒ `null`). The trim-safe JSON
  materialisation helper is now shared (`JsonMaterialization`) between the
  registry and Defender readers. `WindowsPowerSettingReader` reads a setting's
  AC/DC value indices for the active scheme via `powrprof.dll`
  (`PowerGetActiveScheme` / `PowerRead{AC,DC}ValueIndex`), declared with the
  `[LibraryImport]` source generator (chosen over the spec's CsWin32 — no
  build-time generator dependency; deviation recorded in PLAN §7.1); every
  P/Invoke pins `DllImportSearchPath.System32` (CA5392) and `AllowUnsafeBlocks`
  is enabled for the Windows project only, for the generated marshalling stubs.
  `WindowsInteractiveUserResolver` resolves the interactive (console/RDP)
  session's signed-in user via the Terminal Services API
  (`WTSQuerySessionInformation`) — never the elevated process token — and maps
  the account to a SID with the managed `NTAccount` translation, reporting
  whether `HKEY_USERS\<sid>` is loaded. All eight readers have tests that read
  the real registry / SCM / task store / package store / WMI / power scheme /
  session unelevated. `WindowsManagedSettingDetector` reports whether a registry
  value is owned by the Local Group Policy by reading the `Registry.pol` files
  through a new read-only PReg parser (`OpenTheWindows.Core.Policy.
  PolicyRegistryFile` + `PolicyRegistryEntry`) — chosen over an RSOP query
  because the `.pol` files are world-readable (no elevation) and deterministic;
  per-value MDM attribution and per-user MLGPO are deferred to the policy engine
  (M5), and a value that cannot be positively tied to a policy is reported as
  not-managed (the safe direction — it never hides real drift). The PReg parser
  has golden-fixture tests and the detector has deterministic tests over a
  temporary policy root (new shared `PRegFixture` test helper). This completes
  every read-only state reader for the M2 detection engine. Remaining M2 work
  (28-check machine health probe, JSON/CSV/HTML/SARIF report writers, `otw scan`
  / `otw health`, the no-write architectural guard, Stryker.NET, and VM
  integration) is still to come (see docs/milestones/M2-scan.md).

- M2: scan report writers (`OpenTheWindows.Core.Reports`). `IReportWriter` with
  four formats — `JsonReportWriter` (source-generated, camelCase, indented),
  `CsvReportWriter` (RFC 4180, `\n` line endings, one row per action),
  `HtmlReportWriter` (a single self-contained file: inline CSS, no external
  scripts/styles/fonts/images, so it renders offline as archivable evidence),
  and `SarifReportWriter` (SARIF 2.1.0; one rule per drifting id; result `level`
  derived from the entry's risk tier — Breaking ⇒ error, Advanced ⇒ warning,
  else note). `TweakObservation` now carries the entry's `RiskTier` so reports
  can express severity. Golden/validation tests cover all four (CSV exact-string
  golden, JSON/SARIF parse-and-assert, HTML self-containment).

- M2: read-only machine health probe (`WindowsMachineHealthProbe`,
  `IMachineHealthProbe`). The 28 checks from research 04 §5 — Secure Boot, TPM,
  Defender real-time/tamper, BitLocker, VBS/HVCI/Credential Guard, LSA PPL,
  kernel DMA, build support, local admins, join state, firewall, RDP, SMB,
  remote-management services, sudo/Developer Mode, Smart App Control,
  vulnerable-driver blocklist, core isolation, edition, Windows RE, host/
  port-proxy exposure, event-log size, Defender exclusions, local security
  policy, third-party AV and MDM enrolment — each reading only (registry,
  WMI, services). A check whose source needs elevation reports `Unknown` with an
  explanation rather than failing or inventing a value; every check is wrapped so
  the probe never throws. Tested against the real machine unelevated (28 unique
  checks, all with detail; the registry-backed checks return a real verdict).

- M2: `otw scan` and `otw health` CLI commands. `otw scan --profile
  <basic|balanced|strict|paranoid>` runs a read-only drift scan of a built-in
  level profile (`NamedProfile` selects Verified entries at or below the level;
  `--include-draft` adds drafts; `--catalog-dir` overrides) and prints a text
  summary or, with `--json`/`--csv`/`--html`/`--sarif` (and optional `--out`),
  a report; exit 0 compliant, 1 drift, 2 error, 3 unsupported platform.
  `otw health [--json]` runs the 28 read-only checks; exit 0. The CLI composes
  the real Windows readers into the `ScanEngine` and the health probe; command
  tests drive both through the parser with fake readers/probe (drift lists ids,
  unsupported exits 3, unknown profile and conflicting formats exit 2, report
  formats emit to stdout/file). New docs `docs/reports.md`; README documents
  both commands.

- M2: read-only architectural guard. A test (`ReadOnlyArchitectureTests`)
  metadata-scans the compiled Windows assembly's member references and fails if
  any mutating registry / service / package / task / file API is referenced
  (`RegistryKey.SetValue`/`Delete*`, `ServiceController.Start/Stop`,
  `PackageManager.Add/Remove/Stage/…`, task `Register`/`Delete`, `File`/
  `Directory` writes). It matches the declaring type and member, so ordinary
  `List.Add` and friends are not flagged. Proven to fail on an injected
  `SetValue` and pass once reverted (PLAN §7.2).

- M2: mutation-testing step wired (Stryker.NET 4.16.0, pinned in
  `.config/dotnet-tools.json` with `stryker-config.json` scoped to the M2 Core
  logic) as the opt-in `pwsh build/quality.ps1 -Only mutation` gate (report-only:
  `break=0`; never part of `all`; real threshold from M3). It is currently a
  **deferred gate**: Stryker does not yet support Microsoft.Testing.Platform test
  projects (stryker-net#3094) and this repo mandates MTP (no VSTest), so the gate
  detects that condition and reports DEFERRED rather than a spurious failure —
  any other Stryker error still fails it. Recorded in PLAN §7.1.

- M2: VM integration tests (`VmIntegrationTests`, gated by `OTW_INTEGRATION=1`
  via `Assert.SkipUnless`, skipped in normal runs): registry `CurrentBuild`
  matches the OS, the Print Spooler service and the built-in ScheduledDefrag task
  read back, interactive-user resolution returns the session user's SID, managed
  detection is NotManaged on an unmanaged machine, and the health probe returns
  all 28 checks. Verified end-to-end on the lab VM (Windows 11 Pro 25H2,
  build 26200.9168, elevated): `otw scan --profile balanced --include-draft`
  exits 1 and lists the drifting ids; setting a tweak's two policy values flips
  that entry to Compliant; `otw health` prints 28 checks with real elevated
  values (e.g. TPM and BitLocker return verdicts, not Unknown); and all four
  report formats write to file. The VM was reverted to its prior state
  afterwards.

- M1: catalogue model (`OpenTheWindows.Core.Catalog`), JSON Schema
  `catalog/schema/tweak.schema.json`, embedded loader with directory overrides
  and a structural validator (stable rule ids), 29 Draft entries across six
  categories, and read-only CLI `otw catalog list|show|validate`. Full test
  suite (built-in catalogue, one negative per validator rule, loader/override,
  applicability, edition mapping, serialization round-trip, a 1000-entry
  performance guard, and CLI command tests) and a `catalog` quality gate that
  validates the embedded catalogue and is proven to reject
  `tests/fixtures/catalog-bad`.

- Executable milestone specifications `docs/milestones/M1..M8` with a runbook
  and certification template (`docs/milestones/README.md`); catalogue
  authoring guide `docs/catalog-format.md`.
- Enforcement: `build/start-milestone.ps1` (prints rules + spec, records an
  acknowledgement), project-local Claude Code hooks (`.claude/settings.json`,
  `build/hooks/*.ps1`) that block code edits without an acknowledgement, git
  pre-commit hook (`.githooks/pre-commit`, enabled by `build/setup-dev.ps1`),
  and the `plan` consistency gate (`build/check-plan.ps1`) in `quality.ps1`
  and CI.
- AGENTS.md: mandatory sequence and analyzer expectations learned in M0/M1.

### Changed

- M4 acceptance #2 — machine-scope VM verification. 144 Basic/Balanced entries
  are now `Verified` on Windows 11 Pro 25H2 (26200.9168): each was applied alone
  (`otw apply --only <id>`), confirmed by an independent native read (registry /
  service / scheduled-task / optional-feature / Defender preference — never the
  CLI's own detection), reverted, and confirmed back at its pre-state. 131 by full
  round-trip, 13 already-compliant on 25H2. Evidence per entry in
  [docs/certification/M4/machine-scope-verification.md](docs/certification/M4/machine-scope-verification.md);
  the certification matrix and honest deviations are in
  [docs/certification/M4.md](docs/certification/M4.md). Of the 340 Basic/Balanced
  entries, 190 were exercisable over the headless SSH lab session (150 deferred:
  75 per-user hive, 47 network/remote-access that would sever the session, 28 Appx
  removals that are not round-trip reversible); of the 190, the 46 not verified are
  documented (Tamper Protection blocking Defender/ASR writes, TrustedInstaller-owned
  keys, components absent or edition-gated on this image, and Local-GPO
  `registry.pol` write contention). These verification findings are recorded in
  PLAN.md as candidate product hardening.
- PLAN.md section 8 is now an index/status board over the milestone specs.
- Milestone M2 (Scan) certified and marked DONE: `docs/certification/M2.md`
  records all 12 gates green (line 89.3% / branch 75.3%) and the five acceptance
  criteria verified end-to-end on the lab VM (Windows 11 Pro 25H2). PLAN.md
  section 8 and `docs/milestones/M2-scan.md` updated; the M1 status-board row was
  also corrected to DONE.

### Fixed

- `otw scan --only <id>` and `otw apply --only <id>` no longer require
  `--profile`. `--only` is the per-entry verification selector (M4 acceptance #2)
  and ignores the profile, but the parser still marked `--profile` required, so
  the documented `apply --only <id>` command failed with "Option '--profile' is
  required". `--profile` is now optional; it is required only in profile mode
  (no `--only`), and a run selected by `--only` alone is labelled `only:<id>` in
  the journal and scan report so history stays readable. Found while building the
  M4 VM-verification harness.
- `otw updates resume` now clears **every** active Open the Windows pause, not
  just the most recent one. When a user re-paused (for example `pause --days 7`
  then later `pause --days 60 --extended`) without resuming in between, the second
  pause captured the first pause's values as its own revert baseline; resume then
  reverted only the latest run and *restored the earlier pause*, leaving Windows
  Update still paused and `status` still reporting a pause. `UpdateControlService.Resume`
  now reverts all active pause runs newest-first, returning the six `UX\Settings`
  values to absent regardless of how many pauses stacked. Found during M4
  acceptance #3 on the lab VM (25H2); regressioned by
  `Resume_after_a_restacked_pause_clears_every_active_pause` (proven to fail
  against the single-revert implementation), which needed a new advanceable
  `MutableTimeProvider` test clock so the stacked runs journal distinct,
  strictly-increasing timestamps.
- `otw apply --what-if` now exits 0 (success) even when the previewed plan
  contains reboot-required entries. A dry run makes no machine changes, so it
  never itself requires a reboot; the would-be restart requirement is still
  reported in the text and JSON output, and only an actual apply returns the
  3010 reboot-required code. (Surfaced once the security catalogue added
  Basic-level reboot entries such as SEHOP and Early-Launch Antimalware.)
- Flaky PSScriptAnalyzer check: `Invoke-ScriptAnalyzer` (PSScriptAnalyzer 1.25)
  intermittently threw a `NullReferenceException` from a compatibility rule,
  failing at random (this reddened an otherwise green `main`). Both the
  `powershell` quality gate (`build/quality.ps1`) and the CI PSScriptAnalyzer
  step now retry the analysis on a thrown exception (a tool crash, not a
  finding); real findings are still reported and never retried.
- Intermittent Release build failures (RT0002 on the generated WPF
  `*_wpftmp.csproj`, cascading to BG1002 "DesktopApp.baml cannot be found").
  ReferenceTrimmer analysed the transient markup-compilation project — which
  omits the code-behind that uses Core/Windows — and reported those project
  references as removable, which is an error under warnings-as-errors. The temp
  project is now excluded from ReferenceTrimmer (Directory.Build.props); it
  still runs on the real App project. Recorded in PLAN.md §7.3.
- CI publish failed with NU1004 (locked restore vs RID/trim publish graph). Every
  project now declares `RuntimeIdentifiers` (Directory.Build.props) and the
  Windows/CLI projects are `IsTrimmable`, so `packages.lock.json` matches both
  build and publish restores; lock files regenerated.

## [0.1.0] - 2026-08-16

Milestone M0: repository scaffold, quality gates, research.

### Added

- Solution `OpenTheWindows.slnx` with `OpenTheWindows.Core` (net10.0),
  `OpenTheWindows.Windows`, `OpenTheWindows.Cli` (`otw.exe`),
  `OpenTheWindows.App` (WPF), `OpenTheWindows.TestSupport` and four xunit.v3
  test projects (43 tests, 88% line / 83% branch coverage).
- Core domain model: `Category`, `Level`, `RiskTier`, `Scope`,
  `RestartRequirement`, `TweakId` (validated, no regex), `OperatingSystemFacts`,
  `IOperatingSystemInfo`, `IElevationContext`, `DoctorService`/`SystemReport`,
  `ExitCodes` (pinned contract), `AppInfo`.
- Windows adapters: `WindowsOperatingSystemInfo` (registry `CurrentVersion`,
  Windows 11 product-name correction, UBR), `WindowsElevationContext`.
- CLI: `otw doctor [--json]`, `--version`, `--help`; `asInvoker` manifest with
  long-path and UTF-8 code page.
- GUI: elevated WPF shell (`DesktopApp` composition root with validated DI
  graph, `MainWindow` with the system check, Fluent theme, UIA automation ids).
- Quality gates wired in `build/quality.ps1` and `.github/workflows/ci.yml`:
  restore (NuGetAudit as errors, locked mode in CI), format, build (analyzers
  at maximum, banned APIs, unused references, dead code), test, coverage
  thresholds, gitleaks, semgrep, jscpd, markdownlint, PSScriptAnalyzer. Each
  gate verified to fail on a broken input (PLAN.md §7.2).
- Repository configuration: `Directory.Build.props/targets`, central package
  management with pinned versions, `NuGet.config` with source mapping,
  `.editorconfig`, `BannedSymbols.txt`, `global.json` (SDK 10.0.400 + MTP),
  `.gitleaks.toml`, `.jscpd.json`, `.markdownlint-cli2.jsonc`,
  `PSScriptAnalyzerSettings.psd1`, `.config/dotnet-tools.json`, `package.json`.
- Scripts: `build/publish.ps1` (single-file trimmed CLI 13 MB, self-contained
  app, ZIP, CycloneDX SBOM, SHA256SUMS), `build/smoke-gui.ps1` (self-elevating
  window screenshot), `build/vm.ps1` (lab VM copy/run/doctor).
- Documentation: `README.md`, `PLAN.md`, `SECURITY.md`, `CONTRIBUTING.md`,
  ADRs 0001–0004, seven research reports under `docs/research/`
  (telemetry/privacy, landscape/enterprise requirements, Windows Update
  control, security hardening, performance/debloat, stack verification,
  enterprise safety architecture), agent instruction files (`AGENTS.md`,
  `CLAUDE.md`, `.cursor/rules`, `.github/copilot-instructions.md`), Dependabot.

### Changed

- Product tagline removed the same day it was proposed (product decision); the
  product ships with the title only.
- Windows Update pausing/holding declared a first-class feature (PLAN.md D21);
  the refusal list narrowed to permanent Disabled start types and
  WaaSMedicSvc/UsoSvc ownership hacks. README and agent instructions updated.

### Verified

- `otw doctor` exit 0 on the dev machine (Windows 11 Pro 22H2, not elevated)
  and on the lab VM (Windows 11 Pro 25H2 build 26200.9168, elevated).
- GUI launched elevated and rendered the system check (screenshot captured).

[Unreleased]: https://github.com/RandyNorthrup/open-the-windows/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/RandyNorthrup/open-the-windows/releases/tag/v0.1.0
