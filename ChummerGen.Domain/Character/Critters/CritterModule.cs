using ChummerGen.Domain.Abstractions;

namespace ChummerGen.Domain.Character.Critters;

public sealed class CritterModule : ICharacterModule
{
    public string Name => "Critter";

    public Task RecalculateAsync(
        CharacterSession session,
        IRuleCatalog ruleCatalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var model = new CritterCharacterModel(session);
        if (model.IsCritter || model.CritterEnabled)
        {
            model.EnsureCritterContainer();
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CharacterValidationIssue>> ValidateAsync(
        CharacterSession session,
        IRuleCatalog ruleCatalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var issues = new List<CharacterValidationIssue>();
        var model = new CritterCharacterModel(session);

        foreach (var power in model.Powers)
        {
            if (string.IsNullOrWhiteSpace(power.Name))
            {
                issues.Add(new CharacterValidationIssue(
                    Name,
                    "critter-power-missing-name",
                    $"Critter-Kraft mit GUID '{power.Guid}' hat keinen Namen.",
                    CharacterIssueSeverity.Error));
            }

            if (power.Points < 0)
            {
                issues.Add(new CharacterValidationIssue(
                    Name,
                    "critter-power-negative-points",
                    $"Critter-Kraft '{power.Name}' hat negative Kraftpunkte ({power.Points}).",
                    CharacterIssueSeverity.Warning));
            }
        }

        if (model.IsCritter && string.IsNullOrWhiteSpace(session.Model.Metatype))
        {
            issues.Add(new CharacterValidationIssue(
                Name,
                "critter-missing-metatype",
                "Charakter ist als Kritter markiert, hat aber keinen Metatyp zugewiesen.",
                CharacterIssueSeverity.Warning));
        }

        return Task.FromResult<IReadOnlyList<CharacterValidationIssue>>(issues);
    }
}
