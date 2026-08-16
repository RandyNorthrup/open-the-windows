# M8 — Hardening audit mode and compliance reports

## Goal

`otw audit` scores the machine against Microsoft Security Baseline 25H2,
DISA STIG V2R8 and CIS Windows 11 section IDs (IDs only, no CIS text), with
HTML/JSON/CSV/SARIF exports and a scheduled report drop for fleet collection.

## Preconditions

M5 certified (M6/M7 may run in parallel). Read: research 04 §3, §5, §6, §7.

## Design

- `catalog/baselines/<id>.json` (`ms-baseline-25h2`, `disa-stig-v2r8`,
  `cis-win11-ent-v5.1`): list of `{ "ruleId": "V-253254", "title": "...",
  "severity": "CAT I|CAT II|CAT III|L1|L2|BL|NG|Baseline", "tweakIds": [...],
  "checkOnly": <optional read-only check spec when no tweak exists>, "sources": [...] }`.
  Schema `catalog/schema/baseline.schema.json`; validator: every `tweakId`
  exists; every rule has ≥ 1 source; CIS rules carry only the section id and a
  paraphrased title (no benchmark text — ADR 0002).
- `AuditEngine.Audit(baselineId) → AuditReport { Score (0–100 weighted by
  severity), PerRule results (Pass/Fail/NotApplicable/Manual/Unknown), evidence
  per rule (actual values) }`; read-only; uses ScanEngine + health probes.
- Exporters reuse M2 writers; SARIF rule ids = baseline rule ids; HTML report
  is one file with an executive summary and per-domain tables.
- Report signing: `hash` (SHA-256 of canonical JSON) + optional ES256 signature
  with the org key (M5 infrastructure) so a collector can detect tampering.
- `otw audit --baseline <id> [--json|--csv|--html|--sarif] [--out]`; `otw task
  install --audit <id> --weekly --out <UNC path>` for fleet drop.
- GUI: Dashboard shows last audit score per baseline; Security page has a
  "Baseline" filter (`M6` hook: filter by tag).

## Tests

Baseline schema/validator; scoring math (fixed inputs → known score);
Manual/NotApplicable handling; exporter goldens; VM: audit before and after
applying `enterprise-workstation` shows a higher score, and every Fail row
maps to a real setting difference (spot-check 10 rules by hand).

## Acceptance

Three baseline files validate; `otw audit` on VM produces all four formats;
score changes after apply/revert; `docs/certification/M8.md`.
