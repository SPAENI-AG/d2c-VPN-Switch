# openVPN-Switch.ps1
# Umschalten zwischen Sophos Connect, device2cloud (machinetoservice) und eWON (eCatcher/Talk2M)
#
# Hintergrund:
#  - Sophos und device2cloud teilen sich den Dienst "OpenVPNServiceInteractive"
#    -> es kann immer nur einer aktiv sein, das Skript stellt den Dienst um.
#  - eCatcher hat einen eigenen Dienst (Talk2M VPN Service) und einen eigenen
#    TAP-Adapter "Talk2m-eCatcher". Er braucht OpenVPNServiceInteractive nicht.
#    Das Skript prueft fuer eWON nur, ob Dienst und Adapter in Ordnung sind.
#
# Vor dem Umschalten das jeweils andere VPN trennen.

# --- Automatisch als Administrator neu starten ---
$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) {
    Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    exit
}

$svcName = "OpenVPNServiceInteractive"
$svcKey  = "HKLM:\SYSTEM\CurrentControlSet\Services\$svcName"
$sophos  = '"C:\Program Files (x86)\Sophos\Connect\openvpnserv.exe"'
$d2c     = '"C:\Program Files\OpenVPN\bin\openvpnserv.exe"'

function Set-InteractiveService($path, $name) {
    $exe = $path.Trim('"')
    if (-not (Test-Path $exe)) {
        Write-Host "  Datei nicht gefunden: $exe" -ForegroundColor Red
        return
    }
    try {
        Stop-Service $svcName -Force -ErrorAction Stop
        Set-ItemProperty $svcKey -Name ImagePath -Value $path -ErrorAction Stop
        Start-Service $svcName -ErrorAction Stop
        Write-Host "  OpenVPNServiceInteractive -> $name" -ForegroundColor Green
    } catch {
        Write-Host "  Fehler: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Check-Ewon {
    $svc = Get-Service | Where-Object { $_.Name -like "*Talk2m*" -or $_.DisplayName -like "*Talk2M*" }
    if (-not $svc) {
        Write-Host "  Kein Talk2M-Dienst gefunden - ist eCatcher installiert?" -ForegroundColor Red
    } else {
        foreach ($s in $svc) {
            if ($s.Status -ne "Running") {
                try { Start-Service $s.Name -ErrorAction Stop; Write-Host "  $($s.DisplayName) gestartet" -ForegroundColor Green }
                catch { Write-Host "  $($s.DisplayName) laesst sich nicht starten: $($_.Exception.Message)" -ForegroundColor Red }
            } else {
                Write-Host "  $($s.DisplayName) laeuft" -ForegroundColor Green
            }
        }
    }
    $tap = Get-NetAdapter -IncludeHidden -ErrorAction SilentlyContinue | Where-Object Name -eq "Talk2m-eCatcher"
    if ($tap) {
        Write-Host "  Adapter 'Talk2m-eCatcher' vorhanden ($($tap.InterfaceDescription), $($tap.Status))" -ForegroundColor Green
    } else {
        Write-Host "  Adapter 'Talk2m-eCatcher' FEHLT." -ForegroundColor Red
        Write-Host "  Abhilfe: im eCatcher-Ordner ...\eCatcher-Talk2M\Talk2mVpnService\drivers"
        Write-Host "  'addtap.bat' als Admin ausfuehren und den neuen TAP-Adapter in 'Talk2m-eCatcher' umbenennen."
    }
}

function Show-Status {
    Write-Host ""
    $p = (Get-ItemProperty $svcKey -ErrorAction SilentlyContinue).ImagePath
    if ($p -like "*Sophos*")      { $mode = "Sophos Connect" }
    elseif ($p -like "*OpenVPN*") { $mode = "device2cloud (OpenVPN)" }
    else                          { $mode = "unbekannt" }
    Write-Host "  OpenVPNServiceInteractive: $mode" -ForegroundColor Cyan
    Write-Host "  Pfad: $p"
    Write-Host ""
    Get-NetAdapter -IncludeHidden -ErrorAction SilentlyContinue |
        Where-Object InterfaceDescription -match "TAP|Wintun|Sophos" |
        Format-Table Name, InterfaceDescription, Status -AutoSize
}

# --- Menue ---
Show-Status
Write-Host "  [1] Sophos Connect"
Write-Host "  [2] device2cloud / machinetoservice"
Write-Host "  [3] eWON / eCatcher (Talk2M)"
Write-Host "  [0] Beenden"
Write-Host ""
$choice = Read-Host "Auswahl"

switch ($choice) {
    "1" { Set-InteractiveService $sophos "Sophos Connect" }
    "2" { Set-InteractiveService $d2c "device2cloud (OpenVPN)" }
    "3" { Check-Ewon }
    default { exit }
}

Show-Status
Read-Host "Enter zum Schliessen"
