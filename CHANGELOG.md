# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/). Entries record what happened.

## [Unreleased]

### Added

- M4 (security-policy action kind): a new `SecurityPolicy` catalogue action applies
  Windows account, password and lockout policy through `secedit` — the settings in
  the LSA security database that have no registry value or public API, and which
  the closed action set previously could not express (the M4 deferral). Apply stages
  a minimal security template for one setting and imports it with `secedit /configure`;
  revert replays the value captured beforehand (by parsing a `secedit /export`), so
  the round-trip is exact and reversible. All process launches go through the
  existing allow-listed command runner, and the template parse/build logic is
  factored into a unit-tested `SecurityTemplate` helper. Five entries ship under
  `catalog/security/account-policy.json`: enforce password history (24), minimum
  password age (1 day), minimum password length (14), password complexity, and
  account lockout (5 attempts / 15-minute lockout / 15-minute reset). VM-verified on
  Windows 11 Pro 25H2: each applies, is confirmed by an independent `secedit /export`,
  and reverts to the exact prior value — the lockout entry sets its three settings in
  order and restores them in reverse.

- M8 (audit, part 4 — GUI hooks): the Dashboard gains a **Security baselines** card
  that scores the machine against each built-in baseline (read-only, off the UI
  thread, on first activation) with a per-baseline progress bar, score and outcome
  summary, plus a **Re-score** action. The Security page gains a **Baseline** filter
  that narrows the list to the tweaks a selected baseline references (offered on the
  Security page only, since baselines map almost entirely to security controls). A
  new app-side `IAuditCoordinator` builds the `AuditEngine` lazily off the
  DI-validation path (mirroring the apply/update coordinators); the read-only
  `ScanEngine` wiring is now a shared `WindowsScanEngineFactory` used by both the
  CLI and the GUI.

- M8 (audit, part 3 — the `otw audit` command and fleet drop): `otw audit run
  --baseline <id|file> [--json|--csv|--html|--sarif] [--out] [--sign <pem>]` audits
  the machine against a baseline (read-only; no elevation required) and writes the
  report in the chosen format, defaulting to a text summary (score, counts and the
  failing rules) on stdout. `--sign` signs the JSON report with an ES256 PEM key
  and is refused for the non-JSON formats. `otw audit list` lists the built-in
  baselines. `otw task install --audit <id> --out <path> [--weekly|…]` registers a
  SYSTEM, hidden scheduled task (`\OpenTheWindows\Audit`) that runs the audit and
  drops the JSON report to `--out` (for example a fleet UNC share);
  `--profile` and `--audit` are mutually exclusive. The shared report-emit,
  EC-key-load and scheduled-task shape are factored into `CommandSupport` /
  `ScheduledTasks` and reused by the scan, profile and remediation paths.

- M8 (audit, part 2 — report exporters and signing): an audit report exports to
  four formats — JSON, CSV, HTML and SARIF (`IAuditReportWriter`). The JSON export
  wraps the report in a `SignedAuditReport` carrying a SHA-256 integrity hash over
  the canonical JSON and, when a signing key is supplied, an ES256 signature
  (reusing the profile-signing infrastructure); `AuditReportIntegrity.Verify`
  checks the hash and optional signature so a fleet collector can detect tampering.
  The SARIF export uses the baseline rule ids as SARIF rule ids and reports each
  failing rule with a severity-derived level; the HTML export is a single
  self-contained file with an executive summary (score and outcome counts) and one
  table per severity. The CSV and SARIF plumbing (RFC 4180 escaping, the SARIF
  document envelope) is factored into `CsvWriting` / `SarifEnvelope`, shared with
  the M2 scan writers.

