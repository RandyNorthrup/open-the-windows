# Contributing

Thanks for helping make Windows 11 quieter and safer. This project holds itself
to a high bar because it runs elevated on other people's machines.

## Ground rules

1. **Gates are the contract.** `pwsh build/quality.ps1` must pass before you
   open a pull request. CI runs the same gates in locked-restore mode.
2. **No placeholder code.** No mock data outside tests, no silent fallbacks, no
   "TODO: implement". A function that cannot do its job throws or returns an
   explicit error.
3. **No magic literals.** Named constants, enums or configuration; idiomatic
   `0`, `1`, `-1`, empty collections and booleans in direct conditions are fine.
4. **No dead code.** Unused members, files, exports, packages and stale config
   are build errors; delete rather than comment out.
5. **No suppression without a reason.** A `#pragma`, `NoWarn`, or `.editorconfig`
   severity change names the rule, states why, and gets a row in
   `PLAN.md` §7.3.
6. **Every tweak is data.** New tweaks are catalogue entries with sources,
   revert path, risk, level, `appliesTo` and `verifiedOn` — never ad-hoc code.
   New *action kinds* are code and need tests plus an ADR update.
7. **Prove gates and tests can fail.** A new test must have a case that would
   fail without the change; a new gate must be shown failing on a broken input
   (add it to the verification log).
8. **Docs move with code.** README commands must have been run; CHANGELOG gets
   an entry; PLAN.md decisions/open questions are updated.

## Workflow

```powershell
git checkout -b feature/<short-name>
# ...edit...
pwsh build/quality.ps1
git commit -m "feat(core): <what and why>"
```

Conventional Commits (`feat`, `fix`, `docs`, `test`, `build`, `ci`,
`refactor`, `chore`) with a scope (`core`, `windows`, `cli`, `app`, `catalog`,
`build`, `docs`).

## Where things live

- Package versions: `Directory.Packages.props` only.
- Analyzer severities: `.editorconfig`; analyzer packages:
  `Directory.Packages.props` (`GlobalPackageReference`).
- Banned APIs: `BannedSymbols.txt`.
- Tool versions: `.config/dotnet-tools.json`, `package.json`,
  `build/requirements-tools.txt`.
- Research: `docs/research/` (reference; cite it from catalogue entries).

## Reporting security issues

See [SECURITY.md](SECURITY.md). Please do not open public issues for
vulnerabilities.
