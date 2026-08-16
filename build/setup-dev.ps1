<#
.SYNOPSIS
    One-time developer setup: git hooks path, tool restore, node tooling.
        pwsh build/setup-dev.ps1
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot
& git config core.hooksPath .githooks
Write-Host 'git core.hooksPath = .githooks (pre-commit runs format/build/test/plan and requires CHANGELOG with code changes).'
$dotnetExe = if ($IsWindows -and (Test-Path 'C:\Program Files\dotnet\dotnet.exe')) { 'C:\Program Files\dotnet\dotnet.exe' } else { 'dotnet' }
& $dotnetExe tool restore
if (-not (Test-Path (Join-Path $repoRoot 'node_modules/.bin'))) {
    npm ci --no-fund --no-audit --loglevel=error
}
Write-Host 'Done. Next: pwsh build/start-milestone.ps1 -Milestone <Mx>' -ForegroundColor Green
