<#
.SYNOPSIS
    Claude Code PreToolUse hook: refuses Edit/Write under src/, tests/, catalog/,
    build/ until build/start-milestone.ps1 has been run for a milestone in the
    last 12 hours. Reads the tool call JSON from stdin; exit 2 blocks the call
    and the message on stderr is shown to the agent.

.NOTES
    Wired in .claude/settings.json (project-local). Documentation-only edits
    (docs/, *.md at root, PLAN/README/CHANGELOG) are always allowed so plans and
    records can be updated before acknowledging.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$maxAgeHours = 12
$guardedRoots = @('src/', 'tests/', 'catalog/', 'build/', '.github/', 'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props', 'global.json', 'BannedSymbols.txt', '.editorconfig')

$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) {
    exit 0
}

try {
    $payload = $raw | ConvertFrom-Json
}
catch {
    exit 0   # not our JSON shape; never block on parse problems
}

$filePath = $null
if ($payload.PSObject.Properties['tool_input'] -and $payload.tool_input.PSObject.Properties['file_path']) {
    $filePath = [string] $payload.tool_input.file_path
}
if (-not $filePath) {
    exit 0
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$full = [System.IO.Path]::GetFullPath($filePath)
$rootFull = [System.IO.Path]::GetFullPath($repoRoot)
if (-not $full.StartsWith($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
    exit 0   # outside the repository (scratchpad etc.)
}

$relative = [System.IO.Path]::GetRelativePath($rootFull, $full).Replace('\', '/')
$guarded = $false
foreach ($root in $guardedRoots) {
    if ($relative.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase) -or $relative -eq $root) {
        $guarded = $true
        break
    }
}
if (-not $guarded) {
    exit 0
}

# build/hooks and build/start-milestone.ps1 must stay editable to fix the guard itself? No: they are guarded too.
$ackDir = Join-Path $rootFull 'artifacts/.otw-ack'
$fresh = @()
if (Test-Path $ackDir) {
    $fresh = @(Get-ChildItem $ackDir -Filter '*.ack' | Where-Object { $_.LastWriteTimeUtc -gt [DateTime]::UtcNow.AddHours(-$maxAgeHours) })
}
if ($fresh.Count -gt 0) {
    exit 0
}

[Console]::Error.WriteLine("BLOCKED: '$relative' is guarded. Run 'pwsh build/start-milestone.ps1 -Milestone <Mx>' first (it prints AGENTS.md, the runbook and the milestone spec, then records an acknowledgement valid for $maxAgeHours hours). See docs/milestones/README.md.")
exit 2
