using System.Globalization;
using System.Xml.Linq;

namespace ChummerGen.Domain.Character.Cyberware;

public sealed record CyberwareOperationResult(bool Success, string? Error = null, CyberwareItem? Item = null)
{
    public static CyberwareOperationResult Ok(CyberwareItem? item = null) => new(true, null, item);
    public static CyberwareOperationResult Fail(string error) => new(false, error);
}

public sealed record CyberwareEssenceSummary(decimal Cyberware, decimal Bioware, decimal EssenceHole)
{
    public decimal Total => Cyberware + Bioware + EssenceHole;
}

public sealed partial class CyberwareManager
{
    private static readonly string[] LockedGradeCategoryPrefixes = ["Genetech:", "Symbiont", "Genetic Infusions"];

    private readonly CharacterSession _session;

    public CyberwareManager(CharacterSession session, CyberwareCatalog? catalog = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Catalog = catalog ?? CyberwareCatalog.Empty;
    }

    public CyberwareCatalog Catalog { get; }

    public IReadOnlyList<CyberwareItem> GetItems() =>
        _session.Xml.Root?.Element("cyberwares")?.Elements("cyberware").Select(e => new CyberwareItem(e)).ToList() ?? [];

    public IEnumerable<CyberwareItem> GetAllItems() => GetItems().SelectMany(i => i.SelfAndDescendants());

    public CyberwareItem? Find(string guid) =>
        GetAllItems().FirstOrDefault(i => string.Equals(i.Guid, guid, StringComparison.OrdinalIgnoreCase));

    public CyberwareGradeTable GradesFor(CyberwareItem item) => Catalog.GradesFor(item.SourceType);

    public int TotalCost() => GetItems().Sum(i => i.TotalCost(GradesFor(i)));

    // Legacy Character.CyberwareEssence/BiowareEssence/EssenceHole: only top-level pieces are summed; the smaller
    // of cyberware and bioware essence counts at half. Character improvement multipliers are not applied.
    public CyberwareEssenceSummary EssenceSummary(int decimals = 2)
    {
        decimal cyberware = 0, bioware = 0, hole = 0;
        foreach (var item in GetItems())
        {
            var essence = item.CalculatedEssence(GradesFor(item), decimals);
            if (item.IsEssenceHole)
                hole += essence;
            else if (item.SourceType == CyberwareSource.Bioware)
                bioware += essence;
            else
                cyberware += essence;
        }

        return cyberware > bioware
            ? new CyberwareEssenceSummary(cyberware, bioware / 2, hole)
            : new CyberwareEssenceSummary(cyberware / 2, bioware, hole);
    }

    public string? GetAddBlocker(CyberwareCatalogEntry entry)
    {
        foreach (var field in new[] { "name", "category", "ess", "capacity", "avail", "cost", "source", "page" })
        {
            if (entry.Node.Element(field) is null)
                return $"Im Katalogeintrag '{entry.Name}' fehlt das Pflichtfeld '{field}'.";
        }

        if (entry.Cost.StartsWith("Variable", StringComparison.Ordinal) && !CyberwareExpression.TryParseVariableCostRange(entry.Cost, out _, out _))
            return $"'{entry.Name}' hat ein ungültiges Format für variable Kosten: {entry.Cost}.";
        if (CyberwareWeaponFactory.GetBlocker(Catalog, entry.Node) is { } weaponBlocker)
            return $"'{entry.Name}': {weaponBlocker}";
        if (entry.Node.Element("bonus") is { HasElements: true } bonusNode
            && !IsSupportedBonus(bonusNode, 1))
            return DescribeUnsupportedBonus(entry.Name, bonusNode);
        return null;
    }

    private static bool IsSupportedBonus(XElement bonus, int rating)
    {
        if (GetEnableAttribute(bonus) is not null || HasRatingThresholdOffsetBonus(bonus))
            return true;
        if (TryGetMatrixInitiativePassValue(bonus, rating, out _) || TryGetSimpleBonuses(bonus, rating, out _))
            return true;
        if (TryGetMatrixInitiativePassAddValue(bonus, rating, out _))
            return true;
        if (HasBoneDensityBonusShape(bonus))
            return TryGetBoneDensityBonusValues(bonus, rating, out _, out _);
        if (HasOnlyArmorAndDamageResistance(bonus))
            return TryGetArmorAndDamageResistanceBonusValues(bonus, rating, out _, out _);
        return TryGetArmorBonusValues(bonus, rating, out _)
            || TryGetDamageResistanceBonusValue(bonus, rating, out _);
    }

