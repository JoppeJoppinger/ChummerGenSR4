using System.Xml.Linq;

namespace ChummerGen.Tests;

public sealed class OtherTabLocalizationTests
{
    private static readonly string[] Cultures = ["de", "en-us", "fr", "jp"];

    private static readonly IReadOnlyDictionary<string, string[]> ExpectedValues =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Label_Name"] = ["Name:", "Name:", "Nom :", "名前:"],
            ["Label_Category"] = ["Kategorie:", "Category:", "Catégorie :", "分類:"],
            ["String_Rating"] = ["Stufe", "Rating", "Indice", "レーティング"],
            ["Label_Avail"] = ["Verfügbarkeit:", "Avail:", "Disponibilité :", "入手値:"],
            ["Label_GearRating"] = ["Qualitätsstufe:", "Gear Rating:", "Indice d'équipement :", "装備のレーティング:"],
            ["Label_GearQty"] = ["Anzahl:", "Gear Qty:", "Quantité :", "装備の個数:"],
            ["Label_Cost"] = ["Kosten:", "Cost:", "Coût :", "価格:"],
            ["Checkbox_Equipped"] = ["angelegt", "Equipped", "Equipée", "装備済み"],
            ["Checkbox_BlackMarketDiscount"] = ["10% Schwarzmarkt-Rabatt", "Black Market Discount (10%)", "Réduction contact au marché noir (10%)", "ブラックマーケット割引 (10%)"],
            ["String_DescActive"] = ["Aktiv", "Active", "Actif", "能動"],
            ["Tab_Notes"] = ["Notizen", "Notes", "Notes", "ノート"],
            ["Label_Capacity"] = ["Kapazität:", "Capacity:", "Capacité :", "容量:"],
            ["Label_Source"] = ["Quelle:", "Source:", "Source :", "ソース:"],
            ["String_Delete"] = ["Löschen", "Delete", "Effacer", "削除"],
            ["Label_SummaryPositiveQualities"] = ["Gaben", "Positive Qualities", "Avantages", "有利な資質"],
            ["Label_SummaryNegativeQualities"] = ["Handicaps", "Negative Qualities", "Défauts", "不利な資質"],
            ["String_BP"] = ["GP", "BP", "PC", "BP"],
            ["String_Karma"] = ["Karma", "Karma", "Karma", "カルマ"],
            ["Label_ImprovementType"] = ["Art der Verbesserung:", "Improvement Type:", "Type d'amélioration :", "Improvement の種類:"],
            ["Label_CreateImprovementValue"] = ["Wert:", "Value:", "Valeur :", "値:"],
            ["Label_CreateImprovementMinimum"] = ["Minimum:", "Minimum:", "Minimum :", "最大:"],
            ["Label_CreateImprovementMaximum"] = ["Maximum:", "Maximum:", "Maximum :", "最小:"],
            ["String_Date"] = ["Datum", "Date", "Date", "日付"],
            ["String_Reason"] = ["Grund", "Reason", "Description", "理由"],
            ["String_Cancel"] = ["Abbruch", "Cancel", "Annuler", "キャンセル"],
            ["Button_KarmaGained"] = ["Karma verdient", "Karma Gained", "Gain de Karma", "カルマ獲得"],
            ["Button_KarmaSpent"] = ["Karma ausgegeben", "Karma Spent", "Dépense de Karma", "カルマ消費"],
            ["Button_NuyenGained"] = ["Nuyen verdient", "Nuyen Gained", "Gain de nuyens", "新円収入"],
            ["Button_NuyenSpent"] = ["Nuyen ausgegeben", "Nuyen Spent", "Dépense de nuyens", "新円支出"],
            ["Title_CareerMode"] = ["Karriere Modus", "Career Mode", "Carrière", "キャリアモード"],
            ["Title_CreateMode"] = ["Erstellungsmodus", "Create Mode", "Mode Création", "作成モード"],
            ["Label_Expense_KarmaAmount"] = ["Menge Karma:", "Karma Amount:", "Points de Karma :", "カルマの値:"],
            ["Label_Expense_NuyenAmount"] = ["Menge Nuyen:", "Nuyen Amount:", "Nuyens :", "新円の金額:"],
            ["String_Expense_Refund"] = ["Rückerstattung", "Refund", "Rembourser", "払い戻し"],
            ["Label_OmaeUpload_CharacterType"] = ["Charaktertyp:", "Character Type:", "Type de personnage :", "キャラクターの種類:"],
            ["Tab_Critter"] = ["Critterkräfte", "Critter Powers", "Pouvoirs de créatures", "クリッターパワー"],
            ["Label_PowerPoints"] = ["Kraftpunkte:", "Power Points:", "Pts de Pouvoir :", "パワーポイント:"],
            ["Button_AddCritterPower"] = ["Kraft hinzufügen", "&Add Power", "&Ajout Pouvoir", "パワーを追加 (&A)"],
            ["String_ActionAutomatic"] = ["Auto", "Auto", "Automatique", "自動"],
            ["String_Page"] = ["Seite", "page", "page", "page"],
            ["Label_RangeHeading"] = ["Reichweite", "Range", "Portée", "射程"],
            ["Label_Type"] = ["Typ:", "Type:", "Type :", "種類:"],
            ["Label_Action"] = ["Handlung:", "Action:", "Action :", "行動:"],
            ["Label_Duration"] = ["Dauer:", "Duration:", "Durée :", "効果時間:"],
            ["Checkbox_CritterPowerCount"] = ["Zählt zum maximalen Critter-Kraftlevel dazu", "Counts towards Critter Power limit", "Compte dans la limite des points de pouvoirs de créature", "Counts towards Critter Power limit"],
            ["String_ActionFree"] = ["Freie", "Free", "Gratuite", "簡易"],
            ["String_ActionSimple"] = ["Einfache", "Simple", "Simple", "単純"],
            ["String_ActionComplex"] = ["Komplexe", "Complex", "Complexe", "複雑"],
            ["Label_ExportTo"] = ["Exportieren nach:", "Export to:", "Exporter vers:", "エクスポート先:"],
            ["String_OK"] = ["OK", "OK", "OK", "OK"]
        };

    [Theory]
    [InlineData("de", 0)]
    [InlineData("en-us", 1)]
    [InlineData("fr", 2)]
    [InlineData("jp", 3)]
    public void TargetTabKeysHaveExpectedActualXmlValues(string culture, int cultureIndex)
    {
        var xmlPath = Path.Combine(FindRepositoryRoot(), "bin", "lang", $"{culture}.xml");
        Assert.True(File.Exists(xmlPath), $"Language file {xmlPath} must exist.");

        var values = XDocument.Load(xmlPath)
            .Root?
            .Element("strings")?
            .Elements("string")
            .GroupBy(
                element => element.Element("key")?.Value ?? string.Empty,
                element => element.Element("text")?.Value ?? string.Empty,
                StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal)
            ?? throw new InvalidOperationException($"Invalid XML format in {xmlPath}");

        foreach (var (key, expected) in ExpectedValues)
        {
            Assert.True(values.ContainsKey(key), $"Key '{key}' must exist in culture '{culture}'.");
            Assert.Equal(expected[cultureIndex], values[key]);
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "bin", "lang")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root containing bin/lang could not be located.");
    }
}
