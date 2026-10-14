# UI-Referenz: Chummer SR4 (Legacy WinForms)

**Zweck:** visuelle und funktionale Vorlage für die Blazor-Weboberfläche. Grundlage: `Chummer\frmMain.Designer.cs`, `frmCreate.Designer.cs`, `frmCareer.Designer.cs`, ausgewählte `frmSelect*.Designer.cs` und `bin\lang\de.xml`. Designer-Texte sind meist englische Fallbacks; die laufende UI lokalisiert über Sprachdateien. Positionen sind WinForms-Pixel (DPI-/Fenstergröße beachten).

## Gestaltungscharakter

- Klassische Windows-Desktop-App: kompakte Systemschrift (typisch 8–9 pt), hellgraue Formfläche, weiße Eingabefelder/Listen, dünne Rahmen, native Tabs, ComboBoxen, CheckBoxen, NumericUpDowns und Standard-Buttons. Keine Karten-/Dashboard-Ästhetik.
- Dichte, informationsreiche Formulare; viele feste Abstände und zweispaltige Detailbereiche. Modal-Auswahldialoge haben meist Liste links, Details/Optionen rechts, Aktionen unten rechts.
- Selection Trees gruppieren Einträge hierarchisch (z. B. „Selected Positive Qualities“/„Selected Negative Qualities“), oft mit Add/Delete-Buttons, Kontextmenüs und Detailtexten.

## Hauptfenster — `frmMain`

- Referenz-Clientgröße ca. **1175×713 px**; oben Menüleiste (24 px), darunter Toolbar (25 px), danach MDI-Dokumenttabs (`tabForms`, ca. 22 px); Arbeitsfläche füllt den Rest.
- Menüs: **File** (New Character, New Critter, Open, Print/Print Multiple/Print Setup, MRU, Exit), **Tools** (Dice Roller, Options, Updates, Omae), **Windows** (New Window, Close All), **Help** (Wiki, Revision History, About, Dumpshock).
- Toolbar in Reihenfolge: New, Open, Save, Print, Print Preview, Help; primär kleine Icon-Buttons mit Tooltips/Texten. Dokumente bleiben im MDI-Stil als Tabs offen.

## Charakterfenster — `frmCreate` / `frmCareer`

Beide Fenster haben Referenzgröße ca. **1040×664 px** und oben ein großes `TabControl` (ca. 846×637 px). Reihenfolge unten ist die Designer-Reihenfolge; sprachabhängig übersetzte Labels folgen den Ressourcen.

| Reihenfolge / Tab (englischer Designertext) | Wichtigste Bereiche / Interaktion |
|---|---|
| 1 Common | Alias/Metatyp, Attributraster (Basis, Aug, Metatyp-Limit), Qualitäten-Baum (positive/negative Gruppen), Kontakte, Nuyen; Lebenspunkte/Statusübersicht. |
| 2 Skills | Geteilte Skill-Ansichten für aktive und Wissensfertigkeiten sowie Skillgruppen; Hinzufügen/Bearbeiten/Löschen. |
| 3 Martial Arts | Baum für ausgewählte Kampfkünste und Manöver, Rating/Quelle, Add/Delete. |
| 4 Spells and Spirits | Zauberbaum mit Kategorien (Combat, Detection, Health, Illusion, Manipulation), Detailfelder und Sprites/Geister; Add/Delete. |
| 5 Adept Powers | Adept-Kräfte, Power-Points und Hinzufügen. |
| 6 Sprites and Complex Forms | Komplexform-Baum nach Kategorien, Sprite-Bereich, Rating/Quelle, Add/Delete. |
| 7 Critter Powers | Kräfte-/Schwächen-Baum und Punkte/Kosten. |
| 8 Initiation & Submersion | Metamagie-/Initiationsbaum, Rang/Quelle sowie Erhöhen/Senken. |
| 9 Cyberware and Bioware | Cyber-/Bioware-Baum, Suite/Plugin/Gear/Notes-Kontextaktionen. |
| 10 Street Gear | Gear-Inventar, Ausrüstung/Installiert, Menge und Detailaktionen. |
| 11 Vehicles & Drones | Fahrzeugbaum mit Gear, Waffen, Modifikationen, Sensoren und Unteraktionen. |
| 12 Character Info | Identität, Hintergrund, Beschreibung und characterbezogene Angaben. |
| 13 Improvements | Karriere: Verbesserungsbaum; Erstellen: Verbesserungen/Änderungsübersicht. |
| Zusätzliche/bedingte Seiten | Lifestyle, Armor, Weapons, Gear, Pets and Cohorts; Career zusätzlich Karma and Nuyen, Calendar, Notes, Other Info und Condition Monitor. Nicht alle sind im Designer-`Controls.Add`-Block; Sichtbarkeit/Einfügung ist kontextabhängig. Erstellen hat außerdem Build Point Summary und Other Info. |

