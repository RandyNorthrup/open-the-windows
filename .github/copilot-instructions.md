# Copilot instructions — Open the Windows

Follow AGENTS.md (authoritative). In short:

- Elevated Windows 11 utility (.NET 10, WPF + `otw.exe`). Never generate code that
  disables Windows Update, Defender, UAC or protected services, hosts-blocks
  Microsoft endpoints, removes Edge/WebView2, or overrides MDM/GPO managed values.
- Keep Windows API calls inside `src/OpenTheWindows.Windows`. Core is pure .NET.
- Do not use `Registry.CurrentUser`, `DateTime.Now/UtcNow`, `Environment.Exit`,
  or `System.Diagnostics.Process` in `src/` (banned; use the abstractions).
- Tweaks are catalogue JSON with sources/revert/risk/level/appliesTo/verifiedOn.
- Warnings are errors and analyzers run at maximum; fix findings rather than
  suppress. No magic literals, no dead code, no placeholder implementations.
- Tests: xunit.v3 (Microsoft.Testing.Platform), plain `Assert`, fakes from
  `tests/OpenTheWindows.TestSupport`; include a negative case.
- Before finishing: `pwsh build/quality.ps1` must pass; add a CHANGELOG entry;
  keep README/PLAN accurate.
