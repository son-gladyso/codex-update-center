# Codex Update Center

[简体中文](../../README.md) · [English](../README.en.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Português (Brasil)](README.pt-BR.md) · [Русский](README.ru.md) · [繁體中文](README.zh-TW.md)

### Codex unter Windows mit weniger Aufwand aktualisieren.

**Installation finden, ein geprüftes offizielles Update vorbereiten und das tatsächliche Installationsergebnis bestätigen, alles in einem Fenster.** Für alle, die nicht wiederholt Installationsordner suchen, große Pakete erneut herunterladen oder ein nicht funktionierendes Update untersuchen möchten.

[![Windows build](https://github.com/son-gladyso/codex-update-center/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/son-gladyso/codex-update-center/actions/workflows/build.yml)

[Windows-Vorschau herunterladen](https://github.com/son-gladyso/codex-update-center/releases) · [Problem melden](https://github.com/son-gladyso/codex-update-center/issues/new/choose)

Windows x64 · Eigenständiges ZIP mit .NET · MIT · Unabhängiges Community-Projekt, ohne Verbindung zu OpenAI

![Installationsauswahl, Kandidatenprüfung, Netzwerkkosten und Installationsbestätigung](../images/update-center.png)

*Die echte Anwendung mit synthetischen Beispieldaten. Die Oberfläche ist derzeit chinesisch; das Bild belegt kein Update einer tatsächlichen Benutzerinstallation.*

## Ein Fenster für typische Update-Probleme

| Problem | So hilft das Werkzeug |
| --- | --- |
| Welche Installation soll aktualisiert werden: Desktop oder CLI? | Erkennt offizielle MSIX-Installationen des aktuellen Benutzers und standardmäßige npm-CLI-Präfixe. Mehrere Installationen bleiben getrennt; nur das ausgewählte Ziel wird aktualisiert. |
| Nach einer Unterbrechung beginnt der Download von vorn | Speichert Fortsetzungspunkte. Große native CLI-Komponenten werden in fortsetzbaren 1-MiB-Bereichen geladen; offizielle Pakete im Cache werden vor Wiederverwendung erneut geprüft. |
| Das Paket ist bereit, aber ich arbeite noch | Trennt Vorbereitung und Installation. Vor der Installation sind Bestätigung und Prüfung laufender Prozesse erforderlich; Codex wird nie zwangsweise beendet. |
| Erfolgsmeldung, aber die CLI läuft nicht | Erkennt die installierte Version erneut und führt den CLI-Versionsbefehl aus. Erfolg wird erst bei passenden Ergebnissen gemeldet. |
| Mein mobiler Hotspot soll nicht leergezogen werden | Blockiert große Downloads bei getakteten Verbindungen oder unbekannten Kosten standardmäßig. Der aktuelle Download kann ausdrücklich erlaubt werden; vor der npm-Installation werden die Kosten erneut geprüft. |
| Auf C ist kaum Platz | Prüft Platzbedarf und Sicherheitsreserven. C wird nur bevorzugt, wenn die Reserven reichen; andernfalls kommen geeignete interne Festplatten infrage. Der Cache-Pfad wird angezeigt. |

**Signaturen und Inhaltshashes offizieller Pakete werden vor der Installation geprüft.** Kein API key nötig. Das Werkzeug liest weder Kontotoken noch Chatdatenbanken, erfasst keine Telemetrie und ersetzt keine Launcher-Konfiguration.

## In vier Schritten starten

1. Lade `CodexUpdateCenter-*-win-x64.zip` aus [Releases](https://github.com/son-gladyso/codex-update-center/releases) herunter und prüfe `SHA256SUMS.txt`.
2. Entpacke das ZIP in einen beschreibbaren Ordner und starte `CodexUpdateCenter.exe`. Die .NET-Laufzeit ist enthalten.
3. Wähle eine Installation und bereite ihr Update vor. Eine einzelne unterstützte Installation wird standardmäßig automatisch ausgewählt und vorbereitet; diese Startoption lässt sich abschalten.
4. Warte auf die Kandidatenprüfung, speichere deine Arbeit und beende die ausgewählte Codex-Installation regulär. Bestätige dann die Installation und prüfe anschließend die neu erkannte Version.

Nicht erkannte benutzerdefinierte npm-Verzeichnisse lassen sich manuell hinzufügen. Ist Codex noch nicht installiert, zeigt das Fenster die offiziellen Installationshinweise.

> Dies ist eine **Community-Vorschau** ohne kommerzielle Authenticode-Signatur. Deaktiviere keine Windows-Sicherheitsfunktionen, um sie auszuführen. Die Bereitstellungsmatrix für frische Windows-Systeme, praktische Barrierefreiheitstests und einige Proxy-/Netzwerkfälle sind noch nicht verifiziert. Siehe [Validierungsprotokoll](../VALIDATION.md).

## Unterstützte Installationen

| Installation | Unterstützung |
| --- | --- |
| Offiziell Store-signierte `OpenAI.Codex`-x64-Desktopinstallation des aktuellen Benutzers | Erkennt und prüft das offizielle MSIX und aktualisiert über die Windows-Bereitstellungsdienste. Die tatsächliche Windows-Bereitstellung steht noch zur vollständigen Abnahme aus. |
| Stabile globale `@openai/codex`-Installation mit standardmäßigem Node.js/npm | Prüft Hauptpaket und native Windows-Komponenten und aktualisiert nach Bestätigung. Ein echtes Upgrade in einem isolierten Verzeichnis wurde geprüft. |
| Mehrere globale npm-Verzeichnisse oder Desktop und CLI nebeneinander | Getrennte Anzeige, jeweils ein Update-Ziel; npm-Präfixe können manuell ergänzt werden. |
| Portable Kopien, eigene Mehrinstanz-Launcher, WSL, ARM64 und verlinkte Node-Versionsmanager-Strukturen | Keine automatische Übernahme; verwende das ursprüngliche Installationswerkzeug. |

Desktop und CLI werden getrennt gepflegt. Eine eigenständige CLI wird nicht in die Desktopanwendung eingefügt. Ein Download-Cache auf einem anderen Laufwerk verändert den von Windows verwalteten Desktop-Installationsort nicht.

## Klare Ergebnisse bei Problemen

- Fortsetzungspunkte bleiben bei Pause oder Schließen des Fensters erhalten. Eine vom Benutzer ausdrücklich pausierte Übertragung wird nicht automatisch fortgesetzt.
- Bei Fehler, Unterbrechung oder unbestätigter Version wird eine erneute Prüfung verlangt. **Die bloße Rückkehr des Installationsprogramms gilt nicht als Erfolg.**
- npm-Installationen sind nicht atomar; nach einem Fehler kann eine npm-Reparatur nötig sein. Ein automatisches Rollback wird nicht garantiert.
- Das Werkzeug beendet Codex nicht zwangsweise, installiert keinen älteren Kandidaten, migriert keine Konten oder Chats und startet keinen dauerhaft laufenden Hintergrunddienst.
- Es verwendet den wirksamen Systemproxy mit TLS-Prüfung. Es steuert keine VPN-Knoten und verspricht weder Höchstgeschwindigkeit noch schnelle Downloads in jedem Netzwerk. npm-Indexanfragen können bei einigen Proxys weiterhin scheitern; Cache und Fehlerprotokolle bleiben erhalten.

Siehe [Support und Fehlerbehebung](../SUPPORT.md). Der Zustand liegt unter `%LOCALAPPDATA%\CodexUpdateCenter\Public`, Cache-Pfade werden in der Aufgabenansicht angezeigt. Das Entfernen dieses Updaters deinstalliert Codex nicht. Prüfe Zustands- und Cache-Dateien vor dem Bereinigen.

## Was geprüft wurde

- **81 Programmtests** zu Zieländerungen, ungültigen Signaturen, Downloadfortsetzung, Parallelitätssperren, Pause und Netzwerkkostenschranken bei der Installation.
- **10 Prüfungen der Release-Werkzeuge** zu manipulierten Paketen, fehlenden versteckten Dateien, Pfaden außerhalb des erlaubten Bereichs und Datenschutzgrenzen im Quellcode.
- Echtes Upgrade von **CLI 0.161.0 → 0.162.1** in einem neuen isolierten Verzeichnis mit Ausführung der neuen CLI zur Versionsprüfung. Bestehende Installationsmanifeste blieben unverändert.
- Das offizielle Desktoppaket bestand Windows-Signaturprüfung und sämtliche Blockhashprüfungen. Ein unabhängiger Build des Quellpakets sowie Entpacken und Starten des Laufzeitpakets wurden ebenfalls geprüft.

Der reale Upgrade-Test verwendete erneut geprüfte offizielle Pakete und zwischengespeicherte npm-Metadaten. **Das ist kein erfolgreicher Netzwerktest mit leerem Cache.** Signatur- und Hashprüfung belegen keine echte Windows-Bereitstellung. Aktuelle automatische Läufe stehen unter [GitHub Actions](https://github.com/son-gladyso/codex-update-center/actions); genaue Grenzen und offene Punkte unter [VALIDATION](../VALIDATION.md).

## Zuerst ansehen, noch nicht installieren

Entpacke das Werkzeug und prüfe die gefundenen Installationen und aktuellen Versionen, um die Eignung für deine Umgebung zu beurteilen. Eine Installation muss nicht sofort bestätigt werden. Die Vorbereitung kann Netzwerkzugriffe und Downloads auslösen; die automatische Vorbereitung einer einzelnen unterstützten Installation lässt sich deaktivieren.

- Ausführen: Windows x64 und ein beschreibbarer Zielordner. .NET ist enthalten, kein API key erforderlich.
- Prüfen und vorbereiten: Zugriff auf offizielle Quellen, abhängig von Proxy, Netzwerkkostenschranken und freien Speicherreserven.
- Installieren: Arbeit speichern, ausgewählten Codex regulär schließen, Ziel erneut prüfen und erst bestätigen, wenn alles bereit ist.

**Der erste Erfolg bedeutet:** Du kannst Typ, Ort und Version des gewählten Ziels erkennen und seinen Prüf- oder Sperrzustand verstehen. Ein Upgrade ist erst nach den erforderlichen Nachprüfungen erfolgreich.

## Häufige Fragen

**Sind Desktop und CLI eine einzige Installation?** Nein. Sie werden getrennt erkannt und aktualisiert; eine CLI-Versionsausgabe belegt keine Desktopbereitstellung.

**Warum fehlt meine portable Kopie oder eigene Mehrinstanz-Konfiguration?** Diese Strukturen liegen außerhalb der automatischen Verwaltung. Nutze ihr ursprüngliches Werkzeug, statt sie als unterstützte Installation auszugeben.

**Warum ist die Installation nach dem Download noch blockiert?** Kandidatenprüfung, Zielbestätigung sowie geltende Prozess- und Netzwerkkostenschranken müssen noch erfüllt werden. Download abgeschlossen bedeutet nicht Installation abgeschlossen.

**Warum werden Downloads bei unbekannten Kosten blockiert?** Das ist der standardmäßige Kostenschutz. Prüfe das Netzwerk und erlaube gegebenenfalls ausdrücklich diesen Download; schwäche TLS oder Signaturprüfungen nicht ab.

**Wird bei einem Fehler automatisch die alte Version wiederhergestellt?** Ein Rollback wird nicht garantiert. npm ist nicht atomar. Prüfe Ergebnis und [Supportleitfaden](../SUPPORT.md), statt die Installation wiederholt zu bestätigen.

**Was tun, wenn Windows vor dem Werkzeug warnt?** Die Vorschau besitzt keine kommerzielle Authenticode-Signatur. Prüfe Quelle und Prüfsummen, deaktiviere die Systemsicherheit nicht und starte es nicht, wenn seine Vertrauenswürdigkeit nicht feststeht.

### Vor Änderungen die Zuständigkeiten finden

| Bereich | Quellcode-Einstieg | Im PR erklären |
| --- | --- | --- |
| Erkennung und Zielauswahl | `src/UpdateCenter.Core/Discovery.cs` | Vermeidung falscher Ziele und Umgang mit nicht unterstützten Strukturen. |
| Offizielles Netzwerk und Prüfung | `OfficialNetwork.cs`, `Verification.cs` | Beobachtbare Fehler-, Fortsetzungs- und Ablehnungsfälle, ohne Vertrauensprüfungen zu schwächen. |
| Update-Ablauf und Persistenz | `UpdateService.cs`, `Storage.cs` | Pause, Unterbrechung, Zustand nach Neustart und Prüfung nach Installation. |
| Oberfläche und Meldungen | `src/UpdateCenter.App/MainWindow.xaml` und Code-behind | Regressionstests für wiederholte Aktionen, Zustandswechsel und Fehlermeldungen. |
| Build und Veröffentlichung | `scripts/`, `docs/RELEASING.md` | Tatsächliche Audit-, Test- und Manifestprüfergebnisse. |

Die kurzen Core-Dateinamen liegen unter `src/UpdateCenter.Core/`. Siehe [Architektur](../ARCHITECTURE.md); eine simulierte Erfolgsmeldung beweist keine native Windows-Bereitstellung.

## Entwicklung und Mitarbeit

Unter Windows x64 mit PowerShell 7 und dem SDK aus `global.json` bauen:

```powershell
pwsh -File scripts/build.ps1 -Output artifacts/release-local
```

Release-Prüfungen auditieren den Quellcode, stellen gesperrte Abhängigkeiten wieder her, führen Tests aus, erstellen eigenständige Pakete und prüfen dateiweise Release-Manifeste. Der Ausgabeordner muss neu sein; gewöhnliche Tests installieren oder verändern Codex nicht. Windows-CI und isolierte native Abnahme trennen die jeweiligen Nachweisbereiche.

[Architektur](../ARCHITECTURE.md) · [Mitwirken](../../CONTRIBUTING.md) · [Release-Prozess](../RELEASING.md) · [Änderungsverlauf](../../CHANGELOG.md) · [Sicherheit](../../SECURITY.md)

Hilfreiche Rückmeldungen nennen Installationsart, Windows-Version, problematischen Schritt und erwartetes Ergebnis. Verwende die [Problem- oder Vorschlagsvorlagen](https://github.com/son-gladyso/codex-update-center/issues/new/choose). Füge niemals Zugangsdaten, Chats oder rohe Kontoprotokolle bei. Kleine reproduzierbare Korrekturen und klarere Fehlermeldungen sind willkommen.

### Geeignete erste Beiträge

- **Dokumentation und Meldungen:** Einen beobachteten Fehler reproduzierbar beschreiben oder Installationstypen, Cache-Pfade und Fehlermeldungen verständlicher machen.
- **Regressionstests:** Zieländerungen, Pause/Fortsetzung oder fehlgeschlagene Paketprüfungen abdecken und das beobachtbare Ergebnis erläutern.
- **Windows-Validierung:** Genaues System, Installationstyp, Versionen und Testumfang dokumentieren; synthetische Tests, native Vorgänge und Onlineprüfung trennen.

Lies den [Beitragsleitfaden](../../CONTRIBUTING.md) und das [Validierungsprotokoll](../VALIDATION.md), dann wähle ein klar eingegrenztes Problem. Für echte Installationstests nutze eine isolierte Umgebung, nicht standardmäßig deine tägliche Arbeitsinstallation. Besprich Erkennungsregeln, Update-Mechanismus und Fehlerbehandlung, bevor ein neuer Installationstyp als unterstützt bezeichnet wird.

Diese Seite zeigt einen Screenshot der echten Anwendung mit Beispieldaten, keine durchgehende Aufzeichnung von Vorbereitung bis Installationsabschluss. Verbesserte reproduzierbare Anleitungen sind willkommen; Demozustände dürfen aber nicht als reale Upgrade-Ergebnisse ausgegeben werden.

MIT gilt für den Quellcode dieses Werkzeugs. Veröffentlichte Archive enthalten Codex nicht; dessen Pakete werden zur Laufzeit aus offiziellen Quellen geladen und unterliegen eigenen Lizenzen und Bedingungen. Siehe [NOTICE](../../NOTICE.md).
