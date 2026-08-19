# Auditing against security baselines

`otw audit` scores the machine against a **security baseline** — a mapping from an
external framework's control ids to the catalogue tweaks and read-only health
checks that satisfy them. Auditing is **read-only**: it never changes machine
state and does not require elevation (though some health checks report `Unknown`
without it).

## Built-in baselines

Three baselines ship, embedded in the tool. Each rule's framework id and source
URL was verified against the framework's own material.

| Id | Title | Source |
| --- | --- | --- |
| `disa-stig-v2r7` | DISA Microsoft Windows 11 STIG V2R7 | [cyber.trackr.live](https://cyber.trackr.live/stig/Windows_11/2/7), [public.cyber.mil](https://www.cyber.mil/stigs/downloads/) |
| `cis-win11-ent-v4.0` | CIS Microsoft Windows 11 Enterprise Benchmark v4.0.0 | [cisecurity.org](https://www.cisecurity.org/benchmark/microsoft_windows_desktop) |
| `ms-baseline-25h2` | Microsoft Windows 11 Security Baseline (25H2) | [Microsoft Learn](https://learn.microsoft.com/en-us/intune/device-security/security-baselines/ref-windows-mdm-settings) |

The version numbers name the framework release whose exact control ids were
verified: the current DISA STIG is **V2R7** (there is no V2R8 yet), and the CIS
section numbers are from **v4.0.0** (the numbering differs between CIS releases;
v4.0.0 is the release whose section ids were verified). CIS rules carry only the
section id and a paraphrased title — never benchmark text (ADR 0002).

A baseline is catalogue data under `catalog/baselines/*.json`, validated against
`catalog/schema/baseline.schema.json`. Each rule is one of:

- **tweak-backed** — one or more catalogue `tweakIds` that implement the control;
- **check-backed** — a `checkOnly.healthCheckId` naming a read-only machine health
  check (Secure Boot, TPM, BitLocker and the like, which have no single tweak);
- **manual** — neither, for a control a human must confirm.

## Rule outcomes

Each rule resolves to one outcome, with the actual observed value as evidence:

| Outcome | Meaning |
| --- | --- |
| `Pass` | The control is satisfied. |
| `Fail` | The control is not satisfied (drift, a policy enforcing a non-compliant value, or a failing/warning health check). |
| `NotApplicable` | The control does not apply to this machine (edition, build or hardware). |
| `Manual` | The control has no automated mapping (a manual-only rule). |
| `Unknown` | The control could not be determined (needs elevation, or is not detectable). |

A control that is **enforced by Group Policy at the desired value is a `Pass`** —
a policy-enforced compliant setting is more compliant, not less. A managed setting
at a *different* value is a real `Fail` (a policy is enforcing a non-compliant
value).

## Score

The report carries a weighted **0–100 score**. Each rule is weighted by its
severity (CAT I / L1 / Baseline weigh more than CAT III / L2); the score is the
weighted fraction of *scoreable* rules — those with a `Pass` or `Fail` outcome —
that pass. `NotApplicable`, `Manual` and `Unknown` rules are reported but excluded
from the score because they are not automatically determinable as pass or fail. A
baseline with no scoreable rules scores 100 (nothing can fail).

## The `otw audit` command

```text
otw audit list                       # list the built-in baselines
otw audit validate [--baseline-dir]  # validate baselines against the catalogue
otw audit run --baseline <id|file> [--json|--csv|--html|--sarif] [--out <file>] [--sign <pem>]
```

`otw audit run` audits the machine against a baseline (a built-in id or a path to
a baseline `.json`) and writes the report. With no format flag it prints a text
summary — the score, the outcome counts and the failing rules — to stdout; exit
code is `0` when nothing fails and `1` when any rule fails.

Formats:

- **`--json`** — a `SignedAuditReport`: the report plus a SHA-256 integrity hash
  over its canonical JSON. Add **`--sign <key.pem>`** to attach an ES256 signature
  from an org key (the profile-signing infrastructure) so a collector can detect
  tampering. Signing is only available with the JSON format.
- **`--csv`** — one row per rule (`ruleId,severity,outcome,title,tweakIds,evidence`).
- **`--html`** — a single self-contained file: an executive summary (score and
  outcome counts) and one table per severity.
- **`--sarif`** — SARIF 2.1.0 with the baseline rule ids as rule ids; each failing
  rule is a result whose level is derived from its severity (CAT I → error, …).

### Fleet drop (scheduled audit)

```text
otw task install --audit <id> --out <path> [--daily|--weekly|--on-logon|--on-build-change]
```

registers a SYSTEM, hidden scheduled task (`\OpenTheWindows\Audit`) that runs the
audit for `<id>` and writes the JSON report to `--out` — for example a fleet UNC
share a collector reads. `--profile` (a remediation task) and `--audit` (an audit
task) are mutually exclusive.

## Verifying a report's integrity

The JSON report's `hash` is the lower-case hex SHA-256 of the canonical JSON of
the `report` object (object keys sorted, no insignificant whitespace). A collector
recomputes the hash the same way and compares it; if the report is signed, it also
verifies the `signature` (`{ alg, keyId, signature }`) against the org's public
key, exactly as for signed profiles (see [enterprise.md](enterprise.md)).

## In the GUI

- The **Dashboard** shows a *Security baselines* card: the machine's score against
  each built-in baseline (scored read-only, off the UI thread), with a progress
  bar, score and outcome summary, and a **Re-score** action.
- The **Security** page has a *Baseline* filter that narrows the tweak list to the
  tweaks a selected baseline references.

## Limitations (honest)

- A baseline is a curated mapping, not a full reproduction of the framework: it
  covers high-value controls that map to the catalogue and the read-only health
  checks. Rules with no automated mapping are reported as `Manual`.
- A rule backed only by a `Command` action cannot be auto-scored (the command's
  effect is not detectable read-only) and reports `Unknown`.
- Some health checks (BitLocker, the local security policy) need elevation to read
  and report `Unknown` when run without it.
