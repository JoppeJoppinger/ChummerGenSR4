using System.Globalization;
using System.Xml.Linq;
using ChummerGen.Domain.Chum;

namespace ChummerGen.Domain.Character.Cyberware;

public sealed record CyberwareSuiteItem(string Name, int Rating, IReadOnlyList<CyberwareSuiteItem> Children, string? ForcedSide = null);

// Blocker is non-null when the suite uses semantics that are not ported; such a suite is never applied.
public sealed record CyberwareSuite(
    CyberwareSource Source,
    string Name,
    string GradeText,
    string GradeName,
    IReadOnlyList<CyberwareSuiteItem> Items,
    string? Blocker);

public sealed partial class CyberwareManager
{
    private static readonly string[] SuiteUnsupportedCatalogElements = ["cyberwares", "biowares", "gears", "selecttext", "pilot"];

    public IReadOnlyList<CyberwareSuite> ListSuites(CyberwareSource source) =>
        Catalog.SuiteNodes(source).Select(n => ParseSuite(source, n)).ToList();

    // Legacy frmSelectCyberwareSuite: the first suite with a matching name wins.
    public CyberwareSuite? FindSuite(CyberwareSource source, string name) =>
        Catalog.SuiteNodes(source).Where(n => n.Element("name")?.Value == name).Select(n => ParseSuite(source, n)).FirstOrDefault();

    // Legacy frmSelectCyberwareSuite.CyberwareGradeName; unknown values fall back to Standard.
    public static string ResolveSuiteGradeName(string suiteGrade) => suiteGrade switch
    {
        "Alphaware" => "Alphaware",
        "Betaware" => "Betaware",
        "Deltaware" => "Deltaware",
        "Standard (Second-Hand)" or "StandardSecondHand" => "Standard (Second-Hand)",
        "Alphaware (Second-Hand)" or "AlphawareSecondHand" => "Alphaware (Second-Hand)",
        "StandardAdapsin" => "Standard (Adapsin)",
        "AlphawareAdapsin" => "Alphaware (Adapsin)",
        "BetawareAdapsin" => "Betaware (Adapsin)",
        "DeltawareAdapsin" => "Deltaware (Adapsin)",
        "Standard (Second-Hand) (Adapsin)" or "StandardSecondHandAdapsin" => "Standard (Second-Hand) (Adapsin)",
        "Alphaware (Second-Hand) (Adapsin)" or "AlphawareSecondHandAdapsin" => "Alphaware (Second-Hand) (Adapsin)",
        _ => "Standard"
    };

    public int CountSuiteSides(CyberwareSuite suite) => CountSuiteSides(suite.Source, suite.Items);

    private int CountSuiteSides(CyberwareSource source, IReadOnlyList<CyberwareSuiteItem> items) =>
        items.Sum(i => (i.ForcedSide is null && Catalog.Find(source, i.Name) is { } entry && RequiresSide(entry) ? 1 : 0) + CountSuiteSides(source, i.Children));

