using System.Globalization;
using System.Xml.Linq;
using ChummerGen.Domain.Abstractions;
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
        if (el.Element("bonus") is { HasElements: true } bonus && !CyberwareManager.TryParseExternalBonus(bonus, out _))
            return "Qualität mit diesem Bonus wird noch nicht unterstützt.";
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
                if (RequiresText(target))
                    return "Zusatzqualität mit Texteingabe wird noch nicht unterstützt.";
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
}
