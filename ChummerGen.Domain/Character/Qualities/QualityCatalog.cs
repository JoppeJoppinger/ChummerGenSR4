using System.Globalization;
using System.Xml.Linq;
using ChummerGen.Domain.Abstractions;
using ChummerGen.Domain.Character.AttributesSkills;
using ChummerGen.Domain.Character.Cyberware;

namespace ChummerGen.Domain.Character.Qualities;

public sealed record QualityCatalogEntry(XElement Element)
{
    public string Name => Element.Element("name")?.Value.Trim() ?? string.Empty;
    public string Category => Element.Element("category")?.Value.Trim() ?? string.Empty;
    public int Bp => int.TryParse(Element.Element("bp")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
    public bool IsNegative => string.Equals(Category, "Negative", StringComparison.OrdinalIgnoreCase);
}

public sealed class QualityCatalog
{
    public const string CatalogName = "qualities.xml";

    private readonly Dictionary<string, QualityCatalogEntry> _entries;

    public static QualityCatalog Empty { get; } = new(null);

    private readonly CyberwareCatalog _weapons;

    private QualityCatalog(string? xml, string? weaponsXml = null)
    {
        _weapons = CyberwareCatalog.FromXml(null, null, weaponsXml);
        _entries = new Dictionary<string, QualityCatalogEntry>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(xml))
            return;
        var qualities = XDocument.Parse(xml).Root?.Element("qualities")?.Elements("quality") ?? [];
        foreach (var e in qualities)
        {
            var entry = new QualityCatalogEntry(e);
            if (entry.Name.Length > 0)
                _entries.TryAdd(entry.Name, entry);
        }
    }

    public static QualityCatalog FromXml(string? xml, string? weaponsXml = null) => new(xml, weaponsXml);

    public CyberwareCatalog WeaponCatalog => _weapons;

