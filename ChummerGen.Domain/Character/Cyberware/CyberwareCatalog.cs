using System.Globalization;
using System.Xml.Linq;
using ChummerGen.Domain.Abstractions;

namespace ChummerGen.Domain.Character.Cyberware;

public enum CyberwareSource
{
    Cyberware,
    Bioware
}

public sealed record CyberwareGrade(string Name, decimal Essence, double Cost, int Avail, string Source)
{
    public static CyberwareGrade Standard { get; } = new("Standard", 1m, 1.0, 0, "SR4");

    public bool SecondHand => Name.Contains("(Second-Hand)", StringComparison.Ordinal);
    public bool Adapsin => Name.Contains("(Adapsin)", StringComparison.Ordinal);
}

public sealed class CyberwareGradeTable
{
    private readonly Dictionary<string, CyberwareGrade> _grades;

    public CyberwareGradeTable(IEnumerable<CyberwareGrade> grades)
    {
        Grades = grades.ToList();
        _grades = new Dictionary<string, CyberwareGrade>(StringComparer.Ordinal);
        foreach (var grade in Grades)
            _grades.TryAdd(grade.Name, grade);
    }

    public static CyberwareGradeTable Empty { get; } = new([]);

    public IReadOnlyList<CyberwareGrade> Grades { get; }

    public bool Contains(string name) => _grades.ContainsKey(name);

    // Legacy ConvertToCyberwareGrade: unknown names fall back to Standard.
    public CyberwareGrade Get(string name) =>
        _grades.TryGetValue(name, out var grade) ? grade
        : _grades.TryGetValue("Standard", out var standard) ? standard
        : CyberwareGrade.Standard;
}

public sealed class CyberwareCatalogEntry(XElement node, CyberwareSource source)
{
    public XElement Node { get; } = node;
    public CyberwareSource Source { get; } = source;
    public string Name => Node.Element("name")?.Value ?? string.Empty;
    public string Category => Node.Element("category")?.Value ?? string.Empty;
    public string Ess => Node.Element("ess")?.Value ?? string.Empty;
    public string Capacity => Node.Element("capacity")?.Value ?? string.Empty;
    public string Avail => Node.Element("avail")?.Value ?? string.Empty;
    public string Cost => Node.Element("cost")?.Value ?? string.Empty;
    public string SourceBook => Node.Element("source")?.Value ?? string.Empty;
    public string Page => Node.Element("page")?.Value ?? string.Empty;
    public string ForceGrade => Node.Element("forcegrade")?.Value ?? string.Empty;
    public bool TryGetVariableCostRange(out int min, out int max) =>
        CyberwareExpression.TryParseVariableCostRange(Cost, out min, out max);
}

public sealed class CyberwareCatalog
{
    public const string CyberwareCatalogName = "cyberware.xml";
    public const string BiowareCatalogName = "bioware.xml";
    public const string WeaponCatalogName = "weapons.xml";
    public const string SkillCatalogName = "skills.xml";

    private readonly Dictionary<CyberwareSource, XElement?> _roots;

    private readonly XElement? _weapons;
    private readonly XElement? _skills;

    private CyberwareCatalog(XElement? cyberware, XElement? bioware, XElement? weapons = null, XElement? skills = null)
    {
        _weapons = weapons;
        _skills = skills;
        _roots = new() { [CyberwareSource.Cyberware] = cyberware, [CyberwareSource.Bioware] = bioware };
        CyberwareGrades = ReadGrades(cyberware);
        BiowareGrades = ReadGrades(bioware);
    }

    public static CyberwareCatalog Empty { get; } = new(null, null);

    public CyberwareGradeTable CyberwareGrades { get; }
    public CyberwareGradeTable BiowareGrades { get; }

    public static CyberwareCatalog FromXml(
        string? cyberwareXml,
        string? biowareXml,
        string? weaponsXml = null,
        string? skillsXml = null) =>
        new(ParseRoot(cyberwareXml), ParseRoot(biowareXml), ParseRoot(weaponsXml), ParseRoot(skillsXml));

