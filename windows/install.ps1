<#
.SYNOPSIS
    Compiles and installs EasySwitch-Tandem.exe and configures mxswitch for Windows 11.

.DESCRIPTION
    Uses the built-in Windows .NET Framework C# compiler (csc.exe) to compile
    windows\EasySwitchTandem.cs into a zero-window native executable:
        %LOCALAPPDATA%\Programs\EasySwitch\EasySwitch-Tandem.exe
    and writes the default tandem config:
        %LOCALAPPDATA%\mxswitch\config

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\windows\install.ps1
#>
[CmdletBinding()]
param(
    [int]$ChannelA = 1,
    [int]$ChannelB = 2
)

$ErrorActionPreference = 'Stop'

$InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\EasySwitch'
$ConfigDir  = Join-Path $env:LOCALAPPDATA 'mxswitch'
$ConfigPath = Join-Path $ConfigDir 'config'
$ExePath    = Join-Path $InstallDir 'EasySwitch-Tandem.exe'
$CsSource   = Join-Path $PSScriptRoot 'EasySwitchTandem.cs'
$Ps1Source  = Join-Path $PSScriptRoot 'mxswitch.ps1'

if (-not (Test-Path $CsSource)) {
    throw "Cannot find $CsSource"
}

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
New-Item -ItemType Directory -Path $ConfigDir  -Force | Out-Null

# 1. Write config
@"
channel_a=$ChannelA
channel_b=$ChannelB
device=all
"@ | Set-Content -Path $ConfigPath -Encoding UTF8
Write-Host "[1/3] Wrote config to $ConfigPath (channel_a=$ChannelA, channel_b=$ChannelB, device=all)" -ForegroundColor Green

# 2. Copy mxswitch.ps1 for CLI diagnostics (-List / -Info)
Copy-Item -Path $Ps1Source -Destination (Join-Path $InstallDir 'mxswitch.ps1') -Force

# 3. Compile EasySwitch-Tandem.exe using built-in Windows .NET csc.exe
$cscCandidates = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
    throw "Could not find built-in csc.exe in $env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
}

Write-Host "[2/3] Compiling EasySwitch-Tandem.exe with $csc ..." -ForegroundColor Cyan
& $csc /nologo /optimize+ /target:winexe "/out:$ExePath" "$CsSource"
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $ExePath)) {
    throw "Compilation of EasySwitch-Tandem.exe failed (exit code $LASTEXITCODE)"
}

Write-Host "[3/3] Installed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "==================================================================" -ForegroundColor Yellow
Write-Host " Logi Options+ Setup (Windows 11)" -ForegroundColor Yellow
Write-Host "==================================================================" -ForegroundColor Yellow
Write-Host " 1. Open Logi Options+ and select your Signature Plus M750."
Write-Host " 2. Click the Top button (directly below the scroll wheel)."
Write-Host " 3. Choose 'Open application' and paste this exact path:"
Write-Host ""
Write-Host "    $ExePath" -ForegroundColor White
Write-Host ""
Write-Host " Diagnostic CLI commands (optional):"
Write-Host "    powershell -ExecutionPolicy Bypass -File `"$InstallDir\mxswitch.ps1`" -List"
Write-Host "    powershell -ExecutionPolicy Bypass -File `"$InstallDir\mxswitch.ps1`" -Info -All"
Write-Host "==================================================================" -ForegroundColor Yellow
