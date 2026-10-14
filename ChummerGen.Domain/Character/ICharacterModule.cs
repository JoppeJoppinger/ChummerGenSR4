using ChummerGen.Domain.Abstractions;

namespace ChummerGen.Domain.Character;

public interface ICharacterModule
{
    string Name { get; }

    Task RecalculateAsync(
        CharacterSession session,
        IRuleCatalog ruleCatalog,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CharacterValidationIssue>> ValidateAsync(
        CharacterSession session,
        IRuleCatalog ruleCatalog,
        CancellationToken cancellationToken = default);
}

public enum CharacterIssueSeverity
{
    Information,
    Warning,
    Error
}

public sealed record CharacterValidationIssue(
    string Module,
    string Code,
    string Message,
    CharacterIssueSeverity Severity);

public sealed record CharacterEngineResult(
    int ModulesExecuted,
    IReadOnlyList<CharacterValidationIssue> Issues);