<#
.SYNOPSIS
    Helper for the Windows 11 lab VM used for integration and certification runs.

.DESCRIPTION
    Talks to the lab VM over SSH (Windows OpenSSH server on the VM, key auth).
    Connection settings come from environment variables (see .env.example):

        OTW_VM_HOST   IP or host name of the VM            (required)
        OTW_VM_USER   SSH user (must be a local admin)      (default: randy)
        OTW_VM_KEY    path to the private key               (default: temp/ssh/otw_vm_ed25519)

    Commands:
        pwsh build/vm.ps1 -Action doctor              # copy published otw.exe and run `otw doctor`
        pwsh build/vm.ps1 -Action run -Command "..."  # run an arbitrary command on the VM
        pwsh build/vm.ps1 -Action copy -Source <file> -Destination C:/otw-test/<name>

    Nothing here changes VM state except copying files into C:\otw-test.
    Destructive apply/revert runs have their own script with explicit
    checkpoint/restore handling.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('doctor', 'run', 'copy')]
    [string] $Action,
    [string] $Command = '',
    [string] $Source = '',
    [string] $Destination = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$vmHost = $env:OTW_VM_HOST
if (-not $vmHost) {
    throw 'OTW_VM_HOST is not set. Copy .env.example to .env and load it, or set the variable.'
}
$vmUser = if ($env:OTW_VM_USER) { $env:OTW_VM_USER } else { 'randy' }
$vmKey = if ($env:OTW_VM_KEY) { $env:OTW_VM_KEY } else { Join-Path $repoRoot 'temp/ssh/otw_vm_ed25519' }
if (-not (Test-Path $vmKey)) {
    throw "SSH key not found at $vmKey"
}
$target = "$vmUser@$vmHost"
$sshOptions = @('-o', 'BatchMode=yes', '-o', 'ConnectTimeout=15', '-i', $vmKey)

function Invoke-Remote {
    param([Parameter(Mandatory)] [string] $RemoteCommand)
    & ssh @sshOptions $target $RemoteCommand 2>&1 |
        Where-Object { $_ -notmatch 'post-quantum|store now, decrypt later|openssh.com/pq' } |
        ForEach-Object { Write-Host $_ }
    return $LASTEXITCODE
}

function Copy-ToVm {
    param([Parameter(Mandatory)] [string] $LocalPath, [Parameter(Mandatory)] [string] $RemotePath)
    & ssh @sshOptions $target 'if not exist C:\otw-test mkdir C:\otw-test' 2>&1 | Out-Null
    & scp @sshOptions $LocalPath "${target}:$RemotePath" 2>&1 |
        Where-Object { $_ -notmatch 'post-quantum|store now, decrypt later|openssh.com/pq' } |
        ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) { throw "scp to $RemotePath failed" }
}

switch ($Action) {
    'doctor' {
        $cli = Join-Path $repoRoot 'artifacts/publish/win-x64/cli/otw.exe'
        if (-not (Test-Path $cli)) {
            throw "Publish first: pwsh build/publish.ps1 -Runtime win-x64 ($cli missing)"
        }
        Copy-ToVm -LocalPath $cli -RemotePath 'C:/otw-test/otw.exe'
        $code = Invoke-Remote -RemoteCommand 'C:\otw-test\otw.exe doctor'
        Write-Host "otw doctor exit code on VM: $code" -ForegroundColor Cyan
        exit $code
    }
    'run' {
        if (-not $Command) { throw '-Command is required for -Action run' }
        $code = Invoke-Remote -RemoteCommand $Command
        exit $code
    }
    'copy' {
        if (-not $Source -or -not $Destination) { throw '-Source and -Destination are required for -Action copy' }
        Copy-ToVm -LocalPath $Source -RemotePath $Destination
        exit 0
    }
}