**Bäume und Status:** In beiden Fenstern stehen separate Trees für Qualitäten, Kampfkünste, Zauber, komplexe Formen, Critter-Kräfte, Cyberware, Lebensstile, Rüstung, Waffen, Foki, Gear, Fahrzeuge; Career hat zusätzlich Improvements. Am unteren Rand StatusStrip: Create zeigt BP/BP Remaining, Essence, Nuyen Remaining; Career Karma, Essence, Nuyen. Career Common zeigt zusätzlich Attribute mit Improve-/Burn-Buttons, Kontakte (+ Add Contact), Qualitäten (+ Add Quality/Delete/Swap) und Condition-Monitor-/Kampfwerte.

**Buttons / Kontext:** kleine native Add/Delete/Notes-/Rating-Buttons direkt beim jeweiligen Tree oder Panel; tab-spezifische ToolStrips/ContextMenus (z. B. Ausrüstung an-/ablegen, installieren, Gear/Modifikationen hinzufügen, Menge teilen/zusammenführen). Oben im Charakterfenster Save, Print, Copy, Paste; Menüs File/Edit/Special.

## Auswahl- und Eingabedialoge (10 repräsentative)

| Dialog | Struktur und sichtbare Beschriftungen (EN; DE-Ressourcen) |
|---|---|
| `frmSelectArmor` | ca. 584×378; Kategorie oben links, Liste links (x12/y38, 306×329), rechts Ballistic/Impact/Capacity/Avail/Cost/Source, Search, Free/Markup; unten „Add & More“, OK, Cancel. |
| `frmSelectWeapon` | ca. 576×472; Kategorie + Liste links (240×433), Suchfeld oben rechts, rechts Damage/AP/Reach/Ammo/Mode/RC/Avail/Cost und Included Accessories/Modifications; Free/Markup; OK/Cancel/Add & More. |
| `frmSelectGear` | Liste links (344×368), Kategorie & Search über ihr/rechts; Detail rechts mit Rating/Avail/Cost/Maximum Capacity; OK/Cancel/Add & More. |
| `frmSelectQuality` | Qualitätenliste links (275×407), Kategorie; rechts BP/Karma, Quelle, „Show only Qualities I can take“, „Show only Metagenetic Qualities“, Free; OK/Cancel/Add & More. |
| `frmSelectSkill` | kompakter Dialog: Beschreibung oben links, Skill-Combo darunter; OK/Cancel rechts; Fokus auf Auswahl statt langer Itemdetails. |
| `frmSelectSpell` | hoher Dialog: Spell-Tree links (264×536), rechts Suchfeld, Descriptors/Type/Range/Damage/Duration/DV/Source, Limited/Extended Spell; OK/Cancel/Add & More unten rechts. |
| `frmSelectLifestyle` | kompakter Listen-Dialog (436×196) mit Lifestyle-Auswahl und OK/Cancel/Add & More. |
| `frmSelectAttribute` | kleiner Formular-Dialog (343×101): Attribut-Combo, Option „Do not affect Metatype Maximum“, OK/Cancel. |
| `frmSelectVehicle` | breiter Dialog (735×412): Kategorie/Liste/Suche plus Fahrzeugdetails; CheckBoxen Free! und Used Vehicle, OK/Cancel/Add & More. |
| `frmMetatype` | Auswahlfenster (507×363): Kategorien, Metatype-Liste, Metavariant-Combo und kontextabhängige Spirit-/Possession-Optionen; OK/Cancel. |

**Sprachen:** Designer-Fallbacks sind überwiegend Englisch (z. B. `Search`, `Category`, `OK`, `Cancel`, `Add & More`). `bin\lang\de.xml` belegt z. B. `String_OK` → **OK**, `String_Cancel` → **Abbruch**, `String_AddMore` → **Weitere hinzufügen**. In der Web-UI dieselben Schlüssel/Begriffe konsistent lokalisieren; Dialogtitel ebenfalls anhand Sprachdatei statt aus Designer-Fallback ableiten.

