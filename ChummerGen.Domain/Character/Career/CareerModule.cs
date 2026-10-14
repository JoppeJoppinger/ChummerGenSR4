using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ChummerGen.Domain.Abstractions;

namespace ChummerGen.Domain.Character.Career;

public sealed class CareerModule : ICharacterModule
{
    public string Name => "Career";

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

        var karma = session.Model.Karma;
        if (karma.HasValue && karma.Value < 0)
        {
            issues.Add(new CharacterValidationIssue(
                Name,
                "career-karma-negative",
                "Karma darf nicht negativ sein.",
                CharacterIssueSeverity.Error));
        }

        var nuyen = session.Model.Nuyen;
        if (nuyen.HasValue && nuyen.Value < 0)
        {
            issues.Add(new CharacterValidationIssue(
                Name,
                "career-nuyen-negative",
                "Nuyen darf nicht negativ sein.",
                CharacterIssueSeverity.Error));
        }

        return Task.FromResult<IReadOnlyList<CharacterValidationIssue>>(issues);
    }
}