    public static async Task<QualityCatalog> LoadAsync(IRuleCatalog ruleCatalog, string culture = "en-US", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ruleCatalog);
        try
        {
            var doc = await ruleCatalog.GetAsync(CatalogName, culture, cancellationToken).ConfigureAwait(false);
            string? weapons = null;
            try
            {
                weapons = (await ruleCatalog.GetAsync(CyberwareCatalog.WeaponCatalogName, culture, cancellationToken).ConfigureAwait(false))?.Content;
            }
            catch (FileNotFoundException)
            {
            }

            return new QualityCatalog(doc?.Content, weapons);
        }
        catch (FileNotFoundException)
        {
            return Empty;
        }
    }

    public IEnumerable<QualityCatalogEntry> Entries => _entries.Values;

    public QualityCatalogEntry? Find(string name) =>
        _entries.TryGetValue(name ?? string.Empty, out var e) ? e : null;

    private static readonly HashSet<string> SupportedRequired = ["quality", "metatype", "metatypecategory", "metavariant"];
    private static readonly HashSet<string> SupportedForbidden = ["quality", "metatype", "metatypecategory", "metavariant", "metagenetic"];

    // Liefert null, wenn die Qualität hinzugefügt werden darf; nicht portierte Formen bleiben fail-closed.
    public string? GetAddBlocker(CharacterSession session, QualityCatalogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(session);
        var el = entry.Element;
        if (GetSupportBlocker(entry, 0) is { } unsupported)
            return unsupported;

        var root = session.Xml.Root;
        var owned = root?.Element("qualities")?.Elements("quality")
            .Select(q => q.Element("name")?.Value.Trim() ?? string.Empty).ToList() ?? [];
        var multiple = string.Equals(el.Element("limit")?.Value.Trim(), "no", StringComparison.OrdinalIgnoreCase);
        if (!multiple && owned.Contains(entry.Name, StringComparer.Ordinal))
            return "Die Qualität ist bereits vorhanden.";

        string Meta(string tag) => root?.Element(tag)?.Value.Trim() ?? string.Empty;
        bool Has(XElement c) => c.Name.LocalName switch
        {
            "quality" => owned.Contains(c.Value.Trim(), StringComparer.Ordinal),
            "metatype" => c.Value.Trim() == Meta("metatype"),
            "metatypecategory" => c.Value.Trim() == Meta("metatypecategory"),
            "metavariant" => c.Value.Trim() == Meta("metavariant"),
            "metagenetic" => owned.Any(n => Find(n)?.Element.Element("metagenetic")?.Value.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase) == true),
            _ => false
        };

        if (el.Element("forbidden") is { } forbidden)
        {
            if (forbidden.Elements().Any(g => g.Name.LocalName != "oneof" || g.Elements().Any(c => !SupportedForbidden.Contains(c.Name.LocalName))))
                return "Verbotsbedingung wird noch nicht unterstützt.";
            if (forbidden.Elements("oneof").Any(g => g.Elements().Any(Has)))
                return "Die Qualität ist wegen einer Verbotsbedingung nicht erlaubt.";
        }

        if (el.Element("required") is { } required)
        {
            if (required.Elements().Any(g => g.Name.LocalName is not ("oneof" or "allof") || g.Elements().Any(c => !SupportedRequired.Contains(c.Name.LocalName))))
                return "Voraussetzung wird noch nicht unterstützt.";
            if (required.Elements("oneof").Any(g => !g.Elements().Any(Has))
                || required.Elements("allof").Any(g => !g.Elements().All(Has)))
                return "Voraussetzungen der Qualität sind nicht erfüllt.";
        }

        return null;
    }

    // Nicht portierte Formen (Bonus, powers, addweapon, addqualities) bleiben fail-closed.
    public string? GetSupportBlocker(QualityCatalogEntry entry, int depth)
    {
        var el = entry.Element;
        if (el.Element("bonus") is { HasElements: true } bonus)
        {
            var selection = bonus.Element("selectskill");
            var attributeSelection = bonus.Element("selectattribute");
            if (selection is not null || attributeSelection is not null)
            {
                if (selection is not null && attributeSelection is not null)
                    return "Kombinierte Fertigkeits- und Attributauswahl wird noch nicht unterstützt.";
                var selectedNode = selection ?? attributeSelection!;
                var remainingBonus = new XElement("bonus", bonus.Attributes(), bonus.Elements().Where(e => e != selectedNode));
                var invalidSelection = selection is not null
                    ? selection.Attributes().Any(a => a.Name.LocalName != "skillgroup")
                        || selection.Elements().Any(e => e.Name.LocalName is not ("val" or "max" or "applytorating"))
                        || selection.Elements().Any(e => e.HasAttributes || e.HasElements)
                        || selection.Elements("val").Count() + selection.Elements("max").Count() != 1
                        || selection.Elements("applytorating").Any(e => e.Value is not ("yes" or "no"))
                        || selection.Elements().Any(e => e.Name.LocalName != "applytorating" && !CyberwareManager.TryEvaluateExternalSelectionValue(e.Value, out _))
                        || !string.IsNullOrEmpty(selection.Attribute("skillgroup")?.Value) && !selection.Attribute("skillgroup")!.Value.All(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '\'')
                    : attributeSelection!.Attributes().Any()
                        || attributeSelection.Elements().Any(e => e.Name.LocalName is not ("attribute" or "excludeattribute" or "min" or "max"))
                        || attributeSelection.Elements().Any(e => e.HasAttributes || e.HasElements)
                        || attributeSelection.Elements("min").Count() > 1 || attributeSelection.Elements("max").Count() > 1
                        || attributeSelection.Elements("attribute").Any(e => !KnownAttributes.Contains(e.Value, StringComparer.Ordinal))
                        || attributeSelection.Elements("excludeattribute").Any(e => !KnownAttributes.Contains(e.Value, StringComparer.Ordinal))
                        || attributeSelection.Elements().Where(e => e.Name.LocalName is "min" or "max")
                            .Any(e => !CyberwareManager.TryEvaluateExternalSelectionValue(e.Value, out _))
                        || attributeSelection.Elements("min").Count() + attributeSelection.Elements("max").Count() == 0;
                if (invalidSelection
                    || remainingBonus.HasElements && !CyberwareManager.TryParseExternalBonus(remainingBonus, out _))
                    return "Diese Fertigkeits- oder Attributauswahl wird noch nicht unterstützt.";
            }
            else if (!CyberwareManager.TryParseExternalBonus(bonus, out _))
                return "Qualität mit diesem Bonus wird noch nicht unterstützt.";
        }
        if (el.Element("powers") is not null)
            return "Qualität mit Zusatzelementen wird noch nicht unterstützt.";
        if (CyberwareWeaponFactory.GetBlocker(_weapons, el, "Quality") is { } weaponBlocker)
            return weaponBlocker;
        if (el.Element("addqualities") is { } add)
        {
            if (depth > 3 || add.Elements().Any(a => a.Name.LocalName != "addquality" || a.HasAttributes || a.HasElements))
                return "Qualität mit Zusatzqualitäten wird noch nicht unterstützt.";
            foreach (var a in add.Elements("addquality"))
            {
                if (Find(a.Value.Trim()) is not { } target)
                    return "Zusatzqualität fehlt im Katalog.";
                if (GetSupportBlocker(target, depth + 1) is { } nested)
                    return nested;
                if (RequiresText(target) || RequiresSkillSelection(target) || RequiresAttributeSelection(target))
                    return "Zusatzqualität mit Eingabeauswahl wird noch nicht unterstützt.";
            }
        }

        return null;
    }

    public XElement CreateElement(QualityCatalogEntry entry, string source = "Selected")
    {
        var el = entry.Element;
        var contribute = !string.Equals(el.Element("contributetolimit")?.Value.Trim(), "no", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(el.Element("contributetolimit")?.Value.Trim(), "False", StringComparison.OrdinalIgnoreCase);
        var result = new XElement("quality",
            new XElement("guid", Guid.NewGuid().ToString()),
            new XElement("name", entry.Name),
            new XElement("extra", string.Empty),
            new XElement("bp", entry.Bp.ToString(CultureInfo.InvariantCulture)),
            new XElement("contributetolimit", contribute ? "True" : "False"),
            new XElement("print", "True"),
            new XElement("qualitytype", entry.IsNegative ? "Negative" : "Positive"),
            new XElement("qualitysource", source));
        if (el.Element("mutant") is { } mutant && mutant.Value.Length > 0)
            result.Add(new XElement("mutant", mutant.Value));
        result.Add(
            new XElement("source", el.Element("source")?.Value ?? string.Empty),
            new XElement("page", el.Element("page")?.Value ?? string.Empty),
            el.Element("bonus") is { } b ? new XElement("bonus", b.Nodes()) : new XElement("bonus"),
            new XElement("notes", string.Empty));
        return result;
    }

    public bool RequiresText(QualityCatalogEntry entry) =>
        entry.Element.Element("bonus") is { } b && Cyberware.CyberwareManager.TryParseExternalBonus(b, out var text) && text;

    public bool RequiresSkillSelection(QualityCatalogEntry entry) =>
        entry.Element.Element("bonus")?.Element("selectskill") is not null;

    private static readonly string[] SelectableAttributes = ["BOD", "AGI", "REA", "STR", "CHA", "INT", "LOG", "WIL", "EDG"];
    private static readonly string[] KnownAttributes = [.. SelectableAttributes, "MAG", "RES", "ESS"];

    public bool RequiresAttributeSelection(QualityCatalogEntry entry) =>
        entry.Element.Element("bonus")?.Element("selectattribute") is not null;

    public IReadOnlyList<string> GetAttributeChoices(QualityCatalogEntry entry)
    {
        var selection = entry.Element.Element("bonus")?.Element("selectattribute");
        if (selection is null)
            return [];
        var included = selection.Elements("attribute").Select(e => e.Value).ToList();
        var excluded = selection.Elements("excludeattribute").Select(e => e.Value).ToHashSet(StringComparer.Ordinal);
        IEnumerable<string> choices = included.Count > 0 ? included : SelectableAttributes;
        return choices
            .Where(a => !excluded.Contains(a))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public IReadOnlyList<string> GetSkillChoices(CharacterSession session, QualityCatalogEntry entry)
    {
        var selection = entry.Element.Element("bonus")?.Element("selectskill");
        if (selection is null)
            return [];
        var group = selection.Attribute("skillgroup")?.Value;
        return new AttributesSkillsManager(session).GetSkills()
            .Where(s => string.IsNullOrEmpty(group) || string.Equals(s.SkillGroup, group, StringComparison.Ordinal))
            .Select(s => s.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    public bool TryApplySkillSelection(CharacterSession session, string sourceGuid, QualityCatalogEntry entry, string selection)
    {
        var bonus = entry.Element.Element("bonus");
        var select = bonus?.Element("selectskill");
        if (select is null || !GetSkillChoices(session, entry).Contains(selection, StringComparer.Ordinal))
            return false;

        var modifier = select.Element("val") ?? select.Element("max");
        if (modifier is null || !CyberwareManager.TryEvaluateExternalSelectionValue(modifier.Value, out var value))
            return false;

        var improvements = session.Xml.Root?.Element("improvements");
        if (improvements is null)
        {
            improvements = new XElement("improvements");
            session.Xml.Root!.Add(improvements);
        }
        var isValue = select.Element("val") is not null;
        improvements.Add(new XElement("improvement",
            new XElement("unique", bonus!.Attribute("unique")?.Value ?? string.Empty),
            new XElement("improvedname", selection),
            new XElement("sourcename", sourceGuid),
            new XElement("min", "0"),
            new XElement("max", isValue ? "0" : value.ToString(CultureInfo.InvariantCulture)),
            new XElement("aug", "0"),
            new XElement("augmax", "0"),
            new XElement("val", isValue ? value.ToString(CultureInfo.InvariantCulture) : "0"),
            new XElement("rating", "1"),
            new XElement("exclude"),
            new XElement("improvementttype", "Skill"),
            new XElement("improvementsource", "Quality"),
            new XElement("custom", "False"),
            new XElement("customname"),
            new XElement("customid"),
            new XElement("customgroup"),
            new XElement("addtorating", string.Equals(select.Element("applytorating")?.Value, "yes", StringComparison.OrdinalIgnoreCase) ? "True" : "False")));
        return true;
    }

    public bool TryApplyRemainingBonus(CharacterSession session, string sourceGuid, QualityCatalogEntry entry, string? text)
    {
        var bonus = entry.Element.Element("bonus");
        var selectedNode = bonus?.Element("selectskill") ?? bonus?.Element("selectattribute");
        if (bonus is null || selectedNode is null)
            return false;
        var remaining = new XElement("bonus", bonus.Attributes(), bonus.Elements().Where(e => e != selectedNode));
        return !remaining.HasElements || CyberwareManager.TryApplyExternalBonus(session, sourceGuid, "Quality", remaining, text);
    }

    public bool TryApplyAttributeSelection(CharacterSession session, string sourceGuid, QualityCatalogEntry entry, string selection)
    {
        var bonus = entry.Element.Element("bonus");
        var select = bonus?.Element("selectattribute");
        if (select is null || !GetAttributeChoices(entry).Contains(selection, StringComparer.Ordinal))
            return false;

        var improvements = session.Xml.Root?.Element("improvements");
        if (improvements is null)
        {
            improvements = new XElement("improvements");
            session.Xml.Root!.Add(improvements);
        }
        string Value(string name) =>
            select.Element(name) is { } node && CyberwareManager.TryEvaluateExternalSelectionValue(node.Value, out var value)
                ? value.ToString(CultureInfo.InvariantCulture)
                : "0";
        improvements.Add(new XElement("improvement",
            new XElement("unique", bonus!.Attribute("unique")?.Value ?? string.Empty),
            new XElement("improvedname", selection),
            new XElement("sourcename", sourceGuid),
            new XElement("min", Value("min")),
            new XElement("max", Value("max")),
            new XElement("aug", "0"),
            new XElement("augmax", "0"),
            new XElement("val", "0"),
            new XElement("rating", "1"),
            new XElement("exclude"),
            new XElement("improvementttype", "Attribute"),
            new XElement("improvementsource", "Quality"),
            new XElement("custom", "False"),
            new XElement("customname"),
            new XElement("customid"),
            new XElement("customgroup"),
            new XElement("addtorating", "False")));
        return true;
    }
}
