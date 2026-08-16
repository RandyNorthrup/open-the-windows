<#
.SYNOPSIS
    Claude Code SessionStart hook: prints the mandatory-reading reminder and the
    milestone currently in progress so no session starts without it.
#>
Set-StrictMode -Version Latest
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$plan = Join-Path $repoRoot 'PLAN.md'
$current = Select-String -Path $plan -Pattern '^### (M[0-9]+) .*\((in progress|IN PROGRESS)' | Select-Object -First 1
Write-Host 'OPEN THE WINDOWS - mandatory before editing code:'
Write-Host '  1. Read AGENTS.md (rules) and docs/milestones/README.md (runbook).'
Write-Host '  2. Run: pwsh build/start-milestone.ps1 -Milestone <Mx>  (prints the spec, records the acknowledgement; edits under src/tests/catalog/build are blocked until then).'
Write-Host '  3. Run: pwsh build/quality.ps1  (must be green before and after your change).'
if ($current) {
    Write-Host ("  Current milestone per PLAN.md: " + $current.Matches[0].Groups[1].Value + "  -> " + $current.Line)
}
else {
    Write-Host '  Current milestone: see PLAN.md section 8.'
}
