using System.Globalization;
using System.Xml.Linq;

namespace ChummerGen.Domain.Character.Cyberware;

public sealed partial class CyberwareManager
{
    private static bool TryGetArmorBonusValues(XElement? bonus, int rating, out IReadOnlyDictionary<string, int> values)
    {
        values = new Dictionary<string, int>();
        if (bonus is null || bonus.HasAttributes
            || (bonus.Elements().Count() != 1 && !HasOnlyArmorAndDamageResistance(bonus))
            || bonus.Element("armor") is not { HasAttributes: false } armor
            || armor.Elements().Any(e => e.Name.LocalName is not ("b" or "i"))
            || armor.Elements().Select(e => e.Name.LocalName).Distinct(StringComparer.Ordinal).Count() != armor.Elements().Count()
            || !armor.HasElements)
            return false;

        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var element in armor.Elements())
        {
            if (element.HasAttributes || element.HasElements)
                return false;

            var expression = element.Value.Trim();
            int value;
            if (int.TryParse(expression, NumberStyles.Integer, CultureInfo.InvariantCulture, out var literal))
            {
                value = literal;
            }
            else if (expression.Contains("Rating", StringComparison.Ordinal)
                && CyberwareExpression.Evaluate(expression, rating) is { } evaluated
                && evaluated >= int.MinValue
                && evaluated <= int.MaxValue)
            {
                value = decimal.ToInt32(decimal.Floor(evaluated));
            }
            else
            {
                return false;
            }

            result.Add(element.Name.LocalName, value);
        }

        values = result;
        return true;
    }

    private static bool HasOnlyArmorAndDamageResistance(XElement? bonus) =>
        bonus is { HasAttributes: false }
        && bonus.Elements().Count() == 2
        && bonus.Elements().Count(e => e.Name.LocalName == "armor") == 1
        && bonus.Elements().Count(e => e.Name.LocalName == "damageresistance") == 1;

    private static bool TryGetArmorAndDamageResistanceBonusValues(
        XElement? bonus,
        int rating,
        out IReadOnlyDictionary<string, int> armor,
        out int damageResistance)
    {
        armor = new Dictionary<string, int>();
        damageResistance = 0;
        return HasOnlyArmorAndDamageResistance(bonus)
            && TryGetArmorBonusValues(bonus, rating, out armor)
            && TryGetDamageResistanceBonusValue(bonus, rating, out damageResistance);
    }

    private static bool IsArmorImprovement(XElement improvement) =>
        improvement.Element("improvedname")?.Value == string.Empty
        && improvement.Element("unique") is not null
        && improvement.Element("improvementttype")?.Value is "BallisticArmor" or "ImpactArmor"
        && improvement.Element("improvementsource")?.Value is "Cyberware" or "Bioware"
        && int.TryParse(improvement.Element("val")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

    private void ApplyArmorBonus(XElement cyberware, IReadOnlyDictionary<string, int> values)
    {
        var improvements = _session.Xml.Root!.Element("improvements");
        if (improvements is null)
        {
            improvements = new XElement("improvements");
            _session.Xml.Root!.Add(improvements);
        }

        var unique = cyberware.Element("bonus")?.Attribute("unique")?.Value ?? string.Empty;
        var source = cyberware.Element("improvementsource")!.Value;
        foreach (var (part, value) in values)
        {
            improvements.Add(new XElement("improvement",
                new XElement("unique", unique),
                new XElement("improvedname"),
                new XElement("sourcename", cyberware.Element("guid")!.Value),
                new XElement("min", "0"),
                new XElement("max", "0"),
                new XElement("aug", "0"),
                new XElement("augmax", "0"),
                new XElement("val", value.ToString(CultureInfo.InvariantCulture)),
                new XElement("rating", "1"),
                new XElement("exclude"),
                new XElement("improvementttype", part == "b" ? "BallisticArmor" : "ImpactArmor"),
                new XElement("improvementsource", source),
                new XElement("custom", "False"),
                new XElement("customname"),
                new XElement("customid"),
                new XElement("customgroup"),
                new XElement("addtorating", "False"),
                new XElement("enabled", "True"),
                new XElement("order", "0"),
                new XElement("notes")));
        }
    }

    private void UpdateArmorImprovements(CyberwareItem item)
    {
        if (!TryGetArmorBonusValues(item.Bonus, item.Rating, out var values))
            return;
        var improvements = _session.Xml.Root?.Element("improvements")?.Elements("improvement").ToList() ?? [];
        var own = improvements.Where(i => string.Equals(i.Element("sourcename")?.Value, item.Guid, StringComparison.OrdinalIgnoreCase)
            && IsArmorImprovement(i)).ToList();
        var unique = item.Bonus?.Attribute("unique")?.Value ?? string.Empty;
        if (own.Count != values.Count || own.Any(i => i.Element("unique")?.Value != unique))
            return;

        foreach (var (part, value) in values)
        {
            var type = part == "b" ? "BallisticArmor" : "ImpactArmor";
            var matching = own.Where(i => i.Element("improvementttype")?.Value == type).ToList();
            if (matching.Count != 1)
                return;
            matching[0].Element("val")!.Value = value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private bool RatingChangeKeepsArmorImprovements(CyberwareItem item, IReadOnlyCollection<XElement> own)
    {
        var armorBonus = TryGetArmorBonusValues(item.Bonus, item.Rating, out var values)
            || TryGetArmorAndDamageResistanceBonusValues(item.Bonus, item.Rating, out values, out _);
        if (!armorBonus)
            return false;
        var armorImprovements = own.Where(IsArmorImprovement).ToList();
        var unique = item.Bonus?.Attribute("unique")?.Value ?? string.Empty;
        if (armorImprovements.Count != values.Count || armorImprovements.Any(i => i.Element("unique")?.Value != unique))
            return false;
        return values.All(pair => armorImprovements.Count(i => i.Element("improvementttype")?.Value
            == (pair.Key == "b" ? "BallisticArmor" : "ImpactArmor")) == 1);
    }
}
