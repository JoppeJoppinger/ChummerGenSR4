using System.Globalization;
using System.Xml.Linq;
using ChummerGen.Domain.Character.Gear;

namespace ChummerGen.Domain.Character.Cyberware;

// Wraps a <cyberware> element of a .chum file (legacy Cyberware.Save/Load schema). Bioware uses the same
// element with <improvementsource>Bioware</improvementsource>. All edits write straight into the XElement,
// so unknown child nodes (bonus, allowgear, weaponguid, gears, ...) are preserved untouched.
public sealed class CyberwareItem
{
    public const string EssenceHoleName = "Essence Hole";

    public CyberwareItem(XElement element, CyberwareItem? parent = null)
    {
        Element = element ?? throw new ArgumentNullException(nameof(element));
        Parent = parent;
        foreach (var child in Element.Element("children")?.Elements("cyberware") ?? [])
            Children.Add(new CyberwareItem(child, this));
    }

    public XElement Element { get; }
    public CyberwareItem? Parent { get; }
    public List<CyberwareItem> Children { get; } = [];

    public string Guid => GetText("guid");
    public string Name => GetText("name");
    public string Category => GetText("category");
    public string LimbSlot => GetText("limbslot");
    public string EssExpression => GetText("ess");
    public string CapacityExpression => GetText("capacity");
    public string AvailExpression => GetText("avail");
    public string CostExpression => GetText("cost");
    public string Source => GetText("source");
    public string Page => GetText("page");
    public int MinRating => GetInt("minrating");
    public int MaxRating => GetInt("maxrating");
    public string Subsystems => GetText("subsystems");
    public string ForceGrade => GetText("forcegrade");
    public bool Suite => GetBool("suite");
    public XElement? Bonus => Element.Element("bonus") is { HasElements: true } bonus ? bonus : null;
    public bool IsEssenceHole => Name == EssenceHoleName;

    public CyberwareSource SourceType =>
        string.Equals(GetText("improvementsource"), "Bioware", StringComparison.OrdinalIgnoreCase)
            ? CyberwareSource.Bioware
            : CyberwareSource.Cyberware;

    public int Rating { get => GetInt("rating"); set => SetText("rating", value.ToString(CultureInfo.InvariantCulture)); }
    public string GradeName { get => GetText("grade"); set => SetText("grade", value); }
    public string Location { get => GetText("location"); set => SetText("location", value); }
    public string Notes { get => GetText("notes"); set => SetText("notes", value); }
    public int EssenceDiscount { get => GetInt("essdiscount"); set => SetText("essdiscount", value.ToString(CultureInfo.InvariantCulture)); }
    public bool DiscountCost { get => GetBool("discountedcost"); set => SetText("discountedcost", value ? "True" : "False"); }

    public IReadOnlyList<GearItem> Gear =>
        Element.Element("gears")?.Elements("gear").Select(e => new GearItem(e)).ToList() ?? [];

    public string DisplayNameShort => string.IsNullOrWhiteSpace(Name) ? "Unbekannt" : Name;

    public string DisplayName
    {
        get
        {
            var text = DisplayNameShort;
            if (Rating > 0)
                text += $" (Stufe {Rating})";
            if (!string.IsNullOrWhiteSpace(Location))
                text += $" ({Location})";
            return text;
        }
    }

    public IEnumerable<CyberwareItem> SelfAndDescendants()
    {
        yield return this;
        foreach (var child in Children)
            foreach (var descendant in child.SelfAndDescendants())
                yield return descendant;
    }

    public decimal CalculatedEssence(CyberwareGradeTable grades, int decimals = 2)
    {
        var baseEss = CyberwareExpression.Evaluate(EssExpression, Rating) ?? 0m;
        var multiplier = grades.Get(GradeName).Essence;
        if (Suite)
            multiplier -= 0.1m;
        if (EssenceDiscount != 0)
            multiplier -= EssenceDiscount * 0.01m;
        return Math.Round(baseEss * multiplier, decimals, MidpointRounding.AwayFromZero);
    }

    private int GradedBaseCost(CyberwareGradeTable grades)
    {
        var cost = CyberwareExpression.Evaluate(CostExpression, Rating) ?? 0m;
        return CyberwareExpression.ToInt(CyberwareExpression.ToInt((double)cost) * grades.Get(GradeName).Cost);
    }

    private int DiscountedCost(int cost) =>
        DiscountCost ? CyberwareExpression.ToInt(cost * 0.9) : cost;

    // Cost of the item alone (legacy OwnCost / TotalCostWithoutModifiers).
    public int OwnCost(CyberwareGradeTable grades, bool applySuite = true)
    {
        var cost = DiscountedCost(GradedBaseCost(grades));
        var suite = applySuite && Suite ? 0.9 : 1.0;
        return CyberwareExpression.ToInt(Math.Round(cost * suite, 2, MidpointRounding.AwayFromZero));
    }

    // Cost including plugins and gear (legacy TotalCost, without character improvement multipliers).
    public int TotalCost(CyberwareGradeTable grades)
    {
        var graded = GradedBaseCost(grades);
        var total = DiscountedCost(graded);

        foreach (var child in Children)
        {
            if (child.CapacityExpression != "[*]")
            {
                if (child.CostExpression.StartsWith('*'))
                {
                    var multiplierText = child.CostExpression.Replace("*", string.Empty, StringComparison.Ordinal);
                    if (double.TryParse(multiplierText, NumberStyles.Float, CultureInfo.InvariantCulture, out var multiplier))
                    {
                        var pluginCost = CyberwareExpression.ToInt(graded * (multiplier - 1));
                        total += child.DiscountCost ? CyberwareExpression.ToInt(pluginCost * 0.9) : pluginCost;
                    }
                }
                else
                {
                    total += child.OwnCost(grades, applySuite: false);
                }
            }

            total += child.Gear.Sum(g => g.TotalCost);
        }

        total += Gear.Sum(g => g.TotalCost);

        var suite = Suite ? 0.9 : 1.0;
        return CyberwareExpression.ToInt(Math.Round(total * suite, 2, MidpointRounding.AwayFromZero));
    }

