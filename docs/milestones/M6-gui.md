# M6 — WPF GUI

## Goal

Full GUI over the M1–M5 engine. Thin MVVM: every view model is testable
without a UI thread; every action goes through Core services; no OS calls in
`OpenTheWindows.App`.

## Preconditions

M5 certified. Read: research 02 §5 (UX patterns), PLAN §5.4; ADR 0001.

## Structure (`src/OpenTheWindows.App`)

- `DesktopApp` (exists) registers all Core/Windows services + view models +
  `INavigationService` + `IDialogService` (message/confirm/typed-confirm) +
  `IDispatcherService` (marshal to UI thread) — all interfaces in
  `App/Services`, fakes in App.Tests.
- Shell: `MainWindow` with left `NavigationView`-style list (ListBox styled) —
  pages: **Dashboard** (health + last scan + quick actions), **Privacy**,
  **Updates**, **Security**, **Performance**, **Debloat**, **Shell**,
  **Profiles**, **History**, **Settings**, **About**. Category pages share one
  `CategoryPageViewModel` (level dial Off/Basic/Balanced/Strict/Paranoid,
  entry list with search box, filters risk/status/managed, per-entry
  `TweakItemViewModel` with checkbox, risk badge, managed badge, restart
  badge, "Verified on" chip; detail pane with description, rationale, side
  effects, actions (human-readable), sources as links, `verifiedOn`).
- Flows: **What-if** (diff view: entry → action → current → desired), **Apply**
  (progress per entry, cancel between entries, summary with restart
  requirement, "Restart Explorer now" button), **Revert** (History page: run
  list → entries → revert run/entry), **Profiles** (built-in list, import,
  export, sign/verify status), **Updates** page has the pause presets, extended
  pause (warning dialog), resume-now, EOS status, "managed by organisation"
  read-only mode.
- Typed confirmation dialog for Breaking entries (user types the id).
- Settings: restore point on/off, restart Explorer automatically, include
  Draft (dev only), theme (System/Light/Dark), language (en-US only in M6, resx
  infrastructure with `Strings.resx` + `Strings.Designer.cs`), enterprise mode
  (hides consumer profiles; Q2).
- Accessibility: every interactive element has `AutomationProperties.Name`
  and a stable `AutomationId` (`<Page>_<Element>`), full keyboard navigation,
  visible focus, high-contrast check, minimum 4.5:1 text contrast in both
  themes; DPI per-monitor v2 (manifest exists).
- Fix from M0: `CanApply` shows "Yes/No" (converter), not "True/False".

## Tests

- View-model tests for every VM (search/filter/dial/select/apply flow with
  fake engine incl. failure + rollback surface; typed-confirm gating; managed
  entries not selectable without break-glass; Updates page guardrails).
- FlaUI (pin `FlaUI.UIA3` 5.0.0) smoke on the VM: launch elevated, navigate
  every page, apply one Safe entry, verify via `otw scan`, revert, close —
  driven by `build/ui-smoke.ps1`; screenshots per page saved to
  `docs/certification/M6/*.png`.
- Coverage: App project view models ≥ 85% line; XAML code-behind excluded via
  `-filefilters:-*.xaml.cs` **only** for `MainWindow.xaml.cs`-style
  constructors (document in PLAN §7.3).

## Gates / docs / performance / acceptance

- All gates + `ui-smoke` (VM) recorded in certification.
- Docs: `docs/gui.md` (page map + screenshots), README, CHANGELOG, PLAN.
- Performance: cold start < 2 s on the VM (Stopwatch in `build/ui-smoke.ps1`),
  500-entry list virtualised (`VirtualizingStackPanel`), no UI freeze > 100 ms
  during apply (work on background thread, progress marshalled).
- Acceptance: every page reachable by keyboard; screen reader announces
  entry title/risk/state; apply→revert round trip via GUI verified with
  `otw scan`; accessibility manual check record in `docs/certification/M6.md`.
