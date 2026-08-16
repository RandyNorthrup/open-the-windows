<#
.SYNOPSIS
    Runs every quality gate for Open the Windows and fails on any blocking issue.

.DESCRIPTION
    One command, same in CI and on a developer machine:

        pwsh build/quality.ps1            # everything
        pwsh build/quality.ps1 -Ci        # locked restore, no auto-fix, CI annotations
        pwsh build/quality.ps1 -Only test # a single gate (restore, format, build, test, coverage,
                                          #   secrets, sast, duplication, markdown, powershell)

    Gates (see README.md "Quality gates" for the exact command each runs):
      restore      dotnet restore (locked mode in CI) + NuGetAudit (vulnerable/deprecated packages fail restore)
      format       dotnet format --verify-no-changes
      build        dotnet build -c Release (TreatWarningsAsErrors, AnalysisLevel latest-all, style enforced,
                   dead code, banned APIs, unused references)
      test         dotnet test (Microsoft.Testing.Platform) with Cobertura coverage
      coverage     ReportGenerator merge + minimum thresholds (line/branch)
      secrets      gitleaks over git history and the working tree
      sast         semgrep (p/csharp, p/secrets, p/github-actions)
      duplication  jscpd
      markdown     markdownlint-cli2
      powershell   PSScriptAnalyzer -EnableExit over build/ scripts

    Every gate has been observed to fail on a deliberately broken input before
    being trusted (see PLAN.md "Gate verification log").
#>
[CmdletBinding()]
param(
    [switch] $Ci,
    [ValidateSet('all', 'restore', 'format', 'build', 'test', 'coverage', 'secrets', 'sast', 'duplication', 'markdown', 'powershell')]
    [string] $Only = 'all'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

# Coverage thresholds are load-bearing. Raise them; do not lower them to make a build pass.
$minimumLineCoverage = 85
$minimumBranchCoverage = 75

# Pinned tool versions live in one place each: .config/dotnet-tools.json (dotnet tools),
# package.json (node tools), build/requirements-tools.txt (semgrep), and winget for gitleaks.
$semgrepVenv = Join-Path $repoRoot '.venv-tools'
$solution = 'OpenTheWindows.slnx'
$coverageDir = Join-Path $repoRoot 'artifacts/coverage'

$dotnetExe = 'dotnet'
if ($IsWindows -and (Test-Path 'C:\Program Files\dotnet\dotnet.exe')) {
    # This machine may have the x86 host earlier on PATH; the x64 host owns the SDK.
    $dotnetExe = 'C:\Program Files\dotnet\dotnet.exe'
}

$results = [System.Collections.Generic.List[pscustomobject]]::new()
$ciMode = [bool] $Ci
$runOnly = $Only

function Invoke-Gate {
    param(
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [scriptblock] $Body
    )
    if ($runOnly -ne 'all' -and $runOnly -ne $Name) {
        return
    }
    Write-Host ''
    Write-Host "==> $Name" -ForegroundColor Cyan
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $global:LASTEXITCODE = 0
    $failed = $false
    try {
        & $Body
        if ($LASTEXITCODE -ne 0) {
            $failed = $true
        }
    }
    catch {
        Write-Host $_.Exception.Message -ForegroundColor Red
        if ($_.ScriptStackTrace) {
            Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
        }
        $failed = $true
    }
    $stopwatch.Stop()
    $status = if ($failed) { 'FAIL' } else { 'PASS' }
    $colour = if ($failed) { 'Red' } else { 'Green' }
    Write-Host ("<== {0} {1} ({2:n1}s)" -f $Name, $status, $stopwatch.Elapsed.TotalSeconds) -ForegroundColor $colour
    $results.Add([pscustomobject]@{ Gate = $Name; Status = $status; Seconds = [math]::Round($stopwatch.Elapsed.TotalSeconds, 1) })
}

function Get-GitleaksPath {
    $cmd = Get-Command gitleaks -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }
    $wingetPkg = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'
    if (Test-Path $wingetPkg) {
        $found = Get-ChildItem $wingetPkg -Recurse -Filter 'gitleaks.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) {
            return $found.FullName
        }
    }
    throw 'gitleaks not found. Install: winget install Gitleaks.Gitleaks (see README.md).'
}

function Get-SemgrepPath {
    $cmd = Get-Command semgrep -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }
    $exe = if ($IsWindows) { Join-Path $semgrepVenv 'Scripts\semgrep.exe' } else { Join-Path $semgrepVenv 'bin/semgrep' }
    if (-not (Test-Path $exe)) {
        Write-Host "Creating semgrep virtual environment at $semgrepVenv" -ForegroundColor DarkGray
        $python = if ($IsWindows) { 'python' } else { 'python3' }
        & $python -m venv $semgrepVenv
        if ($LASTEXITCODE -ne 0) { throw 'python -m venv failed (Python 3.9+ required for semgrep).' }
        $pip = if ($IsWindows) { Join-Path $semgrepVenv 'Scripts\python.exe' } else { Join-Path $semgrepVenv 'bin/python' }
        & $pip -m pip install --quiet --disable-pip-version-check -r (Join-Path $repoRoot 'build/requirements-tools.txt')
        if ($LASTEXITCODE -ne 0) { throw 'pip install of semgrep failed.' }
    }
    return $exe
}

