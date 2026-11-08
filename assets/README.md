# Logo and icons

The visual identity for **Open the Windows**: a single window with its sash swung
open, light coming through, and a shield on the open sash. It stands for what the
app does: open up Windows 11's settings, keep you protected, and let you close
everything again. It deliberately avoids Microsoft's four-pane Windows logo.

## Files

- `open-the-windows-icon.png`: the app icon, 1024 × 1024 with a transparent
  background. Every other file here is made from it.
- `open-the-windows-logo.png`: the horizontal logo (icon and wordmark) for light
  backgrounds. It is the README banner in light mode.
- `open-the-windows-logo-dark.png`: the same logo for dark backgrounds (the README
  banner in dark mode).
- `brand-preview.png`: the 1280 × 640 GitHub social-preview banner.
- `favicon/`: `favicon.ico` (16, 32 and 48 px), `favicon-32x32.png`,
  `apple-touch-icon.png` (180 px) and `site.webmanifest`, for a web front end.
- `screenshots/`: screenshots of each page of the desktop app, used by the README
  and the wiki.

## Where each asset is used

- **App window, taskbar and Explorer icon**: `src/OpenTheWindows.App/Resources/OpenTheWindows.ico`
  (16 to 256 px), wired in `OpenTheWindows.App.csproj` (`ApplicationIcon`) and on
  the main window (`Icon="Resources/OpenTheWindows.ico"`).
- **In-app mark** (navigation rail): `src/OpenTheWindows.App/Resources/OpenTheWindowsMark.png`
  (256 px), declared as `OtwMark` in `Resources/BrandArt.xaml`.
- **README banner**: `open-the-windows-logo.png` and `open-the-windows-logo-dark.png`,
  chosen by the reader's GitHub theme.
- **GitHub social preview**: `brand-preview.png`, set under the repository's
  **Settings → General → Social preview** (GitHub has no API for it).

## Palette

- Midnight: `#081630`
- Control blue: `#0C3A78`
- Azure: `#1E88E5`
- Light cyan: `#4FC3F7`
- White: `#FFFFFF`

The wordmark is set in Segoe UI Semibold, with the tagline in Segoe UI Bold.