    public CyberwareOperationResult Add(
        CyberwareSource source,
        string name,
        string gradeName,
        int rating,
        string? parentGuid = null,
        int? variableCost = null,
        string? side = null)
    {
        var root = _session.Xml.Root;
        if (root is null)
            return CyberwareOperationResult.Fail("Das Dokument enthält kein Wurzelelement.");

        CyberwareItem? parent = null;
        if (!string.IsNullOrWhiteSpace(parentGuid))
        {
            parent = Find(parentGuid);
            if (parent is null)
                return CyberwareOperationResult.Fail("Das übergeordnete Implantat wurde nicht gefunden.");
            source = parent.SourceType;
        }

        var entry = Catalog.Find(source, name);
        if (entry is null)
            return CyberwareOperationResult.Fail($"'{name}' ist nicht im Katalog vorhanden.");

        var blocker = GetAddBlocker(entry);
        if (blocker is not null)
            return CyberwareOperationResult.Fail(blocker);

        int? chosenCost = null;
        if (entry.Cost.StartsWith("Variable", StringComparison.Ordinal))
        {
            if (!CyberwareExpression.TryParseVariableCostRange(entry.Cost, out var minCost, out var maxCost))
                return CyberwareOperationResult.Fail($"'{entry.Name}' hat ein ungültiges Format für variable Kosten: {entry.Cost}.");
            if (variableCost is null)
                return CyberwareOperationResult.Fail($"'{entry.Name}' erfordert die Angabe variabler Kosten zwischen {minCost} und {maxCost}.");
            if (variableCost.Value < minCost || variableCost.Value > maxCost)
                return CyberwareOperationResult.Fail($"Variable Kosten {variableCost.Value} liegen außerhalb des zulässigen Bereichs von {minCost} bis {maxCost}.");
            chosenCost = variableCost.Value;
        }

        var grades = Catalog.GradesFor(source);
        var grade = ResolveGrade(entry, gradeName, parent);
        if (!grades.Contains(grade))
            return CyberwareOperationResult.Fail($"Grad '{grade}' ist nicht im Katalog vorhanden.");

        var node = entry.Node;
        var hasRating = node.Element("rating") is not null;
        var maxRating = hasRating && int.TryParse(node.Element("rating")!.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var max) ? max : 0;
        var minRating = int.TryParse(node.Element("minrating")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var min)
            ? min
            : maxRating > 0 ? 1 : 0;
        if (hasRating)
        {
            if (rating < minRating || rating > maxRating)
                return CyberwareOperationResult.Fail($"Stufe {rating} liegt außerhalb von {minRating}–{maxRating}.");
        }
        else
        {
            rating = 0;
        }

        var requiresSide = RequiresSide(entry);
        var requiresText = RequiresText(entry);
        if (requiresSide && string.IsNullOrWhiteSpace(side))
            return CyberwareOperationResult.Fail($"'{entry.Name}' erfordert die Auswahl einer Seite (links/rechts).");
        if (requiresText && string.IsNullOrWhiteSpace(side))
            return CyberwareOperationResult.Fail($"'{entry.Name}' erfordert die Eingabe eines Textes.");

        var item = BuildElement(entry, grade, rating, minRating, maxRating, source, chosenCost);
        if (requiresSide || requiresText)
            item.Element("location")!.Value = side!.Trim();
        if (parent is not null)
        {
            var parentError = ValidatePlugin(parent, entry, new CyberwareItem(item));
            if (parentError is not null)
                return CyberwareOperationResult.Fail(parentError);

            var children = parent.Element.Element("children");
            if (children is null)
            {
                children = new XElement("children");
                parent.Element.Add(children);
            }

            AddLinkedWeapons(item, entry);
            if (GetEnableAttribute(item.Element("bonus")) is { } parentAttribute)
                ApplyEnableBonus(item, parentAttribute);
            else if (HasRatingThresholdOffsetBonus(item.Element("bonus")))
                ApplyRatingThresholdOffsetBonus(item);
            else if (TryGetMatrixInitiativePassValue(item.Element("bonus"), rating, out var parentMatrixInitiativePass))
                ApplyMatrixInitiativePass(item, parentMatrixInitiativePass);
            else if (TryGetMatrixInitiativePassAddValue(item.Element("bonus"), rating, out var parentMatrixPass))
                ApplyMatrixInitiativePassAdd(item, parentMatrixPass);
            else if (TryGetArmorAndDamageResistanceBonusValues(item.Element("bonus"), rating, out var parentArmorValues, out var parentResistance))
            {
                ApplyArmorBonus(item, parentArmorValues);
                ApplyDamageResistanceBonus(item, parentResistance);
            }
            else if (TryGetArmorBonusValues(item.Element("bonus"), rating, out var parentArmor))
                ApplyArmorBonus(item, parentArmor);
            else if (TryGetBoneDensityBonusValues(item.Element("bonus"), rating, out var parentBoneDamage, out var parentPhysical))
            {
                ApplyDamageResistanceBonus(item, parentBoneDamage);
                ApplyBoneDensityUnarmedImprovements(item, parentPhysical);
            }
            else if (TryGetDamageResistanceBonusValue(item.Element("bonus"), rating, out var parentDamageResistance))
                ApplyDamageResistanceBonus(item, parentDamageResistance);
            else if (TryGetSimpleBonuses(item.Element("bonus"), rating, out var parentSimple))
                ApplySimpleBonuses(item, parentSimple);
            children.Add(item);
            return CyberwareOperationResult.Ok(Find(item.Element("guid")!.Value));
        }

        var container = root.Element("cyberwares");
        if (container is null)
        {
            container = new XElement("cyberwares");
            root.Add(container);
        }

        AddLinkedWeapons(item, entry);
        if (GetEnableAttribute(item.Element("bonus")) is { } attribute)
            ApplyEnableBonus(item, attribute);
        else if (HasRatingThresholdOffsetBonus(item.Element("bonus")))
            ApplyRatingThresholdOffsetBonus(item);
        else if (TryGetMatrixInitiativePassValue(item.Element("bonus"), rating, out var matrixInitiativePass))
            ApplyMatrixInitiativePass(item, matrixInitiativePass);
        else if (TryGetMatrixInitiativePassAddValue(item.Element("bonus"), rating, out var matrixPass))
            ApplyMatrixInitiativePassAdd(item, matrixPass);
        else if (TryGetArmorAndDamageResistanceBonusValues(item.Element("bonus"), rating, out var armorValues, out var damageResistance))
        {
            ApplyArmorBonus(item, armorValues);
            ApplyDamageResistanceBonus(item, damageResistance);
        }
        else if (TryGetArmorBonusValues(item.Element("bonus"), rating, out var armor))
            ApplyArmorBonus(item, armor);
        else if (TryGetBoneDensityBonusValues(item.Element("bonus"), rating, out var boneDamage, out var physical))
        {
            ApplyDamageResistanceBonus(item, boneDamage);
            ApplyBoneDensityUnarmedImprovements(item, physical);
        }
        else if (TryGetDamageResistanceBonusValue(item.Element("bonus"), rating, out var resistance))
            ApplyDamageResistanceBonus(item, resistance);
        else if (TryGetSimpleBonuses(item.Element("bonus"), rating, out var simple))
        {
            if (requiresText)
                ApplySimpleBonus(item, new SimpleBonus("Text", item.Element("bonus")?.Attribute("unique")?.Value ?? string.Empty, 0, side!.Trim()));
            ApplySimpleBonuses(item, simple);
        }
        container.Add(item);
        return CyberwareOperationResult.Ok(Find(item.Element("guid")!.Value));
    }

