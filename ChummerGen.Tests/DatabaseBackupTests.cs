using ChummerGen.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ChummerGen.Tests;

public sealed class DatabaseBackupTests
{
    [Fact]
    public async Task BackupCanBeRestoredToSeparateDatabaseAndReadIdentityAndCharacterData()
    {
        var root = Path.Combine(Path.GetTempPath(), $"chummergen-restore-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var databasePath = Path.Combine(root, "Data", "chummergen.db");
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            var characterId = Guid.NewGuid();
            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.EnsureCreatedAsync();
                dbContext.Users.Add(new ApplicationUser
                {
                    Id = "restore-user",
                    UserName = "restore-user",
                    NormalizedUserName = "RESTORE-USER"
                });
                dbContext.Characters.Add(new CharacterEntity
                {
                    Id = characterId,
                    OwnerUserId = "restore-user",
                    Name = "Restored character",
                    ChumXml = "<character />",
                    Revision = 3,
                    UpdatedAtUnixMilliseconds = 123
                });
                await dbContext.SaveChangesAsync();
            }

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Data Source=Data/chummergen.db",
                    ["Database:BackupDirectory"] = "Data/backups"
                })
                .Build();
            var environment = new TestHostEnvironment(root);
            var services = new ServiceCollection();
            services.AddChummerPersistence(configuration, environment);
            using var provider = services.BuildServiceProvider();
            var backupService = provider.GetRequiredService<IDatabaseBackupService>();
            var backupName = await backupService.CreateBackupAsync(CancellationToken.None);
            var backupPath = Path.Combine(root, "Data", "backups", backupName);
            var restorePath = Path.Combine(root, "Data", "restored.db");

            var backupConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = backupPath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();
            var restoreConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = restorePath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();
            await using (var backupConnection = new SqliteConnection(backupConnectionString))
            {
                await backupConnection.OpenAsync();
                await using var restoreConnection = new SqliteConnection(restoreConnectionString);
                await restoreConnection.OpenAsync();
                backupConnection.BackupDatabase(restoreConnection);
            }

            var restoreOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(restoreConnectionString)
                .Options;
            await using (var restoredContext = new ApplicationDbContext(restoreOptions))
            {
                var restoredUser = await restoredContext.Users.SingleAsync(user => user.Id == "restore-user");
                var restoredCharacter = await restoredContext.Characters.SingleAsync(character => character.Id == characterId);

                Assert.Equal("restore-user", restoredUser.UserName);
                Assert.Equal("restore-user", restoredCharacter.OwnerUserId);
                Assert.Equal("Restored character", restoredCharacter.Name);
                Assert.Equal("<character />", restoredCharacter.ChumXml);
                Assert.Equal(3, restoredCharacter.Revision);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task BackupAsync_CopiesTheSqliteDatabase()
    {
        var root = Path.Combine(Path.GetTempPath(), $"chummergen-backup-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var databasePath = Path.Combine(root, "Data", "chummergen.db");
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.EnsureCreatedAsync();
                dbContext.Users.Add(new ApplicationUser
                {
                    Id = "backup-user",
                    UserName = "backup-user",
                    NormalizedUserName = "BACKUP-USER"
                });
                await dbContext.SaveChangesAsync();
            }

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Data Source=Data/chummergen.db",
                    ["Database:BackupDirectory"] = "Data/backups"
                })
                .Build();
            var environment = new TestHostEnvironment(root);
            var services = new ServiceCollection();
            services.AddChummerPersistence(configuration, environment);
            using var provider = services.BuildServiceProvider();
            var backupService = provider.GetRequiredService<IDatabaseBackupService>();

            var backupName = await backupService.CreateBackupAsync(CancellationToken.None);
            var backupPath = Path.Combine(root, "Data", "backups", backupName);
            var backupOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"Data Source={backupPath}")
                .Options;
            await using (var backupContext = new ApplicationDbContext(backupOptions))
            {
                Assert.Equal("backup-user", await backupContext.Users.Select(user => user.Id).SingleAsync());
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = nameof(DatabaseBackupTests);

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
