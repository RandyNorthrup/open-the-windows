# Milestone runbook

How any engineer or coding agent executes a milestone in this repository so
that quality does not regress. `PLAN.md` is the index; each `Mx-*.md` file
here is the executable specification for one milestone.

## Before you start a milestone

1. Read, in this order: `AGENTS.md`, `PLAN.md` §1–§5 and §7.3, the milestone
   spec (`docs/milestones/Mx-*.md`), the ADRs it cites, and the research
   sections it cites (by section number — do not read whole research files).
2. Run `pwsh build/quality.ps1`. It must be green **before** you change
   anything. If it is not, stop and fix the tree first (or report).
3. Create a branch: `feature/mx-<short-name>`.
4. Confirm the milestone's "Preconditions" list is satisfied.

## While you work

- Implement in the order the spec's "Implementation steps" gives. Each step
  ends with a build (`dotnet build OpenTheWindows.slnx -c Release`) and the
  tests of that step passing. Do not batch five steps and then build.
- Every new public type gets XML docs; one type per file; file name = type
  name; namespace = folder (see AGENTS.md "Analyzer expectations").
- Every new behaviour gets a test with a case that fails without it, and a
  negative case (wrong input, unsupported platform, missing permission).
- Never lower a coverage threshold or add a suppression to make a build pass.
  If a rule is genuinely wrong for a site, follow the escape-hatch process
  (inline reason + PLAN.md §7.3 row) and mention it in the PR.
- Windows APIs only in `src/OpenTheWindows.Windows`. If Core needs an OS
  capability, add an interface in `Core/Abstractions` first, a fake in
  `tests/OpenTheWindows.TestSupport/Fakes`, then the Windows implementation.
- Keep `README.md` truthful: a command goes into README only after you ran it.
- Update `CHANGELOG.md` under `[Unreleased]` as you go, not at the end.

## Before you finish

1. `pwsh build/quality.ps1` green (all gates, including any gate the
   milestone added).
2. Every acceptance criterion in the spec is checked and the evidence is
   written into `docs/certification/Mx.md` (template below).
3. `PLAN.md` §8 status updated; new decisions added to §3; new escape hatches
   to §7.3; new versions to §7.1 with the source you verified them against.
4. Commit with Conventional Commits; the body says what was verified and how.
5. Push and confirm CI is green (`gh run watch`).

## Certification record template (`docs/certification/Mx.md`)

```markdown
# Mx certification — <title>

Date: YYYY-MM-DD · Commit: <sha> · Engineer/agent: <name>

## Gates
| Gate | Result | Evidence |
|------|--------|----------|
| quality.ps1 (all) | PASS | paste the summary table |
| new gate(s) | PASS + proven to fail on: <input> | paste output |

## Acceptance criteria
| # | Criterion | Result | Evidence (command + output / screenshot path) |

## Tests
Total / passed / skipped (with the reason each skip is legitimate). Coverage line/branch.

## VM runs (if applicable)
Build, edition, commands run, journal ids, before/after states.

## Deviations
Anything not done, and why. Empty is the goal.
```

## What "done" means

A milestone is done only when the certification record exists, every criterion
in it is PASS, CI is green on `main`, and `PLAN.md` says so. Partial work is
committed on the branch, never merged as "done".
