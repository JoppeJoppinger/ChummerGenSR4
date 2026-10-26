using System.Globalization;
using System.Xml.Linq;

namespace ChummerGen.Domain.Character.Cyberware;

// Legacy ImprovementManager.CreateImprovements forms that create exactly one improvement from one isolated bonus tag:
// smartlink, movementpercent, swimpercent, skillwire, lifestylecost and conditionmonitor/physical.
public sealed partial class CyberwareManager
{
    private sealed record SimpleBonus(
        string Type,
        string Unique,
        int Value,
        string ImprovedName = "",
        bool AddToRating = false,
        string ValueElement = "val",
        string Exclude = "");

    // Legacy "specificattribute" (not ESS): one <name> plus <val>; the value is stored as the augmented modifier, not "val".
    private static bool TryGetAttributeBonus(XElement element, string bonusUnique, int rating, out SimpleBonus result)
    {
        result = null!;
        if (element.HasAttributes
            || element.Elements().Any(e => e.Name.LocalName is not ("name" or "val"))
            || element.Elements("name").Count() != 1 || element.Elements("val").Count() != 1
            || element.Nodes().Any(n => n is XText t && !string.IsNullOrWhiteSpace(t.Value)))
            return false;
        var name = element.Element("name")!;
        var val = element.Element("val")!;
        if (name.HasElements || name.Attributes().Any(a => a.Name.LocalName != "precedence")
            || name.Value is not ("BOD" or "AGI" or "REA" or "STR" or "CHA" or "INT" or "LOG" or "WIL" or "EDG")
            || val.HasElements || val.HasAttributes
            || !TryEvaluateSimpleBonusValue(val.Value, rating, out var value))
            return false;
        var unique = name.Attribute("precedence") is { } p ? "precedence" + p.Value : bonusUnique;
        result = new SimpleBonus("Attribute", unique, value, name.Value, false, "aug");
        return true;
    }

    // Legacy "specificskill", "skillgroup", "skillattribute", "skillcategory": a single <name>/<bonus> pair per bonus.
    // The precedence unique key is read from <specificskill> itself, but from <name> for skillattribute.
    private static bool TryGetSkillBonus(XElement element, string bonusUnique, int rating, out SimpleBonus result)
    {
        result = null!;
        var type = element.Name.LocalName switch
        {
            "specificskill" => "Skill",
            "skillgroup" => "SkillGroup",
            "skillattribute" => "SkillAttribute",
            "skillcategory" => "SkillCategory",
            _ => null,
        };
        if (type is null
            || element.Attributes().Any(a => a.Name.LocalName != "precedence" || element.Name.LocalName != "specificskill")
            || element.Elements().Any(e => e.Name.LocalName is not ("name" or "bonus" or "applytorating"))
            || element.Elements("name").Count() != 1 || element.Elements("bonus").Count() != 1
            || element.Elements("applytorating").Count() > 1
            || element.Nodes().Any(n => n is XText t && !string.IsNullOrWhiteSpace(t.Value)))
            return false;
        var name = element.Element("name")!;
        var bonus = element.Element("bonus")!;
        var applyToRating = element.Element("applytorating");
        if (name.HasElements || string.IsNullOrWhiteSpace(name.Value) || bonus.HasElements || bonus.HasAttributes
            || applyToRating is { HasAttributes: true } or { HasElements: true }
            || applyToRating is not null && applyToRating.Value is not ("yes" or "no")
            || !TryEvaluateSimpleBonusValue(bonus.Value, rating, out var value))
            return false;

        var unique = bonusUnique;
        if (type == "SkillAttribute")
        {
            if (name.Attributes().Any(a => a.Name.LocalName != "precedence"))
                return false;
        }
        else if (name.HasAttributes)
            return false;
        var precedence = (type == "SkillAttribute" ? name : element).Attribute("precedence")?.Value;
        if (precedence is not null)
            unique = "precedence" + precedence;

        result = new SimpleBonus(type, unique, value, name.Value, applyToRating?.Value == "yes");
        return true;
    }

    // Legacy <selectside/> creates no improvement; the chosen side only becomes the piece's location.
    private static bool HasSelectSide(XElement? bonus) =>
        bonus?.Elements("selectside").Any(e => !e.HasAttributes && !e.Nodes().Any()) == true;

