namespace ChummerGen.Domain.Models;

public sealed record RuleCatalogDocument(
    string CatalogName,
    string Culture,
    string Version,
    string Content);
