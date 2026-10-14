using ChummerGen.Domain.Abstractions;
using ChummerGen.Infrastructure.Localization;
using ChummerGen.Infrastructure.Persistence;
using ChummerGen.Web.Localization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ChummerGen.Tests;

public sealed class LocalizationAccessorTests
{
    [Fact]
    public async Task MigrationAddsPersistedCulturePreference()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new ApplicationDbContext(options);

        await dbContext.Database.MigrateAsync();
        dbContext.Users.Add(new ApplicationUser
        {
            Id = "migrated-user",
            UserName = "migrated-user",
            NormalizedUserName = "MIGRATED-USER",
            PreferredCulture = "fr"
        });
        await dbContext.SaveChangesAsync();

        Assert.Equal("fr", await dbContext.Users
            .Where(user => user.Id == "migrated-user")
            .Select(user => user.PreferredCulture)
            .SingleAsync());
    }

    [Fact]
    public async Task UsesExactDefaultAndLocalizedKeysFromLanguageFiles()
    {
        await using var harness = await TestHarness.CreateAsync();

        Assert.Equal("de", await harness.Accessor.GetCultureAsync());
        Assert.Equal("Charakter-Information",
            await harness.Accessor.GetRequiredStringAsync("Tab_CharacterInfo"));

        await harness.Accessor.SetCultureAsync("fr");
        Assert.Equal("fr", await harness.Accessor.GetCultureAsync());
        Assert.Equal("Informations",
            await harness.Accessor.GetRequiredStringAsync("Tab_CharacterInfo"));
    }

    [Fact]
    public async Task RejectsUnsupportedCultureAndMissingKeyWithoutFallback()
    {
        await using var harness = await TestHarness.CreateAsync();

        await Assert.ThrowsAsync<CultureNotSupportedException>(
            () => harness.Accessor.SetCultureAsync("es"));
        await Assert.ThrowsAsync<LocalizationKeyNotFoundException>(
            () => harness.Accessor.GetRequiredStringAsync("MissingExactKey"));

        Assert.Null((await harness.DbContext.Users.SingleAsync()).PreferredCulture);
    }

    private sealed class TestHarness : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly IMemoryCache cache;

        private TestHarness(
            SqliteConnection connection,
            ApplicationDbContext dbContext,
            ChummerLocalizationService localization,
            IMemoryCache cache)
        {
            this.connection = connection;
            this.cache = cache;
            DbContext = dbContext;
            Accessor = new ChummerLocalizationAccessor(
                localization,
                dbContext,
                new TestCurrentUser("localization-user"));
        }

        public ApplicationDbContext DbContext { get; }

        public IChummerLocalizationAccessor Accessor { get; }

        public static async Task<TestHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new ApplicationDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.Users.Add(new ApplicationUser
            {
                Id = "localization-user",
                UserName = "localization-user",
                NormalizedUserName = "LOCALIZATION-USER"
            });
            await dbContext.SaveChangesAsync();

            var repoRoot = FindRepositoryRoot();
            var cache = new MemoryCache(new MemoryCacheOptions());
            var localization = new ChummerLocalizationService(
                Path.Combine(repoRoot, "bin", "lang"),
                cache);
            return new TestHarness(connection, dbContext, localization, cache);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await connection.DisposeAsync();
            cache.Dispose();
        }

        private static string FindRepositoryRoot()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
                 directory is not null;
                 directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "bin", "lang")))
                    return directory.FullName;
            }

            throw new DirectoryNotFoundException("Repository root containing bin/lang could not be located.");
        }
    }

    private sealed class TestCurrentUser(string userId) : ICurrentUser
    {
        public Task<string?> GetUserIdAsync(CancellationToken cancellationToken) =>
            Task.FromResult<string?>(userId);
    }
}
