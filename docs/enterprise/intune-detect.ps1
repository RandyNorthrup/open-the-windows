<#
.SYNOPSIS
    Intune Remediations detection script for Open The Windows. Reports whether the
    device still matches a named profile; a non-zero exit tells Intune to run the
    paired remediation (intune-remediate.ps1).

.DESCRIPTION
    Deploy this as the *detection* script of an Intune "Remediation" (formerly a
    Proactive Remediation). It runs in the 64-bit SYSTEM context and shells out to
    `otw scan --profile <id>`, mapping the Open The Windows exit-code contract
    (see ExitCodes in the product) onto Intune's detection contract, where a
    zero exit means "healthy, do not remediate" and any non-zero exit means
    "issue found, run the remediation":

        otw 0    Success / compliant       -> detection 0  (healthy)
        otw 1    Drift                      -> detection 1  (run remediation)
        otw 3    UnsupportedPlatform        -> detection 0  (nothing here to remediate)
        otw 2/4/5 and anything else         -> detection 1  (surface as needing attention)

    Intune captures STDOUT as the pre-remediation detection output, so the script
    writes a single status line there.

.NOTES
    Edit $ProfileId before uploading. If otw.exe is deployed somewhere other than
    the machine PATH or the default install folder, set $OtwPath. Keep $ProfileId
    identical to the paired intune-remediate.ps1.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# --- Deployment configuration: edit before uploading to Intune ----------------
# The profile this device must stay compliant with (a built-in id or a file path).
$ProfileId = 'enterprise-workstation'
# Full path to otw.exe. Leave empty to resolve from PATH, then the default folder.
$OtwPath = ''
# ------------------------------------------------------------------------------

# Intune detection contract: 0 = healthy (no remediation), non-zero = remediate.
$DetectionHealthy = 0
$DetectionRemediate = 1

function Resolve-OtwPath {
    param([string] $Configured)

    if ($Configured -and (Test-Path -LiteralPath $Configured -PathType Leaf)) {
        return $Configured
    }

    $onPath = Get-Command -Name 'otw.exe' -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($onPath) {
        return $onPath.Source
    }

    $default = Join-Path -Path $env:ProgramFiles -ChildPath 'OpenTheWindows\otw.exe'
    if (Test-Path -LiteralPath $default -PathType Leaf) {
        return $default
    }

    return $null
}

$otw = Resolve-OtwPath -Configured $OtwPath
if (-not $otw) {
    Write-Output 'Open The Windows (otw.exe) is not installed or not on PATH.'
    exit $DetectionRemediate
}

& $otw scan --profile $ProfileId | Out-Null
$code = $LASTEXITCODE

switch ($code) {
    0 { Write-Output "Compliant with profile '$ProfileId'."; exit $DetectionHealthy }
    1 { Write-Output "Drift from profile '$ProfileId' detected."; exit $DetectionRemediate }
    3 { Write-Output 'Unsupported Windows build; nothing to remediate.'; exit $DetectionHealthy }
    default {
        Write-Output "otw scan failed (exit $code); see the Open The Windows audit log."
        exit $DetectionRemediate
    }
}
