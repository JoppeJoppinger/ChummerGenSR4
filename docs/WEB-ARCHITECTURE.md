# Web-Migration: Entwicklungsstand

## Meilenstein 1: Funktionsinventar

Die priorisierte Funktionsmatrix mit Legacy-Belegen, Abnahmekriterien, Testscheiben und Risiken steht in [MIGRATION-FUNCTION-MATRIX.md](MIGRATION-FUNCTION-MATRIX.md). Der Bestand nutzt .NET Framework 4.8/WinForms, `.chum`-XML-Charaktere, XML-Regel- und Sprachkataloge, XSLT-Ausgabe und SOAP-Integrationen. `frmTest.cs` ist kein automatisiertes Testprojekt. Die Matrix kennzeichnet alle Webfunktionen bis zur Abnahme als offen.

## Meilenstein 2: Web-Grundgerüst

Die Desktopanwendung bleibt unverändert im Projekt `Chummer`. Die neuen .NET-10-Projekte sind:

- `ChummerGen.Domain`: UI-unabhängige Verträge und Datentransferobjekte.
- `ChummerGen.Infrastructure`: Adapter für Regelkatalog, Charakterablage und Persistenz (folgende Arbeitsschritte).
- `ChummerGen.Web`: Blazor Web App mit interaktivem Servermodus.
- `ChummerGen.Tests`: xUnit-Testprojekt für die neuen Module.

Die Hauptlösung enthält weiterhin das Desktopprojekt und zusätzlich die vier neuen Projekte. `ChummerGen.Domain` definiert Verträge für aktuellen Benutzer, private Charakterablage und Regelkatalog. `/health` stellt den ASP.NET-Core-Health-Check bereit.

Lokale Entwicklung aus dem Projektverzeichnis:

```powershell
dotnet run --project .\ChummerGen.Web\ChummerGen.Web.csproj
```

Die Standardbindung ist nur für lokale Entwicklung gedacht. Die Anwendung noch nicht an ein LAN-Interface binden: Charakterimport und Web-Charakteroberfläche sind noch nicht migriert und der LAN-Betrieb ist nicht abgenommen.

## Meilenstein 3: Konten, Eigentümerschutz und SQLite

- ASP.NET Core Identity speichert Benutzer und Rollen `Administrator`/`User`; ASP.NET Core Cookie-Authentifizierung und die mitgelieferten Konto-Seiten decken Registrierung, Anmeldung, Abmeldung und Kontoeinstellungen ab.
- Neue Registrierungen erhalten ausschließlich die normale `User`-Rolle. Passwörter verlangen mindestens zwölf Zeichen mit Groß-/Kleinbuchstaben, Ziffer und Sonderzeichen; nach fünf Fehlversuchen wird das Konto 15 Minuten gesperrt. Es wird kein Bestätigungs-E-Mail-Versand vorgetäuscht; Konten werden ohne Mailserver direkt bestätigt.
- Ein leeres Datenbankprofil braucht beim ersten Start `InitialAdmin:Email` und `InitialAdmin:Password`. Der Bootstrapper erstellt genau beim erstmaligen Aufsetzen einen Administrator. Bei vorhandenen Konten ohne Administrator verweigert die Anwendung den Start, statt stillschweigend einen neuen privilegierten Zugang anzulegen.
- EF Core verwaltet Identity- und Charaktertabellen mit einer versionierten Migration. Die SQLite-Datei liegt standardmäßig unter `ChummerGen.Web\Data\chummergen.db` (relativ zum Web-Projekt); der Laufzeitstart wendet ausstehende Migrationen an.
- `/Admin/Users` ist auf Administratoren beschränkt und erlaubt Kontenübersicht sowie Sperren/Entsperren. Eigenes Konto und letzter Administrator lassen sich dort nicht sperren. Administratorrechte können nicht bei der Registrierung ausgewählt werden.
- `ICharacterStore` nutzt den serverseitigen angemeldeten Benutzer und filtert Lesen, Listen, Ändern und Löschen auf dessen Eigentümer-ID. Revisionen sind optimistische Sperrmarken; konkurrierende oder veraltete Speicherstände werden abgelehnt. `.chum` bleibt im Speicheradapter vorläufig als XML-Text erhalten.
- Administratoren können im selben Bereich eine konsistente SQLite-Online-Sicherung erzeugen; Sicherungen liegen standardmäßig in `ChummerGen.Web\Data\backups`. Sicherungserstellung und Wiederöffnen der Sicherungsdatei werden automatisiert getestet.

### Lokaler Erststart

In PowerShell im Repository-Ordner `E:\Github\ChummerGen`:

