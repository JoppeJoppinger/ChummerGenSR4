using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;

namespace ChummerGen.Infrastructure.Persistence;

public sealed class SqliteDatabaseBackupService(DatabasePaths paths) : IDatabaseBackupService
{
    public async Task<string> CreateBackupAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(paths.BackupDirectory);

        var backupName = $"chummergen-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.db";
        var backupPath = Path.Combine(paths.BackupDirectory, backupName);
        var sourceConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabaseFile,
            Mode = SqliteOpenMode.ReadOnly,
            DefaultTimeout = 10
        }.ToString();
        var destinationConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = backupPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 10
        }.ToString();

        await using var source = new SqliteConnection(sourceConnectionString);
        await source.OpenAsync(cancellationToken);
        await using var destination = new SqliteConnection(destinationConnectionString);
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);

        return backupName;
    }
}
