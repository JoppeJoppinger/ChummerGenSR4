using ChummerGen.Domain.Abstractions;
using ChummerGen.Domain.Character;
using ChummerGen.Domain.Chum;
using ChummerGen.Domain.Models;
using ChummerGen.Infrastructure.Localization;
using ChummerGen.Web.Components.Characters;
using ChummerGen.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChummerGen.Tests;

public sealed class CyberwareLocalizationTests
{
    private static readonly string[] ExpectedKeys =
    [
        "Label_EssenceHole",
        "Label_Name",
        "Label_Category",
        "Label_Grade",
        "Label_Essence",
        "Label_Capacity",
        "Label_Avail",
        "Label_Cost",
        "Label_Bioware",
        "Label_Notes",
        "Label_Source",
        "Checkbox_BlackMarketDiscount",
        "Checkbox_Options_AllowCyberwareESSDiscounts",
        "String_Rating",
        "String_Delete",
        "Checkbox_Active"
    ];

    [Theory]
    [InlineData("de")]
    [InlineData("en-us")]
    [InlineData("fr")]
    [InlineData("jp")]
    public async Task CyberwareTabRendersExactLabelsOfTheSelectedCulture(string culture)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRuleCatalog, EmptyRuleCatalog>();
        services.AddSingleton<IChummerLocalizationAccessor>(new CultureAccessor(culture));
        using var serviceProvider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(serviceProvider, serviceProvider.GetRequiredService<ILoggerFactory>());
        var session = new CharacterSession(ChumSerializer.Parse("<character><name>Test</name></character>"));

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<CyberwareTab>(
                ParameterView.FromDictionary(new Dictionary<string, object?> { ["Session"] = session }));
            return component.ToHtmlString();
        });

        foreach (var key in new[] { "Label_Name", "Label_Category", "Label_Grade", "Label_Essence", "Label_Capacity", "Label_Avail", "Label_Cost" })
            Assert.Contains($"<th>{culture}|{key}</th>", html.Replace(" class=\"text-end\"", string.Empty));

        Assert.DoesNotContain("Bezeichnung", html);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("en-us")]
    [InlineData("fr")]
    [InlineData("jp")]
    public void AllTargetKeysExistWithExactNonEmptyValuesInAllCultures(string culture)
    {
        var langDir = Path.Combine(FindRepositoryRoot(), "bin", "lang");
        var xmlPath = Path.Combine(langDir, $"{culture}.xml");
        Assert.True(File.Exists(xmlPath), $"Language file {xmlPath} must exist.");

        using var stream = File.OpenRead(xmlPath);
        var doc = System.Xml.Linq.XDocument.Load(stream);
        var lookup = doc.Root?
            .Element("strings")?
            .Elements("string")
            .GroupBy(
                el => el.Element("key")?.Value ?? string.Empty,
                el => el.Element("text")?.Value ?? string.Empty,
                StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal)
            ?? throw new InvalidOperationException($"Invalid XML format in {xmlPath}");

        foreach (var key in ExpectedKeys)
        {
            Assert.True(lookup.ContainsKey(key), $"Key '{key}' must exist in culture '{culture}'.");
            Assert.False(string.IsNullOrWhiteSpace(lookup[key]), $"Value for key '{key}' in culture '{culture}' must not be empty.");
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "bin", "lang")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root containing bin/lang could not be located.");
    }

    private sealed class EmptyRuleCatalog : IRuleCatalog
    {
        public Task<RuleCatalogDocument?> GetAsync(string catalogName, string culture, CancellationToken cancellationToken) =>
            Task.FromResult<RuleCatalogDocument?>(null);
    }

    private sealed class CultureAccessor(string culture) : IChummerLocalizationAccessor
    {
        public IReadOnlyList<string> GetSupportedCultures() => ["de", "en-us", "fr", "jp"];

        public Task<string> GetCultureAsync(CancellationToken cancellationToken = default) => Task.FromResult(culture);

        public Task<string> GetRequiredStringAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult($"{culture}|{key}");

        public Task SetCultureAsync(string culture, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}