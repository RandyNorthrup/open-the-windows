# CLAUDE.md

Project-local instructions for Claude Code. The full rule set is in
[AGENTS.md](AGENTS.md); read it first. Summary:

- MANDATORY before editing code: read docs/milestones/README.md, then run
  `pwsh build/start-milestone.ps1 -Milestone <Mx>` (prints the milestone spec and
  records the acknowledgement the pre-edit hook checks). Follow the spec in order.
  A milestone is done only with `docs/certification/Mx.md` and a green
  `pwsh build/quality.ps1` (incl. the `plan` gate).
- This is an elevated Windows 11 system utility (.NET 10, WPF + CLI). Safety and
  reversibility are the product. Pausing/holding/deferring Windows Update is a
  feature (PLAN.md D21); permanently breaking it (WaaSMedic/wuauserv ACL hacks,
  Disabled start types), disabling Defender/UAC/protected services, hosts-blocking
  Microsoft, or fighting MDM/GPO are refusals.
- Per-user writes go to the interactive user's `HKU\<SID>`; `Registry.CurrentUser`
  is banned. Policy writes go through the Local GPO and are mirrored.
- Tweaks are catalogue data (sources, revert, risk, level, appliesTo, verifiedOn);
  actions are a closed set of typed kinds.
- Build is the gate: warnings are errors, analyzers at maximum. Fix, do not suppress.
  Any suppression needs an inline reason plus a row in PLAN.md section 7.3.
- No placeholders, no dead code, no magic literals, no unverified claims in docs.
- Before finishing: `pwsh build/quality.ps1` green; CHANGELOG entry; PLAN.md
  and README updated when behaviour or commands change.
- Use `C:\Program Files\dotnet\dotnet.exe` if `dotnet` resolves to the x86 host.
- Tests run on Microsoft.Testing.Platform (xunit.v3); do not add VSTest packages.
- Write files with the Write/Edit tools; Bash heredocs here mangle non-ASCII and
  backslashes.
- Never touch global user memory or machine-wide settings from this repo.
