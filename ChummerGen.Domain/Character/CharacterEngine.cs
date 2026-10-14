using ChummerGen.Domain.Abstractions;

namespace ChummerGen.Domain.Character;

public sealed class CharacterEngine(IEnumerable<ICharacterModule> modules)
{
    private readonly ICharacterModule[] _modules = modules?.ToArray()
        ?? throw new ArgumentNullException(nameof(modules));

    public IReadOnlyList<string> ModuleNames => _modules.Select(module => module.Name).ToArray();

    public async Task<CharacterEngineResult> RunAsync(
        CharacterSession session,
        IRuleCatalog ruleCatalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(ruleCatalog);

        foreach (var module in _modules)
            await module.RecalculateAsync(session, ruleCatalog, cancellationToken).ConfigureAwait(false);

        var issues = new List<CharacterValidationIssue>();
        foreach (var module in _modules)
        {
            var moduleIssues = await module.ValidateAsync(session, ruleCatalog, cancellationToken).ConfigureAwait(false);
            if (moduleIssues is not null)
                issues.AddRange(moduleIssues);
        }

        return new CharacterEngineResult(_modules.Length, issues);
    }
}