    // All-or-nothing: the suite is built on a clone of the document and only appended to the real one when every piece succeeded.
    // Sides are consumed in suite order by every piece that needs one.
    public CyberwareOperationResult ApplySuite(CyberwareSource source, string suiteName, IReadOnlyList<string>? sides = null)
    {
        var root = _session.Xml.Root;
        if (root is null)
            return CyberwareOperationResult.Fail("Das Dokument enthält kein Wurzelelement.");
        var suite = FindSuite(source, suiteName);
        if (suite is null)
            return CyberwareOperationResult.Fail($"Die Suite '{suiteName}' ist nicht im Katalog vorhanden.");
        if (suite.Blocker is not null)
            return CyberwareOperationResult.Fail(suite.Blocker);
        var needed = CountSuiteSides(suite);
        if ((sides?.Count ?? 0) != needed)
            return CyberwareOperationResult.Fail($"Suite '{suite.Name}': Es müssen {needed} Seite(n) gewählt werden.");
        var sideQueue = new Queue<string>(sides ?? []);

        var staged = new CharacterSession(new ChumDocument(new XDocument(_session.Xml)), _session.Settings);
        var stagedManager = new CyberwareManager(staged, Catalog) { _suiteMode = true };
        var stagedRoot = staged.Xml.Root!;
        var itemsBefore = stagedRoot.Element("cyberwares")?.Elements("cyberware").Count() ?? 0;
        var weaponsBefore = stagedRoot.Element("weapons")?.Elements("weapon").Count() ?? 0;

        foreach (var item in suite.Items)
        {
            var error = stagedManager.AddSuiteItem(suite, source, item, null, sideQueue);
            if (error is not null)
                return CyberwareOperationResult.Fail($"Suite '{suite.Name}': {error}");
        }

        var newItems = stagedRoot.Element("cyberwares")!.Elements("cyberware").Skip(itemsBefore).ToList();
        var newWeapons = stagedRoot.Element("weapons")?.Elements("weapon").Skip(weaponsBefore).ToList() ?? [];

        var container = root.Element("cyberwares");
        if (container is null)
        {
            container = new XElement("cyberwares");
            root.Add(container);
        }

        container.Add(newItems.Select(e => new XElement(e)));
        if (newWeapons.Count > 0)
        {
            var weapons = root.Element("weapons");
            if (weapons is null)
            {
                weapons = new XElement("weapons");
                root.Add(weapons);
            }

            weapons.Add(newWeapons.Select(e => new XElement(e)));
        }

        return CyberwareOperationResult.Ok();
    }

    private string? AddSuiteItem(CyberwareSuite suite, CyberwareSource source, CyberwareSuiteItem item, string? parentGuid, Queue<string> sides)
    {
        var needsSide = Catalog.Find(source, item.Name) is { } sideEntry && RequiresSide(sideEntry);
        var side = item.ForcedSide ?? (needsSide ? sides.Dequeue() : null);
        var result = Add(source, item.Name, suite.GradeName, item.Rating, parentGuid, null, side);
        if (!result.Success)
            return $"'{item.Name}': {result.Error}";
        // Legacy marks every suite piece, including plugins, as Suite (0.9 cost, -0.1 essence multiplier).
        result.Item!.Element.Element("suite")!.Value = "True";
        foreach (var child in item.Children)
        {
            var error = AddSuiteItem(suite, source, child, result.Item.Guid, sides);
            if (error is not null)
                return error;
        }

        return null;
    }

    private CyberwareSuite ParseSuite(CyberwareSource source, XElement node)
    {
        var name = node.Element("name")?.Value ?? string.Empty;
        var gradeText = node.Element("grade")?.Value ?? string.Empty;
        var gradeName = ResolveSuiteGradeName(gradeText);
        var itemTag = source == CyberwareSource.Bioware ? "bioware" : "cyberware";
        var containerTag = itemTag + "s";
        var items = new List<CyberwareSuiteItem>();
        var blocker = node.Element("grade") is null ? $"Suite '{name}' hat keinen Grad." : null;
        blocker ??= ValidateSuiteNode(node, name, ["name", "grade", containerTag], containerTag, itemTag, source, gradeName, items);
        if (blocker is null && !Catalog.GradesFor(source).Contains(gradeName))
            blocker = $"Suite '{name}': Grad '{gradeName}' ist nicht im Katalog vorhanden.";
        return new CyberwareSuite(source, name, gradeText, gradeName, items, blocker);
    }

