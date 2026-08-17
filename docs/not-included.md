# What Open the Windows leaves out (the snake-oil list)

Many "optimiser" and "debloat" tools ship tweaks that are placebos, are measured
in noise, or trade real security and stability for a benchmark number that does
not move. Open the Windows deliberately does **not** ship these. This page is the
public list, so it is clear that leaving them out is a decision, not an omission.

Three verdicts are used, from research
[05 §7](research/05-performance-debloat-catalog.md):

- **Excluded** — placebo or harmful; never shipped, never advertised.
- **Advanced, with warning** — a real effect but with a security/stability
  trade-off; offered only behind an explicit advanced gate, with a
  plain-language warning and one-click undo, never in a default profile.
- **Check-only** — the app may *show* the current state but never changes it.

Anything that is not merely useless but actively dangerous (breaking Windows
Update, Defender, licensing or the servicing store) is on the separate
[refusals list](refusals.md) instead.

## Timing, scheduling and "FPS" myths

| Tweak (as marketed) | Why it does nothing (or harm) | Verdict |
| --- | --- | --- |
| Timer-resolution "optimisers" (ISLC, forcing 0.5 ms globally) | Since Windows 10 2004 the timer request is per-process; a background service cannot change other processes' resolution, and it costs power. | Excluded |
| `bcdedit useplatformclock`, "disable HPET for FPS", `disabledynamictick`, `tscsyncpolicy` | Forces the slower HPET or changes kernel timekeeping; results are anecdotal and often negative, and editing the BCD risks a BitLocker recovery prompt. | Excluded |
| `NetworkThrottlingIndex=0xFFFFFFFF` | Only affects apps using an MMCSS multimedia thread; a historic Vista workaround with no measurable benefit today. | Excluded |
| `SystemResponsiveness=0` | Values below 10 are clamped to 20 by the driver, so 0 is identical to the default. | Excluded |
| MMCSS `Tasks\Games` GPU/SFIO priority edits | GPU Priority "is not yet used" and SFIO Priority "is not used" per Microsoft; the "High" category can starve the UI. | Excluded |
| `Win32PrioritySeparation` "gaming" values | The client default is already foreground-biased; changes are within noise and can hurt background work. | Check-only |
| Nagle off (`TcpAckFrequency=1`, `TCPNoDelay=1`) | Helps only specific chatty TCP apps; can increase packet rate and CPU. Modern games use UDP. | Advanced, per-app only |

## Memory and storage "boosters"

| Tweak (as marketed) | Why it does nothing (or harm) | Verdict |
| --- | --- | --- |
| `LargeSystemCache=1` | A server file-server setting; on a client it starves apps of memory and worsens paging. | Excluded |
| `DisablePagingExecutive=1` | Only meaningful for kernel debugging; wastes RAM on modern systems. | Excluded |
| Disable / shrink / remove the page file | Breaks crash dumps and causes commit-limit OOM crashes; the SSD-wear argument is obsolete. | Check-only |
| Disable memory compression / SysMain / Superfetch / Prefetch | Windows already adapts to SSDs; disabling gives no gain and can slow app launch. `EnableSuperfetch` is ignored on Windows 10+/11. | Excluded (SysMain: Advanced for HDD "100% disk" only) |
| RAM "optimisers" / `EmptyStandbyList` on a timer | Forces re-reads from disk; the standby list *is* the cache. | Excluded |
| Registry "cleaners" / "defragmenters" | Microsoft does not support them; no measurable gain and well-documented breakage. | Excluded |
| `SvcHostSplitThresholdInKB` (fewer svchosts) | Microsoft split services for reliability, security isolation and diagnostics; merging saves a few MB and reintroduces shared-failure domains. | Excluded |
| `fsutil` NTFS knobs (`disablelastaccess`, `disable8dot3`, `memoryusage 2`, `mftzone`) | Last-access is already managed; 8.3 removal breaks legacy installers; the rest are irrelevant on modern volumes. | Check-only / Developer |

## Power, CPU and GPU pokes

| Tweak (as marketed) | Why it does nothing (or harm) | Verdict |
| --- | --- | --- |
| "Unpark all cores", core-parking registry hacks | The Balanced plan already unparks under load; hidden-attribute editing can corrupt the power plan. | Excluded (documented powercfg indices only) |
| Ultimate Performance plan on laptops | Microsoft hides it on battery systems; it kills battery and heat and can throttle harder. | Excluded on laptops (desktop: Advanced) |
| MSI-mode / interrupt-affinity registry edits | Real but hardware-specific; wrong values cause no-boot or BSOD. | Advanced, expert tooling only |
| HAGS force on/off for everyone | Vendor-driver dependent; can reduce or increase performance. | Check-only |
| MPO disable (`OverlayTestMode=5`) as an "FPS boost" | A flicker-troubleshooting step that can reduce efficiency and performance. | Advanced, troubleshooting only |
| GPU control-panel registry pokes ("prefer max performance", "threaded optimisation") | Undocumented, driver-version-specific keys; out of scope. | Excluded |
| Disable HVCI / VBS / memory integrity "for FPS" by default | A real effect but a kernel-exploit-mitigation regression; Microsoft advises re-enabling after gaming. | Advanced, never default, never silent |

## Shutdown, shell and "latency"

| Tweak (as marketed) | Why it does nothing (or harm) | Verdict |
| --- | --- | --- |
| `AutoEndTasks=1`, short `HungAppTimeout` / `WaitToKill*` "faster shutdown" | Real but risks data loss for apps saving on close; saves marginal seconds. | Advanced (Developer), with warning |
| Global "disable fullscreen optimizations" | Microsoft: FSO improves most titles; a global disable loses HDR and colour management in exclusive fullscreen. | Advanced, per-app only |
| Disable Windows Search service "to save CPU" | Start-menu typing, Explorer search and Outlook search break; the indexer already backs off under load. | Advanced (Paranoid), with warning; scope reduction offered instead |
| "Prefer IPv4" via `DisabledComponents` | Disabling IPv6 (`0xFF`) is not recommended and breaks Teredo, some VPNs and WSL2 port proxy. | Advanced ("prefer IPv4" `0x20` only) |
| DirectStorage / windowed optimisations off "for stability" | Real latency benefits with no evidence of harm. | Excluded (left enabled) |

The rule behind this page: a tweak ships only when there is a documented,
measurable benefit and a clean revert. "It feels faster" is not a measurement,
and a benchmark that moves within the margin of error is not a benefit. Where a
setting has a real effect but a genuine trade-off, it lives behind the Advanced
gate with the cost spelled out — never in a Basic or Balanced profile.
