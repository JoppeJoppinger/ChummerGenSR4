using ChummerGen.Domain.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChummerGen.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddChummerPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var configuredConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
        var connectionString = new SqliteConnectionStringBuilder(configuredConnectionString);
        if (!Path.IsPathRooted(connectionString.DataSource))
        {
            connectionString.DataSource = Path.GetFullPath(
                Path.Combine(environment.ContentRootPath, connectionString.DataSource));
        }

        var backupDirectory = configuration["Database:BackupDirectory"] ?? "Data/backups";
        if (!Path.IsPathRooted(backupDirectory))
        {
            backupDirectory = Path.GetFullPath(Path.Combine(environment.ContentRootPath, backupDirectory));
        }

        var databaseDirectory = Path.GetDirectoryName(connectionString.DataSource);
        if (!string.IsNullOrEmpty(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        connectionString.DefaultTimeout = 10;

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(connectionString.ToString()));
        services.AddSingleton(new DatabasePaths(connectionString.DataSource, backupDirectory));
        services.AddScoped<ICharacterStore, EfCharacterStore>();
        services.AddScoped<IDatabaseBackupService, SqliteDatabaseBackupService>();

        return services;
    }
}
