<#
.SYNOPSIS
    Builds every release artifact for Open the Windows: portable ZIPs, MSIs, a
    framework-dependent ZIP, an SBOM and a SHA-256 sum file.

.DESCRIPTION
        pwsh build/package.ps1                    # win-x64 + win-arm64, dev version
        pwsh build/package.ps1 -Version 1.2.3     # stamp a release version
        pwsh build/package.ps1 -Runtime win-x64   # one runtime
        pwsh build/package.ps1 -SkipMsi           # ZIPs + SBOM only (no WiX build)

    For each runtime it publishes the self-contained CLI (single-file otw.exe) and
    the self-contained WPF app, zips them into a portable bundle, and builds a
    per-machine MSI (WiX 5) from installer/OpenTheWindows.wixproj. It also builds a
    framework-dependent x64 ZIP (needs the .NET 10 Desktop Runtime on the target).
    Finally it writes a CycloneDX SBOM and a SHA256SUMS.txt over every artifact.

    Output goes to artifacts/publish/:
        OpenTheWindows-<version>-win-<rid>.zip            portable, self-contained
        OpenTheWindows-<version>-win-x64-fdd.zip          framework-dependent
        OpenTheWindows-<version>-win-<rid>.msi            per-machine installer
        sbom.cdx.json
        SHA256SUMS.txt

    Versioning: -Version wins. Otherwise the display version is the repository
    VersionPrefix plus "-local" (dev builds). The MSI's internal ProductVersion is
    always the numeric major.minor.build (Windows Installer requires that), while
    file names carry the full display version.

    Binaries are NOT code-signed (ADR 0003). The SHA-256 sums, the SBOM and - in
    CI - the build-provenance attestations are the provenance.
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string[]] $Runtime = @('win-x64', 'win-arm64'),
    [string] $Version = '',
    [switch] $SkipMsi
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$dotnetExe = 'dotnet'
if ($IsWindows -and (Test-Path 'C:\Program Files\dotnet\dotnet.exe')) {
    $dotnetExe = 'C:\Program Files\dotnet\dotnet.exe'
}

# -- Resolve versions --------------------------------------------------------
function Get-VersionPrefix {
    $props = Join-Path $repoRoot 'Directory.Build.props'
    $match = Select-String -Path $props -Pattern '<VersionPrefix>([^<]+)</VersionPrefix>' | Select-Object -First 1
    if ($match) { return $match.Matches[0].Groups[1].Value }
    return '0.1.0'
}

if ($Version) {
    $displayVersion = $Version
}
else {
    $displayVersion = "$(Get-VersionPrefix)-local"
}

if ($displayVersion -match '^\d+\.\d+\.\d+') {
    $msiVersion = $Matches[0]
}
else {
    throw "Cannot derive a numeric MSI version from '$displayVersion'; pass -Version major.minor.build."
}

Write-Host "==> version: display=$displayVersion msi=$msiVersion" -ForegroundColor Cyan

# -- Clean output ------------------------------------------------------------
$publishRoot = Join-Path $repoRoot 'artifacts/publish'
$stageRoot = Join-Path $repoRoot 'artifacts/stage'
foreach ($dir in @($publishRoot, $stageRoot)) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    New-Item -ItemType Directory -Path $dir | Out-Null
}

# The WiX build harvests the staged payload with <Files>. MSBuild's up-to-date
# check does not track those harvested files, so a stale MSI would be reused when
# only the payload changed. Clear the installer's own output to force a real build.
foreach ($dir in @('installer/bin', 'installer/obj')) {
    $full = Join-Path $repoRoot $dir
    if (Test-Path $full) { Remove-Item $full -Recurse -Force }
}

$versionArgs = @("-p:Version=$displayVersion")

