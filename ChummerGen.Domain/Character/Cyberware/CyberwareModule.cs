using ChummerGen.Domain.Abstractions;

namespace ChummerGen.Domain.Character.Cyberware;

public sealed class CyberwareModule : ICharacterModule
{
    public const string ModuleName = "Cyberware";

    public string Name => ModuleName;

    public Task RecalculateAsync(
        CharacterSession session,
        IRuleCatalog ruleCatalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<CharacterValidationIssue>> ValidateAsync(
        CharacterSession session,
        IRuleCatalog ruleCatalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(ruleCatalog);

        var catalog = await CyberwareCatalog.LoadAsync(ruleCatalog, cancellationToken: cancellationToken).ConfigureAwait(false);
        var manager = new CyberwareManager(session, catalog);
        var issues = new List<CharacterValidationIssue>();

        foreach (var item in manager.GetAllItems())
        {
            var grades = manager.GradesFor(item);
            if (grades.Grades.Count > 0 && !grades.Contains(item.GradeName))
            {
                issues.Add(new CharacterValidationIssue(ModuleName, "cyberware-unknown-grade",
                    $"Der Grad '{item.GradeName}' von '{item.DisplayNameShort}' ist unbekannt.",
                    CharacterIssueSeverity.Warning));
            }

            if (item.MaxRating > 0 && (item.Rating < item.MinRating || item.Rating > item.MaxRating))
            {
                issues.Add(new CharacterValidationIssue(ModuleName, "cyberware-rating-range",
                    $"Die Stufe von '{item.DisplayNameShort}' liegt außerhalb von {item.MinRating}–{item.MaxRating}.",
                    CharacterIssueSeverity.Warning));
            }

            if (session.Settings.EnforceCapacity && item.HasCapacity && item.CapacityRemaining < 0)
            {
                issues.Add(new CharacterValidationIssue(ModuleName, "cyberware-capacity-exceeded",
                    $"Die Kapazität für '{item.DisplayNameShort}' wurde überschritten ({item.CapacityRemaining}).",
                    CharacterIssueSeverity.Warning));
            }
        }

        return issues;
    }
}