using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ChummerGen.Domain.Abstractions;
using ChummerGen.Domain.Models;
using ChummerGen.Infrastructure.Import;
using ChummerGen.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChummerGen.Tests;

public sealed class ImportTests
{
    private const string ValidXml = "<character>\r\n<appversion>467</appversion><name>Fips Donnerfaust</name><alias>Fips</alias><metatype>Dwarf</metatype><karma>12</karma><nuyen>3400</nuyen><bp>25</bp></character>";

    [Fact]
    public async Task ValidFile_PreviewsAndSavesOnlyForCurrentUser_AndAllowsDuplicates()
    {
        await using var harness = await ImportHarness.CreateAsync();
        var userAService = harness.CreateService(harness.FirstUser.Id);
        var preview = await userAService.PreviewAsync("Fips.chum", StreamFor(ValidXml));
        Assert.Equal("Fips Donnerfaust", preview.Name);
        Assert.Equal("Fips", preview.Alias);
        Assert.Equal("Dwarf", preview.Metatype);
        Assert.Equal("467", preview.Version);
        Assert.Equal(12, preview.Karma);
        Assert.Equal(3400, preview.Nuyen);
        Assert.Equal(25, preview.BuildPoints);

        var first = await userAService.SaveAsync(preview);
        var second = await userAService.SaveAsync(preview);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(ValidXml, first.ChumXml);
        Assert.Equal(harness.FirstUser.Id, first.OwnerId);
        Assert.Equal(2, (await harness.CreateStore(harness.FirstUser.Id).ListAsync(CancellationToken.None)).Count);
        Assert.Empty(await harness.CreateStore(harness.SecondUser.Id).ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Preview_RejectsOversizedFile()
    {
        await using var harness = await ImportHarness.CreateAsync();
        await using var stream = new MemoryStream(new byte[ChumImportService.MaximumFileSizeBytes + 1]);

        var exception = await Assert.ThrowsAsync<ChumImportException>(
            () => harness.CreateService(harness.FirstUser.Id).PreviewAsync("large.chum", stream));

        Assert.Equal(ChumImportService.MaximumSizeMessage, exception.Message);
    }

    [Fact]
    public async Task Preview_RejectsWrongExtensionAndMalformedXml()
    {
        await using var harness = await ImportHarness.CreateAsync();
        var service = harness.CreateService(harness.FirstUser.Id);

        var extensionError = await Assert.ThrowsAsync<ChumImportException>(
            () => service.PreviewAsync("character.xml", StreamFor(ValidXml)));
        var xmlError = await Assert.ThrowsAsync<ChumImportException>(
            () => service.PreviewAsync("character.chum", StreamFor("<character><name>")));

        Assert.Contains("Endung .chum", extensionError.Message);
        Assert.Contains("ungültiges XML", xmlError.Message);
    }

    [Fact]
    public async Task Preview_RejectsDtd()
    {
        await using var harness = await ImportHarness.CreateAsync();
        const string dtdXml = "<!DOCTYPE character [<!ENTITY xxe SYSTEM 'file:///C:/Windows/win.ini'>]><character><name>&xxe;</name></character>";

        var exception = await Assert.ThrowsAsync<ChumImportException>(
            () => harness.CreateService(harness.FirstUser.Id).PreviewAsync("unsafe.chum", StreamFor(dtdXml)));

        Assert.Contains("verbotene DTD", exception.Message);
    }

    [ReferenceChumFact]
    public async Task ReferenceFile_CanBePreviewedAndImported()
    {
        await using var harness = await ImportHarness.CreateAsync();
        await using var stream = File.OpenRead(ChumRoundtripTests.ReferenceFilePathForTest);
        var preview = await harness.CreateService(harness.FirstUser.Id).PreviewAsync(
            Path.GetFileName(ChumRoundtripTests.ReferenceFilePathForTest),
            stream);

        var saved = await harness.CreateService(harness.FirstUser.Id).SaveAsync(preview);

        Assert.False(string.IsNullOrWhiteSpace(saved.ChumXml));
        Assert.Equal(harness.FirstUser.Id, saved.OwnerId);
    }

    [Fact]
    public async Task Download_Returns404ForAnotherUsersCharacter()
    {
        using var factory = new ImportWebApplicationFactory();
        var (ownerId, otherId) = await factory.SeedTwoUsersAndCharactersAsync();
        Assert.Equal(factory.TestDatabaseFile, factory.Services.GetRequiredService<DatabasePaths>().DatabaseFile);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await factory.SignInAsync(client, factory.OtherUserEmail, factory.OtherUserPassword);

        var foreignResponse = await client.GetAsync($"/characters/{ownerId}/download");
        var ownResponse = await client.GetAsync($"/characters/{otherId}/download");

        Assert.True(foreignResponse.StatusCode == HttpStatusCode.NotFound, $"Foreign download returned {(int)foreignResponse.StatusCode}: {await foreignResponse.Content.ReadAsStringAsync()}");
        Assert.True(ownResponse.StatusCode == HttpStatusCode.OK, $"Own download returned {(int)ownResponse.StatusCode}: {await ownResponse.Content.ReadAsStringAsync()}");
        Assert.Contains("application/xml", ownResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ValidXml, await ownResponse.Content.ReadAsStringAsync());
    }

    private static MemoryStream StreamFor(string xml) => new(Encoding.UTF8.GetBytes(xml));

    private sealed class ImportHarness : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly ApplicationDbContext dbContext;

        private ImportHarness(SqliteConnection connection, ApplicationDbContext dbContext, ApplicationUser firstUser, ApplicationUser secondUser)
        {
            this.connection = connection;
            this.dbContext = dbContext;
            FirstUser = firstUser;
            SecondUser = secondUser;
        }

        public ApplicationUser FirstUser { get; }
        public ApplicationUser SecondUser { get; }

        public static async Task<ImportHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
            var dbContext = new ApplicationDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            var firstUser = CreateUser("import-user-a");
            var secondUser = CreateUser("import-user-b");
            dbContext.Users.AddRange(firstUser, secondUser);
            await dbContext.SaveChangesAsync();
            return new ImportHarness(connection, dbContext, firstUser, secondUser);
        }

        public ICharacterStore CreateStore(string userId) => new EfCharacterStore(dbContext, new TestCurrentUser(userId));

        public ChumImportService CreateService(string userId) => new(CreateStore(userId), new TestCurrentUser(userId));

        public async ValueTask DisposeAsync()
        {
            await dbContext.DisposeAsync();
            await connection.DisposeAsync();
        }

        private static ApplicationUser CreateUser(string id) => new()
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

    private sealed class ImportWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string testRoot = Path.Combine(Path.GetTempPath(), $"chummergen-import-{Guid.NewGuid():N}");

        public ImportWebApplicationFactory()
        {
            Directory.CreateDirectory(testRoot);
            OtherUserEmail = $"other-{Guid.NewGuid():N}@example.test";
            OtherUserPassword = $"Aa1!{Convert.ToHexString(RandomNumberGenerator.GetBytes(24))}";
        }

        public string TestDatabaseFile => Path.Combine(testRoot, "import-tests.db");
        public string OtherUserEmail { get; }
        public string OtherUserPassword { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);
            builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ChummerGen.Web")));
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={TestDatabaseFile};Cache=Shared;Pooling=False");
            builder.UseSetting("Database:BackupDirectory", Path.Combine(testRoot, "backups"));
            builder.UseSetting("InitialAdmin:Email", $"admin-{Guid.NewGuid():N}@example.test");
            builder.UseSetting("InitialAdmin:Password", $"Aa1!{Convert.ToHexString(RandomNumberGenerator.GetBytes(24))}");
        }