```powershell
$env:InitialAdmin__Email = Read-Host "E-Mail des Erstadministrators"
$securePassword = Read-Host "Passwort des Erstadministrators" -AsSecureString
$env:InitialAdmin__Password = [System.Net.NetworkCredential]::new("", $securePassword).Password
try {
    dotnet run --project .\ChummerGen.Web\ChummerGen.Web.csproj
}
finally {
    Remove-Item Env:InitialAdmin__Email, Env:InitialAdmin__Password -ErrorAction SilentlyContinue
    $securePassword = $null
}
```

Die Umgebungsvariablen werden nach Programmende entfernt; das Administratorkennwort wird weder eingecheckt noch in `appsettings.json` abgelegt. Für Wiederholungsstarts mit bestehender Datenbank ist der Erstadministrator nicht erneut erforderlich. Für dauerhaften Betrieb gehören Erstadmin-Geheimnisse in einen geschützten Secret Store und TLS vor den Webserver.

## Meilenstein 4b: `.chum`-Import und Charakterliste

- `.chum`-Uploads sind auf 10 MB begrenzt, werden anhand der Endung und des sicheren XML-Loaders geprüft und mit deutschen Fehlermeldungen abgelehnt, wenn sie ungültig sind oder eine DTD enthalten.
- Die Vorschau zeigt Name, Alias, Metatyp, Chummer-Version, Karma, Nuyen und Erschaffungspunkte. Erst nach Bestätigung wird der Original-XML-Text für den angemeldeten Benutzer gespeichert; Mehrfachimporte erzeugen jeweils einen neuen Datensatz.
- `/characters` zeigt nur eigene Charaktere, bietet bestätigtes Löschen und einen authentifizierten Download-Endpunkt mit serverseitiger Eigentümerprüfung.
- `AddChummerRuleCatalog` ist in der Webanwendung registriert.

## Kontosprache und Chummer-Lokalisierung

- `ApplicationUser.PreferredCulture` speichert eine optionale, pro Konto gewählte Kultur; ohne Einstellung wird `de` verwendet, sofern `bin/lang/de.xml` verfügbar ist, andernfalls die erste exakte verfügbare Sprachdatei.
- `/Account/Language` bietet angemeldeten Konten ausschließlich die Kulturen aus `bin/lang` an. Nicht verfügbare Kulturen werden abgewiesen; UI-Texte laufen über erforderliche Chummer-XML-Schlüssel und fehlende Schlüssel werfen einen Fehler statt einen englischen Fallback vorzutäuschen.
- Die Preference-Spalte wird über `20261012203820_AddPreferredCulture` migriert. Der Composition-Root registriert `AddChummerWebLocalization()` nach `AddChummerPersistence` und `AddChummerLocalization`. Verifizierte Keys werden in Navigation und Charakter-Tabs genutzt. Texte ohne semantisch passenden Originalschlüssel bleiben unübersetzt und sind in der Funktionsmatrix als offene Lokalisierungslücken aufgeführt.

## M4d.1: Charakteransicht und Kernfeldbearbeitung

- `/characters/{id:guid}` öffnet nur eigene Charaktere; fremde und unbekannte IDs erscheinen gleichermaßen als nicht gefunden.
- Die klassische Tab-Leiste stellt Info, Attribute, Fertigkeiten, Ausrüstung, Vor-/Nachteile, Karriere, Kritter und Export bereit. Die Fach-Tabs sind über Slots mit separaten Scheiben-Komponenten vorbereitet.
- Name, Alias und Metatyp werden über `CharacterSession` im vorhandenen `.chum`-XML bearbeitet. Speichern nutzt die Revisionsprüfung; Konflikte zeigen eine deutsche Meldung mit „Neu laden“. Geschlecht und Alter bleiben bis zu typisierten Domainfeldern Anzeige-only.
### Aktueller Umfang und noch offene Abnahme

Identität, Rollen, migrationsbasierte Datenbank, Eigentümerfilter, Revisionen, Backup, `.chum`-Import/Vorschau und geschützte Charakterliste sind implementiert. Erweiterte Charakterbearbeitung über Kernfelder hinaus, Regelservices in der fachlichen UI, Backup-Wiederherstellungsverfahren im Betrieb sowie TLS/Firewall-Konfiguration und vollständige Funktionsparität bleiben vor einem LAN-Umstieg abzunehmen.

## Technologie- und Abhängigkeitsregeln

- Zielruntime der Webmigration ist .NET 10; die bestehende Desktopanwendung bleibt zunächst auf .NET Framework 4.8.
- Abhängigkeiten zeigen von Web/Infrastructure zu Domain, niemals von Domain zur UI oder Datenbank.
- `.chum`-Inhalt bleibt bis zum validierten Import/Export als XML-Dokument erhaltbar; Regelkatalogdateien werden nicht verändert.
- Kontoidentität und private Datentrennung müssen serverseitig durchgesetzt werden, nicht allein durch Blazor-Komponenten.
- Meilenstein 3 ergänzt ASP.NET Core Identity, Rollen und EF-Core/SQLite; erst danach darf die Webanwendung im Heimnetz genutzt werden.