- M8 (audit, part 1 — baselines and the audit engine): security baselines are a
  new kind of catalogue data under `catalog/baselines/*.json`, validated against
  `catalog/schema/baseline.schema.json`. Each baseline maps an external framework's
  control ids to the catalogue tweaks and read-only health checks that satisfy
  them. Three built-ins ship — **DISA Microsoft Windows 11 STIG V2R7**
  (`disa-stig-v2r7`), the **CIS Microsoft Windows 11 Enterprise Benchmark v4.0.0**
  (`cis-win11-ent-v4.0`), and the **Microsoft Windows 11 Security Baseline (25H2)**
  (`ms-baseline-25h2`); every rule's framework id and source URL was verified
  against the official DISA XCCDF, the CIS benchmark and Microsoft Learn, and every
  referenced tweak id and health-check id is validated to exist. `AuditEngine`
  scores the live machine against a baseline (read-only, over the existing
  `ScanEngine` and `IMachineHealthProbe`): each rule resolves to Pass / Fail /
  NotApplicable / Manual / Unknown with the actual observed evidence, and the report
  carries a 0–100 score weighted by severity (`AuditScoring`). The set of
  health-check ids now lives in Core (`HealthCheckIds`) as one vocabulary shared by
  the Windows probe and the baseline `checkOnly` rules. `otw audit validate`
  (`--baseline-dir`, `--json`) validates the baselines against the catalogue and the
  health-check ids; the catalog quality gate runs it and rejects a baseline that
  references an unknown tweak id (`tests/fixtures/baselines-bad`).
  [`docs/install.md`](docs/install.md) covers every channel (MSI, winget, portable
  and framework-dependent ZIPs), verifying SHA-256 sums and build-provenance
  attestations, silent install, upgrade, uninstall and data locations;
  [`docs/enterprise.md`](docs/enterprise.md) gains a **Re-sign and allow-list**
  section (signtool re-signing, AppLocker hash/publisher rules, and packaging the
  MSI as an Intune Win32 app with a version-based `otw.exe` detection rule); the
  README installation section is rewritten around the real channels. The MSI's
  install, in-place upgrade and uninstall were verified on the Windows 11 Pro 25H2
  VM: silent install, the Event Log source registered and removed by the custom
  action, PATH entry and Start-menu shortcut added and removed, a single Add/Remove
  Programs entry across the upgrade, and the audit trail preserved across
  uninstall.

- M7 (packaging, part 5 — release workflow): `.github/workflows/release.yml` runs
  on a `v*` tag (or manual dispatch). It re-runs the correctness gates (restore,
  build, test, coverage thresholds) on the tagged commit, packages the release
  (`build/package.ps1`), regenerates the winget manifest, produces
  `actions/attest-build-provenance` attestations for every asset (the compensating
  control for unsigned binaries, ADR 0003), publishes a GitHub Release with the
  CHANGELOG section as notes and the ZIPs/MSIs/SBOM/SHA256SUMS attached, and prints
  winget submission instructions in the job summary. Every action is pinned to a
  commit SHA; untrusted trigger context is read through `env:` to avoid run-step
  injection. `ci.yml`'s coverage class/file filters were synced to `quality.ps1`
  (M6 App-boundary and M7 Event-Log-installer exclusions) so CI coverage matches
  the local gate.

- M7 (packaging, part 4 — winget manifest): `build/winget-manifest.ps1` generates
  the winget multi-file manifest (`packaging/winget/`, schema 1.6.0) from the built
  MSIs — reading each `InstallerSha256` and `ProductCode` and templating the GitHub
  release `InstallerUrl` — with `InstallerType: wix`, `Scope: machine`, and silent
  install switches. `winget validate --manifest packaging/winget` passes. The
  committed manifest is a template; the release workflow regenerates it against the
  real release assets.

- M7 (packaging, parts 2-3 — MSI and release packager): a WiX 5 installer under
  `installer/` builds a per-machine MSI that lays the app down under
  `%ProgramFiles%\Open the Windows\{app,cli}`, adds a Start-menu shortcut and an
  optional PATH entry for `otw.exe`, registers the Event Log source by running the
  installed CLI (`otw eventlog install`, non-fatal), and always preserves the
  audit trail under `%ProgramData%\OpenTheWindows` across uninstall (see part 6). A
  major upgrade replaces any earlier version in place (one Add/Remove Programs
  entry); silent install is `msiexec /i <msi> /qn`. A new `build/package.ps1`
  produces the full release set: self-contained portable ZIPs (x64 + arm64), the
  per-machine MSI(s), a framework-dependent x64 ZIP (needs the .NET 10 Desktop
  Runtime), a CycloneDX SBOM and a `SHA256SUMS.txt`. WiX 5 is used deliberately:
  WiX 6/7 require accepting FireGiant's paid Open Source Maintenance Fee EULA
  (error WIX7015), and the project stays fee-free and MIT-compatible (ADR 0003).

- M7 (packaging, part 1 — Event Log source command): a new
  `otw eventlog install|uninstall|status` command registers or removes the Open
  the Windows Event Log source (log `OpenTheWindows`, source `OTW`) that audit
  events are written under, so the source can be created at install time (the MSI
  runs `otw eventlog install` as a deferred custom action) rather than only on the
  first elevated operation. `install` and `uninstall` need elevation; `status` is
  a read. The command and the audit sink now share one `IEventLogInstaller`
  implementation (`WindowsEventLogInstaller`), removing the duplicated log/source
  names and registration logic that previously lived in the audit sink.

