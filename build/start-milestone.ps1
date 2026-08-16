<#
.SYNOPSIS
    Mandatory entry point before editing code for a milestone. Prints the rules
    and the milestone specification to the console (so an agent's context
    contains them) and records an acknowledgement marker that the pre-edit hook
    checks.

.DESCRIPTION
        pwsh build/start-milestone.ps1 -Milestone M1

    Prints, in order: AGENTS.md, docs/milestones/README.md (the runbook), the
    milestone spec docs/milestones/<Mx>-*.md, and the current PLAN.md section 8 status
    line. Then writes artifacts/.otw-ack/<Mx>.ack (gitignored) with a timestamp
    and the git HEAD. The Claude Code PreToolUse hook (build/hooks/pre-edit.ps1)
    refuses edits under src/, tests/, catalog/, build/ unless a marker newer
    than 12 hours exists. Re-run after 12 hours or after switching milestone.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^M[0-9]+$')]
    [string] $Milestone
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$spec = Get-ChildItem (Join-Path $repoRoot 'docs/milestones') -Filter "$Milestone-*.md" | Select-Object -First 1
if (-not $spec) {
    throw "No specification found for $Milestone under docs/milestones/. Author it first (see docs/milestones/README.md)."
}

function Show-Section {
    param([string] $Title, [string] $Path)
    Write-Host ''
    Write-Host ('=' * 78) -ForegroundColor Cyan
    Write-Host "== $Title  ($([System.IO.Path]::GetRelativePath($repoRoot, $Path)))" -ForegroundColor Cyan
    Write-Host ('=' * 78) -ForegroundColor Cyan
    Get-Content $Path | ForEach-Object { Write-Host $_ }
}

Show-Section 'RULES (AGENTS.md)' (Join-Path $repoRoot 'AGENTS.md')
Show-Section 'RUNBOOK (docs/milestones/README.md)' (Join-Path $repoRoot 'docs/milestones/README.md')
Show-Section "SPECIFICATION ($Milestone)" $spec.FullName

$planLine = Select-String -Path (Join-Path $repoRoot 'PLAN.md') -Pattern "^### $Milestone " | Select-Object -First 1
Write-Host ''
Write-Host ('=' * 78) -ForegroundColor Cyan
Write-Host ("== PLAN.md status: " + ($(if ($planLine) { $planLine.Line } else { "$Milestone not found in PLAN.md section 8" }))) -ForegroundColor Cyan
Write-Host ('=' * 78) -ForegroundColor Cyan

$ackDir = Join-Path $repoRoot 'artifacts/.otw-ack'
New-Item -ItemType Directory -Force -Path $ackDir | Out-Null
$head = (& git -C $repoRoot rev-parse --short HEAD 2>$null)
$stamp = [DateTimeOffset]::UtcNow.ToString('o')
Set-Content -Path (Join-Path $ackDir "$Milestone.ack") -Value "$stamp $head $($spec.Name)" -Encoding ascii
Write-Host ''
Write-Host "Acknowledged $Milestone at $stamp (HEAD $head). Edits under src/, tests/, catalog/, build/ are now allowed for 12 hours." -ForegroundColor Green
Write-Host "Next: create branch feature/$($Milestone.ToLowerInvariant())-<name>, run pwsh build/quality.ps1, then follow the spec's implementation steps in order." -ForegroundColor Green
