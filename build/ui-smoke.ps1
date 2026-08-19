<#
.SYNOPSIS
    GUI smoke for M6: launches the published WPF app, navigates every page with
    FlaUI, captures a screenshot of each, and measures cold-start time.

.DESCRIPTION
    Milestone-certification harness (M6). It publishes the app self-contained,
    re-launches itself elevated (one UAC prompt, because the app manifest
    requires administrator), attaches with FlaUI/UIA3, clicks each navigation
    item by its stable AutomationId (Nav_<Page>), and saves a screenshot per
    page to docs/certification/M6/<Page>.png. Cold start (launch to first
    window) is measured with a Stopwatch and must be under the target.

    REQUIRES AN INTERACTIVE, ELEVATED DESKTOP SESSION. UI automation renders and
    drives real windows, so this cannot run over a headless SSH session
    (Windows session 0 has no interactive desktop). Run it from an elevated
    console on the machine under test (the lab VM's console, or a dev box).

        pwsh build/ui-smoke.ps1                    # publishes, then drives the app
        pwsh build/ui-smoke.ps1 -ExePath <path>    # drive an already-published exe

    The apply -> revert round trip the GUI performs is the same transactional
    engine the CLI drives; it is verified on the VM through `otw apply`/`otw
    revert` (M5) and by the App view-model tests. This harness focuses on the
    GUI-specific surface: that every page is reachable and renders.
#>
[CmdletBinding()]
param(
    [string] $ExePath = '',
    [string] $OutputDir = '',
    [int] $TimeoutSeconds = 60,
    [double] $ColdStartBudgetSeconds = 2.0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnet = if (Test-Path 'C:\Program Files\dotnet\dotnet.exe') { 'C:\Program Files\dotnet\dotnet.exe' } else { 'dotnet' }
if (-not $OutputDir) {
    $OutputDir = Join-Path $repoRoot 'docs/certification/M6'
}

$pages = @(
    'Dashboard', 'Privacy', 'Updates', 'Security', 'Performance',
    'Debloat', 'Shell', 'Profiles', 'History', 'Settings', 'About'
)

function Publish-App {
    param([string] $Root, [string] $Dotnet)
    $out = Join-Path $Root 'artifacts/publish/ui-smoke'
    Write-Host 'Publishing the app self-contained...' -ForegroundColor Cyan
    & $Dotnet publish (Join-Path $Root 'src/OpenTheWindows.App/OpenTheWindows.App.csproj') `
        -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $out | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    return (Join-Path $out 'OpenTheWindows.exe')
}

function Restore-FlaUi {
    param([string] $Root, [string] $Dotnet)
    $proj = Join-Path $Root 'artifacts/ui-smoke-flaui'
    New-Item -ItemType Directory -Force -Path $proj | Out-Null
    @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="FlaUI.UIA3" Version="5.0.0" />
  </ItemGroup>
</Project>
'@ | Set-Content -Path (Join-Path $proj 'flaui.csproj') -Encoding UTF8
    Write-Host 'Restoring FlaUI...' -ForegroundColor Cyan
    & $Dotnet build (Join-Path $proj 'flaui.csproj') -c Release -o (Join-Path $proj 'bin') | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'FlaUI restore failed.' }
    return (Join-Path $proj 'bin')
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$isElevated = ([Security.Principal.WindowsPrincipal] $identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isElevated) {
    Write-Host 'Re-launching elevated (UAC prompt expected)...' -ForegroundColor Cyan
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath,
        '-ExePath', $ExePath, '-OutputDir', $OutputDir, '-TimeoutSeconds', $TimeoutSeconds)
    Start-Process -FilePath 'pwsh' -ArgumentList $argList -Verb RunAs -Wait
    exit $LASTEXITCODE
}

if (-not $ExePath) { $ExePath = Publish-App -Root $repoRoot -Dotnet $dotnet }
if (-not (Test-Path $ExePath)) { throw "App not found at $ExePath." }
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$flaUiLib = Restore-FlaUi -Root $repoRoot -Dotnet $dotnet
foreach ($dll in @('FlaUI.Core.dll', 'FlaUI.UIA3.dll', 'Interop.UIAutomationClient.dll')) {
    Add-Type -Path (Join-Path $flaUiLib $dll)
}

$automation = [FlaUI.UIA3.UIA3Automation]::new()
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$app = [FlaUI.Core.Application]::Launch($ExePath)
$window = $app.GetMainWindow($automation, [TimeSpan]::FromSeconds($TimeoutSeconds))
$stopwatch.Stop()
$coldStart = $stopwatch.Elapsed.TotalSeconds
Write-Host ("Cold start: {0:N2}s (budget {1:N1}s)" -f $coldStart, $ColdStartBudgetSeconds) -ForegroundColor Cyan

$failures = @()
try {
    foreach ($page in $pages) {
        $navId = "Nav_$page"
        $item = $window.FindFirstDescendant([FlaUI.Core.Conditions.ConditionFactory]::new($automation.PropertyLibrary).ByAutomationId($navId))
        if ($null -eq $item) {
            $failures += "Navigation item '$navId' not found."
            continue
        }

        $item.Click()
        Start-Sleep -Milliseconds 500
        $shot = Join-Path $OutputDir "$page.png"
        [FlaUI.Core.Capturing.Capture]::Element($window).ToFile($shot)
        Write-Host "Captured $page -> $shot"
    }
}
finally {
    $app.Close()
    $automation.Dispose()
}

if ($coldStart -gt $ColdStartBudgetSeconds) { $failures += ("Cold start {0:N2}s exceeded {1:N1}s." -f $coldStart, $ColdStartBudgetSeconds) }
if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Warning $_ }
    throw "UI smoke reported $($failures.Count) issue(s)."
}
Write-Host "UI smoke passed: $($pages.Count) pages captured to $OutputDir." -ForegroundColor Green