    // Rating edits are refused unless every linked improvement can be safely recalculated.
    public CyberwareOperationResult SetRating(CyberwareItem item, int rating)
    {
        if (item.MaxRating > 0 && (rating < item.MinRating || rating > item.MaxRating))
            return CyberwareOperationResult.Fail($"Stufe {rating} liegt außerhalb von {item.MinRating}–{item.MaxRating}.");
        if (item.MaxRating <= 0 && rating != item.Rating)
            return CyberwareOperationResult.Fail("Dieses Implantat hat keine Stufe.");
        if (rating != item.Rating && HasLinkedImprovements(item) && !RatingChangeKeepsImprovements(item))
            return CyberwareOperationResult.Fail("Die Stufe kann nicht geändert werden: verknüpfte Verbesserungen werden noch nicht neu berechnet.");

        item.Rating = rating;
        UpdateRatingThresholdOffsetImprovement(item);
        UpdateArmorImprovements(item);
        UpdateDamageResistanceImprovement(item);
        UpdateBoneDensityUnarmedImprovements(item);
        UpdateMatrixInitiativePass(item);
        UpdateMatrixInitiativePassAdd(item);
        UpdateSimpleBonus(item);
        return CyberwareOperationResult.Ok(item);
    }

    public CyberwareOperationResult SetGrade(CyberwareItem item, string gradeName)
    {
        var grades = GradesFor(item);
        if (!grades.Contains(gradeName))
            return CyberwareOperationResult.Fail($"Grad '{gradeName}' ist nicht im Katalog vorhanden.");
        if (item.Parent is not null)
            return CyberwareOperationResult.Fail("Der Grad eines Plugins folgt dem übergeordneten Implantat.");
        if (!string.IsNullOrEmpty(item.ForceGrade) && item.ForceGrade != gradeName)
            return CyberwareOperationResult.Fail($"Der Grad ist auf '{item.ForceGrade}' festgelegt.");
        if (LockedGradeCategoryPrefixes.Any(p => item.Category.StartsWith(p, StringComparison.Ordinal)) && gradeName != "Standard")
            return CyberwareOperationResult.Fail("Für diese Kategorie ist nur der Grad 'Standard' erlaubt.");

        item.GradeName = gradeName;
        foreach (var child in item.Children)
            child.GradeName = gradeName;
        return CyberwareOperationResult.Ok(item);
    }

