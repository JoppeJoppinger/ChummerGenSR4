using System.Globalization;
using System.Xml.Linq;

namespace ChummerGen.Domain.Character.Cyberware;

// Legacy ImprovementManager.CreateImprovements forms that create exactly one improvement from one isolated bonus tag:
// smartlink, movementpercent, swimpercent, skillwire, lifestylecost and conditionmonitor/physical.
public sealed partial class CyberwareManager
{
    private sealed record SimpleBonus(string Type, string Unique, int Value, string ImprovedName = "", bool AddToRating = false, string ValueElement = "val");

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

    // One or more supported tags in a single <bonus>; each tag must create a distinct improvement.
    private static bool TryGetSimpleBonuses(XElement? bonus, int rating, out IReadOnlyList<SimpleBonus> results)
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
            if (!TryGetSimpleBonusElement(element, bonusUnique, rating, out var single))
                return false;
            list.Add(single);
        }

        if (list.Select(b => (b.Type, b.Unique, b.ImprovedName)).Distinct().Count() != list.Count)
            return false;
        results = list;
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

        if (element.Name.LocalName == "conditionmonitor")
        {
            if (element.HasAttributes || element.Nodes().Count() != 1
                || element.Element("physical") is not { HasAttributes: false, HasElements: false } physical
                || !TryEvaluateSimpleBonusValue(physical.Value, rating, out var physicalValue))
                return false;
            result = new SimpleBonus("PhysicalCM", bonusUnique, physicalValue);
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
