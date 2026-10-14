namespace ChummerGen.Domain.Abstractions;

public interface ICurrentUser
{
    Task<string?> GetUserIdAsync(CancellationToken cancellationToken);
}
