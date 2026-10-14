using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace ChummerGen.Tests;

public sealed class BrowserWorkflowTests : PageTest
{
    private const string CharacterXml = """
        <character><appversion>467</appversion><name>Fips Donnerfaust</name><alias>Fips</alias><metatype>Dwarf</metatype><karma>12</karma><nuyen>3400</nuyen><bp>25</bp></character>
        """;

    [Fact]
    public async Task Browser_RegistersLogsInImportsViewsAndSeparatesAccounts()
    {
        await using var app = new BrowserTestApplicationFactory();
        var baseAddress = await app.StartAsync();
        var ownerEmail = NewTestEmail();
        var ownerPassword = NewTestPassword();

        await Page.GotoAsync($"{baseAddress}/Account/Register");
        await Page.GetByLabel("Email", new() { Exact = true }).FillAsync(ownerEmail);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(ownerPassword);
        await Page.GetByLabel("Confirm Password", new() { Exact = true }).FillAsync(ownerPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = ownerEmail, Exact = true })).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Abmelden", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Anmelden", Exact = true })).ToBeVisibleAsync();
        await Page.GotoAsync($"{baseAddress}/Account/Login");
        await Page.GetByLabel("Email", new() { Exact = true }).FillAsync(ownerEmail);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(ownerPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = ownerEmail, Exact = true })).ToBeVisibleAsync();

        var circuitStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Page.WebSocket += (_, socket) =>
        {
            if (socket.Url.Contains("/_blazor", StringComparison.Ordinal))
                circuitStarted.TrySetResult();
        };
        await Page.GotoAsync($"{baseAddress}/characters/import");
        await circuitStarted.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await Page.WaitForTimeoutAsync(1500);
        await Page.Locator("#chum-file").SetInputFilesAsync(new FilePayload
        {
            Name = "Fips.chum",
            MimeType = "application/xml",
            Buffer = Encoding.UTF8.GetBytes(CharacterXml)
        });
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Vorschau", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.Locator(".preview-fields")).ToContainTextAsync("Fips Donnerfaust");
        await Expect(Page.Locator(".preview-fields")).ToContainTextAsync("Dwarf");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Import bestätigen", Exact = true }).ClickAsync();

        var characterLink = Page.GetByRole(AriaRole.Link, new() { Name = "Fips", Exact = true });
        await Expect(characterLink).ToBeVisibleAsync();
        var characterPath = await characterLink.GetAttributeAsync("href");
        Assert.False(string.IsNullOrWhiteSpace(characterPath));
        await characterLink.ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Fips Donnerfaust (Fips)", Exact = true })).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Abmelden", Exact = true }).ClickAsync();
        var otherEmail = NewTestEmail();
        var otherPassword = NewTestPassword();
        await Page.GotoAsync($"{baseAddress}/Account/Register");
        await Page.GetByLabel("Email", new() { Exact = true }).FillAsync(otherEmail);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(otherPassword);
        await Page.GetByLabel("Confirm Password", new() { Exact = true }).FillAsync(otherPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = otherEmail, Exact = true })).ToBeVisibleAsync();

        await Page.GotoAsync($"{baseAddress}/characters");
        await Expect(Page.GetByText("Noch keine Charaktere importiert.", new() { Exact = true })).ToBeVisibleAsync();
        await Page.GotoAsync(new Uri(new Uri(baseAddress), characterPath).ToString());
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Nicht gefunden", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Fips Donnerfaust", new() { Exact = true })).ToHaveCountAsync(0);
    }

    private static string NewTestEmail() => $"browser-{Guid.NewGuid():N}@example.test";

    private static string NewTestPassword() => $"Aa1!{Convert.ToHexString(RandomNumberGenerator.GetBytes(24))}";

    private sealed class BrowserTestApplicationFactory : IAsyncDisposable
    {
        private readonly string databaseFile = Path.Combine(AppContext.BaseDirectory, $"browser-e2e-{Guid.NewGuid():N}.db");
        private readonly Process process;
        private readonly StringBuilder output = new();
        private readonly object outputLock = new();
        private readonly string baseAddress;

        public BrowserTestApplicationFactory()
        {
            var port = GetFreePort();
            baseAddress = $"http://127.0.0.1:{port}";
            var webProject = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "ChummerGen.Web"));
            var projectFile = Path.Combine(webProject, "ChummerGen.Web.csproj");
            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = webProject,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("run");
            startInfo.ArgumentList.Add("--no-build");
            startInfo.ArgumentList.Add("--no-launch-profile");
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(projectFile);
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            startInfo.Environment["ASPNETCORE_URLS"] = baseAddress;
            startInfo.Environment["ConnectionStrings__DefaultConnection"] = $"Data Source={databaseFile};Cache=Shared;Pooling=False";
            startInfo.Environment["Database__BackupDirectory"] = AppContext.BaseDirectory;
            startInfo.Environment["InitialAdmin__Email"] = NewTestEmail();
            startInfo.Environment["InitialAdmin__Password"] = NewTestPassword();
            startInfo.Environment["Logging__LogLevel__Default"] = "Warning";
            startInfo.Environment["Logging__LogLevel__Microsoft.EntityFrameworkCore"] = "Warning";
            process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, args) => AppendOutput(args.Data);
            process.ErrorDataReceived += (_, args) => AppendOutput(args.Data);
        }

        public async Task<string> StartAsync()
        {
            if (!process.Start())
                throw new InvalidOperationException("The browser-test web host could not be started.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            while (!timeout.IsCancellationRequested)
            {
                if (process.HasExited)
                    throw new InvalidOperationException($"The browser-test web host exited with code {process.ExitCode}.{Environment.NewLine}{GetOutput()}");
                try
                {
                    using var response = await client.GetAsync($"{baseAddress}/health", timeout.Token);
                    if (response.StatusCode == HttpStatusCode.OK)
                        return baseAddress;
                }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) when (!timeout.IsCancellationRequested) { }
                await Task.Delay(200, timeout.Token);
            }

            throw new TimeoutException($"The browser-test web host did not become ready.{Environment.NewLine}{GetOutput()}");
        }

        public async ValueTask DisposeAsync()
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            process.Dispose();

            foreach (var suffix in new[] { "", "-shm", "-wal" })
            {
                var file = databaseFile + suffix;
                if (File.Exists(file))
                    File.Delete(file);
            }
        }

        private void AppendOutput(string? line)
        {
            if (line is null)
                return;
            lock (outputLock)
                output.AppendLine(line);
        }

        private string GetOutput()
        {
            lock (outputLock)
                return output.ToString();
        }

        private static int GetFreePort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
    }
}
