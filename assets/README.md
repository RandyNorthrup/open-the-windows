# Logo and icons

The visual identity for **Open the Windows**, designed around the app's
Windows 11 control-panel role. The mark shows three stable panes and one pane
opening outward — a shorthand for exposing and controlling Windows settings
without copying Microsoft's Windows logo.

## Files

- `open-the-windows-logo.svg` — primary horizontal logo, for light backgrounds
  (used as the README banner).
- `open-the-windows-logo-reversed.svg` — horizontal logo for dark backgrounds.
- `open-the-windows-logo-stacked.svg` — stacked (mark over wordmark) lockup.
- `open-the-windows-mark.svg` — full standalone app mark (with shading).
- `open-the-windows-mark-simple.svg` — flat app mark; the source of the app's
  in-UI vector art (`src/OpenTheWindows.App/Resources/BrandArt.xaml`).
- `brand-preview.png` — the GitHub social-preview banner (see below).
- `favicon/` — `favicon.svg`, `favicon.ico`, `favicon-32x32.png`,
  `apple-touch-icon.png` and `site.webmanifest` for a docs/web front end.

The Windows application icon is embedded from
`src/OpenTheWindows.App/Resources/OpenTheWindows.ico` (a multi-resolution `.ico`,
16–256 px). Windows requires an `.ico` for an executable icon, so that one file
is a raster; everything shown inside the UI is vector.

## Where each asset is used

- **App window / taskbar / Explorer icon** — `OpenTheWindows.ico`, wired in
  `OpenTheWindows.App.csproj` (`ApplicationIcon`) and on the main window
  (`Icon="Resources/OpenTheWindows.ico"`).
- **In-app mark** (navigation header and About page) — the vector
  `DrawingImage` in `Resources/BrandArt.xaml`, converted from
  `open-the-windows-mark-simple.svg`.
- **README banner** — `assets/open-the-windows-logo.svg`.
- **GitHub social preview** — upload `brand-preview.png` under the repository's
  **Settings → General → Social preview** (a one-time manual step; GitHub has no
  API for it).

## Palette

- Midnight: `#09182C`
- Control blue: `#0867C8`
- Open-pane cyan: `#53D7FF`
- Ice white: `#F8FBFF`
- Primary text: `#101D2E`

The SVG wordmark uses the `Inter, Segoe UI, Arial, sans-serif` font stack. The
files remain editable vector artwork.