    public CyberwareOperationResult Remove(string guid)
    {
        var item = Find(guid);
        if (item is null)
            return CyberwareOperationResult.Fail("Das Implantat wurde nicht gefunden.");
        var all = item.SelfAndDescendants().ToList();
        var guids = all.Select(i => i.Guid).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Legacy removes the weapon of the piece and of its direct plugins by the stored weapon GUID only.
        var weaponGuids = item.Children.Prepend(item)
            .Select(i => i.Element.Element("weaponguid")?.Value)
            .Where(g => !string.IsNullOrEmpty(g))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        item.Element.Remove();
        _session.Xml.Root?.Element("weapons")?.Elements("weapon")
            .Where(w => weaponGuids.Contains(w.Element("guid")?.Value ?? string.Empty))
            .ToList()
            .ForEach(w => w.Remove());

        var improvements = _session.Xml.Root?.Element("improvements");
        var removedImprovements = improvements?.Elements("improvement")
            .Where(i => guids.Contains(i.Element("sourcename")?.Value ?? string.Empty)
                && (i.Element("improvementsource")?.Value is "Cyberware" or "Bioware"))
            .ToList() ?? [];
        removedImprovements.ForEach(i => i.Remove());
        RecalculateEnabledFlags(removedImprovements);
        return CyberwareOperationResult.Ok();
    }

    // Legacy Cyberware.Create: every <addweapon> creates a weapon; only the last weapon's GUID is kept on the piece.
    private void AddLinkedWeapons(XElement cyberware, CyberwareCatalogEntry entry)
    {
        var created = entry.Node.Elements("addweapon")
            .Select(a => CyberwareWeaponFactory.Build(Catalog.FindCyberwareWeapon(a.Value)!, Catalog))
            .ToList();
        if (created.Count == 0)
            return;

        var root = _session.Xml.Root!;
        var weapons = root.Element("weapons");
        if (weapons is null)
        {
            weapons = new XElement("weapons");
            root.Add(weapons);
        }

        weapons.Add(created);
        cyberware.Element("improvementsource")!.AddAfterSelf(new XElement("weaponguid", created[^1].Element("guid")!.Value));
    }

    public bool HasLinkedImprovements(CyberwareItem item)
    {
        if (item.Bonus is not null)
            return true;
        var guids = item.SelfAndDescendants().Select(i => i.Guid).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return _session.Xml.Root?.Element("improvements")?.Elements("improvement")
            .Any(i => guids.Contains(i.Element("sourcename")?.Value ?? string.Empty)) ?? false;
    }

