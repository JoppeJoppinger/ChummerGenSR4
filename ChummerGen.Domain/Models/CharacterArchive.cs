namespace ChummerGen.Domain.Models;

public sealed record CharacterArchive(
    Guid Id,
    string OwnerId,
    string Name,
    string ChumXml,
    long Revision,
    DateTimeOffset UpdatedAt);
