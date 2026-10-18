@echo off
rem Startet openVPN-Switch.ps1 (muss im selben Ordner liegen) per Doppelklick
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0openVPN-Switch.ps1"
if errorlevel 1 pause
