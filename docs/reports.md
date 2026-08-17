# Scan report formats

`otw scan` writes its results in one of four formats, chosen with a flag
(`--json`, `--csv`, `--html`, `--sarif`) and optionally directed to a file with
`--out <file>`. With no format flag it prints a short text summary. All formats
are produced by `OpenTheWindows.Core.Reports` and are deterministic for a fixed
report (the timestamp comes from the injected clock), so they can be
golden-tested and diffed.

Every format describes the same model: the scan time, the OS identity, the
profile that was scanned, an aggregate summary, and one entry per catalogue item
with its per-action observations. Compliance states are `Compliant`,
`NotApplicable`, `Unknown`, `Managed` (owned by MDM/Group Policy) and `Drift`.

## JSON (`--json`, extension `json`)

Indented, camel-cased, source-generated (no reflection). Shape:

```json
{
  "schemaVersion": "1.0",
  "generatedAt": "2020-01-02T03:04:05+00:00",
  "profile": "balanced",
  "os": {
    "productName": "Windows 11 Pro", "editionId": "Professional",
    "displayVersion": "24H2", "build": 26100, "revision": 4351,
    "architecture": "x64", "installationType": "Client", "fullBuild": "26100.4351"
  },
  "summary": {
    "total": 3, "compliant": 1, "drift": 1, "managed": 0,
    "notApplicable": 1, "unknown": 0, "isCompliant": false
  },
  "results": [
    {
      "id": "privacy.telemetry.allow", "state": "Drift", "risk": "Breaking",
      "actions": [
        { "kind": "Registry", "state": "Drift", "actual": "1", "desired": "0", "managed": "NotManaged" }
      ]
    }
  ]
}
```

`schemaVersion` is the report shape's own version, independent of the product
version. Consumers should parse defensively and ignore unknown fields.

## CSV (`--csv`, extension `csv`)

RFC 4180, `\n` line endings, one row per action. An entry with no actions (for
example a not-applicable entry) contributes a single row with empty action
columns. Header:

```text
id,state,risk,kind,actionState,actual,desired,managed
```

Fields that contain a comma, quote or newline are quoted and internal quotes are
doubled.

## HTML (`--html`, extension `html`)

A single self-contained file: inline CSS, no external scripts, styles, fonts or
images, so it renders offline and can be archived as compliance evidence. It
contains the OS/profile header, the summary table, and a per-entry table with
each entry's actions.

## SARIF (`--sarif`, extension `sarif`)

SARIF 2.1.0. Every drifting entry becomes a `result`; the drifting ids become
the run's `rules` (one rule per id). A result's `level` is derived from the
entry's risk tier so a security tool can triage by impact:

| Risk tier | SARIF level |
| --------- | ----------- |
| Breaking  | error       |
| Advanced  | warning     |
| Caution   | note        |
| Safe      | note        |

Compliant, not-applicable, managed and unknown entries are not findings and
produce no result. The tool block carries the product name, homepage
(`informationUri`) and version.