function Publish-Bundle {
    param(
        [string] $Rid,
        [bool] $SelfContained,
        [string] $OutDir
    )
    $sc = if ($SelfContained) { 'true' } else { 'false' }

    # The CLI is published single-file but NOT trimmed. Its Windows interop
    # (System.Management/WMI, TaskScheduler, WinRT/Appx) reaches members through
    # reflection the trimmer cannot prove reachable (IL2104), so trimming risks
    # removing code the tool needs at runtime. Correctness beats the smaller size.
    & $dotnetExe publish src/OpenTheWindows.Cli/OpenTheWindows.Cli.csproj -c Release -r $Rid `
        --self-contained $sc -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishTrimmed=false `
        -o (Join-Path $OutDir 'cli') @versionArgs
    if ($LASTEXITCODE -ne 0) { throw "CLI publish failed for $Rid (self-contained=$sc)" }

    & $dotnetExe publish src/OpenTheWindows.App/OpenTheWindows.App.csproj -c Release -r $Rid `
        --self-contained $sc `
        -o (Join-Path $OutDir 'app') @versionArgs
    if ($LASTEXITCODE -ne 0) { throw "App publish failed for $Rid (self-contained=$sc)" }
}

# -- Self-contained bundles: publish, zip, MSI -------------------------------
$builtMsis = @()
foreach ($rid in $Runtime) {
    Write-Host "==> publish self-contained ($rid)" -ForegroundColor Cyan
    $stage = Join-Path $stageRoot $rid
    Publish-Bundle -Rid $rid -SelfContained $true -OutDir $stage

    $zip = Join-Path $publishRoot "OpenTheWindows-$displayVersion-$rid.zip"
    Write-Host "==> zip $zip" -ForegroundColor Cyan
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal

    if (-not $SkipMsi) {
        $platform = if ($rid -eq 'win-arm64') { 'arm64' } else { 'x64' }
        Write-Host "==> MSI ($platform)" -ForegroundColor Cyan
        try {
            $appArg = "-p:AppPublishDir=$(Join-Path $stage 'app')"
            $cliArg = "-p:CliPublishDir=$(Join-Path $stage 'cli')"
            & $dotnetExe build installer/OpenTheWindows.wixproj -c Release `
                $appArg $cliArg "-p:ProductVersion=$msiVersion" "-p:Platform=$platform"
            if ($LASTEXITCODE -ne 0) { throw "wix build exit $LASTEXITCODE" }

            $msiSource = Join-Path $repoRoot "installer/bin/$platform/Release/OpenTheWindows-$msiVersion-win-$platform.msi"
            $msiDest = Join-Path $publishRoot "OpenTheWindows-$displayVersion-win-$platform.msi"
            Copy-Item $msiSource $msiDest -Force
            $builtMsis += $msiDest
        }
        catch {
            if ($rid -eq 'win-x64') { throw "x64 MSI build failed: $_" }
            Write-Warning "arm64 MSI build failed and was skipped: $_"
        }
    }
}

# -- Framework-dependent x64 ZIP (needs the .NET 10 Desktop Runtime) ---------
if ($Runtime -contains 'win-x64') {
    Write-Host '==> publish framework-dependent (win-x64)' -ForegroundColor Cyan
    $fddStage = Join-Path $stageRoot 'win-x64-fdd'
    Publish-Bundle -Rid 'win-x64' -SelfContained $false -OutDir $fddStage
    $fddZip = Join-Path $publishRoot "OpenTheWindows-$displayVersion-win-x64-fdd.zip"
    Write-Host "==> zip $fddZip" -ForegroundColor Cyan
    Compress-Archive -Path (Join-Path $fddStage '*') -DestinationPath $fddZip -CompressionLevel Optimal
}

# -- SBOM --------------------------------------------------------------------
Write-Host '==> SBOM (CycloneDX)' -ForegroundColor Cyan
& $dotnetExe tool restore | Out-Null
& $dotnetExe CycloneDX OpenTheWindows.slnx --output $publishRoot --filename sbom.cdx.json --json --exclude-test-projects
if ($LASTEXITCODE -ne 0) { throw 'SBOM generation failed' }

# -- SHA256SUMS over every artifact ------------------------------------------
Write-Host '==> SHA256SUMS' -ForegroundColor Cyan
$sums = Get-ChildItem $publishRoot -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
}
Set-Content -Path (Join-Path $publishRoot 'SHA256SUMS.txt') -Value $sums -Encoding ascii
$sums | ForEach-Object { Write-Host $_ }

if (-not $SkipMsi -and $Runtime -contains 'win-x64' -and $builtMsis.Count -eq 0) {
    throw 'No MSI was produced although MSI packaging was requested.'
}

Write-Host "Package complete: $displayVersion" -ForegroundColor Green
