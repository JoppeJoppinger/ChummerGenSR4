using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ChummerGen.Infrastructure.Persistence;
using ChummerGen.Web.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChummerGen.Tests;

public sealed class AuthFlowTests
{
    [Fact]
    public async Task Registration_CreatesNormalUserAndSignsIn()
    {
        using var factory = new AuthTestApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        Assert.Equal(factory.TestDatabaseFile, factory.Services.GetRequiredService<DatabasePaths>().DatabaseFile);

        var page = await client.GetAsync("/Account/Register");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var form = await ReadFormAsync(page);
        var email = factory.NewTestEmail("user");
        var password = factory.NewTestPassword();

        var response = await client.PostAsync(
            "/Account/Register",
            new FormUrlEncodedContent(form.Concat(new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = password,
                ["Input.ConfirmPassword"] = password
            })));

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, $"Registration returned {(int)response.StatusCode}");
        Assert.Equal("/", response.Headers.Location?.AbsolutePath);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("ChummerGen.Authentication=", StringComparison.Ordinal));

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email);

        Assert.NotNull(user);
        Assert.True(await users.IsInRoleAsync(user, RoleNames.User));
        Assert.False(await users.IsInRoleAsync(user, RoleNames.Administrator));

        var adminPage = await client.GetAsync("/Admin/Users");
        Assert.NotEqual(HttpStatusCode.OK, adminPage.StatusCode);
    }

    [Fact]
    public async Task Registration_RejectsPostsWithoutAntiforgeryToken()
    {
        using var factory = new AuthTestApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var page = await client.GetAsync("/Account/Register");
        var form = await ReadFormAsync(page);
        form.Remove(form.Keys.Single(key => key.Contains("RequestVerificationToken", StringComparison.Ordinal)));
        var email = factory.NewTestEmail("user");
        var password = factory.NewTestPassword();

        var response = await client.PostAsync(
            "/Account/Register",
            new FormUrlEncodedContent(form.Concat(new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = password,
                ["Input.ConfirmPassword"] = password
            })));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await users.FindByEmailAsync(email));
    }

    [Fact]
    public async Task Login_AllowsInitialAdministratorIntoAdminAreaButNotAnonymousVisitors()
    {
        using var factory = new AuthTestApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        Assert.Equal(factory.TestDatabaseFile, factory.Services.GetRequiredService<DatabasePaths>().DatabaseFile);

        var anonymousResponse = await client.GetAsync("/Admin/Users");
        Assert.Equal(HttpStatusCode.Redirect, anonymousResponse.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var administrator = await users.FindByEmailAsync(factory.InitialAdminEmail);
            Assert.True(administrator is not null, $"Initial administrator {factory.InitialAdminEmail} was not found; users={string.Join(",", users.Users.Select(user => user.Email))}");
            Assert.True(await users.CheckPasswordAsync(administrator, factory.InitialAdminPassword));
            Assert.True(await users.IsInRoleAsync(administrator, RoleNames.Administrator));
        }

        var page = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var form = await ReadFormAsync(page);
        var response = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(form.Concat(new Dictionary<string, string>
            {
                ["Input.Email"] = factory.InitialAdminEmail,
                ["Input.Password"] = factory.InitialAdminPassword,
                ["Input.RememberMe"] = "false"
            })));

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, $"Login returned {(int)response.StatusCode}");
        Assert.Equal("/", response.Headers.Location?.AbsolutePath);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("ChummerGen.Authentication=", StringComparison.Ordinal));

        var adminPage = await client.GetAsync("/Admin/Users");
        Assert.Equal(HttpStatusCode.OK, adminPage.StatusCode);
        Assert.Contains(factory.InitialAdminEmail, await adminPage.Content.ReadAsStringAsync());
    }

    private static async Task<Dictionary<string, string>> ReadFormAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync();
        var inputs = Regex.Matches(html, "<input\\b[^>]*>", RegexOptions.IgnoreCase)
            .Select(match => match.Value)
            .Select(markup => new
            {
                Name = Regex.Match(markup, "\\bname=\"([^\"]+)\"", RegexOptions.IgnoreCase),
                Value = Regex.Match(markup, "\\bvalue=\"([^\"]*)\"", RegexOptions.IgnoreCase),
                Type = Regex.Match(markup, "\\btype=\"([^\"]+)\"", RegexOptions.IgnoreCase)
            })
            .Where(field => field.Name.Success && field.Value.Success && field.Type.Success &&
                string.Equals(field.Type.Groups[1].Value, "hidden", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Contains(inputs, field =>
            WebUtility.HtmlDecode(field.Name.Groups[1].Value).Contains("RequestVerificationToken", StringComparison.Ordinal));
        return inputs.ToDictionary(
            field => WebUtility.HtmlDecode(field.Name.Groups[1].Value),
            field => WebUtility.HtmlDecode(field.Value.Groups[1].Value));
    }

    private sealed class AuthTestApplicationFactory : WebApplicationFactory<Program>
    {
        private static readonly SemaphoreSlim EnvironmentLock = new(1, 1);
        private static readonly string[] ConfigurationKeys =
        [
            "ConnectionStrings__DefaultConnection",
            "Database__BackupDirectory",
            "InitialAdmin__Email",
            "InitialAdmin__Password"
        ];
        private readonly string testRoot = Path.Combine(Path.GetTempPath(), $"chummergen-auth-{Guid.NewGuid():N}");
        private readonly Dictionary<string, string?> originalEnvironment = [];
        private int environmentReleased;

        public AuthTestApplicationFactory()
        {
            EnvironmentLock.Wait();
            Directory.CreateDirectory(testRoot);
            InitialAdminEmail = NewTestEmail("admin");
            InitialAdminPassword = NewTestPassword();
            var values = new[]
            {
                $"Data Source={TestDatabaseFile};Cache=Shared",
                Path.Combine(testRoot, "backups"),
                InitialAdminEmail,
                InitialAdminPassword
            };
            for (var index = 0; index < ConfigurationKeys.Length; index++)
            {
                originalEnvironment[ConfigurationKeys[index]] = Environment.GetEnvironmentVariable(ConfigurationKeys[index]);
                Environment.SetEnvironmentVariable(ConfigurationKeys[index], values[index]);
            }
        }

        public string TestDatabaseFile => Path.Combine(testRoot, "chummergen.db");

        public string InitialAdminEmail { get; }

        public string InitialAdminPassword { get; }

        public string NewTestEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@example.test";

        public string NewTestPassword() => $"Aa1!{Convert.ToHexString(RandomNumberGenerator.GetBytes(24))}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);
            builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ChummerGen.Web")));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                SqliteConnection.ClearAllPools();
                foreach (var key in ConfigurationKeys)
                {
                    Environment.SetEnvironmentVariable(key, originalEnvironment[key]);
                }
                if (Directory.Exists(testRoot))
                {
                    Directory.Delete(testRoot, recursive: true);
                }
                if (Interlocked.Exchange(ref environmentReleased, 1) == 0)
                {
                    EnvironmentLock.Release();
                }
            }
        }
    }
}
