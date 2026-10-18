# Backlog

Priorität: 1 = als Nächstes.

## 1 – Treiber-Diagnose (aktuelles Problem)
- [ ] Tab „Netzwerkadapter“: Spalte **Treiberversion** (WMI `Win32_PnPSignedDriver` oder `Get-NetAdapter`).
- [ ] Warnung in der Kopfzeile, wenn TAP-Windows-V9-Adapter unterschiedliche Treiberversionen haben
      oder ein Adapter nicht läuft (leerer Status / `Win32_PnPEntity.ConfigManagerErrorCode` ≠ 0).
- [ ] Version von `C:\Windows\System32\drivers\tap0901.sys` anzeigen.
- [ ] Knopf „TAP-Treiber angleichen“: alle V9-Adapter auf die neueste installierte Version
      (`pnputil /enum-drivers`, `pnputil /add-driver ... /install` bzw. Update je Gerät). Danach Neustart-Hinweis.

## 2 – Robustheit
- [ ] Auf Windows testen: Reparatur-Assistent, Adapter umbenennen/anlegen, openvpn.exe beenden.
- [ ] `RepairEcatcher`: Annahme „Talk2m-eCatcher existiert, OpenVPN TAP-Windows6 fehlt ⇒ zurückgeben“
      ist heuristisch. Besser: GUID merken, die device2cloud nutzt (aus machinetoservice-Log
      `dev_node = '{GUID}'` unter `%TEMP%\machinetoservice\...` lesen?).
- [ ] eCatcher-Prozessname verifizieren (aktuell `eCatcher` angenommen).
- [ ] Sophos-Connect-Prozesse/Dienste erkennen und vor Umschalten beenden anbieten.
- [ ] Beim Umschalten auf Sophos prüfen, ob noch ein OpenVPN-Tunnel aktiv ist.

## 3 – Komfort
- [ ] Tray-Icon mit Schnellumschaltung.
- [ ] Profil-/Log-Ansicht: letzte OpenVPN-Logs von machinetoservice öffnen.
- [ ] Export „Diagnosebericht“ (alle Tabs + Logs als .txt für Support).

## 4 – Auslieferung
- [ ] Code-Signierung (SmartScreen / AV-Freigabe).
- [ ] Optional Installer oder Ablage auf Firmenfreigabe.
- [ ] Mit device2cloud klären: machinetoservice sollte eigenes OpenVPN + eigenen Dienst/Adapter mitbringen
      oder per `--dev-node` gezielt den eigenen Adapter anlegen (tapctl), statt sich auf
      `OpenVPNServiceInteractive` zu verlassen. Das löst Konflikt 1 + 2 an der Wurzel.
- [ ] Zertifikatslaufzeit der Benutzerprofile (seit 2025 nur 1 Jahr) mit VPN-Admin klären.
