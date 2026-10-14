using System.Globalization;
using System.Xml.Linq;

namespace ChummerGen.Domain.Character.Cyberware;

public sealed partial class CyberwareManager
{
    private static bool HasBoneDensityBonusShape(XElement bonus) =>
        bonus.Elements().Count() == 3
        && bonus.Elements().Count(e => e.Name.LocalName == "damageresistance") == 1
        && bonus.Elements().Count(e => e.Name.LocalName == "unarmeddv") == 1
        && bonus.Elements().Count(e => e.Name.LocalName == "unarmeddvphysical") == 1;

    private static bool TryGetBoneDensityBonusValues(
        XElement? bonus,
        int rating,
        out int damageResistance,
        out int unarmedDamage)
    {
        damageResistance = 0;
        unarmedDamage = 0;
        if (bonus is not { HasAttributes: false }
            || !HasBoneDensityBonusShape(bonus)
            || bonus.Element("unarmeddvphysical") is not { HasAttributes: false, HasElements: false } physical
            || !string.IsNullOrWhiteSpace(physical.Value)
            || !TryGetDamageResistanceBonusValue(bonus, rating, out damageResistance))
            return false;

        var element = bonus.Element("unarmeddv");
        if (element is not { HasAttributes: false, HasElements: false })
            return false;
        var expression = element.Value.Trim();
        if (int.TryParse(expression, NumberStyles.Integer, CultureInfo.InvariantCulture, out unarmedDamage))
            return true;
        if (!expression.Contains("Rating", StringComparison.Ordinal)
            || CyberwareExpression.Evaluate(expression, rating) is not { } evaluated
            || evaluated < int.MinValue || evaluated > int.MaxValue)
            return false;

        unarmedDamage = decimal.ToInt32(decimal.Floor(evaluated));
        return true;
    }

    private static bool IsBoneDensityUnarmedImprovement(XElement improvement, string type, string? unique = null) =>
        improvement.Element("improvedname")?.Value == string.Empty
        && improvement.Element("improvementttype")?.Value == type
        && improvement.Element("improvementsource")?.Value == "Bioware"
        && (unique is null || improvement.Element("unique")?.Value == unique);

    private void ApplyBoneDensityUnarmedImprovements(XElement cyberware, int unarmedDamage)
    {
        var improvements = _session.Xml.Root!.Element("improvements");
        if (improvements is null)
        {
            improvements = new XElement("improvements");
            _session.Xml.Root!.Add(improvements);
        }

        var unique = cyberware.Element("bonus")?.Attribute("unique")?.Value ?? string.Empty;
        var source = cyberware.Element("improvementsource")!.Value;
        improvements.Add(CreateImprovement(cyberware, unique, "UnarmedDV", unarmedDamage, source));
        improvements.Add(CreateImprovement(cyberware, string.Empty, "UnarmedDVPhysical", 0, source));
    }

    private static XElement CreateImprovement(XElement cyberware, string unique, string type, int value, string source) =>
        new("improvement",
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
            new XElement("improvementttype", type),
            new XElement("improvementsource", source),
            new XElement("custom", "False"),
            new XElement("customname"),
            new XElement("customid"),
            new XElement("customgroup"),
            new XElement("addtorating", "False"),
            new XElement("enabled", "True"),
            new XElement("order", "0"),
            new XElement("notes"));

    private void UpdateBoneDensityUnarmedImprovements(CyberwareItem item)
    {
        if (!TryGetBoneDensityBonusValues(item.Bonus, item.Rating, out _, out var unarmedDamage))
            return;
        var own = _session.Xml.Root?.Element("improvements")?.Elements("improvement")
            .Where(i => string.Equals(i.Element("sourcename")?.Value, item.Guid, StringComparison.OrdinalIgnoreCase)).ToList() ?? [];
        var unique = item.Bonus?.Attribute("unique")?.Value ?? string.Empty;
        var damage = own.Where(i => IsBoneDensityUnarmedImprovement(i, "UnarmedDV", unique)).ToList();
        var physical = own.Where(i => IsBoneDensityUnarmedImprovement(i, "UnarmedDVPhysical", string.Empty)).ToList();
        if (damage.Count == 1 && physical.Count == 1)
            damage[0].Element("val")!.Value = unarmedDamage.ToString(CultureInfo.InvariantCulture);
    }

    private bool RatingChangeKeepsBoneDensityUnarmedImprovements(CyberwareItem item, IReadOnlyCollection<XElement> own)
    {
        if (!TryGetBoneDensityBonusValues(item.Bonus, item.Rating, out _, out _))
            return false;
        var unique = item.Bonus?.Attribute("unique")?.Value ?? string.Empty;
        return own.Count(i => IsBoneDensityUnarmedImprovement(i, "UnarmedDV", unique)) == 1
            && own.Count(i => IsBoneDensityUnarmedImprovement(i, "UnarmedDVPhysical", string.Empty)) == 1;
    }
}
