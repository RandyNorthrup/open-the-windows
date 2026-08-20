<#
.SYNOPSIS
    Launches the built WPF app, waits for its main window, captures a screenshot of it, closes it.

.DESCRIPTION
    Visual smoke test for the WPF app. The app
    manifest requires administrator; this script therefore re-launches itself
    elevated (one UAC prompt) so it can bring the window to the foreground and
    capture exactly its client rectangle. Output:
    artifacts/screenshots/main-window-<timestamp>.png

        pwsh build/smoke-gui.ps1                 # uses artifacts/bin/OpenTheWindows.App/release
        pwsh build/smoke-gui.ps1 -ExePath <path> # e.g. a published build
#>
[CmdletBinding()]
param(
    [string] $ExePath = '',
    [int] $TimeoutSeconds = 60,
    [string] $OutputDir = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $ExePath) {
    $ExePath = Join-Path $repoRoot 'artifacts/bin/OpenTheWindows.App/release/OpenTheWindows.exe'
}
if (-not $OutputDir) {
    $OutputDir = Join-Path $repoRoot 'artifacts/screenshots'
}
if (-not (Test-Path $ExePath)) {
    throw "App not found at $ExePath. Build first: dotnet build OpenTheWindows.slnx -c Release"
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$isElevated = ([Security.Principal.WindowsPrincipal] $identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isElevated) {
    Write-Host 'Re-launching elevated (UAC prompt expected)...' -ForegroundColor Cyan
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath,
        '-ExePath', $ExePath, '-TimeoutSeconds', $TimeoutSeconds, '-OutputDir', $OutputDir)
    $elevated = Start-Process -FilePath 'pwsh' -ArgumentList $argList -Verb RunAs -Wait -PassThru
    if ($elevated.ExitCode -ne 0) {
        throw "Elevated smoke test failed with exit code $($elevated.ExitCode)"
    }
    Get-ChildItem $OutputDir -Filter 'main-window-*.png' | Sort-Object LastWriteTime | Select-Object -Last 1 -ExpandProperty FullName
    return
}

Add-Type -Namespace OtwSmoke -Name Native -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool SetForegroundWindow(System.IntPtr hWnd);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool ShowWindow(System.IntPtr hWnd, int nCmdShow);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool GetWindowRect(System.IntPtr hWnd, out RECT lpRect);
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
'@

Write-Host "Launching $ExePath" -ForegroundColor Cyan
$process = Start-Process -FilePath $ExePath -PassThru
try {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $process.Refresh()
        if ($process.HasExited) {
            throw "App exited early with code $($process.ExitCode)"
        }
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
            break
        }
        Start-Sleep -Milliseconds 250
    }
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) {
        throw "Main window did not appear within $TimeoutSeconds s"
    }
    Write-Host "Main window: '$($process.MainWindowTitle)'" -ForegroundColor Green

    $hwnd = $process.MainWindowHandle
    $null = [OtwSmoke.Native]::ShowWindow($hwnd, 9)   # SW_RESTORE
    $null = [OtwSmoke.Native]::SetForegroundWindow($hwnd)
    Start-Sleep -Seconds 2   # let the Fluent theme and bindings render

    $rect = New-Object OtwSmoke.Native+RECT
    if (-not [OtwSmoke.Native]::GetWindowRect($hwnd, [ref] $rect)) {
        throw 'GetWindowRect failed'
    }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) {
        throw "Window rectangle is empty ($width x $height)"
    }

    Add-Type -AssemblyName System.Drawing
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size $width, $height))
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $file = Join-Path $OutputDir "main-window-$stamp.png"
    $bitmap.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
    Write-Host "Screenshot: $file ($width x $height)" -ForegroundColor Green
    Write-Output $file
}
finally {
    if (-not $process.HasExited) {
        $null = $process.CloseMainWindow()
        if (-not $process.WaitForExit(5000)) {
            $process.Kill()
        }
    }
}
