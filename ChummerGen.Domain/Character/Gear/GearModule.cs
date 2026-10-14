using System.Xml.Linq;
using ChummerGen.Domain.Abstractions;

namespace ChummerGen.Domain.Character.Gear;

public sealed class GearModule : ICharacterModule
{
    public const string ModuleName = "Gear";

    public string Name => ModuleName;

    public Task RecalculateAsync(
        CharacterSession session,
        IRuleCatalog ruleCatalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CharacterValidationIssue>> ValidateAsync(
        CharacterSession session,
        IRuleCatalog ruleCatalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var issues = new List<CharacterValidationIssue>();
        var items = GetGearItems(session);

        var maxAvail = session.Model.GetIntValue("maxavail") ?? 12;

        var allItems = Flatten(items).ToList();

        foreach (var item in allItems)
        {
            if (item.CapacityRemaining < 0)
            {
                issues.Add(new CharacterValidationIssue(
                    ModuleName,
                    "gear-capacity-exceeded",
                    $"Die Kapazität für '{item.DisplayNameShort}' wurde überschritten ({item.CapacityRemaining}).",
                    CharacterIssueSeverity.Warning));
            }
        }

        var restrictedCount = 0;
        foreach (var rootItem in items)
        {
            if (rootItem.CalculateAvailValue() > maxAvail)
                restrictedCount++;

            foreach (var child in rootItem.Children)
            {
                if (!child.AvailExpression.StartsWith('+') && child.CalculateAvailValue() > maxAvail)
                    restrictedCount++;

                foreach (var subChild in child.Children)
                {
                    if (!subChild.AvailExpression.StartsWith('+') && subChild.CalculateAvailValue() > maxAvail)
                        restrictedCount++;
                }
            }
        }

        if (restrictedCount > 0)
        {
            issues.Add(new CharacterValidationIssue(
                ModuleName,
                "gear-avail-exceeded",
                $"{restrictedCount} Ausrüstungsgegenstände überschreiten die maximale Verfügbarkeit von {maxAvail}.",
                CharacterIssueSeverity.Warning));
        }

        return Task.FromResult<IReadOnlyList<CharacterValidationIssue>>(issues);
    }

    public static IReadOnlyList<GearItem> GetGearItems(CharacterSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var gearsEl = session.Xml.Root?.Element("gears");
        if (gearsEl is null)
            return [];

        return gearsEl.Elements("gear").Select(e => new GearItem(e)).ToList();
    }

    public static int CalculateTotalGearCost(CharacterSession session)
    {
        var items = GetGearItems(session);
        return items.Sum(i => i.TotalCost);
    }

    private static IEnumerable<GearItem> Flatten(IEnumerable<GearItem> items)
    {
        foreach (var item in items)
        {
            yield return item;
            foreach (var child in Flatten(item.Children))
                yield return child;
        }
    }
}

public static class CharacterModelGearExtensions
{
    public static int? GetIntValue(this CharacterModel model, string elementName)
    {
        var val = model.GetValue(elementName);
        return int.TryParse(val, out var res) ? res : null;
    }
}