Invoke-Gate 'restore' {
    if ($ciMode) {
        & $dotnetExe restore $solution --locked-mode
    }
    else {
        & $dotnetExe restore $solution
    }
    if ($LASTEXITCODE -ne 0) { return }
    & $dotnetExe tool restore
    if ($LASTEXITCODE -ne 0) { return }
    if (-not (Test-Path (Join-Path $repoRoot 'node_modules/.bin'))) {
        npm ci --no-fund --no-audit --loglevel=error
    }
}

Invoke-Gate 'format' {
    & $dotnetExe format $solution --verify-no-changes --no-restore
}

Invoke-Gate 'build' {
    & $dotnetExe build $solution -c Release --no-restore
}

Invoke-Gate 'test' {
    if (Test-Path $coverageDir) {
        Remove-Item $coverageDir -Recurse -Force
    }
    & $dotnetExe test $solution -c Release --no-build `
        --coverage --coverage-output-format cobertura --results-directory $coverageDir
}

Invoke-Gate 'coverage' {
    # NOTE: the threshold argument is a bare "minimumCoverageThresholds:..." token.
    # "-settings:..." and "settings:..." variants are silently ignored by ReportGenerator
    # (verified 2026-08-16); do not "fix" this line into one of those forms.
    & $dotnetExe reportgenerator `
        "-reports:$coverageDir/**/*.cobertura.xml" `
        "-targetdir:$coverageDir/report" `
        '-reporttypes:TextSummary;Cobertura;MarkdownSummaryGithub;HtmlInline' `
        '-assemblyfilters:-*.Tests;-OpenTheWindows.TestSupport' `
        '-filefilters:-*.g.cs;-*.g.i.cs' `
        '-verbosity:Warning' `
        "minimumCoverageThresholds:lineCoverage=$minimumLineCoverage" `
        "minimumCoverageThresholds:branchCoverage=$minimumBranchCoverage"
    if (Test-Path "$coverageDir/report/Summary.txt") {
        Get-Content "$coverageDir/report/Summary.txt" | Select-Object -First 16
    }
}

Invoke-Gate 'secrets' {
    $gitleaks = Get-GitleaksPath
    if (Test-Path (Join-Path $repoRoot '.git')) {
        & $gitleaks git --redact --config .gitleaks.toml --exit-code 1 .
        if ($LASTEXITCODE -ne 0) { return }
    }
    & $gitleaks dir --redact --config .gitleaks.toml --exit-code 1 .
}

Invoke-Gate 'sast' {
    $semgrep = Get-SemgrepPath
    & $semgrep scan --config p/csharp --config p/secrets --config p/github-actions `
        --error --quiet --metrics=off `
        --exclude 'artifacts' --exclude 'node_modules' --exclude '.venv-tools' --exclude 'temp' .
}

Invoke-Gate 'duplication' {
    npx --no-install jscpd --config .jscpd.json .
}

Invoke-Gate 'markdown' {
    npx --no-install markdownlint-cli2
}

Invoke-Gate 'powershell' {
    if (-not (Get-Module -ListAvailable PSScriptAnalyzer)) {
        Install-Module PSScriptAnalyzer -Scope CurrentUser -Force -AcceptLicense
    }
    Import-Module PSScriptAnalyzer
    # Do NOT rely on -EnableExit here: it sets the host exit code, which this
    # runner would overwrite; a decorative pass is exactly what we must avoid.
    $records = @(Invoke-ScriptAnalyzer -Path (Join-Path $repoRoot 'build') -Recurse `
            -Settings (Join-Path $repoRoot 'PSScriptAnalyzerSettings.psd1'))
    if ($records.Count -gt 0) {
        $records | Format-Table -AutoSize | Out-String | Write-Host
        throw "PSScriptAnalyzer reported $($records.Count) finding(s)."
    }
}

Write-Host ''
Write-Host 'Quality gate summary' -ForegroundColor Cyan
$results | Format-Table -AutoSize | Out-String | Write-Host

$failures = @($results | Where-Object { $_.Status -eq 'FAIL' })
if ($failures.Count -gt 0) {
    Write-Host ("{0} gate(s) failed: {1}" -f $failures.Count, (($failures | ForEach-Object { $_.Gate }) -join ', ')) -ForegroundColor Red
    exit 1
}
Write-Host 'All gates passed.' -ForegroundColor Green
exit 0
