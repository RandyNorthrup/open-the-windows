# Open the Windows

Free, open-source (MIT), enterprise-grade Windows 11 control panel for
**privacy**, **Windows Update**, **security hardening**, **performance** and
**debloat** — with levels per category, per-tweak risk badges, full
reversibility, drift detection and remediation, and an audit trail. GUI
(WPF) and CLI (`otw.exe`) over one shared engine.

> Status: **all milestones M0–M8 complete and certified** — engine, catalogue,
> profiles/enterprise, GUI, packaging and audit — see [PLAN.md](PLAN.md) and
> [docs/certification/](docs/certification/). The catalogue ships **440 entries**
> (`otw catalog validate` green); the CLI and GUI scan, apply, revert and inspect
> history, audit against security baselines (STIG/CIS/Microsoft), and pause/resume
> Windows Update. Machine-scope Basic/Balanced entries are `Verified` on Windows 11
> 25H2 where the lab VM permits — including the Group-Policy Attack Surface
> Reduction rules and the `secedit` account/password/lockout policy (evidence under
> [docs/certification/M4/](docs/certification/M4/)); the rest ship as `Draft` until
> verified. Only `Verified` entries apply unless you pass `--include-draft`.

## What it will do (and what it will never do)

- Turn off telemetry/diagnostic data, ads, activity history, Recall/Copilot
  features, and similar tracking — selectively, by level.
- Pause / hold / defer / target Windows Update with guardrails: 35-day pause
  by default, an extended pause (Advanced tier, with warnings and a
  configurable ceiling), end-of-servicing warnings, per-update hide, and a
  one-click "resume now".
- Apply security hardening mapped to the Microsoft Security Baseline, DISA STIG
  and CIS section IDs; read-only compliance scoring.
- Apply measurable performance tweaks and remove inbox bloat safely
  (with the exact "how it comes back" knowledge for each package).
- Journal every change first, verify after, roll back on failure, revert on
  demand; detect drift and remediate on a schedule; write to the Windows Event
  Log.
- **Never** permanently break Windows Update (no `WaaSMedicSvc`/`wuauserv`
  ACL hacks or Disabled start types — Windows reverts them and they break the
  Store and Defender), never disable Defender, UAC or protected services;
  never hosts-block Microsoft endpoints; never fight MDM/GPO managed settings
  without an explicit break-glass flag; never ship placebo tweaks.

The research behind every category is in [docs/research/](docs/research/).

## Stack

.NET 10 (LTS) · WPF (Fluent) · System.CommandLine 2.0 · xunit.v3 on
Microsoft.Testing.Platform · WiX 7 (planned) · Windows 11 21H2+ (x64, ARM64)

Layering: `OpenTheWindows.Core` (pure .NET, all logic) → `OpenTheWindows.Windows`
(the only project that touches the OS) → `OpenTheWindows.Cli` / `OpenTheWindows.App`.
See [docs/adr/](docs/adr/).

## Requirements

- Windows 11 (build 22000+) client edition. Windows Server is refused.
- To build: .NET SDK 10.0.400 (`global.json`), Node 22 (repo tooling only),
  PowerShell 7.4+, Git. Optional for the full gate set: Python 3.9+ (semgrep),
  gitleaks (`winget install Gitleaks.Gitleaks`).
- Visual Studio 2022 cannot load SDK 10.0.400 solutions; use the CLI, VS Code
  or Visual Studio 2026.

## Installation (end users)

Each `v*` release publishes three channels — a per-machine **MSI**, the
**winget** package `RandyNorthrup.OpenTheWindows`, and a **portable ZIP** (plus a
smaller framework-dependent ZIP). All are self-contained except the `-fdd` ZIP.

```powershell
winget install RandyNorthrup.OpenTheWindows          # or download the MSI / ZIP
msiexec /i OpenTheWindows-<version>-win-x64.msi /qn  # silent, per-machine
```

