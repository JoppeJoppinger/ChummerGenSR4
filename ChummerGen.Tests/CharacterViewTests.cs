using ChummerGen.Domain.Abstractions;
using ChummerGen.Domain.Character;
using ChummerGen.Domain.Chum;
using ChummerGen.Domain.Models;
using ChummerGen.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;

namespace ChummerGen.Tests;

public sealed class CharacterViewTests
{
    [Fact]
    public async Task OtherUser_CannotOpenCharacter()
    {
        await using var harness = await TestHarness.CreateAsync();
        var ownerStore = harness.CreateStore(harness.Owner.Id);
        var otherStore = harness.CreateStore(harness.OtherUser.Id);
        var saved = await ownerStore.SaveAsync(NewArchive(harness.Owner.Id), CancellationToken.None);

        Assert.Null(await otherStore.GetAsync(saved.Id, CancellationToken.None));
    }

    [Fact]
    public async Task NameChange_SaveAndReload_PreservesOtherXml()
    {
        await using var harness = await TestHarness.CreateAsync();
        var store = harness.CreateStore(harness.Owner.Id);
        const string xml = "<character><name>Alt</name><alias>Fips</alias><age>32</age><unknown keep=\"yes\"><nested>bleibt</nested></unknown></character>";
        var saved = await store.SaveAsync(NewArchive(harness.Owner.Id, xml), CancellationToken.None);
        var loaded = await store.GetAsync(saved.Id, CancellationToken.None);
        var session = new CharacterSession(ChumSerializer.Parse(loaded!.ChumXml));
        session.Model.Name = "Neu";

        await store.SaveAsync(loaded with { Name = session.Model.Name!, ChumXml = session.Serialize() }, CancellationToken.None);
        var reloaded = await store.GetAsync(saved.Id, CancellationToken.None);
        var expectedXml = ChumSerializer.Parse(xml).Xml;
        expectedXml.Root!.Element("name")!.Value = "Neu";

        Assert.Equal("Neu", ChumSerializer.Parse(reloaded!.ChumXml).Name);
        Assert.True(XNode.DeepEquals(expectedXml, ChumSerializer.Parse(reloaded.ChumXml).Xml));
    }

    [Fact]
    public async Task Save_RejectsStaleRevision()
    {
        await using var harness = await TestHarness.CreateAsync();
        var store = harness.CreateStore(harness.Owner.Id);
        var original = await store.SaveAsync(NewArchive(harness.Owner.Id), CancellationToken.None);
        await store.SaveAsync(original with { Name = "Aktuell" }, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<CharacterConcurrencyException>(
            () => store.SaveAsync(original with { Name = "Veraltet" }, CancellationToken.None));

        Assert.Contains(original.Id.ToString(), exception.Message);
    }

    private static CharacterArchive NewArchive(string ownerId, string xml = "<character><name>Test</name></character>") =>
        new(Guid.NewGuid(), ownerId, "Test", xml, 0, DateTimeOffset.UnixEpoch);

    private sealed class TestHarness : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly ApplicationDbContext dbContext;

        private TestHarness(SqliteConnection connection, ApplicationDbContext dbContext, ApplicationUser owner, ApplicationUser otherUser)
        {
            this.connection = connection;
            this.dbContext = dbContext;
            Owner = owner;
            OtherUser = otherUser;
        }

        public ApplicationUser Owner { get; }
        public ApplicationUser OtherUser { get; }

        public static async Task<TestHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
            var dbContext = new ApplicationDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            var owner = NewUser("view-owner");
            var otherUser = NewUser("view-other");
            dbContext.Users.AddRange(owner, otherUser);
            await dbContext.SaveChangesAsync();
            return new TestHarness(connection, dbContext, owner, otherUser);
        }

        public ICharacterStore CreateStore(string userId) => new EfCharacterStore(dbContext, new TestCurrentUser(userId));

        public async ValueTask DisposeAsync()
        {
            await dbContext.DisposeAsync();
            await connection.DisposeAsync();
        }

        private static ApplicationUser NewUser(string id) => new()
        {
            Id = id,
            UserName = id,
            NormalizedUserName = id.ToUpperInvariant()
        };
    }

    private sealed class TestCurrentUser(string userId) : ICurrentUser
    {
        public Task<string?> GetUserIdAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(userId);
    }
}


