using System.Xml.Linq;

namespace ChummerGen.Domain.Character.Cyberware;

// Wiederverwendung der Cyberware-Bonuslogik fuer andere Quellen (z. B. Qualitaeten). Auswahl-Tags
// (selectside/selectskill/selectskillgroup/selectattribute) sind hier nicht angebunden und bleiben fail-closed.
public sealed partial class CyberwareManager
{
    internal static bool TryParseExternalBonus(XElement? bonus, out bool requiresText)
    {
        requiresText = false;
        if (bonus is null || !bonus.HasElements
            || bonus.Elements().Any(e => e.Name.LocalName is "selectside" or "selectskill" or "selectskillgroup" or "selectattribute")
            || !TryGetSimpleBonuses(bonus, 1, out _))
            return false;
        requiresText = HasSelectText(bonus);
        return true;
    }

    internal static bool TryApplyExternalBonus(CharacterSession session, string sourceGuid, string improvementSource, XElement bonus, string? text)
    {
        if (!TryParseExternalBonus(bonus, out var requiresText)
            || !TryGetSimpleBonuses(bonus, 1, out var simple)
            || (requiresText && string.IsNullOrWhiteSpace(text)))
            return false;

        var source = new XElement("source", new XElement("guid", sourceGuid), new XElement("improvementsource", improvementSource));
        var manager = new CyberwareManager(session);
        if (requiresText)
            manager.ApplySimpleBonus(source, new SimpleBonus("Text", bonus.Attribute("unique")?.Value ?? string.Empty, 0, text!.Trim()));
        manager.ApplySimpleBonuses(source, simple);
        return true;
    }
}
