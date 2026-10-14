namespace ChummerGen.Infrastructure.Persistence;

public interface IDatabaseBackupService
{
    Task<string> CreateBackupAsync(CancellationToken cancellationToken);
}