## Empfehlung für Blazor

- **AppShell**: schmale Menüzeile + icon-/textbasierte Toolbar, darunter dokument-/charakterbezogene Tabs. Tabzustand und aktiven Charakter persistent halten; breite Arbeitsfläche, Statusleiste unten mit BP/Karma, Essence, Nuyen.
- **CharacterWorkspace**: gemeinsame Header-/Statuskomponente und Tab-Router; wiederverwendbare AttributeGrid, GroupedTree/List, DetailPane, InlineActions und Modal ItemPicker. ItemPicker erhält gemeinsame Kategorie-/Search-Leiste, Masterliste links, Detail-/Optionsspalte rechts und Action-Footer (Cancel/OK/Add & More).
- **Responsive Umsetzung ohne Informationsverlust**: desktopnah 2-/3-Spaltenlayout, Splitter-/Resize-Verhalten; auf schmalen Displays Details unter die Liste, Bäume einklappbar, Tabellen horizontal scrollen. Tastaturkürzel, Fokusreihenfolge, Enter=OK, Escape=Abbruch und Kontextaktionen erhalten.
- **CSS-Startwerte (klassische WinForms-Anmutung, nicht Pixelklon):** `--app-bg:#f0f0f0; --surface:#fff; --control-face:#efefef; --border:#a6a6a6; --text:#1f1f1f; --muted:#666; --selection:#316ac5; --selection-text:#fff; --font-ui:"Segoe UI",Tahoma,Arial,sans-serif; --font-size:13px; --control-radius:2px;`. Dünne Kanten, wenig Schatten, kompakte 4/8 px Abstände, native Checkbox-/Select-Anmutung; keine übergroßen Cards oder runden Pill-Buttons.


## Detailreferenz: Tabs in `frmCreate` und `frmCareer`

Die folgenden Bereiche beschreiben die WinForms-Designer-Anordnung (grob links/rechts/oben/unten, kein Pixelklon). Beide Fenster verwenden `tabCharacterTabs` (ca. 846×637 px); die gleichnamigen Tabs teilen meist denselben Aufbau. Die Sprachdatei `bin\lang\de.xml` ist vorhanden, eine `en.xml` liegt hier nicht vor. Die dortigen `Tab_*`-Texte sind die deutschen Laufzeitbeschriftungen; Designer-Text in Klammern ist der englische Fallback. Weitere dynamische Labels werden von `LanguageManager` lokalisiert.

### 1. Attribute und Fertigkeiten

- **Allgemein** (`Tab_Common`: „Allgemein“; Fallback `Common`) – links der Baum für gewählte Qualitäten (Details in Abschnitt 3), mittig das Attributraster mit Spalten **Attribut**, **Base**, **(Aug)** und **Metatype Limits**; rechts/unten Kontaktbereich sowie Nuyen-Eingabe. Oben stehen **Alias** und **Metatyp** samt Quellenangabe; bei mystischen Adepten kommt die Magie/Adepten-Aufteilung hinzu. Deutsche Attributnamen/Kürzel aus `de.xml`: Konstitution/KON, Geschicklichkeit/GES, Reaktion/REA, Stärke/STR, Charisma/CHA, Intuition/INT, Logik/LOG, Willenskraft/WIL, Edge/EDG, Magie/MAG, Resonanz/RES, Initiative/INI.
- **Fertigkeiten** (`Tab_Skills`: „Fertigkeiten“; Fallback `Skills`) – oben zweigeteilt: links **Skill Groups** (Fertigkeitsgruppen), rechts **Active Skills** (aktive Fertigkeiten); unten über die Breite **Knowledge Skills** (Wissensfertigkeiten). Jede Liste ist ein dynamisch gefülltes Panel aus Skill-Zeilen statt eines einzelnen Tabellen-Controls; oben Filter-ComboBox, dazu **Add Exotic Skill** und **Add Knowledge Skill**. Im Karrierefenster dieselbe Aufteilung mit Skill-Filter.
- **Kampfkünste** (`Tab_MartialArts`: „Kampfkünste“) – großer Tree links (**Selected Martial Arts**), rechts Detail-/Manöverbereich; kleine Hinzufügen/Löschen- und Notizaktionen am/unter dem Baum. Manöver und Stile sind gruppiert.
- **Sprüche und Geister** (`Tab_Magician`: „Sprüche und Geister“) – oben/links der Tree **Selected Spells**, darunter der Bereich **Spirits**; Detail- und Auswahlfelder rechts, Aktionen nahe dem jeweiligen Bereich.
- **Adeptenkräfte** (`Tab_Adept`: „Adeptenkräfte“) – Liste/Panel gewählter Kräfte mit Rating-/Power-Point-Angaben und Hinzufügen; verfügbares Punktebudget ist Summenfeld.
- **Komplexe Formen und Sprites** (`Tab_Technomancer`: „Komplexe Formen und Sprites“) – links **Complex Forms**-Tree, darunter **Sprites**; rechts Auswahl-/Detailfelder einschließlich Rating und Kosten.
- **Initiation/Wandlung** (`Tab_Initiation`: „Initiation“ bzw. „Wandlung“ je nach Charakter) – links Tree **Metamagics**, daneben/weiter rechts Initiationsstufe und Optionen; `lstInitiation` führt verfügbare Einträge. Pfeilbuttons **Improve/Reduce Initiation** erhöhen bzw. senken den Rang.
- **Quelldateien:** `Chummer\frmCreate.Designer.cs`, `frmCreate.cs`, `frmCareer.Designer.cs`, `frmCareer.cs`, `SkillControl.Designer.cs`, `SkillControl.cs`, `SkillGroupControl.Designer.cs`, `SkillGroupControl.cs`.

