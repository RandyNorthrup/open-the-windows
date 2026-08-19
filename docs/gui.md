# The Open the Windows GUI

`OpenTheWindows.exe` is a WPF desktop app (`src/OpenTheWindows.App`) over the
same Core engine the `otw` CLI uses. It runs elevated (the manifest requests
administrator) because its purpose is to read and change OS state; per-user
changes still target the interactive user's hive, never the elevating account.

The app is a thin MVVM shell: every page is a view model that is testable
without a UI thread, and no OS calls live in the App project — they all go
through Core services (`IApplyCoordinator`, `IUpdateCoordinator`, the profile
loader, the doctor). The shell is a left navigation list bound to a content
host; each page view model is resolved from the DI container by its key and
rendered through a `DataTemplate`. A **Back** button above the content returns to
the previous page (a navigation back-stack), and the navigation rail always
highlights the page actually shown, including navigations started from a quick
action.

**Nothing scans on its own.** Reads that probe the machine — the Dashboard's
baseline audit and the Updates status — run only when the user asks (the “Score
this machine” and “Refresh status” buttons). The Dashboard's system-check
(OS / edition / elevation) is the one automatic read: it is environment detection,
not a tweak or update scan, and it also has a Re-check button.

Every interactive control and every list badge carries a plain-language tooltip;
the tooltips describe the control or the badge concept and point to the detail
pane for an entry's specifics, so they never duplicate the detail pane. Each page
owns its own scrolling, so a page never shows a second, detached scrollbar.

## Pages

| Page | What it does |
| ---- | ------------ |
| **Dashboard** | System health (the pre-flight doctor report: OS, edition, elevation, support status, whether changes can be applied), the security-baseline scores (run on demand with **Score this machine**), and task quick actions (review privacy, pause Windows Update, harden security, apply a profile, view history). |
| **Privacy / Security / Performance / Debloat / Shell** | The shared category page. A level dial (Off / Basic / Balanced / Strict / Paranoid) pre-selects the entries a profile at that level would apply; the user refines per entry. A search box (id / title / description / rationale / tags) and risk / draft filters narrow the visible list without changing the selection. Each entry shows risk / restart / draft / "verified on" badges; the selected row opens a detail pane with the description, rationale, side effects, the human-readable actions, source links and verification history. **Review & apply…** opens the apply flow. When nothing is visible the list is replaced by a hint explaining why (a section whose entries are all still drafts — e.g. Shell today — points you to the Drafts toggle). |
| **Updates** | The running release and its end-of-servicing (with the 90-day warning), the pause state and any pinned release — loaded on **Refresh status**. Quick pause presets within the 35-day Windows maximum, an extended pause beyond it behind a warning, and resume. When the pause is owned by organisation policy the controls are read-only. |
| **Profiles** | The built-in profiles (scope, signing state, resolved entry count). **Review & apply…** resolves a profile and opens the apply flow; **Export…** writes a profile file; **Import…** loads one, validates it, and reports its signing state (unsigned / untrusted / trusted / invalid). |
| **History** | Prior runs (newest first) with time, profile, result and entry count; a completed non-revert run can be reverted, restoring the values it changed. |
| **Settings** | Restore-point-before-apply, automatic Explorer restart, show-draft-entries, enterprise mode (hides the consumer-only profiles), theme (System / Light / Dark, applied live) and language (en-US in M6). |
| **About** | Product identity, version, licence and the non-affiliation notice. |

## The review-and-apply flow

Every apply — from a category page or a profile — goes through one modal flow
(`ApplyFlowViewModel` / `ApplyDialog`):

1. **Preview** — the what-if plan (`ApplyEngine.Plan`): per entry, what the run
   would do, its risk, a note, and each action with its current compliance
   state.
2. **Confirm** — every Breaking entry is gated behind typing its id.
3. **Apply** — the transactional apply runs on a background thread so the UI
   never freezes; the result shows the per-entry outcome and the aggregate
   restart requirement, and offers to restart Explorer.
4. **Revert** — from the History page; the engine restores the run's captured
   prior state.

A failed run is rolled back by the engine and reported as leaving the machine
unchanged. The atomic apply is not interrupted mid-transaction; cancellation is
available before it starts.

## Accessibility

- Every interactive element has a stable `AutomationId` of the form
  `<Page>_<Element>` (navigation items are `Nav_<Page>`), and a name — from its
  text content or an explicit `AutomationProperties.Name`.
- Risk and status are shown with text as well as colour, never colour alone.
- The window is per-monitor-DPI-v2 aware (app manifest) and follows the WPF
  Fluent theme, so colours track the light/dark/high-contrast system setting;
  the theme can also be pinned in Settings.
- Keyboard navigation and visible focus use the WPF defaults; the navigation
  list is keyboard-traversable.

## Smoke test

`build/ui-smoke.ps1` publishes the app, launches it elevated, navigates every
page with FlaUI, captures a screenshot of each to `docs/certification/M6/`, and
measures cold start. It requires an interactive, elevated desktop session (UI
automation drives real windows, so it cannot run over a headless SSH session).

```powershell
pwsh build/ui-smoke.ps1
```

`build/smoke-gui.ps1` is the lighter visual smoke: it launches the app and
captures the shell window.
