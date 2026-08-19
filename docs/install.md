# Installing Open the Windows

Open the Windows ships three ways: an **MSI** (per-machine installer), the
**winget** package, and a **portable ZIP**. All are produced by
`build/package.ps1` and published on the GitHub Releases page for each `v*` tag.

The binaries are **not code-signed** (see
[ADR 0003](adr/0003-unsigned-distribution.md)). Instead every release carries
SHA-256 sums, a CycloneDX SBOM, and GitHub build-provenance attestations so you
can verify an asset was built by our CI from a specific commit. On first run of a
downloaded, unsigned binary SmartScreen shows an "unknown publisher" prompt —
choose **More info → Run anyway**. Enterprises that allow-list by publisher must
re-sign or add hash rules; see [enterprise.md](enterprise.md).

Requires Windows 11 (build 22000 or newer). Applying tweaks needs an elevated
(administrator) token; the app requests it via UAC, and the CLI must be run from
an elevated prompt.

## Channels at a glance

| Channel | File | Size | Needs .NET runtime? | Best for |
| --- | --- | --- | --- | --- |
| MSI | `OpenTheWindows-<version>-win-<arch>.msi` | ~90 MB | No (self-contained) | Most installs; enterprise deployment |
| winget | `RandyNorthrup.OpenTheWindows` | — | No | Command-line install and upgrade |
| Portable ZIP | `OpenTheWindows-<version>-win-<arch>.zip` | ~110 MB | No (self-contained) | No-install / USB / locked-down hosts |
| Framework-dependent ZIP | `OpenTheWindows-<version>-win-x64-fdd.zip` | ~18 MB | Yes (.NET 10 Desktop Runtime) | Smallest download when the runtime is present |

`<arch>` is `x64` or `arm64`.

## MSI (recommended)

Per-machine install into `%ProgramFiles%\Open the Windows\{app,cli}`, with a
Start-menu shortcut, an optional "Add otw to PATH" feature (on by default), and
the Event Log source registered at install time.

```powershell
# Interactive
msiexec /i OpenTheWindows-<version>-win-x64.msi

# Silent (no UI)
msiexec /i OpenTheWindows-<version>-win-x64.msi /qn

# Silent, without adding otw.exe to PATH
msiexec /i OpenTheWindows-<version>-win-x64.msi /qn REMOVE=PathEntry
```

**Upgrade:** install a newer MSI over an older one — it performs a major upgrade
in place, leaving a single Add/Remove Programs entry. **Uninstall:** Settings →
Apps → Installed apps → Open the Windows → Uninstall, or
`msiexec /x OpenTheWindows-<version>-win-x64.msi /qn`.

## winget

Once the manifest is accepted into the community repository:

```powershell
winget install RandyNorthrup.OpenTheWindows
winget upgrade RandyNorthrup.OpenTheWindows
winget uninstall RandyNorthrup.OpenTheWindows
```

The winget manifest carries the installer's SHA-256, which winget verifies before
installing.

## Portable ZIP

No installer. Extract anywhere and run in place:

```powershell
Expand-Archive OpenTheWindows-<version>-win-x64.zip -DestinationPath C:\Tools\OpenTheWindows
C:\Tools\OpenTheWindows\app\OpenTheWindows.exe   # GUI (elevates via UAC)
C:\Tools\OpenTheWindows\cli\otw.exe doctor       # CLI (run elevated to apply)
```

The framework-dependent ZIP (`-fdd`) is the same layout but needs the
[.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
installed; it is much smaller because it does not bundle the runtime.

## Verify a download

Every release includes `SHA256SUMS.txt` and `sbom.cdx.json`.

```powershell
# Compare the hash against SHA256SUMS.txt
(Get-FileHash OpenTheWindows-<version>-win-x64.msi -Algorithm SHA256).Hash

# Verify build provenance with the GitHub CLI (proves our CI built it)
gh attestation verify OpenTheWindows-<version>-win-x64.msi --repo RandyNorthrup/open-the-windows
```

The SBOM (`sbom.cdx.json`, CycloneDX) lists every component and version that went
into the build.

## Where data lives

- **Install directory:** `%ProgramFiles%\Open the Windows\` (`app\`, `cli\`).
- **Journals and audit trail:** `%ProgramData%\OpenTheWindows\journal\` and
  `…\audit\`. These record every apply/revert so changes stay reversible.
- **Windows Event Log:** a custom log named `OpenTheWindows`, source `OTW`
  (registered by the installer, or on first elevated run — see
  [event-log.md](event-log.md); `otw eventlog status` reports it).

**Uninstall keeps the journals and audit trail on purpose** — they are the record
of what the tool changed on the machine, and for an elevated security tool that
history is worth preserving. To remove that data as well, delete it manually
**after** uninstalling:

```powershell
Remove-Item -Recurse -Force "$env:ProgramData\OpenTheWindows"
```

The installer intentionally offers no silent switch to wipe this data, so an
uninstall can never quietly destroy the audit trail.
