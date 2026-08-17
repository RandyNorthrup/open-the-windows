# Evidence — Windows Update pause / resume / status + EOS (M4 acceptance #3)

- **Build:** Windows 11 Pro, 25H2, build 26200.9168
- **Edition:** Professional / Client (Consumer servicing)
- **Date:** 2026-08-17
- **Host:** lab VM `win-11-vm` (10.10.11.183), elevated SSH session (`randy`, admin)
- **Binary:** self-contained `otw.exe` published from the tip of
  `feature/m4-catalogue-population` (catalogue = 435 entries, `catalog validate`
  exit 0 on the VM)
- **Method:** `build/vm.ps1`-style SSH; the harness
  (`temp scratchpad wu-accept.ps1`) drives the CLI and reads the effect back with
  an independent PowerShell `Get-ItemProperty` on
  `HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings` (never the CLI's own
  detection), plus `Get-WinEvent` and the ProgramData JSONL trail for the audit
  events.

## Result: PASS

Every documented behaviour of `otw updates pause|resume|status` was exercised on
the live VM and confirmed by an independent registry / event-log read. The VM was
left un-paused.

| Step | Command | Expected | Independent read | Result |
| --- | --- | --- | --- | --- |
| A | `updates status` (baseline) | not paused; EOS date + days-to-EOS | six UX\Settings values absent | PASS — "Updates not paused", EOS 2027-10-12 (421 days) |
| B | `updates pause --days 7 --what-if` | plan only, no writes | all six still absent after the call | PASS — planned "until 2026-08-24 18:11 UTC", registry unchanged |
| C | `updates pause --days 7` | writes the six values; status paused | all six = ISO-8601 UTC (start 18:11:06Z, expiry 2026-08-24T18:11:06Z) | PASS — status "paused until 2026-08-24 18:11 UTC" |
| D | `updates pause --days 45` (no `--extended`) | rejected at the 35-day cap; state unchanged | six values still the step-C values | PASS — "Pause must be between 1 and 35 days", **exit 4**, no change |
| E | `updates pause --days 60 --extended` | warns; writes 60-day values; Event 4002 | expiry now 2026-10-16T18:11:07Z; **Event 4002** in the `OpenTheWindows` log (src `OTW`) and in the JSONL trail | PASS |
| F | `updates resume` | clears the pause; triggers a scan; status not paused | **all six values absent again** | PASS — status "Updates not paused" |

## Notes

- **Stacked-pause regression found and fixed here.** Step E re-pauses on top of the
  step-C pause without resuming first. Before the fix, `resume` reverted only the
  most-recent pause run, which restored the *earlier* pause's values and left the
  device paused (status still "paused until 2026-08-24"). `UpdateControlService.Resume`
  now reverts **every** active pause run, newest-first, so resume returns the six
  UX\Settings values to absent regardless of how many pauses stacked. Covered by
  `UpdateControlServiceTests.Resume_after_a_restacked_pause_clears_every_active_pause`
  (proven to fail against the single-revert implementation).
- **Extended-pause audit** is written to the custom `OpenTheWindows` Windows event
  log (source `OTW`), id **4002**, and mirrored to
  `%ProgramData%\OpenTheWindows\audit\otw-YYYYMM.jsonl` — both confirmed on the VM.
- **EOS warning window.** 25H2 is 421 days from end-of-servicing, outside the
  90-day warning window, so `status` correctly shows the countdown without a
  warning banner. The day-count and the warning-window predicate are unit-covered
  by `EndOfServicingCalendarTests`; the live run confirms the calendar resolves the
  running build/edition and computes the countdown against it.
