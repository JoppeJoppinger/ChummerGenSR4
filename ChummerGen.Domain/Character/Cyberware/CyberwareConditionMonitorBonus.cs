using System.Globalization;
using System.Xml.Linq;

namespace ChummerGen.Domain.Character.Cyberware;

public sealed partial class CyberwareManager
{
    private static bool HasRatingThresholdOffsetBonus(XElement? bonus)
    {
        if (bonus is null || bonus.HasAttributes || bonus.Elements().Count() != 1)
            return false;
        var conditionMonitor = bonus.Element("conditionmonitor");
        if (conditionMonitor is null || conditionMonitor.HasAttributes || conditionMonitor.Elements().Count() != 1)
            return false;
        var thresholdOffset = conditionMonitor.Element("thresholdoffset");
        return thresholdOffset is { HasAttributes: false, HasElements: false }
            && thresholdOffset.Value.Trim() == "Rating";
    }

    private static bool IsRatingThresholdOffsetImprovement(XElement improvement) =>
        improvement.Element("unique")?.Value == string.Empty
        && improvement.Element("improvedname")?.Value == string.Empty
        && improvement.Element("improvementttype")?.Value == "CMThresholdOffset"
        && improvement.Element("improvementsource")?.Value is "Cyberware" or "Bioware"
        && int.TryParse(improvement.Element("val")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

    private void ApplyRatingThresholdOffsetBonus(XElement cyberware)
    {
        var root = _session.Xml.Root!;
        var improvements = root.Element("improvements");
        if (improvements is null)
        {
            improvements = new XElement("improvements");
            root.Add(improvements);
        }

        improvements.Add(new XElement("improvement",
            new XElement("unique"),
            new XElement("improvedname"),
            new XElement("sourcename", cyberware.Element("guid")!.Value),
            new XElement("min", "0"),
            new XElement("max", "0"),
            new XElement("aug", "0"),
            new XElement("augmax", "0"),
            new XElement("val", cyberware.Element("rating")!.Value),
            new XElement("rating", "1"),
            new XElement("exclude"),
            new XElement("improvementttype", "CMThresholdOffset"),
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

    private void UpdateRatingThresholdOffsetImprovement(CyberwareItem item)
    {
        if (!HasRatingThresholdOffsetBonus(item.Bonus))
            return;
        var improvement = _session.Xml.Root?.Element("improvements")?.Elements("improvement")
            .SingleOrDefault(i => string.Equals(i.Element("sourcename")?.Value, item.Guid, StringComparison.OrdinalIgnoreCase)
                && IsRatingThresholdOffsetImprovement(i));
        if (improvement?.Element("val") is { } value)
            value.Value = item.Rating.ToString(CultureInfo.InvariantCulture);
    }
}