    private static string ResolveGrade(CyberwareCatalogEntry entry, string requested, CyberwareItem? parent)
    {
        if (parent is not null)
            return parent.GradeName;
        if (!string.IsNullOrEmpty(entry.ForceGrade))
            return entry.ForceGrade;
        if (LockedGradeCategoryPrefixes.Any(p => entry.Category.StartsWith(p, StringComparison.Ordinal)))
            return "Standard";
        return string.IsNullOrWhiteSpace(requested) ? "Standard" : requested;
    }

    private string? ValidatePlugin(CyberwareItem parent, CyberwareCatalogEntry entry, CyberwareItem candidate)
    {
        if (!entry.Capacity.Contains('['))
            return $"'{entry.Name}' ist kein Plugin (Kapazität ohne Klammern).";
        var allowed = parent.Subsystems.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!allowed.Contains(entry.Category, StringComparer.Ordinal))
            return $"'{parent.DisplayNameShort}' akzeptiert keine Plugins der Kategorie '{entry.Category}'.";
        if (_session.Settings.EnforceCapacity && parent.HasCapacity && candidate.PluginCapacity > parent.CapacityRemaining)
            return $"Nicht genug Kapazität in '{parent.DisplayNameShort}' (frei: {parent.CapacityRemaining}, benötigt: {candidate.PluginCapacity}).";
        return null;
    }

    private static XElement BuildElement(
        CyberwareCatalogEntry entry,
        string grade,
        int rating,
        int minRating,
        int maxRating,
        CyberwareSource source,
        int? chosenCost = null)
    {
        var node = entry.Node;
        string Text(string name) => node.Element(name)?.Value ?? string.Empty;
        var element = new XElement("cyberware",
            new XElement("guid", System.Guid.NewGuid().ToString()),
            new XElement("name", entry.Name),
            new XElement("category", entry.Category),
            new XElement("limbslot", Text("limbslot")),
            new XElement("ess", entry.Ess),
            new XElement("capacity", entry.Capacity),
            new XElement("avail", entry.Avail),
            new XElement("cost", chosenCost?.ToString(CultureInfo.InvariantCulture) ?? entry.Cost),
            new XElement("source", entry.SourceBook),
            new XElement("page", entry.Page),
            new XElement("rating", rating.ToString(CultureInfo.InvariantCulture)),
            new XElement("minrating", minRating.ToString(CultureInfo.InvariantCulture)),
            new XElement("maxrating", maxRating.ToString(CultureInfo.InvariantCulture)),
            new XElement("subsystems", node.Element("subsystems") is { } subs && subs.HasElements
                ? string.Join(",", subs.Elements("subsystem").Select(s => s.Value.Trim()))
                : Text("subsystems")),
            new XElement("grade", grade),
            new XElement("location"),
            new XElement("suite", "False"),
            new XElement("essdiscount", "0"),
            new XElement("forcegrade", entry.ForceGrade),
            node.Element("bonus") is { } bonus
                && (GetEnableAttribute(bonus) is not null
                    || HasRatingThresholdOffsetBonus(bonus)
                    || TryGetMatrixInitiativePassValue(bonus, rating, out _)
                    || TryGetSimpleBonuses(bonus, rating, out _)
                    || TryGetMatrixInitiativePassAddValue(bonus, rating, out _)
                    || TryGetArmorBonusValues(bonus, rating, out _)
                    || TryGetDamageResistanceBonusValue(bonus, rating, out _)
                    || TryGetArmorAndDamageResistanceBonusValues(bonus, rating, out _, out _)
                    || TryGetBoneDensityBonusValues(bonus, rating, out _, out _))
                ? new XElement(bonus)
                : new XElement("bonus"));
        if (node.Element("allowgear") is { } allowGear)
            element.Add(new XElement(allowGear));
        element.Add(
            new XElement("improvementsource", source.ToString()),
            new XElement("children"),
            new XElement("notes"),
            new XElement("discountedcost", "False"));
        return element;
    }

    private static string DescribeUnsupportedBonus(string itemName, XElement bonusNode)
    {
        var tags = bonusNode.Elements().Select(e => e.Name.LocalName).Distinct().ToList();
        var tagList = tags.Count > 0 ? string.Join(", ", tags) : "unbekannt";
        return $"'{itemName}' hat Boni ({tagList}); das Erzeugen der Verbesserungen ist noch nicht portiert.";
    }
}