    private string? ValidateSuiteNode(
        XElement node,
        string suiteName,
        string[] allowed,
        string containerTag,
        string itemTag,
        CyberwareSource source,
        string gradeName,
        List<CyberwareSuiteItem> into)
    {
        if (node.Elements().FirstOrDefault(e => !allowed.Contains(e.Name.LocalName)) is { } unknown)
            return $"Suite '{suiteName}': das Element '{unknown.Name.LocalName}' wird nicht unterstützt.";

        string? firstBlocker = null;
        foreach (var itemNode in node.Elements(containerTag).SelectMany(c => c.Elements(itemTag)))
        {
            var itemName = itemNode.Element("name")?.Value;
            if (string.IsNullOrWhiteSpace(itemName))
            {
                firstBlocker ??= $"Suite '{suiteName}': ein Eintrag hat keinen Namen.";
                continue;
            }

            var rating = 0;
            string? error;
            var nameAttributes = itemNode.Element("name")!.Attributes().ToList();
            if (nameAttributes.Count > 0 && !(nameAttributes.Count == 1 && nameAttributes[0].Name.LocalName == "select"
                && (nameAttributes[0].Value is "Left" or "Right")
                && Catalog.Find(source, itemName) is { } sideEntry && RequiresSide(sideEntry)))
                error = $"'{itemName}' verwendet ein nicht unterstütztes Auswahlattribut (nur select=Left/Right bei seitenabhängigen Teilen).";
            else if (itemNode.Elements().FirstOrDefault(e => e.Name.LocalName is not ("name" or "rating") && e.Name.LocalName != containerTag) is { } extra)
                error = $"'{itemName}' enthält das nicht unterstützte Element '{extra.Name.LocalName}' (z. B. Grad-Überschreibung).";
            else if (itemNode.Element("rating") is { } ratingNode
                && !int.TryParse(ratingNode.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out rating))
                error = $"'{itemName}' hat eine ungültige Stufe '{ratingNode.Value}'.";
            else
                error = GetSuiteItemBlocker(source, itemName, gradeName);

            if (error is not null)
            {
                firstBlocker ??= $"Suite '{suiteName}': {error}";
                continue;
            }

            var children = new List<CyberwareSuiteItem>();
            firstBlocker ??= ValidateSuiteNode(itemNode, suiteName, ["name", "rating", containerTag], containerTag, itemTag, source, gradeName, children);
            into.Add(new CyberwareSuiteItem(itemName, rating, children, nameAttributes.Count == 1 ? nameAttributes[0].Value : null));
        }

        return firstBlocker;
    }

    private string? GetSuiteItemBlocker(CyberwareSource source, string name, string gradeName)
    {
        var entry = Catalog.Find(source, name);
        if (entry is null)
            return $"'{name}' ist nicht im Katalog vorhanden.";
        if (GetAddBlocker(entry) is { } blocker)
            return blocker;
        if (RequiresText(entry))
            return $"'{name}' erfordert eine Texteingabe; das wird für Suiten nicht unterstützt.";
        if (RequiresAttributeSelection(entry) || RequiresSkillSelection(entry) || RequiresSkillGroupSelection(entry))
            return $"'{name}' erfordert eine Attribut- oder Fertigkeitsauswahl; das wird für Suiten nicht unterstützt.";
        if (SuiteUnsupportedCatalogElements.FirstOrDefault(t => entry.Node.Element(t) is not null) is { } tag)
            return $"'{name}' enthält '{tag}' (Standard-Plugins/Auswahltext); das wird für Suiten nicht unterstützt.";
        if (entry.Cost.StartsWith("Variable", StringComparison.Ordinal))
            return $"'{name}' hat variable Kosten, die die Suite nicht festlegt.";
        if (!string.IsNullOrEmpty(entry.ForceGrade) && entry.ForceGrade != gradeName)
            return $"'{name}' erzwingt den Grad '{entry.ForceGrade}', die Suite hat '{gradeName}'.";
        if (LockedGradeCategoryPrefixes.Any(p => entry.Category.StartsWith(p, StringComparison.Ordinal)) && gradeName != "Standard")
            return $"'{name}' erlaubt nur den Grad 'Standard', die Suite hat '{gradeName}'.";
        return null;
    }
}
