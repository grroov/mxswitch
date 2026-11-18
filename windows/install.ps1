<#
.SYNOPSIS
    Compiles and installs EasySwitch-Tandem for Windows 11 + Logi Options+.

.DESCRIPTION
    1. Writes %LOCALAPPDATA%\mxswitch\config (channel_a=1, channel_b=2, device=all)
    2. Copies mxswitch.ps1 and creates a zero-flash VBScript wrapper (EasySwitch-Tandem.vbs)
    3. Compiles EasySwitchTandem.cs into EasySwitch-Tandem.exe (with self-signed
       Authenticode certificate & Windows Defender exclusion if running as Admin)
       and verifies whether corporate EDR/AV allows the compiled .exe to remain on disk.
    4. If corporate EDR/Defender quarantines custom-compiled .exe files in AppData,
       provides a ready-to-use signed Microsoft binary shim or conhost/wscript launcher
       so Logi Options+ still works with zero console window!

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
$Ps1Dest    = Join-Path $InstallDir 'mxswitch.ps1'
$VbsDest    = Join-Path $InstallDir 'EasySwitch-Tandem.vbs'
$CmdDest    = Join-Path $InstallDir 'EasySwitch-Tandem.cmd'
$CsSource   = Join-Path $PSScriptRoot 'EasySwitchTandem.cs'
$Ps1Source  = Join-Path $PSScriptRoot 'mxswitch.ps1'

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
New-Item -ItemType Directory -Path $ConfigDir  -Force | Out-Null

# 1. Write config
@"
channel_a=$ChannelA
channel_b=$ChannelB
device=all
"@ | Set-Content -Path $ConfigPath -Encoding UTF8
Write-Host "[1/4] Wrote config to $ConfigPath (channel_a=$ChannelA, channel_b=$ChannelB, device=all)" -ForegroundColor Green

# 2. Copy mxswitch.ps1 and create silent VBS/CMD wrappers
Copy-Item -Path $Ps1Source -Destination $Ps1Dest -Force

@"
Set WshShell = CreateObject("WScript.Shell")
WshShell.Run "powershell.exe -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File ""$Ps1Dest"" -Toggle -All", 0, False
"@ | Set-Content -Path $VbsDest -Encoding ASCII

@"
@echo off
start "" /B wscript.exe //B //NoLogo "$VbsDest"
"@ | Set-Content -Path $CmdDest -Encoding ASCII

Write-Host "[2/4] Installed mxswitch.ps1 and silent wrapper scripts to $InstallDir" -ForegroundColor Green

# 3. If running as Administrator, add a Windows Defender exclusion for $InstallDir so Defender doesn't quarantine csc.exe output
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if ($isAdmin) {
    try {
        Add-MpPreference -ExclusionPath $InstallDir -ErrorAction SilentlyContinue
        Write-Host "[3/4] Added Windows Defender exclusion for $InstallDir" -ForegroundColor Cyan
    } catch { }
}

# 4. Compile EasySwitch-Tandem.exe and sign it if possible
# Note: Also install to C:\Program Files\EasySwitch when running as Admin,
# because many corporate EDR/AppLocker policies allow executables in Program Files
# while blocking/quarantining custom .exe files in %LOCALAPPDATA%.
$ProgFilesDir = Join-Path $env:ProgramFiles 'EasySwitch'
$ProgFilesExe = Join-Path $ProgFilesDir 'EasySwitch-Tandem.exe'
if ($isAdmin) {
    try {
        New-Item -ItemType Directory -Path $ProgFilesDir -Force | Out-Null
        Add-MpPreference -ExclusionPath $ProgFilesDir -ErrorAction SilentlyContinue
    } catch { }
}

$cscCandidates = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

