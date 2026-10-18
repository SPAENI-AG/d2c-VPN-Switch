# CLAUDE.md – D2C VPN Switch

Kontext für Claude Code. Sprache im Projekt: **Deutsch** (UI-Texte, Logs, Doku; UI zusätzlich Englisch). Code-Bezeichner Englisch.
Seit 2.0.0: **WPF / .NET 10** im SPÄNI-Hausstil (Skill `spaeni-wpf-app-layout`). Die WinForms-Version 1.x liegt lokal unter `legacy/` (nicht im Repo, steht in `.gitignore`).

## Worum geht es

Windows-Desktop-Tool, das auf einem Techniker-PC mehrere OpenVPN-basierte Fernwartungs-Clients nebeneinander lauffähig hält:

| Client | Dienst | TAP-Adapter (Name) | Treiber |
|---|---|---|---|
| device2cloud / machinetoservice | `OpenVPNServiceInteractive` → `C:\Program Files\OpenVPN\bin\openvpnserv.exe` | `OpenVPN TAP-Windows6` | tap0901 9.24.x |
| Sophos Connect | **gleicher Dienstname** `OpenVPNServiceInteractive` → `C:\Program Files (x86)\Sophos\Connect\openvpnserv.exe` | `Sophos-TAP` (+ eigener „Sophos TAP Adapter“, Treiber 9.27) | tap0901 9.24.x |
| eWON eCatcher (Talk2M) | eigener Dienst `Talk2mVpnService` | `Talk2m-eCatcher` (per Name gesucht) | bringt **tap0901 9.0.0.21** mit |

Die vollständige Fehleranalyse (Log-Auszüge, Ursachenkette) liegt lokal in `docs/HINTERGRUND.md` (nicht im Repo, steht in `.gitignore`).

## Die drei bekannten Konflikte

1. **Dienst-Kollision:** Sophos Connect registriert seinen eigenen `openvpnserv.exe` unter dem
   Standard-Dienstnamen `OpenVPNServiceInteractive`. Der device2cloud-Client spricht dann mit dem
   falschen Dienst → `Adapter '{GUID}' not found`. Lösung im Tool: `ImagePath` des Dienstes umschalten.
2. **TAP-Adapter-Kollision:** eCatcher und Sophos überschreiben bei der Installation fremde
   TAP-Adapter. Lösung: jedem Client einen eigenen, korrekt benannten Adapter geben
   (`tapctl create --hwid root\tap0901 --name ...` oder eCatchers `addtap.bat` + umbenennen).
3. **Treiber-Kollision (aktuell offen):** Alle „TAP-Windows Adapter V9“ teilen sich `tap0901.sys`.
   eCatcher installiert die alte 9.0.0.21 → Adapter, die auf 9.24 eingerichtet sind, starten nicht
   (leerer Status in `Get-NetAdapter`) → OpenVPN: `CreateFile failed on tap-windows6 device`.
   Lösung: OpenVPN-MSI reparieren (9.24 zurück), eCatcher-Adapter im Geräte-Manager auf 9.24 umstellen, Neustart.

## Aufbau

