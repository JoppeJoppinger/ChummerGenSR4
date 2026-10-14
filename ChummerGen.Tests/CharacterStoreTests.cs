using ChummerGen.Domain.Abstractions;
using ChummerGen.Domain.Models;
using ChummerGen.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChummerGen.Tests;

public sealed class CharacterStoreTests
{
    [Fact]
    public async Task SaveAsync_PreservesXmlAndCreatesFirstRevision()
    {
        await using var harness = await TestHarness.CreateAsync();
        var store = harness.CreateStore(harness.FirstUser.Id);
        var xml = "<character>\r\n<name>Test</name>\r\n</character>";
        var input = new CharacterArchive(
            Guid.NewGuid(),
            harness.FirstUser.Id,
            "Test",
            xml,
            0,
            DateTimeOffset.UnixEpoch);

        var saved = await store.SaveAsync(input, CancellationToken.None);

        Assert.Equal(xml, saved.ChumXml);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(harness.FirstUser.Id, saved.OwnerId);
    }

    [Fact]
    public async Task OtherUser_CannotReadOrDeleteCharacter()
    {
        await using var harness = await TestHarness.CreateAsync();
        var ownerStore = harness.CreateStore(harness.FirstUser.Id);
        var otherStore = harness.CreateStore(harness.SecondUser.Id);
        var character = await ownerStore.SaveAsync(
            NewCharacter(harness.FirstUser.Id),
            CancellationToken.None);

        Assert.Null(await otherStore.GetAsync(character.Id, CancellationToken.None));
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => otherStore.DeleteAsync(character.Id, CancellationToken.None));
        Assert.NotNull(await ownerStore.GetAsync(character.Id, CancellationToken.None));
    }

    [Fact]
    public async Task SaveAsync_RejectsAStaleRevision()
    {
        await using var harness = await TestHarness.CreateAsync();
        var store = harness.CreateStore(harness.FirstUser.Id);
        var original = await store.SaveAsync(
            NewCharacter(harness.FirstUser.Id),
            CancellationToken.None);
        var changed = await store.SaveAsync(
            original with { Name = "Updated" },
            CancellationToken.None);

        await Assert.ThrowsAsync<CharacterConcurrencyException>(
            () => store.SaveAsync(original with { Name = "Stale" }, CancellationToken.None));
        Assert.Equal("Updated", changed.Name);
    }

    [Fact]
    public async Task SaveAsync_RejectsOwnerSpoofing()
    {
        await using var harness = await TestHarness.CreateAsync();
        var store = harness.CreateStore(harness.FirstUser.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => store.SaveAsync(NewCharacter(harness.SecondUser.Id), CancellationToken.None));
    }

    private static CharacterArchive NewCharacter(string ownerId) =>
        new(Guid.NewGuid(), ownerId, "Test", "<character />", 0, DateTimeOffset.UnixEpoch);

    private sealed class TestHarness : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly ApplicationDbContext dbContext;

        private TestHarness(
            SqliteConnection connection,
            ApplicationDbContext dbContext,
            ApplicationUser firstUser,
            ApplicationUser secondUser)
        {
            this.connection = connection;
            this.dbContext = dbContext;
            FirstUser = firstUser;
            SecondUser = secondUser;
        }

        public ApplicationUser FirstUser { get; }

        public ApplicationUser SecondUser { get; }

        public static async Task<TestHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new ApplicationDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            var firstUser = NewUser("user-one");
            var secondUser = NewUser("user-two");
            dbContext.Users.AddRange(firstUser, secondUser);
            await dbContext.SaveChangesAsync();

            return new TestHarness(connection, dbContext, firstUser, secondUser);
        }

        public ICharacterStore CreateStore(string userId) =>
            new EfCharacterStore(dbContext, new TestCurrentUser(userId));

        public async ValueTask DisposeAsync()
        {
            await dbContext.DisposeAsync();
            await connection.DisposeAsync();
        }

        private static ApplicationUser NewUser(string id) =>
            new()
            {
                Id = id,
                UserName = id,
                NormalizedUserName = id.ToUpperInvariant()
            };
    }

    private sealed class TestCurrentUser(string userId) : ICurrentUser
    {
        public Task<string?> GetUserIdAsync(CancellationToken cancellationToken) =>
            Task.FromResult<string?>(userId);
    }
}
