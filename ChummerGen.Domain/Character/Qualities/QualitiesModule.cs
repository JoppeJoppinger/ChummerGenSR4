using ChummerGen.Domain.Abstractions;

namespace ChummerGen.Domain.Character.Qualities;

public sealed class QualitiesModule : ICharacterModule
{
    public const string ModuleName = "Qualities";
    public string Name => ModuleName;

    public const int DefaultBpLimit = 35;
    public const int DefaultKarmaLimit = 70;

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
        var manager = new QualitiesManager(session);
        var summary = manager.CalculateSummary();
        var settings = session.Settings;
        var isBpMethod = string.Equals(settings.BuildMethod, "BP", StringComparison.OrdinalIgnoreCase);

        if (!settings.ExceedPositiveQualities)
        {
            if (isBpMethod && summary.PositiveBpTotal > DefaultBpLimit)
            {
                issues.Add(new CharacterValidationIssue(
                    Module: Name,
                    Code: "qualities-positive-bp-limit",
                    Message: $"Gaben übersteigen das Limit von {DefaultBpLimit} GP (aktuell: {summary.PositiveBpTotal} GP).",
                    Severity: CharacterIssueSeverity.Warning));
            }
            else if (!isBpMethod && summary.PositiveKarmaTotal > DefaultKarmaLimit)
            {
                issues.Add(new CharacterValidationIssue(
                    Module: Name,
                    Code: "qualities-positive-karma-limit",
                    Message: $"Gaben übersteigen das Limit von {DefaultKarmaLimit} Karma (aktuell: {summary.PositiveKarmaTotal} Karma).",
                    Severity: CharacterIssueSeverity.Warning));
            }
        }

        if (!settings.ExceedNegativeQualities)
        {
            // Negative qualities have negative BP, e.g. -5, -10; limit is 35 (or -35)
            if (isBpMethod && Math.Abs(summary.NegativeBpTotal) > DefaultBpLimit)
            {
                issues.Add(new CharacterValidationIssue(
                    Module: Name,
                    Code: "qualities-negative-bp-limit",
                    Message: $"Handicaps übersteigen das Limit von {DefaultBpLimit} GP (aktuell: {Math.Abs(summary.NegativeBpTotal)} GP).",
                    Severity: CharacterIssueSeverity.Warning));
            }
            else if (!isBpMethod && Math.Abs(summary.NegativeKarmaTotal) > DefaultKarmaLimit)
            {
                issues.Add(new CharacterValidationIssue(
                    Module: Name,
                    Code: "qualities-negative-karma-limit",
                    Message: $"Handicaps übersteigen das Limit von {DefaultKarmaLimit} Karma (aktuell: {Math.Abs(summary.NegativeKarmaTotal)} Karma).",
                    Severity: CharacterIssueSeverity.Warning));
            }
        }

        return Task.FromResult<IReadOnlyList<CharacterValidationIssue>>(issues);
    }
}
