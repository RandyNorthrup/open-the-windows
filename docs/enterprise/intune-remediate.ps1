<#
.SYNOPSIS
    Intune Remediations remediation script for Open The Windows. Re-applies a named
    profile's drifted settings unattended.

.DESCRIPTION
    Deploy this as the *remediation* script paired with intune-detect.ps1. It runs
    in the 64-bit SYSTEM context, so it already holds the elevated token
    `otw remediate` needs, and shells out to
    `otw remediate --profile <id> --json --out <report>`. It maps the Open The
    Windows exit-code contract onto Intune's remediation contract, where a zero
    exit means success and any non-zero exit means failure:

        otw 0     applied / already compliant  -> remediation 0  (success)
        otw 3010  RebootRequired                -> remediation 0  (success; reboot noted)
        otw 3     UnsupportedPlatform           -> remediation 0  (cannot apply here; not a failure)
        otw 2/4/5 and anything else             -> remediation 1  (failure)

    The full JSON run report is written to
    %ProgramData%\OpenTheWindows\reports\last-remediate.json for auditing.

.NOTES
    Edit $ProfileId before uploading; keep it identical to the paired
    intune-detect.ps1. If otw.exe is deployed somewhere other than the machine
    PATH or the default install folder, set $OtwPath.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# --- Deployment configuration: edit before uploading to Intune ----------------
# The profile to re-enforce (a built-in id or a file path). Match intune-detect.ps1.
$ProfileId = 'enterprise-workstation'
# Full path to otw.exe. Leave empty to resolve from PATH, then the default folder.
$OtwPath = ''
# ------------------------------------------------------------------------------

# Intune remediation contract: 0 = success, non-zero = failure.
$RemediationSucceeded = 0
$RemediationFailed = 1

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
    exit $RemediationFailed
}

$reportPath = Join-Path -Path $env:ProgramData -ChildPath 'OpenTheWindows\reports\last-remediate.json'

& $otw remediate --profile $ProfileId --json --out $reportPath
$code = $LASTEXITCODE

switch ($code) {
    0 { Write-Output "Applied profile '$ProfileId'."; exit $RemediationSucceeded }
    3010 { Write-Output "Applied profile '$ProfileId'; a reboot is required to complete it."; exit $RemediationSucceeded }
    3 { Write-Output 'Unsupported Windows build; nothing applied.'; exit $RemediationSucceeded }
    default {
        Write-Output "otw remediate failed (exit $code); see $reportPath and the audit log."
        exit $RemediationFailed
    }
}