    public static async Task<CyberwareCatalog> LoadAsync(
        IRuleCatalog ruleCatalog,
        string culture = "en-US",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ruleCatalog);
        var cyberware = await TryGetAsync(ruleCatalog, CyberwareCatalogName, culture, cancellationToken).ConfigureAwait(false);
        var bioware = await TryGetAsync(ruleCatalog, BiowareCatalogName, culture, cancellationToken).ConfigureAwait(false);
        var weapons = await TryGetAsync(ruleCatalog, WeaponCatalogName, culture, cancellationToken).ConfigureAwait(false);
        var skills = await TryGetAsync(ruleCatalog, SkillCatalogName, culture, cancellationToken).ConfigureAwait(false);
        return FromXml(cyberware, bioware, weapons, skills);
    }

    public CyberwareGradeTable GradesFor(CyberwareSource source) =>
        source == CyberwareSource.Bioware ? BiowareGrades : CyberwareGrades;

    public IEnumerable<CyberwareCatalogEntry> Entries(CyberwareSource source)
    {
        var root = _roots[source];
        var container = root?.Element(source == CyberwareSource.Bioware ? "biowares" : "cyberwares");
        if (container is null)
            return [];
        var nodeName = source == CyberwareSource.Bioware ? "bioware" : "cyberware";
        return container.Elements(nodeName).Select(n => new CyberwareCatalogEntry(n, source));
    }

    public IEnumerable<XElement> SuiteNodes(CyberwareSource source) =>
        _roots[source]?.Element("suites")?.Elements("suite") ?? [];

    public IReadOnlyList<string> Categories(CyberwareSource source)
    {
        var container = _roots[source]?.Element("categories");
        return container is null ? [] : container.Elements("category").Select(c => c.Value).ToList();
    }

    // Legacy: /chummer/weapons/weapon[name = X and starts-with(category, "Cyberware")]
    public XElement? FindCyberwareWeapon(string name) =>
        _weapons?.Element("weapons")?.Elements("weapon")
            .FirstOrDefault(w => w.Element("name")?.Value == name && (w.Element("category")?.Value.StartsWith("Cyberware", StringComparison.Ordinal) ?? false));

    public XElement? FindWeaponMod(string name) =>
        _weapons?.Element("mods")?.Elements("mod").FirstOrDefault(m => m.Element("name")?.Value == name);

    public IReadOnlyList<XElement> SkillNodes => _skills?.Element("skills")?.Elements("skill").ToList() ?? [];

    public IReadOnlyList<string> SkillGroupNames =>
        _skills?.Element("skillgroups")?.Elements("name").Select(n => n.Value.Trim()).Where(n => n.Length > 0).ToList() ?? [];

    public CyberwareCatalogEntry? Find(CyberwareSource source, string name) =>
        Entries(source).FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));

    private static async Task<string?> TryGetAsync(IRuleCatalog catalog, string name, string culture, CancellationToken ct)
    {
        try
        {
            return (await catalog.GetAsync(name, culture, ct).ConfigureAwait(false))?.Content;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static XElement? ParseRoot(string? xml) =>
        string.IsNullOrWhiteSpace(xml) ? null : XDocument.Parse(xml).Root;

    private static CyberwareGradeTable ReadGrades(XElement? root)
    {
        var list = new List<CyberwareGrade>();
        foreach (var grade in root?.Element("grades")?.Elements("grade") ?? [])
        {
            var name = grade.Element("name")?.Value;
            if (string.IsNullOrEmpty(name)
                || !decimal.TryParse(grade.Element("ess")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var ess)
                || !double.TryParse(grade.Element("cost")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var cost)
                || !int.TryParse(grade.Element("avail")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var avail))
                continue;
            list.Add(new CyberwareGrade(name, ess, cost, avail, grade.Element("source")?.Value ?? string.Empty));
        }

        return new CyberwareGradeTable(list);
    }
}