Binaries are not code-signed (see
[ADR 0003](docs/adr/0003-unsigned-distribution.md)); every release carries
SHA-256 sums, a CycloneDX SBOM and GitHub build-provenance attestations you can
verify with `gh attestation verify`. First run of an unsigned download shows a
SmartScreen prompt (**More info → Run anyway**). Full instructions — silent
install, upgrade, uninstall, verifying hashes and attestations, and where data
lives — are in **[docs/install.md](docs/install.md)**; enterprise re-signing and
allow-listing are in **[docs/enterprise.md](docs/enterprise.md)**. To build from
source instead, see [Development](#development).

## Development

```powershell
git clone https://github.com/RandyNorthrup/open-the-windows.git
cd open-the-windows
npm ci                                  # repo tooling (jscpd, markdownlint)
dotnet tool restore                     # reportgenerator, CycloneDX
dotnet restore OpenTheWindows.slnx
dotnet build OpenTheWindows.slnx -c Release
dotnet test OpenTheWindows.slnx -c Release --no-build
```

Run the CLI and the GUI from the build output:

```powershell
artifacts\bin\OpenTheWindows.Cli\release\otw.exe doctor
artifacts\bin\OpenTheWindows.Cli\release\otw.exe doctor --json
artifacts\bin\OpenTheWindows.Cli\release\otw.exe catalog list --category Privacy --level Basic
artifacts\bin\OpenTheWindows.Cli\release\otw.exe catalog show privacy.advertising-id.disable
artifacts\bin\OpenTheWindows.Cli\release\otw.exe catalog validate
artifacts\bin\OpenTheWindows.App\release\OpenTheWindows.exe   # elevates (UAC)
```

### The GUI

`OpenTheWindows.exe` is the WPF desktop app over the same engine as the CLI. It
opens on a **Dashboard** (system health and quick actions) with a left
navigation rail to the category pages (**Privacy**, **Updates**, **Security**,
**Performance**, **Debloat**, **Shell**), **Profiles**, **History**, **Settings**
and **About**. A category page has a level dial, search, risk/draft filters and a
per-entry detail pane; **Review & apply…** opens a modal flow that previews the
what-if plan, confirms Breaking entries by typing their id, applies
transactionally on a background thread, and lets you revert from **History**.
**Updates** pauses/resumes Windows Update (with an extended pause behind a
warning, and a read-only mode when it is managed by policy); **Profiles**
applies, imports and exports profiles with their signing state. See
[docs/gui.md](docs/gui.md) for the page map and the `build/ui-smoke.ps1` smoke
harness.

`otw doctor` prints OS/edition/build, whether the process is elevated, and the
support verdict; exit code 0 = supported, 3 = unsupported platform. Elevation
does not change the exit code.

`otw catalog` inspects the built-in tweak catalogue (read-only): `list`
(optionally `--category`/`--level`, or `--json`), `show <id>` (`--json` for the
raw entry), and `validate` (schema + structural rules; `--catalog-dir` adds an
operator override directory). Exit 0 when valid, 4 on invalid input.

`otw profile` inspects, validates and signs profiles: `list` shows the seven
built-in profiles (`--json` for the array), `show <id|path>` prints one profile's
level dial, include/exclude, scope, options and applicability (a built-in id such
as `home`, or a path to a profile `.json` file; `--json` for the raw object), and
`validate <id|path>` checks a profile against `catalog/schema/profile.schema.json`
and against the catalogue (include/exclude ids must exist, built-ins never apply a
Breaking entry; `--catalog-dir` adds an override directory, `--json` emits the
issue array). `sign <file> --key <pem>` writes a detached ES256 (ECDSA P-256)
signature next to the profile (`<file>.sig`, or `--out`); `verify <file> [--key
<pem>]` checks that signature against a public key or, by default, the machine
trust store (`%ProgramData%\OpenTheWindows\trusted-keys\*.pem`). Signatures are
taken over a canonical form of the JSON, so they survive re-formatting. Exit 0
when valid/verified, 4 on invalid input or a signature that does not verify. A
machine can *require* signatures with the Group Policy setting
`RequireSignedProfiles` (`HKLM\SOFTWARE\Policies\OpenTheWindows`, shipped as
`admx/OpenTheWindows.admx`): when it is on, `scan`/`apply` refuse a profile
**file** that is unsigned or not signed by a trusted key, while built-in ids and
level names stay exempt. The full format, resolution, validation, signing and
policy reference is [docs/profile-format.md](docs/profile-format.md).

`otw scan --profile <value>` runs a read-only drift scan against the machine —
it never changes anything. `--profile` accepts a **level** (`basic`, `balanced`,
`strict`, `paranoid`, applied uniformly), a **built-in profile id** (`home`,
`enterprise-workstation`, … — see `otw profile list`), or a **path to a profile
`.json` file**; a named profile carries its own per-category dials, include/
exclude, risk gate and Draft setting. `--include-draft` also scans Draft entries
(level profiles only); `--catalog-dir` adds an override directory; `--json`, `--csv`, `--html` or `--sarif` (with optional
`--out <file>`) write a report instead of the text summary. Exit 0 = compliant,
1 = drift, 2 = error, 3 = unsupported platform. Report formats are documented in
[docs/reports.md](docs/reports.md).

`otw health [--json]` runs the read-only machine health checks (Secure Boot,
TPM, Defender, BitLocker, firewall, and more — research 04 §5). It only reads;
checks whose source needs elevation report `Unknown`. Exit 0 unless the command
itself errors.

`otw audit run --baseline <id|file>` scores the machine against a **security
baseline** (read-only, no elevation needed): `disa-stig-v2r7`,
`cis-win11-ent-v4.0` or `ms-baseline-25h2` (see `otw audit list`), or a path to a
baseline `.json`. Each rule maps a framework control id to the catalogue tweaks and
health checks that satisfy it, resolving to Pass / Fail / NotApplicable / Manual /
Unknown; the report carries a 0–100 score weighted by severity. `--json` (with a
SHA-256 integrity hash, plus `--sign <key.pem>` for an ES256 signature), `--csv`,
`--html` or `--sarif` (with optional `--out`) write the report; otherwise a text
summary prints. Exit 0 = nothing failed, 1 = failures found. `otw audit validate`
validates the baselines against the catalogue and the health-check ids. Baselines
and scoring are documented in [docs/audit.md](docs/audit.md).

`otw apply --profile <value>` **applies** a profile transactionally (elevated),
where `--profile` accepts the same three forms as `scan` (a level, a built-in
profile id, or a profile `.json` file). It journals every prior state before
changing anything, verifies each change by re-reading it, and rolls the whole run
back if any step fails. `--what-if` plans without changing anything; `--include-draft`
also applies Draft entries; a System Restore point is created first unless
`--no-restore-point`; `--allow-advanced` / `--allow-breaking` permit higher-risk
entries (Breaking also needs typed confirmation unless `--yes`); `--break-glass`
overrides MDM/Group-Policy-managed settings (audited); `--restart-explorer`
restarts the shell for entries that need it; `--json` emits a stable result
document. Exit 0 = applied, 3010 = applied but a reboot is required, 2 = error
(rolled back), 3 = unsupported platform, 4 = invalid input / conflict, 5 =
elevation required. When `--profile` resolves to a real profile (a built-in id or
a file), that profile's `options` seed these defaults — restore point, restart
Explorer, allow-advanced and allow-breaking — and the flags above still override.
Journals live in `%ProgramData%\OpenTheWindows\journal`
([docs/journal-format.md](docs/journal-format.md)); the audit trail goes to the
Windows Event Log and JSONL ([docs/event-log.md](docs/event-log.md)).

`otw remediate --profile <value>` is the **unattended** form of apply, meant for
automation (the drift scheduled task and Intune): it scans, then applies only the
drifted entries, with no interactive confirmation, and never break-glasses a
managed setting. `--out <file>` writes the stable JSON report (creating the
directory) — for example the task's `last-remediate.json` — otherwise `--json`
writes it to stdout. It takes the same `--profile`, `--include-draft`,
`--no-restore-point`, `--allow-advanced`, `--allow-breaking`, `--restart-explorer`
and `--what-if` options, and returns the same exit codes as `apply`. With
`--if-build-changed` it first compares the running OS build/UBR against the most
recent journal and exits 0 without changes when the build is unchanged — this is
how the drift task re-applies only after a feature update (with no prior run to
compare against, it applies).

`otw task install --profile <value> [--daily|--weekly|--on-logon|--on-build-change]`
registers a scheduled task `\OpenTheWindows\Remediate` that runs `otw remediate`
for that profile — SYSTEM account, highest privileges, hidden, on the chosen
schedule (daily by default; `--on-build-change` registers a boot-triggered task
that runs `remediate --if-build-changed`, re-applying only after a feature
update) — and writes its report to
`%ProgramData%\OpenTheWindows\reports\last-remediate.json`. `otw task remove`
deletes it. Both need elevation. Exit 0 on success, 4 for an unknown profile or
conflicting schedule flags, 5 when not elevated. `otw task install --audit <id>
--out <path>` instead registers a `\OpenTheWindows\Audit` task that runs `otw audit
run` for a baseline and drops the JSON report to `--out` (for example a fleet UNC
share); `--profile` and `--audit` are mutually exclusive.

When an **all-users** profile (`scope: AllUsers`) is applied, `apply` and
`remediate` also register a per-user **Active Setup logon fallback**
(`HKLM\SOFTWARE\Microsoft\Active Setup\Installed Components\{OTW-<profileId>}`)
whose stub runs `otw remediate --user-scope --profile <id>` in each user's own
session at their next sign-in — the standard provisioning mechanism that reaches
users who were logged off, or created after the apply, and which needs no
elevation (writing one's own hive is not an administrator action). `--user-scope`
applies only the profile's user-scope entries to the calling user's own hive;
elevation is required by what a run writes, so a user-scope run is exempt while
machine and all-users runs still need it. Registration bumps the component's
version each apply (so the stub re-runs), is skipped during OOBE, and `otw task
remove` clears the fallback along with the drift task.

For a managed fleet, [docs/enterprise.md](docs/enterprise.md) is the deployment
guide: the paired Intune Remediations scripts
[docs/enterprise/intune-detect.ps1](docs/enterprise/intune-detect.ps1) and
[docs/enterprise/intune-remediate.ps1](docs/enterprise/intune-remediate.ps1)
(wrapping `otw scan`/`remediate` and mapping the exit codes onto Intune's
detection and remediation contracts), signed profiles with the
`RequireSignedProfiles` policy and its ADMX, and WDAC allow-listing of `otw.exe`.

`otw revert <runId|last> [--what-if] [--json]` restores the state captured before
a prior run, journaling a new revert run. `otw history [--json]` lists prior
apply and revert runs, newest first. `otw users [--json]` lists the real user
profiles on the machine (the per-user hives an all-users apply would target) and
whether each hive is currently loaded; it is read-only. `scan` and `apply` also take `--only <id>`
to operate on a single catalogue entry (including Draft) for per-entry
verification.

`otw updates` controls Windows Update safely and reversibly (research 03):
`pause --days <1-35> [--extended] [--what-if]` writes the Settings-app pause
values (the six `UX\Settings` timestamps) so updates stop for that window — the
default cap is Microsoft's 35 days, and `--extended` raises it up to a
configurable ceiling as an Advanced-risk change with a warning and a Windows
Event 4002; `resume [--what-if]` clears an Open the Windows pause and triggers a
fresh scan; `status` shows the running version, its days to end-of-servicing
(with a 90-day warning), the pause state and any `TargetReleaseVersion` pin.
Pause and resume need elevation and are journaled/reversible; status is
read-only. What the app **refuses** to do to Windows Update (and Defender,
services, the shell) is listed in [docs/refusals.md](docs/refusals.md); placebo
and low-value "optimisations" it deliberately leaves out are in
[docs/not-included.md](docs/not-included.md).

Publish (single-file trimmed CLI + self-contained app + SBOM + hashes):

```powershell
pwsh build/publish.ps1 -Runtime win-x64        # or omit -Runtime for x64 + arm64
```

## Working on a milestone (humans and agents)

The plan is executable: `PLAN.md` §8 indexes one specification per milestone
under `docs/milestones/`, and `docs/milestones/README.md` is the runbook.
Enforcement is built in:

```powershell
pwsh build/setup-dev.ps1                          # once per clone: git hooks (format/build/test/plan + CHANGELOG rule)
pwsh build/start-milestone.ps1 -Milestone M1      # prints AGENTS.md + runbook + spec, records the acknowledgement
pwsh build/quality.ps1                            # all gates incl. `plan` (PLAN/spec/certification consistency)
```

The project-local Claude Code hook (`.claude/settings.json`) blocks edits under
`src/`, `tests/`, `catalog/`, `build/` until `start-milestone.ps1` has been run
in the last 12 hours; CI and the git pre-commit hook run the `plan` gate.

## Quality gates

One command runs everything and fails on any blocking issue:

```powershell
pwsh build/quality.ps1            # all gates
pwsh build/quality.ps1 -Ci        # locked restore (what CI runs)
pwsh build/quality.ps1 -Only test # a single gate
```

| Gate | Command | What makes it a gate |
| ------ | --------- | ---------------------- |
| restore | `dotnet restore OpenTheWindows.slnx [--locked-mode]` | `NuGetAudit` (all, low) + `TreatWarningsAsErrors` → vulnerable/deprecated packages fail restore (verified with a known-vulnerable package) |
| format | `dotnet format OpenTheWindows.slnx --verify-no-changes` | exit 2 on any whitespace/style drift |
| build | `dotnet build OpenTheWindows.slnx -c Release` | `TreatWarningsAsErrors`, `AnalysisLevel latest-all`, `EnforceCodeStyleInBuild`, Roslynator + Sonar + Meziantou + IDisposable + VS-Threading analyzers, banned APIs (`BannedSymbols.txt`), unused references (ReferenceTrimmer), dead code (IDE0051/52/59/60/005) |
| test | `dotnet test OpenTheWindows.slnx -c Release --coverage --coverage-output-format cobertura --results-directory artifacts/coverage` | xunit.v3 on Microsoft.Testing.Platform |
| coverage | `dotnet reportgenerator … minimumCoverageThresholds:lineCoverage=85 minimumCoverageThresholds:branchCoverage=75` | exit 1 below threshold (bare token syntax; `-settings:` forms are silently ignored) |
| catalog | built `otw.exe catalog validate` on the embedded catalogue | non-zero fails; proven to reject `tests/fixtures/catalog-bad` (exit 4) so it is not decorative |
| secrets | `gitleaks git … && gitleaks dir …` with `.gitleaks.toml` | `--exit-code 1`; history + working tree; custom rules for VM-inventory passwords and OpenSSH keys |
| sast | `semgrep scan --config p/csharp --config p/secrets --config p/github-actions --error` | exit 1 on findings |
| duplication | `npx jscpd --config .jscpd.json .` | `threshold: 0`, `exitCode: 1` |
| markdown | `npx markdownlint-cli2` | default rules, 120-col |
| powershell | `Invoke-ScriptAnalyzer -Path build -Recurse -Settings PSScriptAnalyzerSettings.psd1` | runner fails on **any** record (not `-EnableExit`, which a wrapper can mask) |
| plan | `pwsh build/check-plan.ps1` | PLAN.md milestone table, `docs/milestones/*.md`, `docs/certification/*.md`, agent instruction files and the catalogue guide must agree; exit 1 otherwise |

Every gate above has been observed to fail on a deliberately broken input
before being trusted; the log is in [PLAN.md §7.2](PLAN.md).

## Environment variables

The product reads none. Developer helpers read `OTW_VM_HOST`, `OTW_VM_USER`,
`OTW_VM_KEY` for the lab VM (see [.env.example](.env.example) and
`build/vm.ps1`).

## Project structure

```text
OpenTheWindows.slnx          solution (slnx)
Directory.Build.props        compiler + analyzer gates for every project
Directory.Build.targets      exe-only relaxations, BannedSymbols wiring
Directory.Packages.props     ALL package versions (central package management)
global.json                  SDK 10.0.400 + Microsoft.Testing.Platform runner
BannedSymbols.txt            banned APIs for src/ (RS0030)
.editorconfig                style + analyzer severities
src/OpenTheWindows.Core      domain model, abstractions, DoctorService, ExitCodes
src/OpenTheWindows.Windows   OS adapters (registry-based OS facts, elevation)
src/OpenTheWindows.Cli       otw.exe (System.CommandLine): doctor
src/OpenTheWindows.App       WPF GUI (DesktopApp, MainWindow, view models)
tests/OpenTheWindows.TestSupport   shared fakes
tests/*.Tests                one test project per shipping project
catalog/                     tweak catalogue (JSON) — populated from M1
build/                       quality.ps1, publish.ps1, smoke-gui.ps1, vm.ps1
docs/adr/                    architecture decision records
docs/research/               research reports (01–07)
.github/workflows/ci.yml     CI (Windows build/test/gates + Linux SAST)
```

Build output goes to `artifacts/` (gitignored) via `UseArtifactsOutput`.

## Deployment notes

- Portable ZIP, MSI (WiX 7) and winget are planned for M7; the MSI will
  register the Event Log source and optionally add `otw` to PATH.
- Unsigned binaries: SmartScreen shows "unknown publisher" once. Organisations
  can sign the MSI/EXE with their own certificate (`signtool sign /fd SHA256
  /a …`) or add WDAC/AppLocker hash rules; verify downloads with
  `Get-FileHash` and `gh attestation verify` (see [SECURITY.md](SECURITY.md)).

## Security notes

See [SECURITY.md](SECURITY.md) for reporting and the security model
(closed action set, journal-first apply, no telemetry, no network).

## Troubleshooting

- **`dotnet --list-sdks` shows nothing / SDK not found**: on some machines the
  x86 host `C:\Program Files (x86)\dotnet` precedes the x64 host on `PATH`.
  Put `C:\Program Files\dotnet\` first (machine PATH) and open a new shell.
  `build/*.ps1` call the x64 host explicitly.
- **`dotnet test` says "Testing with VSTest target is no longer supported"**:
  `global.json` must contain `"test": { "runner": "Microsoft.Testing.Platform" }`
  (it does); do not add `Microsoft.NET.Test.Sdk`.
- **`Invoke-ScriptAnalyzer` throws NullReferenceException**: a non-existent
  compatibility profile name in `PSScriptAnalyzerSettings.psd1`; PSSA 1.25 ships
  `core-6.1.0-windows` as the newest core profile.
- **The GUI does not appear in front**: it runs elevated; unelevated tools
  cannot bring it to the foreground (UIPI). `build/smoke-gui.ps1` self-elevates.
- **SSH key "bad permissions" (Windows OpenSSH)**:
  `icacls <key> /inheritance:r /grant:r "$env:USERNAME:F"`.

## License

MIT — see [LICENSE](LICENSE). Free for everyone; no paid tier.
