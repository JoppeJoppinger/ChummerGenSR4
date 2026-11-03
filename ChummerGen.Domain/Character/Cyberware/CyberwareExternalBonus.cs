using System.Xml.Linq;

namespace ChummerGen.Domain.Character.Cyberware;

// Wiederverwendung der Cyberware-Bonuslogik fuer andere Quellen (z. B. Qualitaeten). Auswahl-Tags
// (selectside/selectskill/selectskillgroup/selectattribute) sind hier nicht angebunden und bleiben fail-closed.
// Zusaetzlich: Werte-Tags und Charakter-Flags, die nur fuer Nicht-Cyberware-Quellen portiert sind.
public sealed partial class CyberwareManager
{
    // Tag -> (Improvement-Typ, true = Bonus-Unique, false = leer), wie im Legacy CreateImprovements.
    private static readonly Dictionary<string, (string Type, bool UsesBonusUnique)> ExternalValueTags = new(StringComparer.Ordinal)
    {
        ["notoriety"] = ("Notoriety", false),
        ["nuyenamt"] = ("Nuyen", true),
        ["restricteditemcount"] = ("RestrictedItemCount", false),
        ["genetechcostmultiplier"] = ("GenetechCostMultiplier", true),
        ["transgenicsgenetechcost"] = ("TransgenicsBiowareCost", true),
        ["nuyenmaxbp"] = ("NuyenMaxBP", false),
        ["unarmeddv"] = ("UnarmedDV", true),
        ["reach"] = ("Reach", true),
        ["composure"] = ("Composure", false),
        ["judgeintentions"] = ("JudgeIntentions", false),
        ["cyberwareessmultiplier"] = ("CyberwareEssCost", true),
        ["basicbiowareessmultiplier"] = ("BasicBiowareEssCost", true),
        ["biowareessmultiplier"] = ("BiowareEssCost", true),
    };

    // Tag ohne Wert -> (Improvement-Typ, fester Unique oder null = Bonus-Unique, Charakter-Flag oder leer)
    private static readonly Dictionary<string, (string Type, string? FixedUnique, string Flag)> ExternalMarkerTags = new(StringComparer.Ordinal)
    {
        ["sensitivesystem"] = ("SensitiveSystem", "sensitivesystem", ""),
        ["blackmarketdiscount"] = ("BlackMarketDiscount", null, "blackmarket"),
        ["uneducated"] = ("Uneducated", null, "uneducated"),
        ["uncouth"] = ("Uncouth", null, "uncouth"),
        ["infirm"] = ("Infirm", null, "infirm"),
    };

    private static readonly (string Type, string Flag)[] ExternalFlagTypes =
    [
        ("BlackMarketDiscount", "blackmarket"), ("Uneducated", "uneducated"), ("Uncouth", "uncouth"), ("Infirm", "infirm"),
    ];

    private sealed record ExternalExtra(string Type, string Unique, int Value, string Flag);

    private static bool IsExternalTag(XElement e) =>
        ExternalValueTags.ContainsKey(e.Name.LocalName) || ExternalMarkerTags.ContainsKey(e.Name.LocalName);

    // Zerlegt den Bonus in externe Tags und einen Rest fuer die Cyberware-Logik.
    private static bool TrySplitExternalBonus(XElement bonus, out List<ExternalExtra> extras, out XElement? rest)
    {
        extras = [];
        rest = null;
        if (bonus.Nodes().Any(n => n is not XElement) || bonus.Attributes().Any(a => a.Name.LocalName != "unique"))
            return false;
        var unique = bonus.Attribute("unique")?.Value ?? string.Empty;
        var tags = bonus.Elements().Where(IsExternalTag).ToList();
        if (tags.GroupBy(t => t.Name.LocalName).Any(g => g.Count() > 1))
            return false;
        foreach (var tag in tags)
        {
            var name = tag.Name.LocalName;
            if (tag.HasAttributes || tag.HasElements)
                return false;
            if (ExternalValueTags.TryGetValue(name, out var value))
            {
                if (!TryEvaluateSimpleBonusValue(tag.Value, 1, out var number))
                    return false;
                extras.Add(new ExternalExtra(value.Type, value.UsesBonusUnique ? unique : string.Empty, number, string.Empty));
            }
            else
            {
                if (tag.Nodes().Any())
                    return false;
                var marker = ExternalMarkerTags[name];
                extras.Add(new ExternalExtra(marker.Type, marker.FixedUnique ?? unique, 0, marker.Flag));
            }
        }

        var others = bonus.Elements().Where(e => !IsExternalTag(e)).ToList();
        if (others.Count > 0)
            rest = new XElement("bonus", bonus.Attributes(), others);
        return true;
    }

    internal static bool TryParseExternalBonus(XElement? bonus, out bool requiresText)
    {
        requiresText = false;
        if (bonus is null || !bonus.HasElements
            || bonus.Elements().Any(e => e.Name.LocalName is "selectside" or "selectskill" or "selectskillgroup" or "selectattribute")
            || !TrySplitExternalBonus(bonus, out _, out var rest))
            return false;
        if (rest is not null)
        {
            if (!TryGetSimpleBonuses(rest, 1, out _))
                return false;
            requiresText = HasSelectText(rest);
        }
        return true;
    }

    internal static bool TryEvaluateExternalSelectionValue(string text, out int value) =>
        TryEvaluateSimpleBonusValue(text, 1, out value);

    internal static bool TryApplyExternalBonus(CharacterSession session, string sourceGuid, string improvementSource, XElement bonus, string? text)
    {
        if (!TryParseExternalBonus(bonus, out var requiresText)
            || !TrySplitExternalBonus(bonus, out var extras, out var rest)
            || (requiresText && string.IsNullOrWhiteSpace(text)))
            return false;

        IReadOnlyList<SimpleBonus> simple = [];
        if (rest is not null && !TryGetSimpleBonuses(rest, 1, out simple))
            return false;

        var source = new XElement("source", new XElement("guid", sourceGuid), new XElement("improvementsource", improvementSource));
        var manager = new CyberwareManager(session);
        if (requiresText)
            manager.ApplySimpleBonus(source, new SimpleBonus("Text", bonus.Attribute("unique")?.Value ?? string.Empty, 0, text!.Trim()));
        manager.ApplySimpleBonuses(source, simple);
        foreach (var extra in extras)
        {
            manager.ApplySimpleBonus(source, new SimpleBonus(extra.Type, extra.Unique, extra.Value));
            if (extra.Flag.Length > 0)
                session.Xml.Root!.SetElementValue(extra.Flag, "True");
        }
        return true;
    }

    // Legacy RemoveImprovements: Flags fallen zurueck, wenn kein Improvement des Typs mehr existiert.
    internal static void RefreshExternalFlags(CharacterSession session)
    {
        var root = session.Xml.Root;
        if (root is null)
            return;
        var improvements = root.Element("improvements")?.Elements("improvement").ToList() ?? [];
        foreach (var (type, flag) in ExternalFlagTypes)
        {
            if (root.Element(flag) is { } el
                && string.Equals(el.Value, "True", StringComparison.OrdinalIgnoreCase)
                && !improvements.Any(i => i.Element("improvementttype")?.Value == type))
                el.Value = "False";
        }
    }
}