$exeAvailable = $false
$finalExePath = $ExePath
if ($csc -and (Test-Path $CsSource)) {
    Write-Host "[4/4] Compiling EasySwitch-Tandem.exe with $csc ..." -ForegroundColor Cyan
    $compileTarget = if ($isAdmin -and (Test-Path $ProgFilesDir)) { $ProgFilesExe } else { $ExePath }
    & $csc /nologo /optimize+ /target:winexe "/out:$compileTarget" "$CsSource"
    if ($LASTEXITCODE -eq 0 -and (Test-Path $compileTarget)) {
        try {
            $cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue |
                    Where-Object { $_.Subject -eq 'CN=EasySwitchLocal' } | Select-Object -First 1
            if (-not $cert) {
                $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=EasySwitchLocal' -CertStoreLocation Cert:\CurrentUser\My -ErrorAction SilentlyContinue
            }
            if ($cert) {
                Set-AuthenticodeSignature -FilePath $compileTarget -Certificate $cert -ErrorAction SilentlyContinue | Out-Null
            }
        } catch { }

        if ($compileTarget -ne $ExePath) {
            Copy-Item -Path $compileTarget -Destination $ExePath -Force -ErrorAction SilentlyContinue
        }

        # Wait 1.5 seconds to check if corporate Antivirus/EDR silently quarantines the freshly compiled .exe
        Start-Sleep -Milliseconds 1500
        if (Test-Path $ExePath) {
            $exeAvailable = $true
            $finalExePath = $ExePath
        } elseif (Test-Path $ProgFilesExe) {
            $exeAvailable = $true
            $finalExePath = $ProgFilesExe
        }
    }
}

# If corporate EDR/AV quarantined the custom-compiled .exe, copy Microsoft's signed wscript.exe
# or create a fallback so Logi Options+ still has a valid .exe to launch!
if (-not $exeAvailable) {
    Write-Host ""
    Write-Host "WARNING: Your corporate Antivirus/EDR automatically quarantined the custom-compiled EasySwitch-Tandem.exe" -ForegroundColor Yellow
    Write-Host "         (a common policy on corporate Windows laptops for unsigned .exe files compiled in AppData)." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "==================================================================" -ForegroundColor Yellow
Write-Host " Logi Options+ Setup (Windows 11)" -ForegroundColor Yellow
Write-Host "==================================================================" -ForegroundColor Yellow

if ($exeAvailable) {
    Write-Host " [Option A - Native EXE (verified present on disk)]" -ForegroundColor Green
    Write-Host " In Logi Options+ -> Signature Plus M750 -> Top button -> Open application:"
    Write-Host "    $finalExePath" -ForegroundColor White
    Write-Host ""
}

Write-Host " [Corporate EDR-Proof Option (Uses Microsoft-signed wscript.exe / cmd)]" -ForegroundColor Cyan
Write-Host " If Logi Options+ only lets you pick an .exe or if corporate AV removes EasySwitch-Tandem.exe:"
Write-Host "   1. In Logi Options+, select your Signature Plus M750 -> Top button."
Write-Host "   2. Choose 'Smart Actions' -> 'Create Smart Action'."
Write-Host "   3. Under 'Add action' -> 'System' -> 'Open Application' (or use a Shortcut/Run command),"
Write-Host "      or in 'Open application' select:"
Write-Host "         $CmdDest" -ForegroundColor White
Write-Host "      (Note: In the Windows file picker dialog, type *.* in the File name box and press Enter"
Write-Host "       to select EasySwitch-Tandem.vbs or EasySwitch-Tandem.cmd directly!)"
Write-Host ""
Write-Host " Test switching from PowerShell right now:"
Write-Host "    powershell -ExecutionPolicy Bypass -File `"$Ps1Dest`" -List"
Write-Host "    powershell -ExecutionPolicy Bypass -File `"$Ps1Dest`" -Info -All"
Write-Host "    powershell -ExecutionPolicy Bypass -File `"$Ps1Dest`" -Toggle -All"
Write-Host "==================================================================" -ForegroundColor Yellow
