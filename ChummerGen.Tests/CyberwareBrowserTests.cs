using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace ChummerGen.Tests;

public sealed class CyberwareBrowserTests : PageTest
{
    private const string CharacterXml = """
        <character><appversion>467</appversion><name>Fips Donnerfaust</name><alias>Fips</alias><metatype>Dwarf</metatype><karma>12</karma><nuyen>3400</nuyen><bp>25</bp><cyberwares /><weapons /></character>
        """;

    [Fact]
    public async Task Browser_AddsEditsPersistsAndBlocksUnsupportedCyberware()
    {
        await using var app = new BrowserTestApplicationFactory();
        var baseAddress = await app.StartAsync();
        var email = NewTestEmail();
        var password = NewTestPassword();

        await Page.GotoAsync($"{baseAddress}/Account/Register");
        await Page.GetByLabel("Email", new() { Exact = true }).FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await Page.GetByLabel("Confirm Password", new() { Exact = true }).FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = email, Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Abmelden", Exact = true }).ClickAsync();
        await Page.GotoAsync($"{baseAddress}/Account/Login");
        await Page.GetByLabel("Email", new() { Exact = true }).FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = email, Exact = true })).ToBeVisibleAsync();

        await NavigateAndWaitForCircuitAsync($"{baseAddress}/characters/import");
        await Page.WaitForTimeoutAsync(1500);
        await Page.Locator("#chum-file").SetInputFilesAsync(new FilePayload
        {
            Name = "Fips.chum",
            MimeType = "application/xml",
            Buffer = Encoding.UTF8.GetBytes(CharacterXml)
        });
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Vorschau", Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Import bestätigen", Exact = true }).ClickAsync();
        var characterLink = Page.GetByRole(AriaRole.Link, new() { Name = "Fips", Exact = true });
        await Expect(characterLink).ToBeVisibleAsync();
        var characterPath = await characterLink.GetAttributeAsync("href");
        Assert.False(string.IsNullOrWhiteSpace(characterPath));
        var characterUrl = new Uri(new Uri(baseAddress), characterPath!).ToString();
        await NavigateAndWaitForCircuitAsync(characterUrl);
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Fips Donnerfaust (Fips)", Exact = true })).ToBeVisibleAsync();

        var cyberwareTab = Page.GetByRole(AriaRole.Tab, new() { Name = "Cyberware und Bioware", Exact = true });
        await Expect(cyberwareTab).ToBeVisibleAsync();
        await cyberwareTab.ClickAsync();
        await Expect(cyberwareTab).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("selected"));
        var addPane = Page.Locator(".cyber-add-pane");
        var entryPicker = addPane.Locator("select").Nth(2);
        await Expect(entryPicker).ToContainTextAsync("Data Filter");
        await entryPicker.SelectOptionAsync("Data Filter");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Hinzufügen", Exact = true }).ClickAsync();
        var itemRow = Page.Locator(".cyber-table tbody tr").Filter(new() { HasText = "Data Filter" });
        await Expect(itemRow).ToBeVisibleAsync();
        await itemRow.ClickAsync();
        var notes = Page.Locator(".cyber-detail-pane textarea");
        const string persistedNote = "browser cyberware note";
        await notes.FillAsync(persistedNote);
        await notes.BlurAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Speichern", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Änderungen wurden gespeichert.", new() { Exact = true })).ToBeVisibleAsync();

        await NavigateAndWaitForCircuitAsync(characterUrl);
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Fips Donnerfaust (Fips)", Exact = true })).ToBeVisibleAsync();
        cyberwareTab = Page.GetByRole(AriaRole.Tab, new() { Name = "Cyberware und Bioware", Exact = true });
        await cyberwareTab.ClickAsync();
        await Expect(cyberwareTab).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("selected"));
        await Expect(Page.Locator(".cyber-table tbody")).ToContainTextAsync("Data Filter");
        await Page.Locator(".cyber-table tbody tr").Filter(new() { HasText = "Data Filter" }).ClickAsync();
        await Expect(Page.Locator(".cyber-detail-pane textarea")).ToHaveValueAsync(persistedNote);

        await Page.Locator(".cyber-add-pane select").Nth(0).SelectOptionAsync("Bioware");
        var blockerPicker = Page.Locator(".cyber-add-pane select").Nth(2);
        await blockerPicker.SelectOptionAsync("Braveheart");
        var blocker = Page.Locator(".cyber-add-pane .text-warning");
        await Expect(blocker).ToContainTextAsync("hat Boni");
        await Expect(blocker).ToContainTextAsync("noch nicht portiert");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Hinzufügen", Exact = true })).ToBeDisabledAsync();
        await Expect(Page.Locator(".cyber-table tbody tr")).ToHaveCountAsync(1);
        await Expect(Page.GetByText("Ungespeicherte Änderungen", new() { Exact = true })).ToHaveCountAsync(0);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Speichern", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Änderungen wurden gespeichert.", new() { Exact = true })).ToBeVisibleAsync();

        var savedXml = await app.ReadCharacterXmlAsync("Fips Donnerfaust");
        var savedCyberware = XDocument.Parse(savedXml).Root!.Element("cyberwares")!.Elements("cyberware").ToList();
        var savedItem = Assert.Single(savedCyberware);
        Assert.Equal("Data Filter", savedItem.Element("name")?.Value);
        Assert.Equal(persistedNote, savedItem.Element("notes")?.Value);
    }

    private static string NewTestEmail() => $"browser-{Guid.NewGuid():N}@example.test";
    private static string NewTestPassword() => $"Aa1!{Convert.ToHexString(RandomNumberGenerator.GetBytes(24))}";

    private async Task NavigateAndWaitForCircuitAsync(string url)
    {
        var circuitStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Page.WebSocket += (_, socket) =>
        {
            if (socket.Url.Contains("/_blazor", StringComparison.Ordinal))
                circuitStarted.TrySetResult();
        };
        await Page.GotoAsync(url);
        await circuitStarted.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await Page.WaitForTimeoutAsync(1500);
    }

    private sealed class BrowserTestApplicationFactory : IAsyncDisposable
    {
        private readonly string databaseFile = Path.Combine(AppContext.BaseDirectory, $"cyberware-browser-{Guid.NewGuid():N}.db");
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

        public async Task<string> ReadCharacterXmlAsync(string name)
        {
            await using var connection = new SqliteConnection($"Data Source={databaseFile};Mode=ReadOnly;Pooling=False");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT ChumXml FROM Characters WHERE Name = $name";
            command.Parameters.AddWithValue("$name", name);
            return (string?)await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException($"Character '{name}' was not saved to the per-run SQLite database.");
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
