using System.Globalization;
using System.Xml.Linq;

namespace ChummerGen.Domain.Character.Cyberware;

public sealed partial class CyberwareManager
{
    private static bool TryGetDamageResistanceBonusValue(XElement? bonus, int rating, out int value)
    {
        value = 0;
        if (bonus is null || bonus.HasAttributes
            || (bonus.Elements().Count() != 1 && !HasOnlyArmorAndDamageResistance(bonus) && !HasBoneDensityBonusShape(bonus))
            || bonus.Element("damageresistance") is not { HasAttributes: false, HasElements: false } element)
            return false;

        var expression = element.Value.Trim();
        if (int.TryParse(expression, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            return true;
        if (!expression.Contains("Rating", StringComparison.Ordinal)
            || CyberwareExpression.Evaluate(expression, rating) is not { } evaluated
            || evaluated < int.MinValue || evaluated > int.MaxValue)
            return false;

        value = decimal.ToInt32(decimal.Floor(evaluated));
        return true;
    }

    private static bool IsDamageResistanceImprovement(XElement improvement) =>
        improvement.Element("unique")?.Value == "damageresistance"
        && improvement.Element("improvedname")?.Value == string.Empty
        && improvement.Element("improvementttype")?.Value == "DamageResistance"
        && improvement.Element("improvementsource")?.Value is "Cyberware" or "Bioware"
        && int.TryParse(improvement.Element("val")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

    private void ApplyDamageResistanceBonus(XElement cyberware, int value)
    {
        var improvements = _session.Xml.Root!.Element("improvements");
        if (improvements is null)
        {
            improvements = new XElement("improvements");
            _session.Xml.Root!.Add(improvements);
        }

        improvements.Add(new XElement("improvement",
            new XElement("unique", "damageresistance"),
            new XElement("improvedname"),
            new XElement("sourcename", cyberware.Element("guid")!.Value),
            new XElement("min", "0"),
            new XElement("max", "0"),
            new XElement("aug", "0"),
            new XElement("augmax", "0"),
            new XElement("val", value.ToString(CultureInfo.InvariantCulture)),
            new XElement("rating", "1"),
            new XElement("exclude"),
            new XElement("improvementttype", "DamageResistance"),
            new XElement("improvementsource", cyberware.Element("improvementsource")!.Value),
            new XElement("custom", "False"),
            new XElement("customname"),
            new XElement("customid"),
            new XElement("customgroup"),
            new XElement("addtorating", "False"),
            new XElement("enabled", "True"),
            new XElement("order", "0"),
            new XElement("notes")));
    }

    private void UpdateDamageResistanceImprovement(CyberwareItem item)
    {
        if (!TryGetDamageResistanceBonusValue(item.Bonus, item.Rating, out var value))
            return;
        var matching = _session.Xml.Root?.Element("improvements")?.Elements("improvement")
            .Where(i => string.Equals(i.Element("sourcename")?.Value, item.Guid, StringComparison.OrdinalIgnoreCase)
                && IsDamageResistanceImprovement(i)).ToList() ?? [];
        if (matching.Count == 1)
            matching[0].Element("val")!.Value = value.ToString(CultureInfo.InvariantCulture);
    }

    private bool RatingChangeKeepsDamageResistanceImprovement(CyberwareItem item, IReadOnlyCollection<XElement> own) =>
        (TryGetDamageResistanceBonusValue(item.Bonus, item.Rating, out _)
            || TryGetArmorAndDamageResistanceBonusValues(item.Bonus, item.Rating, out _, out _)
            || TryGetBoneDensityBonusValues(item.Bonus, item.Rating, out _, out _))
        && own.Count(IsDamageResistanceImprovement) == 1;
}