### 2. Ausrüstung

- **Cyberware und Bioware** (`Tab_Cyberware`: „Cyberware und Bioware“) – links der Tree `treCyberware` (ca. 295×572 px), rechts Detailfelder und Inlineaktionen für Rating, Einbau und Notizen; kontextabhängig Suite-, Plugin- und Gear-Aktionen. Ein Essenzsummenfeld zeigt die Auswirkung.
- **Straßenausrüstung** (`Tab_StreeGear`: „Straßenausrüstung“; Designername `Street Gear`) ist ein verschachteltes TabControl: **Lifestyle/Lebensstil**, **Armor/Rüstung**, **Weapons/Waffen**, **Gear/Ausrüstung** und **Pets and Cohorts**. Je Seite links ein hierarchischer Tree (Lebensstile, Rüstungen, Waffen bzw. Gear), rechts Detail-/Modifikationsfelder; bei Rüstung und Gear etwa **Equipped** (angelegt), bei Waffen-Zubehör **Installed** (installiert). Foki haben einen eigenen Bereich **Bonded Foci**. Buttons und Kontextmenüs bieten Hinzufügen/Löschen, Menge, Modifikationen sowie an-/ablegen oder installieren.
- **Fahrzeuge und Drohnen** (`Tab_Vehicles`: „Fahrzeuge und Drohnen“) – links ein breiter Fahrzeug-Tree (`treVehicles`, ca. 403×572 px), rechts Eigenschaften und verschachtelte Ausrüstung: Gear, Waffen/Zubehör, Modifikationen und Sensoren; Detailaktionen arbeiten auf dem ausgewählten Fahrzeug/Unterobjekt.
- **Summen:** Nuyen wird in „Allgemein“ eingegeben und als Gesamtwert angezeigt; Essenz steht zusätzlich dauerhaft in der unteren Statusleiste. Ausrüstung verändert die Werte, die Status-/Detailfelder zeigen die resultierenden Summen.
- **Quelldateien:** `Chummer\frmCreate.Designer.cs`, `frmCreate.cs`, `frmCareer.Designer.cs`, `frmCareer.cs`, `Chummer\frmCreateCyberwareSuite.Designer.cs`, sowie `Chummer\PetControl.Designer.cs` und `PetControl.cs`.

### 3. Vor-/Nachteile und Verbesserungen

