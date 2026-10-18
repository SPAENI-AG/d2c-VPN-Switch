# Changelog

Format nach [Keep a Changelog](https://keepachangelog.com/de/1.1.0/). Der Release-Workflow übernimmt den Abschnitt der jeweiligen Version als Release-Notiz.

## [Unveröffentlicht]

## [2.0.0] - 2026-10-18

### Geändert
- Umbenannt in **D2C VPN Switch** (exe `D2C-VPN-Switch.exe`).
- Neuschreibung als **WPF-Anwendung (.NET 10)** im SPÄNI-Hausstil (vorher WinForms, .NET Framework 4.8):
  Menü, Footer, Info-Fenster, Kartenlayout, Tabellen mit Farbmarkierung.
- Logik in die Bibliothek `VpnSwitch.Core` ausgelagert (Suche, Aktionen, Texte).
- Administratorrechte über das Anwendungsmanifest statt Selbst-Neustart.
- Die ausgelieferte exe enthält die .NET-Laufzeit (Einzeldatei, win-x64).

### Neu
- **TAP-Treiber-Diagnose:** Spalte Treiber im Tab Netzwerkadapter, Warnzeile bei verschiedenen tap0901-Treiberversionen oder Geräteproblemen (Geräte-Manager-Code), Version von tap0901.sys.
- eCatcher wird in der Umschaltliste mit Status «läuft» statt «●» angezeigt (eigener Dienst).
- Optik: Kopfkarte mit Icon, Tabs mit Akzent-Unterstrich, Karten, höhere Tabellenzeilen. Fortschrittsanzeige während der Suche; die Dateisuche läuft parallel zu WMI und Registry.
- Oberfläche **Deutsch/English**, Umschaltung ohne Neustart (Ansicht › Sprache).
- Hilfe im Browser (F1) mit Sprungmarken und `?`-Knöpfen.
- Einstellungen (Sprache, Update-Prüfung) unter `%APPDATA%\D2C-VPN-Switch\settings.json`.
- Update-Hinweis über GitHub Releases (abgeschaltet, bis das Repository in `Directory.Build.props` eingetragen ist).
- Tests für Übersetzungen, Hilfe-Anker und Kernfunktionen.

## [1.1.0]
- Adapter-Verwaltung, eCatcher-Reparatur, openvpn.exe-Erkennung/Beenden.

## [1.0.2]
- eCatcher in der Umschaltliste.

## [1.0.1]
- Build gegen .NET-4.8-Referenzen (Fix „Methode nicht gefunden: String.Split“).

## [1.0.0]
- Erste Version.
