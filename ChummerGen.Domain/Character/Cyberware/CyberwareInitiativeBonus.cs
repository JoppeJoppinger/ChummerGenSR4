using System.Globalization;
using System.Xml.Linq;

namespace ChummerGen.Domain.Character.Cyberware;

public sealed partial class CyberwareManager
{
    private static bool TryGetMatrixInitiativePassValue(XElement? bonus, int rating, out int value)
    {
        value = 0;
        if (bonus is not { HasAttributes: false }
            || bonus.Elements().Count() != 1
            || bonus.Element("matrixinitiativepass") is not { HasAttributes: false, HasElements: false } element)
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

    private static bool IsMatrixInitiativePassImprovement(XElement improvement) =>
        improvement.Element("unique")?.Value == "matrixinitiativepass"
        && improvement.Element("improvedname")?.Value == string.Empty
        && improvement.Element("improvementttype")?.Value == "MatrixInitiativePass"
        && improvement.Element("improvementsource")?.Value is "Cyberware" or "Bioware"
        && int.TryParse(improvement.Element("val")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

    private void ApplyMatrixInitiativePass(XElement cyberware, int value)
    {
        var improvements = _session.Xml.Root!.Element("improvements");
        if (improvements is null)
        {
            improvements = new XElement("improvements");
            _session.Xml.Root!.Add(improvements);
        }

        improvements.Add(CreateImprovement(cyberware, "matrixinitiativepass", "MatrixInitiativePass", value,
            cyberware.Element("improvementsource")!.Value));
    }

    private void UpdateMatrixInitiativePass(CyberwareItem item)
    {
        if (!TryGetMatrixInitiativePassValue(item.Bonus, item.Rating, out var value))
            return;
        var matching = _session.Xml.Root?.Element("improvements")?.Elements("improvement")
            .Where(i => string.Equals(i.Element("sourcename")?.Value, item.Guid, StringComparison.OrdinalIgnoreCase)
                && IsMatrixInitiativePassImprovement(i)).ToList() ?? [];
        if (matching.Count == 1)
            matching[0].Element("val")!.Value = value.ToString(CultureInfo.InvariantCulture);
    }

    private bool RatingChangeKeepsMatrixInitiativePass(CyberwareItem item, IReadOnlyCollection<XElement> own) =>
        TryGetMatrixInitiativePassValue(item.Bonus, item.Rating, out _)
        && own.Count(IsMatrixInitiativePassImprovement) == 1;

    private static bool TryGetMatrixInitiativePassAddValue(XElement? bonus, int rating, out int value)
    {
        value = 0;
        if (bonus is not { HasAttributes: false }
            || bonus.Elements().Count() != 1
            || bonus.Element("matrixinitiativepassadd") is not { HasAttributes: false, HasElements: false } element)
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

    private static bool IsMatrixInitiativePassAddImprovement(XElement improvement, string unique) =>
        improvement.Element("unique")?.Value == unique
        && improvement.Element("improvedname")?.Value == string.Empty
        && improvement.Element("improvementttype")?.Value == "MatrixInitiativePass"
        && improvement.Element("improvementsource")?.Value is "Cyberware" or "Bioware"
        && int.TryParse(improvement.Element("val")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

    private void ApplyMatrixInitiativePassAdd(XElement cyberware, int value)
    {
        var improvements = _session.Xml.Root!.Element("improvements");
        if (improvements is null)
        {
            improvements = new XElement("improvements");
            _session.Xml.Root!.Add(improvements);
        }

        var unique = cyberware.Element("bonus")?.Attribute("unique")?.Value ?? string.Empty;
        improvements.Add(CreateImprovement(cyberware, unique, "MatrixInitiativePass", value,
            cyberware.Element("improvementsource")!.Value));
    }

    private void UpdateMatrixInitiativePassAdd(CyberwareItem item)
    {
        if (!TryGetMatrixInitiativePassAddValue(item.Bonus, item.Rating, out var value))
            return;
        var unique = item.Bonus?.Attribute("unique")?.Value ?? string.Empty;
        var matching = _session.Xml.Root?.Element("improvements")?.Elements("improvement")
            .Where(i => string.Equals(i.Element("sourcename")?.Value, item.Guid, StringComparison.OrdinalIgnoreCase)
                && IsMatrixInitiativePassAddImprovement(i, unique)).ToList() ?? [];
        if (matching.Count == 1)
            matching[0].Element("val")!.Value = value.ToString(CultureInfo.InvariantCulture);
    }

    private bool RatingChangeKeepsMatrixInitiativePassAdd(CyberwareItem item, IReadOnlyCollection<XElement> own)
    {
        if (!TryGetMatrixInitiativePassAddValue(item.Bonus, item.Rating, out _))
            return false;
        var unique = item.Bonus?.Attribute("unique")?.Value ?? string.Empty;
        return own.Count(i => IsMatrixInitiativePassAddImprovement(i, unique)) == 1;
    }
}
