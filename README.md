# D2C VPN Switch

Windows-Tool (WPF, Administratorrechte), das OpenVPN-basierte Fernwartungs-Clients auf einem PC nebeneinander betreibbar macht:
**device2cloud / machinetoservice**, **Sophos Connect** und **eWON eCatcher (Talk2M)**.

*English: Windows tool (WPF, needs administrator rights) that keeps several OpenVPN-based remote-access clients
(device2cloud, Sophos Connect, eWON eCatcher) working side by side on one PC. German and English UI, switchable at runtime.*

- Sucht alle `openvpnserv.exe`, VPN-Dienste, TAP/Wintun-Adapter und installierte VPN-Software
- Schaltet den gemeinsamen Dienst `OpenVPNServiceInteractive` zwischen Sophos und OpenVPN um
- Startet/stoppt VPN-Dienste, beendet hängende `openvpn.exe`
- Adapter umbenennen, aktivieren/deaktivieren, neue TAP-Adapter anlegen
- Assistent „eCatcher-Adapter reparieren“
- Oberfläche Deutsch/English, Hilfe im Browser (F1)

## Download und Nutzung
Neueste Version unter **Releases** (ZIP `D2C-VPN-Switch_<version>_win-x64.zip`, .NET ist enthalten).
ZIP entpacken, `D2C-VPN-Switch.exe` starten (Windows fragt nach Administratorrechten).
Vor dem Umschalten alle VPN-Verbindungen trennen.
Unsignierte Exe: SmartScreen → „Weitere Informationen“ → „Trotzdem ausführen“.

Das Programm greift nur für die optionale Update-Prüfung auf das Netzwerk zu (GitHub, höchstens alle 20 Stunden, keine Nutzungsdaten).
Es speichert keine Passwörter und fasst keine Profildateien (`.srconnect`, `.ovpn`) an.

## Build
Voraussetzung: .NET 10 SDK, Windows.
```
dotnet build VpnSwitch.slnx -c Release
dotnet run --project tests/VpnSwitch.Tests -c Release
```
Aufbau: `src/VpnSwitch.Core` (Logik, Texte, Update-Prüfung), `src/VpnSwitch.App` (WPF), `tests/VpnSwitch.Tests`.

## Doku
- `CLAUDE.md` – Projektkontext, Architektur, Konventionen
- `docs/BACKLOG.md` – offene Punkte
- `CHANGELOG.md` – Versionen
- `scripts/` – PowerShell-Vorgänger (Menü-Variante)

## Support
SPÄNI Elektro-Mechanik AG · [support.elektro-spaeni.ch](https://support.elektro-spaeni.ch) · support@elektro-spaeni.ch

Lizenz: MIT (siehe `LICENSE`)
