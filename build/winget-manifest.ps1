<#
.SYNOPSIS
    Generates the winget multi-file manifest for Open the Windows from the built
    MSIs.

.DESCRIPTION
        pwsh build/winget-manifest.ps1 -Version 1.2.3
        pwsh build/winget-manifest.ps1                     # version 0.1.0 (template)

    Reads the per-architecture MSIs in -AssetsDir (default artifacts/publish),
    computes each InstallerSha256 and reads each ProductCode, and writes the three
    winget manifest files into -OutDir (default packaging/winget):

        RandyNorthrup.OpenTheWindows.yaml                 (version)
        RandyNorthrup.OpenTheWindows.installer.yaml       (installer)
        RandyNorthrup.OpenTheWindows.locale.en-US.yaml    (default locale)

    InstallerUrl points at the GitHub release for tag v<Version>. Run
    build/package.ps1 -Version <Version> first so the asset names, SHA-256 sums and
    the URLs all line up. The committed copy is a template; the release workflow
    regenerates it against the real release assets (their SHA-256 and ProductCode).
#>
[CmdletBinding()]
param(
    [string] $Version = '0.1.0',
    [string] $RepoOwner = 'RandyNorthrup',
    [string] $RepoName = 'open-the-windows',
    [string] $AssetsDir = 'artifacts/publish',
    [string] $OutDir = 'packaging/winget'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$packageId = "$RepoOwner.OpenTheWindows"
$manifestVersion = '1.6.0'
$assets = Join-Path $repoRoot $AssetsDir
$out = Join-Path $repoRoot $OutDir

if (-not (Test-Path $assets)) {
    throw "Assets directory '$assets' not found; run build/package.ps1 first."
}

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Path $out | Out-Null

function Get-MsiProductCode {
    param([string] $Path)
    $installer = New-Object -ComObject WindowsInstaller.Installer
    try {
        $db = $installer.GetType().InvokeMember('OpenDatabase', 'InvokeMethod', $null, $installer, @((Resolve-Path $Path).Path, 0))
        $view = $db.GetType().InvokeMember('OpenView', 'InvokeMethod', $null, $db, @("SELECT Value FROM Property WHERE Property='ProductCode'"))
        # Execute returns a value that would leak into the function's output; discard it.
        $null = $view.GetType().InvokeMember('Execute', 'InvokeMethod', $null, $view, $null)
        $rec = $view.GetType().InvokeMember('Fetch', 'InvokeMethod', $null, $view, $null)
        $value = $rec.GetType().InvokeMember('StringData', 'GetProperty', $null, $rec, [object[]]@(1))
        $text = [string]($value | Select-Object -First 1)
        if ($text -match '\{[0-9A-Fa-f-]{36}\}') { return $Matches[0] }
        throw "Could not read a ProductCode GUID from '$Path' (got '$text')."
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($installer) | Out-Null
    }
}

# Discover the per-architecture MSIs and build their installer entries. Scalar
# locals per iteration keep the assignments off the PSSA hashtable-alignment path.
$installerEntries = [System.Collections.Generic.List[string]]::new()
$summary = [System.Collections.Generic.List[string]]::new()
foreach ($arch in @('x64', 'arm64')) {
    $msi = Get-ChildItem $assets -Filter "OpenTheWindows-*-win-$arch.msi" -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $msi) { continue }

    $sha = (Get-FileHash $msi.FullName -Algorithm SHA256).Hash
    $productCode = Get-MsiProductCode $msi.FullName
    $url = "https://github.com/$RepoOwner/$RepoName/releases/download/v$Version/OpenTheWindows-$Version-win-$arch.msi"

    $installerEntries.Add("  - Architecture: $arch")
    $installerEntries.Add("    InstallerUrl: $url")
    $installerEntries.Add("    InstallerSha256: $sha")
    $installerEntries.Add("    ProductCode: '$productCode'")
    $summary.Add("  ${arch}: $sha")
}

if ($installerEntries.Count -eq 0) {
    throw "No MSI found in '$assets' (looked for OpenTheWindows-*-win-{x64,arm64}.msi)."
}

# --- version manifest -------------------------------------------------------
$versionYaml = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.$manifestVersion.schema.json
PackageIdentifier: $packageId
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: $manifestVersion
"@

# --- installer manifest -----------------------------------------------------
$installerLines = [System.Collections.Generic.List[string]]::new()
$installerLines.Add("# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.$manifestVersion.schema.json")
$installerLines.Add("PackageIdentifier: $packageId")
$installerLines.Add("PackageVersion: $Version")
$installerLines.Add('InstallerType: wix')
$installerLines.Add('Scope: machine')
$installerLines.Add('InstallModes:')
$installerLines.Add('  - interactive')
$installerLines.Add('  - silent')
$installerLines.Add('  - silentWithProgress')
$installerLines.Add('InstallerSwitches:')
$installerLines.Add('  Silent: /qn')
$installerLines.Add('  SilentWithProgress: /qb!')
$installerLines.Add('UpgradeBehavior: install')
$installerLines.Add('Installers:')
$installerEntries | ForEach-Object { $installerLines.Add($_) }
$installerLines.Add('ManifestType: installer')
$installerLines.Add("ManifestVersion: $manifestVersion")
$installerYaml = ($installerLines -join "`n")

# --- default-locale manifest ------------------------------------------------
$localeYaml = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.$manifestVersion.schema.json
PackageIdentifier: $packageId
PackageVersion: $Version
PackageLocale: en-US
Publisher: Open The Windows contributors
PublisherUrl: https://github.com/$RepoOwner/$RepoName
PublisherSupportUrl: https://github.com/$RepoOwner/$RepoName/issues
PackageName: Open the Windows
PackageUrl: https://github.com/$RepoOwner/$RepoName
License: MIT
LicenseUrl: https://github.com/$RepoOwner/$RepoName/blob/main/LICENSE
ShortDescription: Windows 11 privacy, update, security and performance control.
Description: >-
  Open the Windows is an elevated Windows 11 utility that applies reversible,
  catalogue-driven tweaks for privacy, security, performance and debloat, controls
  Windows Update pausing, and works from a WPF app or the otw command-line tool.
  Every change is transactional and can be reverted; nothing self-defeating (no
  breaking Windows Update, Defender or update servicing) is offered.
Moniker: open-the-windows
Tags:
  - windows
  - windows-11
  - privacy
  - security
  - performance
  - debloat
  - telemetry
  - hardening
ManifestType: defaultLocale
ManifestVersion: $manifestVersion
"@

$nl = "`n"
Set-Content -Path (Join-Path $out "$packageId.yaml") -Value ($versionYaml -replace "`r`n", $nl) -Encoding utf8 -NoNewline
Add-Content -Path (Join-Path $out "$packageId.yaml") -Value $nl -Encoding utf8 -NoNewline
Set-Content -Path (Join-Path $out "$packageId.installer.yaml") -Value ($installerYaml + $nl) -Encoding utf8 -NoNewline
Set-Content -Path (Join-Path $out "$packageId.locale.en-US.yaml") -Value ($localeYaml -replace "`r`n", $nl) -Encoding utf8 -NoNewline
Add-Content -Path (Join-Path $out "$packageId.locale.en-US.yaml") -Value $nl -Encoding utf8 -NoNewline

Write-Host "Wrote winget manifest for $packageId $Version to $OutDir" -ForegroundColor Green
$summary | ForEach-Object { Write-Host $_ }
