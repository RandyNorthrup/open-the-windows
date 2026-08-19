<#
.SYNOPSIS
    Publishes release binaries for Open the Windows: the CLI (otw.exe) and the WPF app.

.DESCRIPTION
        pwsh build/publish.ps1                       # win-x64 + win-arm64, self-contained
        pwsh build/publish.ps1 -Runtime win-x64      # one runtime
        pwsh build/publish.ps1 -Version 0.2.0        # stamp a release version

    Output: artifacts/publish/<runtime>/cli/otw.exe (single-file, self-contained, NOT trimmed: ~100 MB)
            artifacts/publish/<runtime>/app/OpenTheWindows.exe (+ files; WPF cannot single-file trim)

    The CLI is not trimmed: its Windows interop (WMI, TaskScheduler, WinRT/Appx)
    uses reflection the trimmer cannot prove safe (IL2104), so trimming could
    remove members the tool needs at runtime. build/package.ps1 is the full
    release packager (ZIPs + MSI + SBOM); this script is the CI publish smoke.
            artifacts/publish/OpenTheWindows-<version>-<runtime>.zip (portable bundle)
            artifacts/publish/SHA256SUMS.txt

    Binaries are NOT code-signed (ADR 0003). Hashes and SBOM are the provenance.
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string[]] $Runtime = @('win-x64', 'win-arm64'),
    [string] $Version = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$dotnetExe = 'dotnet'
if ($IsWindows -and (Test-Path 'C:\Program Files\dotnet\dotnet.exe')) {
    $dotnetExe = 'C:\Program Files\dotnet\dotnet.exe'
}

$publishRoot = Join-Path $repoRoot 'artifacts/publish'
if (Test-Path $publishRoot) {
    Remove-Item $publishRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $publishRoot | Out-Null

$versionArgs = @()
if ($Version) {
    $versionArgs = @("-p:Version=$Version")
}

foreach ($rid in $Runtime) {
    Write-Host "==> publish CLI ($rid)" -ForegroundColor Cyan
    & $dotnetExe publish src/OpenTheWindows.Cli/OpenTheWindows.Cli.csproj -c Release -r $rid `
        --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishTrimmed=false `
        -o (Join-Path $publishRoot "$rid/cli") @versionArgs
    if ($LASTEXITCODE -ne 0) { throw "CLI publish failed for $rid" }

    Write-Host "==> publish App ($rid)" -ForegroundColor Cyan
    & $dotnetExe publish src/OpenTheWindows.App/OpenTheWindows.App.csproj -c Release -r $rid `
        --self-contained true `
        -o (Join-Path $publishRoot "$rid/app") @versionArgs
    if ($LASTEXITCODE -ne 0) { throw "App publish failed for $rid" }

    $stamp = if ($Version) { $Version } else { 'local' }
    $zip = Join-Path $publishRoot "OpenTheWindows-$stamp-$rid.zip"
    Write-Host "==> zip $zip" -ForegroundColor Cyan
    Compress-Archive -Path (Join-Path $publishRoot "$rid/*") -DestinationPath $zip -CompressionLevel Optimal
}

Write-Host '==> SBOM (CycloneDX)' -ForegroundColor Cyan
& $dotnetExe tool restore | Out-Null
& $dotnetExe CycloneDX OpenTheWindows.slnx --output $publishRoot --filename sbom.cdx.json --json --exclude-test-projects
if ($LASTEXITCODE -ne 0) { throw 'SBOM generation failed' }

Write-Host '==> SHA256SUMS' -ForegroundColor Cyan
$sums = Get-ChildItem $publishRoot -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
}
Set-Content -Path (Join-Path $publishRoot 'SHA256SUMS.txt') -Value $sums -Encoding ascii
$sums | ForEach-Object { Write-Host $_ }
Write-Host 'Publish complete.' -ForegroundColor Green