    public bool RequiresSide(CyberwareCatalogEntry entry) =>
        entry.Node.Element("bonus") is { } bonus && HasSelectSide(bonus) && TryGetSimpleBonuses(bonus, 1, out _);

    // Legacy <selecttext/> creates a Text improvement with the entered value, which also becomes the location.
    private static bool HasSelectText(XElement? bonus) =>
        bonus?.Elements("selecttext").Any(e => !e.HasAttributes && !e.Nodes().Any()) == true;

    public bool RequiresText(CyberwareCatalogEntry entry) =>
        entry.Node.Element("bonus") is { } bonus && HasSelectText(bonus) && TryGetSimpleBonuses(bonus, 1, out _);

    private static readonly string[] SelectableAttributeNames =
        ["BOD", "AGI", "REA", "STR", "CHA", "INT", "LOG", "WIL", "EDG", "MAG", "RES"];

    public IReadOnlyList<string> GetSelectableAttributes(CyberwareCatalogEntry entry)
    {
        var selection = entry.Node.Element("bonus")?.Element("selectattribute");
        if (!IsSupportedSelectAttribute(selection, 1, out _, out var included, out var excluded))
            return [];
        IEnumerable<string> options = included.Count > 0 ? included : SelectableAttributeNames;
        return options
            .Where(attribute => !excluded.Contains(attribute, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public bool RequiresAttributeSelection(CyberwareCatalogEntry entry) =>
        GetSelectableAttributes(entry).Count > 0;

    public IReadOnlyList<string> GetSelectableSkills(CyberwareCatalogEntry entry)
    {
        var selection = entry.Node.Element("bonus")?.Element("selectskill");
        if (!IsSupportedSelectSkill(selection, 1, out _, out _))
            return [];

        var group = selection!.Attribute("skillgroup")?.Value;
        var category = selection.Attribute("skillcategory")?.Value;
        var excludes = SplitSelectionList(selection.Attribute("excludecategory")?.Value);
        var limitTo = SplitSelectionList(selection.Attribute("limittoskill")?.Value);
        return Catalog.SkillNodes
            .Where(skill => skill.Element("exotic") is null)
            .Where(skill => group is null || skill.Element("skillgroup")?.Value == group)
            .Where(skill => category is null || skill.Element("category")?.Value == category)
            .Where(skill => excludes.Count == 0 || !excludes.Contains(skill.Element("category")?.Value, StringComparer.Ordinal))
            .Where(skill => selection.Attribute("excludeskillgroup")?.Value is not { } excludedGroup
                || !string.Equals(skill.Element("skillgroup")?.Value, excludedGroup, StringComparison.Ordinal))
            .Where(skill => limitTo.Count == 0 || limitTo.Contains(skill.Element("name")?.Value, StringComparer.Ordinal))
            .Select(skill => skill.Element("name")?.Value.Trim() ?? string.Empty)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    public IReadOnlyList<string> GetSelectableSkillGroups(CyberwareCatalogEntry entry)
    {
        var selection = entry.Node.Element("bonus")?.Element("selectskillgroup");
        if (!IsSupportedSelectSkillGroup(selection, 1, out _, out _))
            return [];
        var excludes = SplitSelectionList(selection!.Attribute("excludecategory")?.Value);
        return Catalog.SkillGroupNames
            .Where(group => excludes.Count == 0 || Catalog.SkillNodes.Any(skill =>
                skill.Element("skillgroup")?.Value == group
                && !excludes.Contains(skill.Element("category")?.Value, StringComparer.Ordinal)))
            .OrderBy(group => group, StringComparer.Ordinal)
            .ToList();
    }

    public bool RequiresSkillSelection(CyberwareCatalogEntry entry) =>
        entry.Node.Element("bonus")?.Element("selectskill") is { } selection
        && IsSupportedSelectSkill(selection, 1, out _, out _);

    public bool RequiresSkillGroupSelection(CyberwareCatalogEntry entry) =>
        entry.Node.Element("bonus")?.Element("selectskillgroup") is { } selection
        && IsSupportedSelectSkillGroup(selection, 1, out _, out _);

    private static List<string> SplitSelectionList(string? text) =>
        (text ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    // One or more supported tags in a single <bonus>; each tag must create a distinct improvement.
    private static bool TryGetSimpleBonuses(
        XElement? bonus,
        int rating,
        out IReadOnlyList<SimpleBonus> results,
        string? selectedAttribute = null,
        string? selectedSkill = null,
        string? selectedSkillGroup = null)
    {
        results = [];
        if (bonus is null || !bonus.HasElements
            || bonus.Nodes().Any(n => n is not XElement)
            || bonus.Attributes().Any(a => a.Name.LocalName != "unique"))
            return false;
        var elements = bonus.Elements().ToList();
        if (elements.Count(e => e.Name.LocalName is "selectside" or "selecttext") > 1
            || elements.Any(e => e.Name.LocalName is "selectside" or "selecttext" && (e.HasAttributes || e.Nodes().Any())))
            return false;
        elements.RemoveAll(e => e.Name.LocalName is "selectside" or "selecttext");
        // Legacy iterates these tags as lists; every other tag is read once, so duplicates stay blocked.
        if (elements.GroupBy(e => e.Name.LocalName)
            .Any(g => g.Count() > 1 && g.Key is not ("specificattribute" or "specificskill" or "skillgroup" or "skillattribute" or "skillcategory")))
            return false;
        var bonusUnique = bonus.Attribute("unique")?.Value ?? string.Empty;
        var list = new List<SimpleBonus>();
        foreach (var element in elements)
        {
            if (element.Name.LocalName == "selectskill")
            {
                if (!IsSupportedSelectSkill(element, rating, out var selectedSkillBonuses, out _))
                    return false;
                if (selectedSkill is not null)
                {
                    if (!TryGetSelectedSkillBonuses(element, bonusUnique, rating, selectedSkill, out var skillBonuses))
                        return false;
                    list.AddRange(skillBonuses);
                }
                continue;
            }
            if (element.Name.LocalName == "selectskillgroup")
            {
                if (!IsSupportedSelectSkillGroup(element, rating, out _, out var excludedCategory))
                    return false;
                if (selectedSkillGroup is not null)
                {
                    if (!TryGetSelectedSkillGroupBonus(element, bonusUnique, rating, selectedSkillGroup, excludedCategory, out var skillGroupBonus))
                        return false;
                    list.Add(skillGroupBonus);
                }
                continue;
            }
            if (element.Name.LocalName == "selectattribute")
            {
                if (!IsSupportedSelectAttribute(element, rating, out _, out var included, out var excluded))
                    return false;
                IEnumerable<string> options = included.Count > 0 ? included : SelectableAttributeNames;
                if (!options.Any(attribute => !excluded.Contains(attribute, StringComparer.Ordinal)))
                    return false;
                if (selectedAttribute is not null)
                {
                    if (!TryGetSelectedAttributeBonus(bonus, rating, selectedAttribute, out var attributeBonus))
                        return false;
                    list.Add(attributeBonus);
                }
                continue;
            }
            if (!TryGetSimpleBonusElement(element, bonusUnique, rating, out var single))
                return false;
            list.Add(single);
        }

        if (list.Select(b => (b.Type, b.Unique, b.ImprovedName, b.ValueElement)).Distinct().Count() != list.Count)
            return false;
        results = list;
        return true;
    }

    private static bool IsSupportedSelectSkill(
        XElement? element,
        int rating,
        out IReadOnlyList<SimpleBonus> improvements,
        out string? excludedSkillGroup)
    {
        improvements = [];
        excludedSkillGroup = null;
        if (element is not { HasElements: true }
            || element.Attributes().Any(a => a.Name.LocalName is not ("skillgroup" or "skillcategory" or "excludecategory" or "excludeskillgroup" or "limittoskill"))
            || element.Elements().Any(e => e.Name.LocalName is not ("val" or "max" or "applytorating"))
            || element.Elements("val").Count() > 1 || element.Elements("max").Count() > 1
            || element.Elements("applytorating").Count() > 1
            || element.Elements().Any(e => e.HasAttributes || e.HasElements)
            || element.Nodes().Any(n => n is XText t && !string.IsNullOrWhiteSpace(t.Value)))
            return false;

        var filterAttributes = element.Attributes().Count(a =>
            a.Name.LocalName is "skillgroup" or "skillcategory" or "excludecategory" or "excludeskillgroup" or "limittoskill");
        if (filterAttributes > 1)
            return false;
        excludedSkillGroup = element.Attribute("excludeskillgroup")?.Value;

        var applyToRating = element.Element("applytorating");
        if (applyToRating is not null && applyToRating.Value is not ("yes" or "no"))
            return false;
        var list = new List<SimpleBonus>();
        foreach (var valueName in new[] { "val", "max" })
        {
            if (element.Element(valueName) is not { } valueNode)
                continue;
            if (!TryEvaluateSimpleBonusValue(valueNode.Value, rating, out var value))
                return false;
            list.Add(new SimpleBonus("Skill", string.Empty, value, string.Empty,
                applyToRating?.Value == "yes", valueName));
        }
        if (list.Count == 0)
            return false;
        improvements = list;
        return true;
    }

    private static bool IsSupportedSelectSkillGroup(
        XElement? element,
        int rating,
        out SimpleBonus improvement,
        out string excludedCategory)
    {
        improvement = null!;
        excludedCategory = string.Empty;
        if (element is not { }
            || element.Attributes().Any(a => a.Name.LocalName != "excludecategory")
            || element.Elements().Any(e => e.Name.LocalName is not ("bonus" or "applytorating"))
            || element.Elements("bonus").Count() != 1 || element.Elements("applytorating").Count() > 1
            || element.Nodes().Any(n => n is XText t && !string.IsNullOrWhiteSpace(t.Value))
            || element.Element("bonus") is not { HasAttributes: false, HasElements: false } bonus
            || element.Element("applytorating") is { } apply && (apply.HasAttributes || apply.HasElements || apply.Value is not ("yes" or "no"))
            || !TryEvaluateSimpleBonusValue(bonus.Value, rating, out var value))
            return false;
        excludedCategory = element.Attribute("excludecategory")?.Value ?? string.Empty;
        improvement = new SimpleBonus("SkillGroup", string.Empty, value, string.Empty,
            element.Element("applytorating")?.Value == "yes",
            Exclude: excludedCategory);
        return true;
    }

    private static bool TryGetSelectedSkillBonuses(
        XElement element,
        string unique,
        int rating,
        string selectedSkill,
        out IReadOnlyList<SimpleBonus> improvements)
    {
        improvements = [];
        if (!IsSupportedSelectSkill(element, rating, out var bonuses, out _))
            return false;
        var list = bonuses.Select(b => b with { Unique = unique, ImprovedName = selectedSkill }).ToList();
        improvements = list;
        return true;
    }

    private static bool TryGetSelectedSkillGroupBonus(
        XElement element,
        string unique,
        int rating,
        string selectedSkillGroup,
        string exclude,
        out SimpleBonus improvement)
    {
        improvement = null!;
        if (!IsSupportedSelectSkillGroup(element, rating, out var parsed, out _))
            return false;
        improvement = parsed with { Unique = unique, ImprovedName = selectedSkillGroup, Exclude = exclude };
        return true;
    }

    private static bool IsSupportedSelectAttribute(
        XElement? element,
        int rating,
        out int max,
        out List<string> included,
        out List<string> excluded)
    {
        max = 0;
        included = [];
        excluded = [];
        if (element is not { HasAttributes: false }
            || element.Elements().Any(e => e.Name.LocalName is not ("attribute" or "excludeattribute" or "max"))
            || element.Elements("max").Count() != 1
            || element.Elements("attribute").Any(e => e.HasAttributes || e.HasElements || string.IsNullOrWhiteSpace(e.Value))
            || element.Elements("excludeattribute").Any(e => e.HasAttributes || e.HasElements || string.IsNullOrWhiteSpace(e.Value))
            || element.Elements().Any(e => e.Name.LocalName == "max" && (e.HasAttributes || e.HasElements))
            || element.Nodes().Any(n => n is XText t && !string.IsNullOrWhiteSpace(t.Value))
            || !TryEvaluateSimpleBonusValue(element.Element("max")!.Value, rating, out max)
            || max < 0)
            return false;
        included = element.Elements("attribute").Select(e => e.Value.Trim()).Distinct(StringComparer.Ordinal).ToList();
        excluded = element.Elements("excludeattribute").Select(e => e.Value.Trim()).Distinct(StringComparer.Ordinal).ToList();
        return included.Concat(excluded).All(a => SelectableAttributeNames.Contains(a, StringComparer.Ordinal));
    }

    private static bool TryGetSelectedAttributeBonus(
        XElement? bonus,
        int rating,
        string? selectedAttribute,
        out SimpleBonus result)
    {
        result = null!;
        if (bonus?.Element("selectattribute") is not { } selection)
            return false;
        if (!IsSupportedSelectAttribute(selection, rating, out var max, out var included, out var excluded))
            return false;
        IEnumerable<string> options = included.Count > 0 ? included : SelectableAttributeNames;
        if (string.IsNullOrWhiteSpace(selectedAttribute)
            || !options.Contains(selectedAttribute, StringComparer.Ordinal)
            || excluded.Contains(selectedAttribute, StringComparer.Ordinal))
            return false;
        result = new SimpleBonus("Attribute", bonus.Attribute("unique")?.Value ?? string.Empty, max, selectedAttribute, false, "max");
        return true;
    }

    private static bool TryGetSimpleBonusElement(XElement element, string bonusUnique, int rating, out SimpleBonus result)
    {
        result = null!;
        // Legacy ignores the precedence attribute on <initiative>; the improvement uses the bonus unique.
        if (element.Name.LocalName == "initiative" && !element.HasElements
            && element.Attributes().All(a => a.Name.LocalName == "precedence")
            && element.Nodes().Count() == 1
            && TryEvaluateSimpleBonusValue(element.Value, rating, out var initiativeValue))
        {
            result = new SimpleBonus("Initiative", bonusUnique, initiativeValue);
            return true;
        }

        if (element.Name.LocalName is "specificskill" or "skillgroup" or "skillattribute" or "skillcategory")
            return TryGetSkillBonus(element, bonusUnique, rating, out result);

        if (element.Name.LocalName == "specificattribute")
            return TryGetAttributeBonus(element, bonusUnique, rating, out result);

        if (element.Name.LocalName == "skillarticulation")
        {
            if (element.HasAttributes || element.Nodes().Count() != 1
                || element.Element("bonus") is not { HasAttributes: false, HasElements: false } articulation
                || !TryEvaluateSimpleBonusValue(articulation.Value, rating, out var articulationValue))
                return false;
            result = new SimpleBonus("EnhancedArticulation", bonusUnique, articulationValue);
            return true;
        }
        if (element.Name.LocalName == "skillsoftaccess")
        {
            if (element.HasAttributes || element.Nodes().Any())
                return false;
            result = new SimpleBonus("SkillsoftAccess", string.Empty, 0);
            return true;
        }
        if (element.Name.LocalName == "adapsin")
        {
            if (element.HasAttributes || element.Nodes().Any())
                return false;
            result = new SimpleBonus("Adapsin", "adapsin", 0);
            return true;
        }
        if (element.Name.LocalName == "smartlink")
        {
            if (element.HasAttributes || element.Nodes().Any())
                return false;
            result = new SimpleBonus("Smartlink", "smartlink", 0);
            return true;
        }

        if (element.Name.LocalName == "cyborgessence")
        {
            if (element.HasAttributes || element.Nodes().Any())
                return false;
            result = new SimpleBonus("CyborgEssence", string.Empty, 0);
            return true;
        }

        if (element.Name.LocalName == "conditionmonitor")
        {
            if (element.HasAttributes || element.Nodes().Count() != 1
                || element.Elements().SingleOrDefault() is not { HasElements: false } child
                || child.Name.LocalName is not ("physical" or "stun" or "threshold" or "thresholdoffset" or "overflow")
                || child.Attributes().Any(a => a.Name.LocalName != "precedence"
                    || child.Name.LocalName is not ("threshold" or "thresholdoffset"))
                || !TryEvaluateSimpleBonusValue(child.Value, rating, out var monitorValue))
                return false;
            var monitorUnique = child.Attribute("precedence") is { } cmPrecedence ? "precedence" + cmPrecedence.Value : bonusUnique;
            var monitorType = child.Name.LocalName switch
            {
                "physical" => "PhysicalCM",
                "stun" => "StunCM",
                "threshold" => "CMThreshold",
                "thresholdoffset" => "CMThresholdOffset",
                _ => "CMOverflow"
            };
            result = new SimpleBonus(monitorType, monitorUnique, monitorValue);
            return true;
        }

        if (element.HasAttributes || element.HasElements
            || !TryEvaluateSimpleBonusValue(element.Value, rating, out var value))
            return false;
        switch (element.Name.LocalName)
        {
            case "movementpercent":
                result = new SimpleBonus("MovementPercent", string.Empty, value);
                return true;
            case "swimpercent":
                result = new SimpleBonus("SwimPercent", string.Empty, value);
                return true;
            case "memory":
                result = new SimpleBonus("Memory", string.Empty, value);
                return true;
            case "drainresist":
                result = new SimpleBonus("DrainResistance", string.Empty, value);
                return true;
            case "skillwire":
                result = new SimpleBonus("Skillwire", string.Empty, value);
                return true;
            case "lifestylecost":
                result = new SimpleBonus("LifestyleCost", bonusUnique, value);
                return true;
            case "initiative":
                result = new SimpleBonus("Initiative", bonusUnique, value);
                return true;
            case "matrixinitiativepass":
                result = new SimpleBonus("MatrixInitiativePass", "matrixinitiativepass", value);
                return true;
            case "matrixinitiativepassadd":
                result = new SimpleBonus("MatrixInitiativePass", bonusUnique, value);
                return true;
            case "initiativepass":
                result = new SimpleBonus("InitiativePass", "initiativepass", value);
                return true;
            default:
                return false;
        }
    }

    private static bool TryEvaluateSimpleBonusValue(string text, int rating, out int value)
    {
        var expression = text.Trim();
        if (int.TryParse(expression, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            return true;
        value = 0;
        if (!(expression.Contains("Rating", StringComparison.Ordinal) || expression.StartsWith("FixedValues(", StringComparison.Ordinal))
            || CyberwareExpression.Evaluate(expression, rating) is not { } evaluated
            || evaluated < int.MinValue || evaluated > int.MaxValue)
            return false;
        value = decimal.ToInt32(decimal.Floor(evaluated));
        return true;
    }

    private static bool IsAnySimpleBonusImprovement(XElement improvement, IReadOnlyList<SimpleBonus> bonuses) =>
        bonuses.Any(b => IsSimpleBonusImprovement(improvement, b));

    private static bool IsSimpleBonusImprovement(XElement improvement, SimpleBonus bonus) =>
        improvement.Element("unique")?.Value == bonus.Unique
        && improvement.Element("improvedname")?.Value == bonus.ImprovedName
        && improvement.Element("improvementttype")?.Value == bonus.Type
        && improvement.Element("improvementsource")?.Value is "Cyberware" or "Bioware"
        && int.TryParse(improvement.Element(bonus.ValueElement)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

    private void ApplySimpleBonuses(XElement cyberware, IReadOnlyList<SimpleBonus> bonuses)
    {
        foreach (var bonus in bonuses)
            ApplySimpleBonus(cyberware, bonus);
    }

    private void ApplySimpleBonus(XElement cyberware, SimpleBonus bonus)
    {
        var improvements = _session.Xml.Root!.Element("improvements");
        if (improvements is null)
        {
            improvements = new XElement("improvements");
            _session.Xml.Root!.Add(improvements);
        }

        var valueText = bonus.Value.ToString(CultureInfo.InvariantCulture);
        var improvement = CreateImprovement(cyberware, bonus.Unique, bonus.Type, 0,
            cyberware.Element("improvementsource")!.Value);
        improvement.Element(bonus.ValueElement)!.Value = valueText;
        improvement.Element("improvedname")!.Value = bonus.ImprovedName;
        improvement.Element("addtorating")!.Value = bonus.AddToRating ? "True" : "False";
        improvement.Element("exclude")!.Value = bonus.Exclude;
        improvements.Add(improvement);
    }

    private void UpdateSimpleBonus(CyberwareItem item)
    {
        if (!TryGetSimpleBonuses(item.Bonus, item.Rating, out var bonuses))
            return;
        foreach (var bonus in bonuses)
        {
            var matching = _session.Xml.Root?.Element("improvements")?.Elements("improvement")
                .Where(i => string.Equals(i.Element("sourcename")?.Value, item.Guid, StringComparison.OrdinalIgnoreCase)
                    && IsSimpleBonusImprovement(i, bonus)).ToList() ?? [];
            if (matching.Count == 1)
                matching[0].Element(bonus.ValueElement)!.Value = bonus.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static bool RatingChangeKeepsSimpleBonus(CyberwareItem item, IReadOnlyCollection<XElement> own) =>
        TryGetSimpleBonuses(item.Bonus, item.Rating, out var bonuses)
        && bonuses.All(b => own.Count(i => IsSimpleBonusImprovement(i, b)) == 1);
}
