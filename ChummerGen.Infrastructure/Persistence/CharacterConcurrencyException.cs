namespace ChummerGen.Infrastructure.Persistence;

public sealed class CharacterConcurrencyException(Guid characterId, Exception innerException)
    : Exception($"Character '{characterId}' was changed by another request.", innerException);
