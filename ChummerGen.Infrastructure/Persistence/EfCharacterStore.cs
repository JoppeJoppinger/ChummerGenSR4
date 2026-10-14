using ChummerGen.Domain.Abstractions;
using ChummerGen.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace ChummerGen.Infrastructure.Persistence;

public sealed class EfCharacterStore(ApplicationDbContext dbContext, ICurrentUser currentUser)
    : ICharacterStore
{
    public async Task<IReadOnlyList<CharacterArchive>> ListAsync(CancellationToken cancellationToken)
    {
        var ownerId = await RequireUserIdAsync(cancellationToken);

        return await dbContext.Characters
            .AsNoTracking()
            .Where(character => character.OwnerUserId == ownerId)
            .OrderByDescending(character => character.UpdatedAtUnixMilliseconds)
            .Select(character => new CharacterArchive(
                character.Id,
                character.OwnerUserId,
                character.Name,
                character.ChumXml,
                character.Revision,
                DateTimeOffset.FromUnixTimeMilliseconds(character.UpdatedAtUnixMilliseconds)))
            .ToListAsync(cancellationToken);
    }

    public async Task<CharacterArchive?> GetAsync(Guid characterId, CancellationToken cancellationToken)
    {
        var ownerId = await RequireUserIdAsync(cancellationToken);

        return await dbContext.Characters
            .AsNoTracking()
            .Where(character => character.Id == characterId && character.OwnerUserId == ownerId)
            .Select(character => new CharacterArchive(
                character.Id,
                character.OwnerUserId,
                character.Name,
                character.ChumXml,
                character.Revision,
                DateTimeOffset.FromUnixTimeMilliseconds(character.UpdatedAtUnixMilliseconds)))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<CharacterArchive> SaveAsync(CharacterArchive character, CancellationToken cancellationToken)
    {
        var ownerId = await RequireUserIdAsync(cancellationToken);
        if (!string.Equals(character.OwnerId, ownerId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Characters can only be saved by their owner.");
        }

        var existing = await dbContext.Characters
            .SingleOrDefaultAsync(
                item => item.Id == character.Id && item.OwnerUserId == ownerId,
                cancellationToken);

        if (existing is null)
        {
            if (character.Revision != 0)
            {
                throw new CharacterConcurrencyException(character.Id, new InvalidOperationException("The character no longer exists."));
            }

            existing = new CharacterEntity
            {
                Id = character.Id == Guid.Empty ? Guid.NewGuid() : character.Id,
                OwnerUserId = ownerId,
                Revision = 1
            };
            dbContext.Characters.Add(existing);
        }
        else
        {
            if (existing.Revision != character.Revision)
            {
                throw new CharacterConcurrencyException(character.Id, new InvalidOperationException("The character revision is stale."));
            }

            dbContext.Entry(existing).Property(item => item.Revision).OriginalValue = character.Revision;
            existing.Revision++;
        }

        existing.Name = character.Name;
        existing.ChumXml = character.ChumXml;
        existing.UpdatedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new CharacterConcurrencyException(character.Id, exception);
        }

        return new CharacterArchive(
            existing.Id,
            existing.OwnerUserId,
            existing.Name,
            existing.ChumXml,
            existing.Revision,
            DateTimeOffset.FromUnixTimeMilliseconds(existing.UpdatedAtUnixMilliseconds));
    }

    public async Task DeleteAsync(Guid characterId, CancellationToken cancellationToken)
    {
        var ownerId = await RequireUserIdAsync(cancellationToken);
        var deleted = await dbContext.Characters
            .Where(character => character.Id == characterId && character.OwnerUserId == ownerId)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            throw new KeyNotFoundException("The character does not exist or is not owned by the current user.");
        }
    }

    private async Task<string> RequireUserIdAsync(CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetUserIdAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(ownerId)
            ? throw new UnauthorizedAccessException("A signed-in user is required.")
            : ownerId;
    }
}
