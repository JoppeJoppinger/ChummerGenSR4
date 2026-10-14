# Domain-Scheiben

Das Domain-Grundgerüst hält Charakterdaten als `.chum`-XML-Arbeitsmodell (`CharacterSession`/`CharacterModel`) und lässt unbekannte XML-Inhalte unangetastet. Module werden über `AddChummerCharacterEngine` registriert, nacheinander neu berechnet und validiert; gemeinsame Regelabfragen laufen über `IRuleCatalog`. Kontooptionen liegen in `CharacterSettings`, nicht im Charakter-XML. Keine WinForms-Typen, globalen Singletons oder Querverweise zwischen Scheiben; gemeinsam genutzte Verträge gehören in `Character`.

## Konventionen

- Implementierung: `ChummerGen.Domain\Character\<Slice>\`; Tests: `ChummerGen.Tests\<Slice>Tests.cs`.
- Jede Scheibe implementiert `ICharacterModule` mit eindeutigem `Name`, `RecalculateAsync` und `ValidateAsync`; Ergebnisse und abgeleitete Werte gehören ins Modell/XML, Eingabedaten werden nicht stillschweigend verworfen.
- Vor Änderungen die passende Zeile in `docs\MIGRATION-FUNCTION-MATRIX.md` aktualisieren; Testfälle vergleichen Regelresultate mit Legacy-Belegen und prüfen den XML-Roundtrip.
- Scheiben bleiben unabhängig; Abhängigkeiten nur über Domain-Verträge und `IRuleCatalog`, nicht über Klassen einer anderen Scheibe.

| Scheibe | Matrixzeile | Legacy-Quellen |
|---|---|---|
| Charakterkern / Erschaffung | Charakter erstellen; Charakter öffnen, bearbeiten, speichern | `Chummer\clsCharacter.cs`, `Chummer\frmCreate.cs`, `Chummer\frmMain.cs` |
| Attribute / Fertigkeiten | Fertigkeiten, Attribute, Vor-/Nachteile | `Chummer\clsCharacter.cs`, `Chummer\clsUnique.cs`, `Chummer\frmSelectAttribute.cs`, `Chummer\frmSelectSkill.cs` |
| Ausrüstung | Ausrüstung und Fahrzeuge | `Chummer\clsCharacter.cs`, `Chummer\clsEquipment.cs`, `Chummer\frmSelectWeapon.cs`, `Chummer\frmSelectArmor.cs` |
| Vor-/Nachteile | Fertigkeiten, Attribute, Vor-/Nachteile | `Chummer\clsUnique.cs`, `Chummer\clsImprovement.cs`, `Chummer\frmSelectQuality.cs` |
| Karriere / Karma / Nuyen | Karriere, Karma und Ausgaben | `Chummer\clsCharacter.cs`, `Chummer\clsExpenses.cs`, `Chummer\frmCareer.cs`, `Chummer\frmExpense.cs` |
| Kritter | Sondercharaktere und Zusatzwerkzeuge | `Chummer\clsCharacter.cs`, `Chummer\clsUnique.cs`, `Chummer\frmSelectCritterPower.cs` |
| Export | Export und Charakterblätter; Drucken / Mehrfachdruck | `Chummer\frmExport.cs`, `Chummer\frmViewer.cs`, `bin\export\`, `bin\sheets\` |

Legacy-Dateien sind nur Referenzen: neue Scheiben portieren fachliche Regeln schrittweise und übernehmen weder UI-Abhängigkeiten noch globale Zustände.