- M6 (GUI, part 7 — settings): a new **Settings** page, the last of the eleven
  shell pages. Its toggles are wired to real behaviour, not decoration: the
  restore-point and automatic-Explorer-restart choices flow into every apply,
  "show draft entries" seeds the category pages, and enterprise mode hides the
  consumer-only built-in profiles (`home`, `gamer`). The theme choice
  (System / Light / Dark) applies immediately via the WPF Fluent `ThemeMode`.
  Language is en-US only in M6 over the existing resx infrastructure. Settings
  are shared through an `AppSettings` singleton and are session-scoped
  (persistence is a follow-up).
- M6 (GUI, part 6 — updates page): a new **Updates** page shows the running
  release and how close it is to end-of-servicing (flagging the 90-day warning
  window), whether updates are paused and until when, and any pinned release. It
  offers quick pause presets within the Windows-supported 35-day maximum, an
  extended pause beyond it behind an explicit warning, and a resume action — all
  run on a background thread over the existing `UpdateControlService`. When the
  pause is owned by organisation policy (a what-if pause plans as Managed) the
  controls switch to a read-only banner, never fighting Group Policy.
- M6 (GUI, part 5 — profiles): a new **Profiles** page lists the built-in
  profiles, each with its scope, signing state and the number of entries it
  resolves to on this machine. **Review & apply…** resolves the selected profile
  and opens the same review-and-apply flow as the category pages. **Export…**
  writes a profile to a `.json` file, and **Import…** loads a profile file,
  validates it against the schema and the catalogue, and reports its signing
  state — unsigned, signed by an untrusted key, signed by a trusted key, or an
  invalid signature — before it can be applied.
- M6 (GUI, part 4 — history and revert): a new **History** page lists the prior
  runs (newest first) with their time, profile, result and entry count, and
  reverts a selected completed run — restoring the values it changed — through
  the engine's transactional revert, on a background thread and behind a
  confirmation. Runs load when the page is shown (a new `IActivatable` hook the
  navigation service calls), so nothing reads the journal until the page is
  opened. This completes the apply→revert round trip from the GUI.
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

- Repository reorganised so the quality gates verify code, build and tests only —
  never the existence of a document. Removed the `plan` gate and its
  `build/check-plan.ps1`; the CI and pre-commit steps that ran it are gone.
- Decoupled the catalogue from documents: the `verifiedOn.evidence` field (which
  pointed every verified entry at a certification markdown file) is removed from
  the schema, the `Verification` model, the validator, all 144 verified entries
  and the tests. Verification is now recorded as build/edition/date alone.
- Split the README: it is now product/user-facing, and all developer, build,
  quality-gate, project-structure and troubleshooting detail moved to
  `CONTRIBUTING.md`.
- Rewrote `AGENTS.md`, `CLAUDE.md` and the Cursor rules to drop the milestone
  runbook process, and added two standing rules — never add an analyzer
  suppression without explicit approval, and never remove or disable app
  functionality to make a build or gate pass.
- M6 (GUI polish — button-up from live testing): the desktop app was reworked for
  clarity and to stop reading the machine on its own.
  - **Nothing scans on load.** The Dashboard's baseline **audit** and the Updates
    **status** no longer run when their page opens; the user starts them with the
    “Score this machine” and “Refresh status” buttons. The only automatic read left
    is the lightweight Dashboard system-check (Windows version, edition, elevation),
    which is environment detection, not a tweak/update scan; it also has a Re-check
    button. No `ScanEngine`/`AuditEngine` work happens until the user asks.
  - **Meaningful tooltips everywhere.** Every interactive control (level dial,
    search, risk/baseline filters, Drafts, Review & apply, pause presets, resume,
    extended pause, settings toggles, history/profile actions) and every list badge
    (risk, restart, draft, signature) has a plain-language tooltip. Tooltips explain
    the control or the badge concept and point to the detail pane for an entry's
    specifics — they do not duplicate the detail pane, which stays the source of
    truth and carries no tooltips. Tooltips on disabled controls (managed Updates,
    Review & apply, Revert, Export) explain *why* they are disabled.
  - **Useful Dashboard quick actions.** The single redundant “About” quick action
    (which duplicated the About page) was replaced with task shortcuts: Review
    privacy, Pause Windows Update, Harden security, Apply a profile, View history.
  - **One scrollbar per region.** The shell no longer wraps every page in a second
    scroll viewer; each page owns its own scrolling, so a page that already scrolls
    its own content (Updates, Settings) no longer shows a second, detached scrollbar,
    and the two-pane pages (categories, Profiles, History) scroll their list and
    detail panes internally instead of the whole window.
  - **A shared visual language** (`Resources/Styles.xaml`): page titles, section
    headers, cards, field labels and muted text are defined once so the pages read
    as one product.
