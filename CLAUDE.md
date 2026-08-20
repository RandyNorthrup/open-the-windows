# CLAUDE.md

Project-local instructions for Claude Code. The full rule set is in
[AGENTS.md](AGENTS.md); read it first. Build, run and gate commands are in
[CONTRIBUTING.md](CONTRIBUTING.md). Summary:

- This is an elevated Windows 11 system utility (.NET 10, WPF + the `otw.exe`
  CLI). Safety and reversibility are the product. Pausing/holding/deferring
  Windows Update is a feature; permanently breaking it (WaaSMedic/wuauserv ACL
  hacks, Disabled start types), disabling Defender/UAC/protected services,
  hosts-blocking Microsoft, or fighting MDM/GPO are refusals.
- Per-user writes go to the interactive user's `HKU\<SID>`; `Registry.CurrentUser`
  is banned. Policy writes go through the Local GPO and are mirrored.
- Tweaks are catalogue data (sources, revert, risk, level, appliesTo, verifiedOn);
  actions are a closed set of typed kinds.
- Build is the gate: warnings are errors, analyzers at maximum. Fix the finding.
  Never add a suppression (`[SuppressMessage]`, `#pragma warning`, `NoWarn`, an
  `.editorconfig` severity downgrade) without explicitly asking first.
- Never remove or disable app functionality to make something build or pass.
- No placeholders, no dead code, no magic literals, no unverified claims.
- Before finishing: `pwsh build/quality.ps1` green; add a CHANGELOG entry; keep
  README accurate when behaviour or commands change.
- Use `C:\Program Files\dotnet\dotnet.exe` if `dotnet` resolves to the x86 host.
- Tests run on Microsoft.Testing.Platform (xunit.v3); do not add VSTest packages.
- Write files with the Write/Edit tools; Bash heredocs here mangle non-ASCII and
  backslashes.
- Never touch global user memory or machine-wide settings from this repo.
