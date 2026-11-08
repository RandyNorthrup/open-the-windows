# Open the Windows

![Open the Windows](assets/open-the-windows-logo.svg)

Free, open-source (MIT), enterprise-grade Windows 11 control panel for
**privacy**, **Windows Update**, **security hardening**, **performance** and
**debloat** — with levels per category, per-tweak risk badges, full
reversibility, drift detection and remediation, and an audit trail. GUI
(WPF) and CLI (`otw.exe`) over one shared engine.

> The catalogue ships **458 entries** across privacy, Windows Update, security,
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

Current release: **v0.2.0**, on the GitHub **Releases** page. Download the
per-machine **MSI** or a self-contained **portable ZIP** (x64 or ARM64); a
smaller framework-dependent `-fdd` ZIP is also published for machines that
already have the .NET 10 Desktop Runtime.

```powershell
msiexec /i OpenTheWindows-0.2.0-win-x64.msi /qn   # silent, per-machine
```

From the release after v0.2.0 on, the programs, their own DLLs and the MSIs are
Authenticode-signed with Azure Artifact Signing; the publisher shown by Windows
is **Randy Northrup**. v0.2.0 itself is not signed, so its first run shows a
SmartScreen prompt (**More info → Run anyway**), and SmartScreen can still warn
about a newly signed release until the publisher builds up a reputation. Every
release carries SHA-256 sums, a CycloneDX SBOM and GitHub build-provenance
attestations you can verify with `gh attestation verify` (see
[SECURITY.md](SECURITY.md)). To build from source, see
[CONTRIBUTING.md](CONTRIBUTING.md).

## The app

`OpenTheWindows.exe` is the WPF desktop app over the same engine as the CLI. It
opens on a **Dashboard** (system health) with a left navigation rail to the
category pages (**Privacy**, **Updates**, **Security**, **Performance**,
**Debloat**, **Shell**), **Profiles**, **History**, **Settings** and **About**. A
category page has a level dial (Verified entries, as the named profiles pick
them), search, risk and "not applied" filters, **Scan** to badge what this PC
already has, and a per-entry detail pane; **Review & apply…** opens a modal flow
that previews the what-if plan, asks you to acknowledge Advanced entries and to
type the id of each Breaking one, and applies transactionally on a background
thread. **History** shows each run's entries and reverts a whole run, a single
entry, or everything the app has changed. **Updates** pauses/resumes Windows
Update (with an extended pause behind a warning), holds feature updates at a
release, runs the supervised Windows Update repair, hides/unhides single
updates, and is read-only when updates are managed by policy. **Profiles**
applies, imports, exports and captures profiles with their signing state.
**About** can check GitHub Releases for a newer version when you ask.

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
  run. It refuses a run that was already reverted, or whose settings a later run
  changed again, unless you pass `--force`; a change that cannot be undone
  automatically (a removed app) is reported as not reversible. `--entry <id>`
  reverts just one entry of a run; `otw revert --all` undoes every change the
  tool has made, newest first. `otw history` lists past apply and revert runs and
  whether each journal verifies.
- **`otw remediate --profile <…>`** — the unattended form of apply, for
  automation: it re-applies only the drifted entries, never prompting and never
  overriding a managed (MDM/GPO) setting.
- **`otw task install --profile <…>`** — register a scheduled task that runs
  `remediate` on a schedule (daily, weekly, at logon, or after a feature update)
  to keep the machine on-profile. `otw task remove` deletes it. The task runs as
  SYSTEM, so it is refused unless `otw.exe` sits in a folder only administrators
  can change (the MSI's Program Files install qualifies; a portable copy in
  Downloads does not), and a profile file is copied to a protected folder first.
- **`otw audit run --baseline <stig|cis|microsoft|file>`** — score the machine
  against a security baseline (DISA STIG, CIS, or Microsoft), read-only, with a
  0–100 result and a Pass / Fail / Manual verdict per control. Export with
  `--json`, `--csv`, `--html` or `--sarif`.
- **`otw profile`** — inspect, validate and sign profiles: `list`, `show`,
  `validate`, `capture --out <file>` (write a profile of the settings this PC
  already has, to apply them to others), and `sign` / `verify` (detached ES256
  signatures). A machine can be
  set by policy to require signed profiles; the policy then also covers the GUI
  and any extra catalogue loaded with `--catalog-dir`.
- **`otw updates`** — control Windows Update safely and reversibly: `pause --days
  <1-35>` (with an opt-in extended pause; re-pausing counts from the first day of
  the pause in force, so the limit cannot be stepped around; administrators can
  lower the extended limit by policy), `resume`, `status` (running version,
  days to end-of-servicing, and pause state; `--json` for scripts), `hidden`,
  `hide --kb <id>` and `unhide --kb <id>` (per-update hide through the Windows
  Update Agent, verified after the change), `target --version <24H2|25H2|…>` /
  `target --clear` (hold feature updates at a version while security updates keep
  installing; Pro, Enterprise and Education), and `repair` (reset a stuck update
  client: its services are stopped only for the repair and its caches set aside,
  never deleted).
- **`otw check-update`** — opt-in query of GitHub Releases for a newer version
  (one HTTPS request; nothing runs on a schedule).

Exit codes are part of the contract and listed by `otw --help`: 0 success
(compliant), 1 drift, 2 error, 3 unsupported OS, 4 invalid input (including a
mistyped command line), 5 elevation required, 6 managed by policy (nothing
changed), 3010 reboot required.

## Contributing

Build from source, the quality gates, project layout and coding standards are in
[CONTRIBUTING.md](CONTRIBUTING.md).

## Security

See [SECURITY.md](SECURITY.md) for reporting and the security model (closed
action set, journal-first apply, no telemetry, no network unless you opt into
the update check).

## License

MIT — see [LICENSE](LICENSE). Free for everyone; no paid tier.

## Support this project

If this project saves you time, you can
[buy me a coffee](https://www.paypal.com/donate/?hosted_button_id=Q9VC7B42R7K82)
via PayPal. Thank you!