- **Qualitäten** befinden sich auf dem Tab **Allgemein**: links Tree `treQualities` (ca. 273×545 px) mit getrennten Gruppen für gewählte positive und negative Qualitäten; oben **Add Quality** und **Delete**. Unter dem Baum stehen **BP/Karma** und **Source** zum ausgewählten Eintrag; Karriere zeigt **Karma** und zusätzlich **Swap Quality**. Detailtext/Notizen und Kontextmenü beziehen sich auf die Auswahl.
- **Verbesserungen** (`Tab_Improvements`: „Verbesserungen“) – links Tree `treImprovements`, rechts Beschreibung/Quelle und kontextabhängige Detailfelder. In `frmCreate` dient die Ansicht der Vorschau/Änderungsübersicht; `frmCareer` führt die aktiven Verbesserungen. Die Aktion **Re-apply Improvements** liegt im Menü **Special**.
- **Quelldateien:** `Chummer\frmCreate.Designer.cs`, `frmCreate.cs`, `frmCareer.Designer.cs`, `frmCareer.cs`, `Chummer\frmCreateImprovement.Designer.cs`, `frmCreateImprovement.cs`.

### 4. Karriere, Karma und Nuyen

- **Karma und Nuyen** (`Tab_Karma`: gleichlautend) ist nur im Karrierefenster sichtbar und horizontal zweigeteilt: links Karma, rechts Nuyen. Je Bereich oben **Gained**, **Spent**, **Edit Expense**; darunter eine `ListView` (ca. 409×353 px) mit Spalten **Date**, **Amount**, **Reason**. Die aktuellen Werte stehen als Summen darunter/nebendran, ein Diagramm visualisiert den Verlauf; Kontextmenüs ermöglichen Bearbeiten/Rückgängig.
- **Kalender** (`Tab_Calendar`: „Kalender“) – nahezu volle Seitenbreite mit `ListView` und Spalten **Date**, **Notes**; oben/rechts **Add Week**, **Edit Week**, **Change Start Week**.
- **Notizen** (`Tab_Notes`: „Notizen“) – mehrzeiliges `txtGameNotes` füllt den Arbeitsbereich.
- **Build-Point-Übersicht** (`Tab_BPSummary`: „GP-Übersicht“) im Create-`tabInfo`: kompakte Kategorien mit GP-Summen für Attribute, Fertigkeiten, Wissensfertigkeiten, Kontakte, Nuyen, Spezialattribute sowie weitere gekaufte Vorteile/Optionen. Beim Karma-basierten Erstellen wechselt der Titel zu „Karma-Übersicht“ (`Tab_BPSummary_Karma`).
- **Statusleiste unten:** Create zeigt **BP**, **BP Remaining** („GP verbleibend“), **Essence** und **Nuyen Remaining**; Career zeigt **Karma**, **Essence** und **Nuyen**. Die Zahlen sind Live-Summen, keine Eingabefelder.
- **Quelldateien:** `Chummer\frmCareer.Designer.cs`, `frmCareer.cs`, `Chummer\frmExpense.Designer.cs`, `frmExpense.cs`, `Chummer\frmLifestyleNuyen.Designer.cs`, plus `Chummer\frmCreate.Designer.cs` für GP-/Karma-Übersicht und Statusleiste.

### 5. Critter

- **Critterkräfte** (`Tab_Critter`: „Critterkräfte“) – links Tree `treCritterPowers` (ca. 333×576 px), rechts Detail-/Ratingfelder und Hinzufügen/Löschen. Checkbox **Counts towards Critter Power limit** steuert, ob der Eintrag gegen die zulässige Kräftezahl zählt; die Anzeige fasst Limit/Kosten zusammen.
- **Pets and Cohorts** (Untertab der Straßenausrüstung; Fallback englisch) – Tier-/Begleiterliste als hierarchische Auswahl mit Detail-/Wertebereich rechts; über `PetControl` werden Profilwerte bearbeitet. Nicht mit dem Critterkräfte-Baum verwechseln.
- **Quelldateien:** `Chummer\frmCreate.Designer.cs`, `frmCreate.cs`, `frmCareer.Designer.cs`, `frmCareer.cs`, `Chummer\PetControl.Designer.cs`, `PetControl.cs`.

### 6. Export, Druck und Würfel

Diese Funktionen sitzen nicht als eigener Charaktertab im `tabCharacterTabs`, sondern im Kopfmenü/Toolbar bzw. in modalen Dialogen.