- M6 (GUI): removed the per-entry verification-status display from the Security/
  category pages — the `✓ <build>` chip on each list row and the “Verified on”
  build/edition/date block in the detail pane. That is internal QA/certification
  provenance (raw Windows build numbers and test dates), not information a user
  acts on; the catalogue still records `verifiedOn` and the gates still enforce it.
- M6 (GUI): the Profiles detail pane no longer repeats the list row. The list row
  carries the name, signing badge, scope and change count; the detail pane now leads
  with the description and breaks the profile down into a **What it changes** list (the
  affected category, its level and change count per category) and a **How it applies**
  list (risk ceiling, drafts, restore point, Explorer restart, include/exclude counts),
  above the applies-to / signing / changes summary — so the two views are complementary
  rather than duplicated.
- Local builds report the plain version (e.g. `0.1.0`) instead of `0.1.0-local`;
  the developer and the shipped build now carry the same version string (the
  `-local` pre-release tag only confused users).
- M7: the MSI now **always** preserves `%ProgramData%\OpenTheWindows` (journals and
  audit) on uninstall; the `REMOVEDATA=1` purge switch was removed. Component
  install-state cannot be driven by an uninstall-time property, so the switch never
  purged in testing — and, more importantly, an installer flag that silently wipes
  the audit trail of an elevated security tool (`msiexec /x … REMOVEDATA=1 /qn`) is
  a liability, not a feature. Removing the data is now a deliberate manual step
  (documented in `docs/install.md`). This also drops the unused
  `WixToolset.Util.wixext` dependency.
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

### Removed

- Deleted the `docs/` tree (milestone specs, certification records, research and
  reference docs), `PLAN.md`, `build/check-plan.ps1`, `build/start-milestone.ps1`,
  and the Claude Code pre-edit/session-start hook scripts — the documentation-driven
  milestone apparatus. `.claude/settings.json` no longer wires those hooks.
- Removed the Intune Remediations scripts; scheduled enforcement remains
  available through `otw task install`.

### Fixed

- `build/setup-dev.ps1` no longer points at the removed milestone apparatus: it
  dropped the `plan` gate from its pre-commit description and replaced the
  `build/start-milestone.ps1` "next step" (that script was deleted) with
  `build/quality.ps1`. It still sets `core.hooksPath=.githooks`, so the
  pre-commit gate is wired on a fresh clone.
- M6 (GUI): controls and detail panes were clipped on the right edge — the Profiles and
  category two-pane layouts, the category toolbar's **Review & apply** button, and the
  navigation-rail title were all cut off. The cause was layout overflow, not text that
  needed wrapping: fixed-width grid columns with large minimum widths, and horizontal
  `StackPanel`s (which measure children at infinite width), overran the window. The
  two-pane bodies now use star-sized columns, the category page moves its selection
  summary and **Review & apply** button up to the title row so the toolbar no longer
  overflows, and the navigation-rail header is a `DockPanel` that bounds the title so it
  fits the rail.
- M6 (GUI): the dark Fluent theme rendered several surfaces with light-theme colours —
  the navigation-rail fill, card and detail-pane borders, the Updates “managed” banner
  and the link/warning text used legacy `SystemColors.*` brushes that do not track the
  Fluent theme. WPF has no `ResourceDictionary.ThemeDictionaries` (that is a WinUI
  feature), so the app now defines theme-agnostic translucent-grey brushes in
  `Resources/Styles.xaml` — a semi-transparent grey lightens a dark surface and darkens
  a light one, so a single definition is correct on both themes without theme
  detection — and the managed banner's text inherits the theme foreground.
- M6 (GUI): navigating from a quick action left the navigation rail highlighting a
  stale page, and clicking the already-highlighted entry did nothing — so there was
  no way back to the page you jumped from. The rail now follows the page the shell
  actually shows (including programmatic navigations), and a **Back** button returns
  to the previous page via a navigation back-stack.
- M6 (GUI): a section whose entries are all still drafts (every Shell entry today)
  showed an empty list with no explanation. Category pages now show a hint that
  explains the section is all drafts and how to reveal them, instead of looking
  broken; the message also covers the “no edition match” and “no search/filter
  match” cases.
