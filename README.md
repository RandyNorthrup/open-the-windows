# Open the Windows

![Open the Windows](assets/open-the-windows-logo.svg)

Free, open-source (MIT), enterprise-grade Windows 11 control panel for
**privacy**, **Windows Update**, **security hardening**, **performance** and
**debloat** — with levels per category, per-tweak risk badges, full
reversibility, drift detection and remediation, and an audit trail. GUI
(WPF) and CLI (`otw.exe`) over one shared engine.

> The catalogue ships **440 entries** across privacy, Windows Update, security,
> performance, debloat and shell; the CLI and GUI scan, apply, revert and inspect
> history, audit against security baselines (STIG/CIS/Microsoft), and pause/resume
> Windows Update. Entries confirmed on Windows 11 25H2 are marked `Verified`; the
> rest ship as `Draft` and are not applied unless you pass `--include-draft`.

## What it does (and what it never does)

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

## Requirements

Windows 11 (build 22000+), client editions (Home, Pro, Enterprise, Education).
Windows Server is refused. x64 and ARM64.

## Install

Current release: **v0.1.0**, on the GitHub **Releases** page. Download the
per-machine **MSI** or a self-contained **portable ZIP** (x64 or ARM64); a
smaller framework-dependent `-fdd` ZIP is also published for machines that
already have the .NET 10 Desktop Runtime.

```powershell
msiexec /i OpenTheWindows-0.1.0-win-x64.msi /qn   # silent, per-machine
```

A **winget** manifest for `RandyNorthrup.OpenTheWindows` ships with each release;
`winget install RandyNorthrup.OpenTheWindows` works once it is accepted into the
winget community repository.

Binaries are not code-signed; every release carries SHA-256 sums, a CycloneDX
SBOM and GitHub build-provenance attestations you can verify with
`gh attestation verify` (see [SECURITY.md](SECURITY.md)). The first run of an
unsigned download shows a SmartScreen prompt (**More info → Run anyway**). To
build from source, see [CONTRIBUTING.md](CONTRIBUTING.md).

## The app

`OpenTheWindows.exe` is the WPF desktop app over the same engine as the CLI. It
opens on a **Dashboard** (system health) with a left navigation rail to the
category pages (**Privacy**, **Updates**, **Security**, **Performance**,
**Debloat**, **Shell**), **Profiles**, **History**, **Settings** and **About**. A
category page has a level dial, search, a risk filter and a per-entry detail
pane; **Review & apply…** opens a modal flow that previews the what-if plan,
confirms Breaking entries by typing their id, applies transactionally on a
background thread, and lets you revert from **History**. **Updates**
pauses/resumes Windows Update (with an extended pause behind a warning, and a
read-only mode when it is managed by policy); **Profiles** applies, imports and
exports profiles with their signing state.

## Using the CLI (`otw`)

After install, `otw` is the command-line tool; the GUI drives the same engine.
Every command takes `--help` for its full options and exit codes, and most take
`--json` for scripting.

- **`otw doctor`** — report the OS, edition and build, and whether the machine is
  supported and the session elevated.
- **`otw catalog`** — browse the built-in tweak catalogue: `list` (filter by
  `--category` / `--level`), `show <id>`, and `validate`.
- **`otw health`** — run the read-only machine health checks (Secure Boot, TPM,
  Defender, BitLocker, firewall and more). Nothing is changed.
- **`otw scan --profile <level|profile|file>`** — read-only drift scan showing how
  far the machine is from a level (`basic` … `paranoid`), a built-in profile
  (`home`, `enterprise-workstation`, …), or a profile file. Export the result
  with `--json`, `--csv`, `--html` or `--sarif`.
- **`otw apply --profile <…>`** — apply a profile, transactionally and elevated.
  Every prior value is journaled first and each change re-read to verify it, so a
  failed step rolls the whole run back. `--what-if` previews; a System Restore
  point is taken unless `--no-restore-point`; higher-risk entries need
  `--allow-advanced` / `--allow-breaking`. An all-users profile also reaches
  users who are logged off or added later.
- **`otw revert <runId|last>`** — restore the exact state captured before a prior
  run. `otw history` lists past apply and revert runs.
- **`otw remediate --profile <…>`** — the unattended form of apply, for
  automation: it re-applies only the drifted entries, never prompting and never
  overriding a managed (MDM/GPO) setting.
- **`otw task install --profile <…>`** — register a scheduled task that runs
  `remediate` on a schedule (daily, weekly, at logon, or after a feature update)
  to keep the machine on-profile. `otw task remove` deletes it.
- **`otw audit run --baseline <stig|cis|microsoft|file>`** — score the machine
  against a security baseline (DISA STIG, CIS, or Microsoft), read-only, with a
  0–100 result and a Pass / Fail / Manual verdict per control. Export with
  `--json`, `--csv`, `--html` or `--sarif`.
- **`otw profile`** — inspect, validate and sign profiles: `list`, `show`,
  `validate`, and `sign` / `verify` (detached ES256 signatures). A machine can be
  set by policy to require signed profiles.
- **`otw updates`** — control Windows Update safely and reversibly: `pause --days
  <1-35>` (with an opt-in extended pause), `resume`, and `status` (running
  version, days to end-of-servicing, and pause state).

## Contributing

Build from source, the quality gates, project layout and coding standards are in
[CONTRIBUTING.md](CONTRIBUTING.md).

## Security

See [SECURITY.md](SECURITY.md) for reporting and the security model (closed
action set, journal-first apply, no telemetry, no network).

## License

MIT — see [LICENSE](LICENSE). Free for everyone; no paid tier.
