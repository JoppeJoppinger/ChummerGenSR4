using ChummerGen.Domain.Models;

namespace ChummerGen.Domain.Abstractions;

public interface IRuleCatalog
{
    Task<RuleCatalogDocument?> GetAsync(
        string catalogName,
        string culture,
        CancellationToken cancellationToken);
}
