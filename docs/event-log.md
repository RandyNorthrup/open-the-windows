# Audit trail: Event Log and JSONL

Every apply and revert run emits audit events to two places:

1. **Windows Event Log** — log `OpenTheWindows`, source `OTW`. Created on the
   first elevated run (or by the installer in M7). If the source cannot be
   created (an unelevated first run), Event Log writing is skipped and the JSONL
   trail is still written — the sink never silently loses an event.
2. **JSONL** — `%ProgramData%\OpenTheWindows\audit\otw-YYYYMM.jsonl`, one JSON
   object per line, rotated monthly. Machine-readable for SIEM ingestion.

Read the Event Log with:

```powershell
Get-WinEvent -LogName OpenTheWindows | Format-Table TimeCreated, Id, LevelDisplayName, Message
```

## Event ids

Ids are a contract: they never change once released; new ones are appended.

| Id | Name | Level | Meaning |
| ---- | ------ | ------- | --------- |
| 1000 | ApplyStarted | Information | An apply run started. |
| 1001 | TweakApplied | Information | One entry was applied and verified. |
| 1002 | TweakFailed | Warning | One entry failed to apply or verify. |
| 1003 | RunRolledBack | Warning | An apply run was rolled back after a failure. |
| 1004 | RevertStarted | Information | A revert run started. |
| 1005 | RevertCompleted | Information | A revert run completed. |
| 2000 | ScanDrift | Information | A scan detected drift. |
| 3000 | Rollback | Warning | One action was rolled back during a run's rollback. |
| 4000 | Refusal | Warning | A refused operation was attempted (protected service, disallowed executable, managed setting without break-glass). |
| 4001 | BreakGlass | Warning | A managed setting was overridden under break-glass. |
| 9000 | Error | Error | An unexpected error, or a blocking pre-flight. |

## JSONL shape

```json
{"id":"ApplyStarted","at":"2026-08-17T20:00:00+00:00","message":"Apply 'balanced' started (12 entr(y/ies)).","runId":"37e93a65-4f51-4619-afde-f66383a1bd48","entryId":null}
```

`message` never contains secrets. `runId` and `entryId` correlate an event to a
journal file and a catalogue entry.
