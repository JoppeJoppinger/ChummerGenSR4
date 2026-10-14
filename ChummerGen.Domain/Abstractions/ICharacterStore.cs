using ChummerGen.Domain.Models;

namespace ChummerGen.Domain.Abstractions;

public interface ICharacterStore
{
    Task<IReadOnlyList<CharacterArchive>> ListAsync(CancellationToken cancellationToken);

    Task<CharacterArchive?> GetAsync(Guid characterId, CancellationToken cancellationToken);

    Task<CharacterArchive> SaveAsync(CharacterArchive character, CancellationToken cancellationToken);

    Task DeleteAsync(Guid characterId, CancellationToken cancellationToken);
}