    public (int Value, string Suffix) CalculateAvail(CyberwareGradeTable grades)
    {
        var expression = AvailExpression.Trim();
        var (own, ownSuffix, isModifier) = ResolveOwnAvail(expression, grades);
        if (isModifier)
            return (own, ownSuffix);

        var value = own;
        var suffix = ownSuffix;
        foreach (var child in Children)
        {
            var childAvail = child.AvailExpression.Trim();
            if (!childAvail.Contains('+'))
                continue;
            var (childValue, childSuffix, _) = child.ResolveOwnAvail(childAvail, grades);
            value += childValue;
            if (childSuffix.Length > 0 && suffix != "F")
                suffix = childSuffix;
        }

        return (Math.Max(0, value), suffix);
    }

    public string TotalAvail(CyberwareGradeTable grades)
    {
        var (value, suffix) = CalculateAvail(grades);
        var prefix = AvailExpression.TrimStart().StartsWith('+') ? "+" : string.Empty;
        return prefix + value.ToString(CultureInfo.InvariantCulture) + suffix;
    }

    private (int Value, string Suffix, bool IsModifier) ResolveOwnAvail(string expression, CyberwareGradeTable grades)
    {
        var isModifier = expression.StartsWith('+');
        var raw = isModifier ? expression[1..] : expression;
        var fixedValues = CyberwareExpression.IsFixedValues(raw);
        if (fixedValues)
        {
            var values = CyberwareExpression.FixedValues(raw);
            var index = Rating - 1;
            raw = index >= 0 && index < values.Length ? values[index] : "0";
        }

        var suffix = string.Empty;
        if (raw.Length > 0 && (raw[^1] == 'F' || raw[^1] == 'R'))
        {
            suffix = raw[^1].ToString();
            raw = raw[..^1];
        }

        var evaluated = CyberwareExpression.EvaluateArithmetic(CyberwareExpression.SubstituteRating(raw, Rating)) ?? 0m;
        var value = CyberwareExpression.ToInt((double)evaluated);
        if (!isModifier && !fixedValues)
            value += grades.Get(GradeName).Avail;
        return (value, suffix, isModifier);
    }

    // Legacy CalculatedCapacity: evaluates Rating/FixedValues and keeps the "[x]" and "a/[b]" notation.
    public string CalculatedCapacity
    {
        get
        {
            var capacity = CapacityExpression;
            var slash = capacity.IndexOf("/[", StringComparison.Ordinal);
            if (slash >= 0)
                return EvaluateCapacityPart(capacity[..slash]) + "/" + EvaluateCapacityPart(capacity[(slash + 1)..]);
            return EvaluateCapacityPart(capacity);
        }
    }

    private string EvaluateCapacityPart(string part)
    {
        var trimmed = part.Trim();
        if (trimmed == "[*]" || trimmed == "*")
            return trimmed;

        var bracketed = trimmed.StartsWith('[') && trimmed.EndsWith(']');
        var inner = bracketed ? trimmed[1..^1] : trimmed;
        if (!inner.Contains("Rating", StringComparison.Ordinal) && !CyberwareExpression.IsFixedValues(inner))
            return trimmed;

        var value = CyberwareExpression.Evaluate(inner, Rating);
        var text = value is null ? "0" : value.Value.ToString("0.##", CultureInfo.InvariantCulture);
        return bracketed ? $"[{text}]" : text;
    }

    // Capacity this item uses inside a parent (the bracketed value); [*] items cost no capacity.
    public int PluginCapacity
    {
        get
        {
            var capacity = CalculatedCapacity;
            if (capacity.Contains("/[", StringComparison.Ordinal))
                capacity = capacity[(capacity.IndexOf('[') + 1)..capacity.IndexOf(']')];
            else if (capacity.Contains('['))
                capacity = capacity[1..^1];
            if (capacity == "*")
                return 0;
            return int.TryParse(capacity, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        }
    }

    public int CapacityRemaining
    {
        get
        {
            var expression = CapacityExpression;
            string baseCapacity;
            if (expression.Contains("/[", StringComparison.Ordinal))
                baseCapacity = CalculatedCapacity[..CalculatedCapacity.IndexOf('/')];
            else if (!expression.Contains('['))
                baseCapacity = CalculatedCapacity;
            else
                return 0;

            if (!int.TryParse(baseCapacity, NumberStyles.Integer, CultureInfo.InvariantCulture, out var capacity))
                return 0;
            return capacity - Children.Sum(c => c.PluginCapacity);
        }
    }

    public bool HasCapacity =>
        CapacityExpression.Contains("/[", StringComparison.Ordinal) || !CapacityExpression.Contains('[');

    public string GetText(string name) => Element.Element(name)?.Value ?? string.Empty;

    public int GetInt(string name) =>
        int.TryParse(Element.Element(name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    public bool GetBool(string name) =>
        bool.TryParse(Element.Element(name)?.Value, out var value) && value;

    public void SetText(string name, string value)
    {
        var node = Element.Element(name);
        if (node is null)
            Element.Add(new XElement(name, value));
        else
            node.Value = value;
    }
}