```
Directory.Build.props        Version (einzige Stelle), GitHubRepository, Firma, Lizenz
VpnSwitch.slnx
src/VpnSwitch.Core/          UI-frei (net10.0-windows): Logik, Texte, Update-Prüfung
  Scanner.cs                 openvpnserv.exe (Program Files, Tiefe 4), VPN-Dienste (WMI), Adapter (WMI),
                             installierte Software (Uninstall-Keys), eCatcher-Erkennung
  Ops.cs                     Aktionen: Umschalten, Dienst start/stop, openvpn beenden, eCatcher-Reparatur
  Tools.cs                   Prozessaufrufe, PowerShell (-EncodedCommand), Adapter umbenennen/aktivieren/anlegen
  Models.cs                  Datenklassen + Zustandsschlüssel (St)
  Loc.cs, Strings.cs         DE/EN-Texte (Schlüssel -> (De, En)), Live-Umschaltung
  AppSettings.cs             %APPDATA%\D2C-VPN-Switch\settings.json (keine Passwörter)
  UpdateChecker.cs           GitHub Releases (aus: solange GitHubRepository "OWNER/…" ist)
  HelpServer.cs              Loopback-Webserver für die Hilfe
src/VpnSwitch.App/           WPF
  App.xaml(.cs)              Styles (Hausstil), Einstellungen laden, Sprache setzen
  MainWindow.xaml(.cs)       Menü, Update-Banner, Kopfzeile, Tabs, Log, Footer
  Views/SettingsView         Sprache, Update-Prüfung
  AboutWindow, PromptWindow  Info-Fenster (Entwickler nur dort), Eingabedialog
  Help/de.html, en.html      Hilfe, gleiche Anker in beiden Sprachen
  app.manifest               requireAdministrator (UAC statt Selbst-Neustart)
tests/VpnSwitch.Tests/       Konsolen-Testlauf: Übersetzungen, Hilfe-Anker, Kernfunktionen
legacy/                      WinForms-Version 1.x (lokal, nicht im Repo)
scripts/                     PowerShell-Vorgänger (Menü)
```

## Build und Test

```
dotnet build VpnSwitch.slnx -c Release
dotnet run --project tests/VpnSwitch.Tests -c Release
```
Ohne UAC-Abfrage zum Ausprobieren (Admin-Funktionen schlagen dann fehl):
PowerShell `$env:__COMPAT_LAYER='RunAsInvoker'; & .\src\VpnSwitch.App\bin\Release\net10.0-windows\D2C-VPN-Switch.exe`

Release: `<Version>` in `Directory.Build.props` erhöhen, Abschnitt in `CHANGELOG.md`, Tag `vX.Y.Z` → Workflow `release.yml`
(self-contained Einzeldatei win-x64, ZIP + SHA-256). GitHub-Repository: `SPAENI-AG/d2c-VPN-Switch` (noch nicht angelegt/gepusht).

## Konventionen

- Code-Behind, Steuerelementnamen mit Typpräfix (`tb_`, `bt_`, `dg_`, `tc_`, `mi_`, `chk_`, `cb_`).
- **Jeder sichtbare Text über `Loc`** (`{l:Tr Key}` in XAML, `Loc.T("Key", args)` im Code), DE und EN in `Strings.cs`.
  In Code gesetzte Texte werden nach `Loc.Instance.LanguageChanged` neu aufgebaut (`MainWindow.Fill`). Zustände sind Schlüssel (`St.*`), nie Anzeigetexte vergleichen.
- Firmenname immer **SPÄNI Elektro-Mechanik AG**; Entwicklername nur im Info-Fenster.
- Langlaufende Aktionen in `Task.Run` (`MainWindow.RunLogged`), danach immer `DoScan()`.
- Jede Aktion, die Dienste/Adapter verändert, loggt vorher und nachher.
- Keine Netzwerkzugriffe ausser der optionalen Update-Prüfung, keine Telemetrie. Keine Profildateien (.srconnect) anfassen oder speichern – sie enthalten private Schlüssel.
- Version nur in `Directory.Build.props` ändern und bei jeder ausgelieferten Änderung erhöhen.

## Teststatus

- Gebaut, Tests grün, WPF-Fenster startet (Prozess läuft, Titel gesetzt). Single-File-Publish geprüft.
- **Auf Windows nicht von Hand durchgeklickt:** Oberfläche (Farben, Tabellen), Sprachwechsel, Umschalten, Reparatur-Assistent, Adapter umbenennen/anlegen, openvpn.exe beenden, Hilfe im Browser.
- Der Funktionsumfang entspricht 1.1.0; die Logik wurde 1:1 übernommen (Texte jetzt über `Loc`).

## Offene Punkte / Backlog

Siehe `docs/BACKLOG.md`. Zur Veröffentlichung: Repository anlegen, `GitHubRepository` setzen, Freigabe Geschäftsleitung (MIT), Git-Identität vor dem ersten Commit setzen.
