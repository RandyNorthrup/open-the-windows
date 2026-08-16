# ADR 0004 — Tweaks are data (JSON + schema); actions are code

- Status: Accepted (2026-08-16)

## Context

The product must be feature-rich (hundreds of tweaks), provably safe (every
tweak documented, sourced, reversible, level-rated, and verified on specific
Windows builds), and extensible by enterprises without recompiling.

## Decision

- Every tweak is a **catalogue entry** in JSON under `catalog/`, validated by
  a JSON Schema and by catalogue tests. An entry carries: id, title,
  description, rationale, category, default level, risk tier, scope
  (machine / user / all-users), `appliesTo` (editions, min/max build),
  `actions[]` (declarative), `detect` (how to read current state), explicit
  `revert` where it cannot be derived, `requires` (reboot / logoff / Explorer
  restart), dependencies/conflicts, `sources[]` (URLs), and `verifiedOn[]`
  (builds + date + evidence link).
- **Actions are code**: a small, closed set of typed action kinds implemented
  in Core (semantics) and Windows (execution) — registry value, service start
  type, scheduled task state, Appx removal/deprovision, optional feature,
  Defender preference, power setting, firewall, audit policy, local security
  policy, and a guarded "command" kind for the few things that have no API.
- The engine plans, snapshots prior state to a journal, applies, verifies,
  and rolls back on failure. Revert replays the journal in reverse.
- The built-in catalogue is embedded in Core; a directory of extra/override
  catalogue files can be supplied for enterprise customisation, and profiles
  can be signed with an organisation-supplied Ed25519 key.

## Consequences

- Adding a tweak is a data change plus tests, reviewable by non-C# experts.
- New action kinds require code and tests; that is intentional — the action
  set is the safety boundary.
- Catalogue tests enforce: unique ids, schema validity, at least one source,
  a revert path, a risk and level, and no unknown action kinds.
