using System.Globalization;
using System.Xml.Linq;

namespace ChummerGen.Domain.Character.Cyberware;

// Builds <weapon> elements for catalog <addweapon> entries following legacy Weapon.Create/Save (clsEquipment.cs).
internal static class CyberwareWeaponFactory
{
    private const string EmptyGuid = "00000000-0000-0000-0000-000000000000";
    private static readonly string[] RequiredWeaponFields = ["name", "category", "type", "reach", "damage", "ap", "mode", "ammo", "rc", "avail", "cost", "source", "page"];
    private static readonly string[] RequiredModFields = ["name", "slots", "avail", "cost", "source", "page"];

    public static string? GetBlocker(CyberwareCatalog catalog, XElement cyberwareNode, string categoryPrefix = "Cyberware")
    {
        foreach (var add in cyberwareNode.Elements("addweapon"))
        {
            var weapon = catalog.FindWeapon(add.Value, categoryPrefix);
            if (weapon is null)
                return $"Die Cyberware-Waffe '{add.Value}' ist nicht im Waffenkatalog vorhanden.";
            if (RequiredWeaponFields.FirstOrDefault(f => weapon.Element(f) is null) is { } missing)
                return $"Der Waffe '{add.Value}' fehlt das Pflichtfeld '{missing}'.";
            if (!IsInt(weapon, "reach") || !IsInt(weapon, "cost"))
                return $"Die Waffe '{add.Value}' hat ungültige Zahlenwerte für Reichweite oder Kosten.";
            if (weapon.Element("underbarrel") is not null || weapon.Element("accessories") is not null)
                return $"Die Waffe '{add.Value}' enthält Unterlauf-Waffen oder Zubehör; das wird noch nicht unterstützt.";
            if (weapon.Element("range")?.Attribute("multiply") is { } multiply
                && !double.TryParse(multiply.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                return $"Die Waffe '{add.Value}' hat einen ungültigen Reichweiten-Multiplikator.";
            foreach (var modRef in weapon.Element("mods")?.Elements("mod") ?? [])
            {
                var mod = catalog.FindWeaponMod(modRef.Value);
                if (mod is null)
                    return $"Die Waffenmodifikation '{modRef.Value}' ist nicht im Waffenkatalog vorhanden.";
                if (RequiredModFields.FirstOrDefault(f => mod.Element(f) is null) is { } missingMod)
                    return $"Der Waffenmodifikation '{modRef.Value}' fehlt das Pflichtfeld '{missingMod}'.";
                if (!IsInt(mod, "slots"))
                    return $"Die Waffenmodifikation '{modRef.Value}' hat ungültige Slots.";
                if (modRef.Attribute("rating") is { } rating && !int.TryParse(rating.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    return $"Die Waffenmodifikation '{modRef.Value}' hat eine ungültige Stufe.";
            }
        }

        return null;
    }

    public static XElement Build(XElement weapon, CyberwareCatalog catalog)
    {
        string Text(string name) => weapon.Element(name)?.Value ?? string.Empty;
        int IntOr(string name, int fallback) =>
            int.TryParse(weapon.Element(name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        string I(int v) => v.ToString(CultureInfo.InvariantCulture);

        var multiply = 1d;
        if (weapon.Element("range")?.Attribute("multiply") is { } attr)
            multiply = double.Parse(attr.Value, NumberStyles.Float, CultureInfo.InvariantCulture);
        var requireAmmo = !bool.TryParse(weapon.Element("requireammo")?.Value, out var ra) || ra;

        var element = new XElement("weapon",
            new XElement("guid", System.Guid.NewGuid().ToString()),
            new XElement("name", Text("name")),
            new XElement("category", Text("category")),
            new XElement("type", Text("type")),
            new XElement("spec", Text("spec")),
            new XElement("spec2", string.Empty),
            new XElement("reach", I(IntOr("reach", 0))),
            new XElement("damage", Text("damage")),
            new XElement("ap", Text("ap")),
            new XElement("mode", Text("mode")),
            new XElement("rc", Text("rc")),
            new XElement("ammo", Text("ammo")),
            new XElement("ammocategory", Text("ammocategory")),
            new XElement("ammoremaining", "0"),
            new XElement("ammoremaining2", "0"),
            new XElement("ammoremaining3", "0"),
            new XElement("ammoremaining4", "0"),
            new XElement("ammoloaded", EmptyGuid),
            new XElement("ammoloaded2", EmptyGuid),
            new XElement("ammoloaded3", EmptyGuid),
            new XElement("ammoloaded4", EmptyGuid),
            new XElement("conceal", I(IntOr("conceal", 0))),
            new XElement("avail", Text("avail")),
            new XElement("cost", I(IntOr("cost", 0))),
            new XElement("useskill", Text("useskill")),
            new XElement("range", Text("range")),
            new XElement("rangemultiply", multiply.ToString(CultureInfo.InvariantCulture)),
            new XElement("fullburst", I(IntOr("fullburst", 10))),
            new XElement("suppressive", I(IntOr("suppressive", 20))),
            new XElement("source", Text("source")),
            new XElement("page", Text("page")),
            new XElement("weaponname", string.Empty),
            new XElement("included", "False"),
            new XElement("installed", "True"),
            new XElement("requireammo", requireAmmo ? "True" : "False"));

        var mods = (weapon.Element("mods")?.Elements("mod") ?? [])
            .Select(m => BuildMod(catalog.FindWeaponMod(m.Value)!, m))
            .ToList();
        if (mods.Count > 0)
            element.Add(new XElement("weaponmods", mods));

        element.Add(
            new XElement("location", string.Empty),
            new XElement("notes", string.Empty),
            new XElement("discountedcost", "False"));
        return element;
    }

    private static XElement BuildMod(XElement mod, XElement reference)
    {
        string Text(string name) => mod.Element(name)?.Value ?? string.Empty;
        int IntOr(string name, int fallback) =>
            int.TryParse(mod.Element(name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        string I(int v) => v.ToString(CultureInfo.InvariantCulture);

        var rating = reference.Attribute("rating") is { } r ? int.Parse(r.Value, CultureInfo.InvariantCulture) : 0;
        var element = new XElement("weaponmod",
            new XElement("guid", System.Guid.NewGuid().ToString()),
            new XElement("name", Text("name")),
            new XElement("slots", I(IntOr("slots", 0))),
            new XElement("avail", Text("avail")),
            new XElement("rc", Text("rc")),
            new XElement("rcgroup", I(IntOr("rcgroup", 0))),
            new XElement("dvbonus", I(IntOr("dvbonus", 0))),
            new XElement("apbonus", I(IntOr("apbonus", 0))),
            new XElement("conceal", I(IntOr("conceal", 0))),
            new XElement("cost", Text("cost")),
            new XElement("included", "True"),
            new XElement("installed", "True"),
            new XElement("rating", I(rating)));
        if (IntOr("ammobonus", 0) != 0)
            element.Add(new XElement("ammobonus", I(IntOr("ammobonus", 0))));
        if (Text("ammoreplace") != string.Empty)
            element.Add(new XElement("ammoreplace", Text("ammoreplace")));
        if (IntOr("accessorycostmultiplier", 1) != 1)
            element.Add(new XElement("accessorycostmultiplier", I(IntOr("accessorycostmultiplier", 1))));
        if (IntOr("modcostmultiplier", 1) != 1)
            element.Add(new XElement("modcostmultiplier", I(IntOr("modcostmultiplier", 1))));
        if (Text("addmode") != string.Empty)
            element.Add(new XElement("addmode", Text("addmode")));
        if (Text("dicepool") != string.Empty)
            element.Add(new XElement("dicepool", Text("dicepool")));
        element.Add(
            new XElement("fullburst", I(IntOr("fullburst", 0))),
            new XElement("suppressive", I(IntOr("suppressive", 0))),
            new XElement("rangebonus", I(IntOr("rangebonus", 0))),
            new XElement("extra", string.Empty),
            new XElement("source", Text("source")),
            new XElement("page", Text("page")),
            new XElement("notes", string.Empty),
            new XElement("discountedcost", "False"));
        return element;
    }

    private static bool IsInt(XElement node, string field) =>
        int.TryParse(node.Element(field)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
}