        public async Task<(Guid OwnerCharacterId, Guid OtherCharacterId)> SeedTwoUsersAndCharactersAsync()
        {
            using var scope = Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ownerEmail = $"owner-{Guid.NewGuid():N}@example.test";
            var owner = new ApplicationUser { UserName = ownerEmail, Email = ownerEmail, EmailConfirmed = true };
            var other = new ApplicationUser { UserName = OtherUserEmail, Email = OtherUserEmail, EmailConfirmed = true };
            Assert.True((await users.CreateAsync(owner, OtherUserPassword)).Succeeded);
            Assert.True((await users.CreateAsync(other, OtherUserPassword)).Succeeded);

            var ownerId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            dbContext.Characters.AddRange(
                new CharacterEntity { Id = ownerId, OwnerUserId = owner.Id, Name = "Besitz von A", ChumXml = ValidXml, Revision = 1, UpdatedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                new CharacterEntity { Id = otherId, OwnerUserId = other.Id, Name = "Besitz von B", ChumXml = ValidXml, Revision = 1, UpdatedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
            await dbContext.SaveChangesAsync();
            return (ownerId, otherId);
        }

        public async Task SignInAsync(HttpClient client, string email, string password)
        {
            var page = await client.GetAsync("/Account/Login");
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            var form = ReadHiddenForm(await page.Content.ReadAsStringAsync());
            using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(form.Concat(new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = password,
                ["Input.RememberMe"] = "false"
            })));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                if (Directory.Exists(testRoot))
                    Directory.Delete(testRoot, recursive: true);
            }
        }

        private static Dictionary<string, string> ReadHiddenForm(string html)
        {
            var values = Regex.Matches(html, "<input\\b[^>]*>", RegexOptions.IgnoreCase)
                .Select(match => match.Value)
                .Select(markup => new
                {
                    Name = Regex.Match(markup, "\\bname=\"([^\"]+)\"", RegexOptions.IgnoreCase),
                    Value = Regex.Match(markup, "\\bvalue=\"([^\"]*)\"", RegexOptions.IgnoreCase),
                    Type = Regex.Match(markup, "\\btype=\"([^\"]+)\"", RegexOptions.IgnoreCase)
                })
                .Where(input => input.Name.Success && input.Value.Success && input.Type.Success &&
                    string.Equals(input.Type.Groups[1].Value, "hidden", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(input => WebUtility.HtmlDecode(input.Name.Groups[1].Value), input => WebUtility.HtmlDecode(input.Value.Groups[1].Value));
            Assert.Contains(values.Keys, key => key.Contains("RequestVerificationToken", StringComparison.Ordinal));
            return values;
        }
    }
}