- M4 (Defender ASR under Tamper Protection): the 18 Attack Surface Reduction rules
  now apply through the Local Group Policy registry
  (`SOFTWARE\Policies\Microsoft\Windows Defender\Windows Defender Exploit Guard\ASR\Rules\<GUID>`,
  the per-rule value carrying the block/audit action) instead of `Set-MpPreference`.
  Tamper Protection blocks the `Set-MpPreference` ASR channel — the write is rolled
  back — but the Group Policy ASR surface is honoured even with Tamper Protection
  on, so the rules are now applicable on a default machine rather than failing. Each
  entry is a single Registry Policy action (the per-GUID rule value is sufficient;
  no shared `ExploitGuard_ASR_Rules` enabler, so reverting one rule never disturbs
  another). VM-verified on Windows 11 Pro 25H2 with Tamper Protection ON: each rule
  applies, becomes effective in `Get-MpPreference` with its action preserved, and
  reverts cleanly.

- M4 (Defender preferences typed correctly): the Microsoft Defender boolean
  preferences (`DisableRealtimeMonitoring`, `DisableBehaviorMonitoring`,
  `DisableIOAVProtection`, `DisableScriptScanning`, `DisableBlockAtFirstSeen`,
  `DisableRemovableDriveScanning`, `DisableArchiveScanning`, `DisableEmailScanning`,
  `CheckForSignaturesBeforeRunningScan`) now carry JSON boolean values instead of
  `0`/`1`. `MSFT_MpPreference` exposes these as CIM booleans, so the WMI read
  returns `true`/`false`; comparing that against an integer desired value reported
  false drift, and the integer then failed the WMI `Set` (wrong type) so the apply
  rolled back. With the values typed as booleans,
  `security.defender.real-time-protection` reads compliant when protection is
  already on, and `cloud-protection` / `scan-options` apply and revert cleanly under
  Tamper Protection. VM-verified on 25H2.

- M4 (catalogue verification): `otw scan` no longer reports a policy-managed
  registry value as compliant when the effective value does not match the desired
  data. A value recorded in the Local GPO `Registry.pol` but absent or different in
  the live hive — the inconsistent state a policy write whose revert did not finish
  leaves behind — was short-circuited to "managed" and counted as satisfied, so a
  half-reverted policy value could read as "in the desired state". A managed value
  is now compliant only when the live value actually matches; otherwise it is drift,
  while still recording that Group Policy owns it so planning keeps deferring to
  policy. Reproduced and confirmed fixed on the Windows 11 Pro 25H2 VM by driving a
  policy value into that split state and re-scanning; resolves the
  `security.media.dma-disable-under-lock` scan discrepancy noted during M4
  verification.

- M4 (Local Group Policy writes): a machine policy write or delete now retries a
  brief `Registry.pol` sharing violation (`0x80070020`, `ERROR_SHARING_VIOLATION`)
  with a short backoff instead of failing the whole run. Two elevated writers
  touching the policy file at the same moment — a concurrent `gpupdate`, the
  registry policy client-side extension, or another Open the Windows invocation —
  lock it only briefly; only a persistent lock, or any non-sharing failure, now
  propagates. This removes the Local-GPO revert-contention failures seen during the
  M4 batch verification, which were the source of the split policy/live states the
  scan fix above now also reports honestly.

- M8 (audit): a policy-managed setting is now scored by its value, not treated as
  a blanket "Manual". A control enforced by Group Policy at the desired value is a
  **Pass** (the control is satisfied); enforced at a different value it is a
  **Fail** (a policy is enforcing a non-compliant value). Previously any managed
  setting mapped to Manual, so applying a policy-backed hardening tweak did not
  show as Pass in the audit. Found during the Windows 11 Pro 25H2 VM verification.

- Publishing the CLI single-file with trimming was broken (IL2104 → NETSDK1144):
  the tool's Windows interop (WMI via `System.Management`, `TaskScheduler`,
  WinRT/Appx) reaches members through reflection the trimmer cannot prove
  reachable, so `PublishTrimmed=true` failed the build under warnings-as-errors —
  and, had it linked, could have removed members the tool needs at runtime. The
  CLI now publishes single-file self-contained but **untrimmed** (~100 MB); the
  framework-dependent ZIP stays small for those who have the runtime.
  `build/publish.ps1` (the CI publish smoke) is updated to match. Found while
  building the M7 release packager.
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
