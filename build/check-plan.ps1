<#
.SYNOPSIS
    Plan/spec/certification consistency gate. Fails (exit 1) when the planning
    documents disagree with each other or with the repository, so an agent
    cannot mark work done without the evidence the runbook requires.

.DESCRIPTION
    Checks:
      1. Every milestone heading in PLAN.md section 8 (### Mx ...) has a spec
         docs/milestones/Mx-*.md, and every spec is referenced from PLAN.md.
      2. Every milestone PLAN.md marks DONE has docs/certification/Mx.md
         (M0's certification is recorded inline in PLAN.md and is exempt).
      3. AGENTS.md, CLAUDE.md, .cursor/rules/*.mdc and .github/copilot-instructions.md
         each reference docs/milestones/README.md and build/start-milestone.ps1.
      4. CHANGELOG.md has an [Unreleased] section.
      5. docs/catalog-format.md lists every action kind the schema defines.
      6. .claude/settings.json wires build/hooks/pre-edit.ps1.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure { param([string] $Message) $failures.Add($Message); Write-Host "PLAN CHECK: $Message" -ForegroundColor Red }

$plan = Get-Content (Join-Path $repoRoot 'PLAN.md') -Raw
$headings = [regex]::Matches($plan, '(?m)^### (M\d+) [^\r\n]*')
if ($headings.Count -eq 0) { Add-Failure 'PLAN.md has no "### Mx" milestone headings.' }

$specDir = Join-Path $repoRoot 'docs/milestones'
$specs = @(Get-ChildItem $specDir -Filter 'M*-*.md' -ErrorAction SilentlyContinue)
$specIds = @($specs | ForEach-Object { ($_.Name -split '-')[0] })

foreach ($h in $headings) {
    $id = $h.Groups[1].Value
    if ($id -eq 'M0') { continue }   # M0 = scaffold; recorded inline in PLAN.md
    if ($specIds -notcontains $id) { Add-Failure "PLAN.md lists $id but docs/milestones/$id-*.md does not exist." }
    if ($h.Value -match 'DONE') {
        $cert = Join-Path $repoRoot "docs/certification/$id.md"
        if (-not (Test-Path $cert)) { Add-Failure "PLAN.md marks $id DONE but $cert is missing." }
    }
}
foreach ($id in $specIds) {
    if ($plan -notmatch "docs/milestones/$id-") { Add-Failure "docs/milestones/$id-*.md exists but PLAN.md section 8 does not link it." }
}

foreach ($file in @('AGENTS.md', 'CLAUDE.md', '.github/copilot-instructions.md') + @(Get-ChildItem (Join-Path $repoRoot '.cursor/rules') -Filter '*.mdc' | ForEach-Object { ".cursor/rules/$($_.Name)" })) {
    $path = Join-Path $repoRoot $file
    if (-not (Test-Path $path)) { Add-Failure "$file is missing."; continue }
    $text = Get-Content $path -Raw
    if ($text -notmatch 'docs/milestones/README\.md') { Add-Failure "$file does not reference docs/milestones/README.md." }
    if ($text -notmatch 'start-milestone\.ps1') { Add-Failure "$file does not reference build/start-milestone.ps1." }
}

$changelog = Get-Content (Join-Path $repoRoot 'CHANGELOG.md') -Raw
if ($changelog -notmatch '(?m)^## \[Unreleased\]') { Add-Failure 'CHANGELOG.md has no [Unreleased] section.' }

$schema = Get-Content (Join-Path $repoRoot 'catalog/schema/tweak.schema.json') -Raw | ConvertFrom-Json
$kinds = @($schema.'$defs'.action.properties.kind.enum)
$guide = Get-Content (Join-Path $repoRoot 'docs/catalog-format.md') -Raw
foreach ($kind in $kinds) {
    if ($guide -notmatch "\| ``$kind`` \|") { Add-Failure "docs/catalog-format.md does not document action kind '$kind'." }
}

$settings = Get-Content (Join-Path $repoRoot '.claude/settings.json') -Raw
if ($settings -notmatch 'build/hooks/pre-edit\.ps1') { Add-Failure '.claude/settings.json does not wire build/hooks/pre-edit.ps1.' }

if ($failures.Count -gt 0) {
    Write-Host "$($failures.Count) plan consistency failure(s)." -ForegroundColor Red
    exit 1
}
Write-Host 'Plan, specs, certification records and agent instructions are consistent.' -ForegroundColor Green
exit 0
