# ADR 0001 — .NET 10 + WPF, with Core / Windows / CLI / App layering

- Status: Accepted (2026-08-16)
- Deciders: Randy Northrup (product owner)

## Context

Open the Windows is a Windows 11-only, elevated system utility that reads and
writes registry values, services, scheduled tasks, Appx packages, Windows
Update policy, Defender preferences and similar OS state. It needs a rich GUI,
a scriptable CLI for enterprise automation, and a core that can be unit-tested
without touching the OS. Options considered for the GUI were WinUI 3 (Windows
App SDK), WPF on .NET 10, Avalonia, and a web-technology shell.

## Decision

- Runtime: **.NET 10 (LTS)**, pinned in `global.json`.
- GUI: **WPF** on .NET 10 with the built-in Fluent theme (`ThemeMode`).
  Chosen by the product owner for maturity, tooling, and UI-automation
  friendliness (FlaUI/UIA) over WinUI 3's more native look but higher CI and
  tooling friction. The GUI is a thin MVVM shell; swapping it later is a
  bounded change because no OS logic lives in it.
- Layering (one solution, four shipping projects + tests):
  - `OpenTheWindows.Core` — `net10.0`, no Windows API references. Domain
    model (tweak, action, level, risk, profile), catalogue loading and schema
    validation, planning/apply/revert/scan engine, journal, report model,
    and the **abstractions** every OS capability is accessed through.
  - `OpenTheWindows.Windows` — `net10.0-windows10.0.26100.0`. Implementations
    of the Core abstractions using Win32/WinRT/COM/WMI. The only project
    allowed to touch the OS.
  - `OpenTheWindows.Cli` — `otw.exe`. Headless entry point, JSON output,
    documented exit codes, unattended mode.
  - `OpenTheWindows.App` — WPF GUI.
- Tests: one test project per shipping project; unit tests use in-memory
  fakes of the abstractions; integration tests run only when elevated and
  target a dedicated Windows 11 VM.

## Consequences

- Core is testable on any .NET 10 machine, including Linux CI runners for
  the pure-logic tests; Windows-specific tests are gated on OS + elevation.
- WPF has no trimming/Native AOT; the GUI ships framework-dependent or
  self-contained without trimming. The CLI may be trimmed/single-file.
- A future WinUI 3 or web front-end would consume Core/Windows unchanged.
