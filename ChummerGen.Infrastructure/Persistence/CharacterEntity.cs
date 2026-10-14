namespace ChummerGen.Infrastructure.Persistence;

public sealed class CharacterEntity
{
    public Guid Id { get; set; }

    public string OwnerUserId { get; set; } = "";

    public string Name { get; set; } = "";

    public string ChumXml { get; set; } = "";

    public long Revision { get; set; }

    public long UpdatedAtUnixMilliseconds { get; set; }
}
