using System.Xml.Linq;

namespace ChummerGen.Domain.Character.Cyberware;

// Legacy ImprovementManager.CreateImprovements "addattribute" for MAG/RES: sets Character.MAGEnabled/RESEnabled and records
// CreateImprovement(name, source, guid, Attribute, "enableattribute", value 0, rating 0). Removal (RemoveImprovements)
// clears the flag when no improvement with another sourcename still enables the same attribute.
public sealed partial class CyberwareManager
{
    private static string? GetEnableAttribute(XElement? bonus)
    {
        if (bonus is null || bonus.HasAttributes || bonus.Nodes().Count() != 1)
            return null;
        if (bonus.Element("addattribute") is not { HasAttributes: false } add || add.Nodes().Count() != 1)
            return null;
        if (add.Element("name") is not { HasAttributes: false, HasElements: false } name)
            return null;
        return name.Value is "MAG" or "RES" ? name.Value : null;
    }

    private static string FlagElementName(string attribute) => attribute == "MAG" ? "magenabled" : "resenabled";

    private static bool IsEnableImprovement(XElement i, string? attribute = null) =>
        i.Element("unique")?.Value == "enableattribute"
        && i.Element("improvementttype")?.Value == "Attribute"
        && (attribute is null || i.Element("improvedname")?.Value == attribute);

    private void ApplyEnableBonus(XElement cyberware, string attribute)
    {
        var root = _session.Xml.Root!;
        var flag = root.Element(FlagElementName(attribute));
        if (flag is null)
            root.Add(new XElement(FlagElementName(attribute), "True"));
        else
            flag.Value = "True";

        var improvements = root.Element("improvements");
        if (improvements is null)
        {
            improvements = new XElement("improvements");
            root.Add(improvements);
        }

        improvements.Add(new XElement("improvement",
            new XElement("unique", "enableattribute"),
            new XElement("improvedname", attribute),
            new XElement("sourcename", cyberware.Element("guid")!.Value),
            new XElement("min", "0"),
            new XElement("max", "0"),
            new XElement("aug", "0"),
            new XElement("augmax", "0"),
            new XElement("val", "0"),
            new XElement("rating", "0"),
            new XElement("exclude"),
            new XElement("improvementttype", "Attribute"),
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

    // Rating changes are allowed only when every linked improvement can be recalculated by a supported bonus handler.
    private bool RatingChangeKeepsImprovements(CyberwareItem item)
    {
        var improvements = _session.Xml.Root?.Element("improvements")?.Elements("improvement").ToList() ?? [];
        foreach (var piece in item.SelfAndDescendants())
        {
            var own = improvements.Where(i => string.Equals(i.Element("sourcename")?.Value, piece.Guid, StringComparison.OrdinalIgnoreCase)).ToList();
            if (own.Any(i => !IsEnableImprovement(i) && !IsRatingThresholdOffsetImprovement(i)
                && !IsArmorImprovement(i) && !IsDamageResistanceImprovement(i)
                && !IsBoneDensityUnarmedImprovement(i, "UnarmedDV") && !IsBoneDensityUnarmedImprovement(i, "UnarmedDVPhysical")
                && !IsMatrixInitiativePassImprovement(i)
                && !(TryGetSimpleBonuses(piece.Bonus, piece.Rating, out var simpleOwn) && IsAnySimpleBonusImprovement(i, simpleOwn))
                && !IsMatrixInitiativePassAddImprovement(i, piece.Bonus?.Attribute("unique")?.Value ?? string.Empty)))
                return false;
            var attribute = piece.Bonus is null ? null : GetEnableAttribute(piece.Bonus);
            var thresholdOffset = HasRatingThresholdOffsetBonus(piece.Bonus);
            var armorBonus = piece.Bonus is not null && (TryGetArmorBonusValues(piece.Bonus, piece.Rating, out _)
                || TryGetArmorAndDamageResistanceBonusValues(piece.Bonus, piece.Rating, out _, out _));
            var damageResistance = piece.Bonus is not null && (TryGetDamageResistanceBonusValue(piece.Bonus, piece.Rating, out _)
                || TryGetArmorAndDamageResistanceBonusValues(piece.Bonus, piece.Rating, out _, out _)
                || TryGetBoneDensityBonusValues(piece.Bonus, piece.Rating, out _, out _));
            var boneDensity = piece.Bonus is not null && TryGetBoneDensityBonusValues(piece.Bonus, piece.Rating, out _, out _);
            var matrixPass = piece.Bonus is not null && TryGetMatrixInitiativePassValue(piece.Bonus, piece.Rating, out _);
            var matrixPassAdd = piece.Bonus is not null && TryGetMatrixInitiativePassAddValue(piece.Bonus, piece.Rating, out _);
            var simpleBonus = piece.Bonus is not null && TryGetSimpleBonuses(piece.Bonus, piece.Rating, out _);
            if (piece.Bonus is not null && attribute is null && !thresholdOffset && !armorBonus && !damageResistance && !matrixPass && !matrixPassAdd && !simpleBonus)
                return false;
            if (simpleBonus && !RatingChangeKeepsSimpleBonus(piece, own))
                return false;
            if (attribute is not null && !own.Any(i => IsEnableImprovement(i, attribute)))
                return false;
            if (thresholdOffset && own.Count(IsRatingThresholdOffsetImprovement) != 1)
                return false;
            if (armorBonus && !RatingChangeKeepsArmorImprovements(piece, own))
                return false;
            if (damageResistance && !RatingChangeKeepsDamageResistanceImprovement(piece, own))
                return false;
            if (boneDensity && !RatingChangeKeepsBoneDensityUnarmedImprovements(piece, own))
                return false;
            if (matrixPass && !RatingChangeKeepsMatrixInitiativePass(piece, own))
                return false;
            if (matrixPassAdd && !RatingChangeKeepsMatrixInitiativePassAdd(piece, own))
                return false;
        }

        return true;
    }

    private void RecalculateEnabledFlags(IEnumerable<XElement> removed)
    {
        var root = _session.Xml.Root!;
        var remaining = root.Element("improvements")?.Elements("improvement").ToList() ?? [];
        foreach (var gone in removed.Where(i => IsEnableImprovement(i)))
        {
            var attribute = gone.Element("improvedname")?.Value;
            if (attribute is not ("MAG" or "RES"))
                continue;
            var sourceName = gone.Element("sourcename")?.Value;
            var stillEnabled = remaining.Any(i => i.Element("sourcename")?.Value != sourceName && IsEnableImprovement(i, attribute));
            if (!stillEnabled)
            {
                var flag = root.Element(FlagElementName(attribute));
                if (flag is null)
                    root.Add(new XElement(FlagElementName(attribute), "False"));
                else
                    flag.Value = "False";
            }
        }
    }
}