- **Charakterkopf oben:** Toolbar **Save Character**, **Print Character**, **Copy**, **Paste**; Menü **File** (Save, Save As, Close, Print), **Edit** (Copy/Paste), **Special** (u. a. Cyber-/Bioware-Suite, PACKS-Kit, Metatyp-Wechsel, Critter-Konvertierung, Verbesserungen erneut anwenden). Symbole sind klein; Text erscheint als Tooltip.
- **Export** (`frmExport`) – Dialog mit Label **Export to:** und ComboBox für XSLT-Exportformat; unten rechts **Cancel/OK** (Deutsch: „Abbruch“/„OK“). Es gibt keinen Export-Tab im Charakterfenster.
- **Drucken** (`frmPrintMultiple`) – Charakter-Tree links, rechts **Add Character**, **Remove Character** und **Print Characters**; der einfache Einzeldruck wird über Toolbar/Menü ausgelöst.
- **Würfel** (`frmDiceRoller`) – oben Eingabe **Roll** / **D6**, Methodenauswahl und **Roll**-Button; Optionen **using Rule of 6**, **Hit on 4, 5, or 6**, **Rushed Job**, **Re-Roll Misses**. Rechts/unten erscheinen **Threshold**, **Gremlins**, **Results** und die Würfelergebnisse. `de.xml` liefert dafür u. a. „Würfeln“, „Erfolge“, „Fehlschalg“ (so im XML geschrieben) und „kritischer Patzer“.
- **Quelldateien:** `Chummer\frmCreate.Designer.cs`, `frmCareer.Designer.cs`, `Chummer\frmMain.Designer.cs`, `frmExport.Designer.cs`, `frmPrintMultiple.Designer.cs`, `frmDiceRoller.Designer.cs`, `frmDiceHits.Designer.cs`.

### 7. Info und Header

- **Charakter-Information** (`Tab_CharacterInfo`: „Charakter-Information“) – links Identität und Profilfelder **Character Name**, **Player Name**, **Age**, **Sex**, **Height**, **Weight**, **Hair**, **Eyes**, **Skin**; daneben/rechts Mugshot mit **Add/Delete Mugshot**, Konzept, Beschreibung, Hintergrund und Notizen. Oben **Mark character as Created**. Unten/rechts schreibgeschützte abgeleitete Werte **Street Cred**, **Notoriety**, **Public Aware**.
- **Weitere Informationen** (`Tab_OtherInfo`: „Weitere Informationen“) – Wert-/Labelraster, keine großen Listen: **Movement**, **Lift/Carry**, **Memory**, **Judge Intentions**, **Composure**, Flug-/Schwimmwerte und Initiative-Passes (Astral/Matrix). Werte werden aus Attributen und Ausrüstung berechnet.
- **Header auf „Allgemein“:** **Alias**, **Metatype** samt Quelle und – falls passend – Commlink/Matrixfelder (**Rating**, **System**, **Firewall**, **Response**, **Signal**). Career hat im Attributraster zusätzlich **Improve**-/**Burn**-Buttons; Kontakte mit **Add Contact** und Zähler, Qualitäten-Aktionen wie oben.
- **Quelldateien:** `Chummer\frmCreate.Designer.cs`, `frmCreate.cs`, `frmCareer.Designer.cs`, `frmCareer.cs`, `Chummer\ContactControl.Designer.cs`, `ContactControl.cs`.

**Tabtitel-Übersetzungen:** `bin\lang\de.xml` ordnet u. a. `Tab_Common` → „Allgemein“, `Tab_Skills` → „Fertigkeiten“, `Tab_StreeGear` → „Straßenausrüstung“, `Tab_Karma` → „Karma und Nuyen“, `Tab_Improvements` → „Verbesserungen“, `Tab_CharacterInfo` → „Charakter-Information“, `Tab_OtherInfo` → „Weitere Informationen“ zu. Fallbacks und fachbezogene Laufzeit-Labels sind in den jeweiligen `*.Designer.cs` bzw. `LanguageManager`-Ressourcen verankert; `de.xml` enthält außerdem `String_BP` → „GP“, `String_Remaining` → „verbleibend“, `String_AddMore` → „Weitere hinzufügen“, `String_OK` → „OK“ und `String_Cancel` → „Abbruch“.

- **Zustandsmonitor** (Career, `Tab_ConditionMonitor`: „Zustandsmonitor“) – zwei nebeneinanderliegende Checkbox-Raster für **Physical Condition Track** und **Stun Condition Track**; daneben/unten die Summen **CM Penalty**, **Ballistic Armor**, **Impact Armor** und **Dmg Resistance Pool**. Oben/rechts werden Edge-Änderungen über **Edge Gained**/**Edge Spent** verbucht. Quelldateien: `Chummer\frmCareer.Designer.cs`, `frmCareer.cs`.

