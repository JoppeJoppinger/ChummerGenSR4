using System.Xml;
using ChummerGen.Domain.Abstractions;
using ChummerGen.Infrastructure.Rules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ChummerGen.Tests;

public sealed class RuleCatalogTests
{
    [Fact]
    public async Task LoadsRealCatalogsAndSupportsXPathQueries()
    {
        using var provider = CreateProvider(out _);
        var catalog = provider.GetRequiredService<IRuleCatalog>();

        var skills = await catalog.GetAsync("skills.xml", "en-us", CancellationToken.None);
        var weapons = await catalog.GetAsync("weapons.xml", "en-us", CancellationToken.None);
        var skillsXml = LoadXml(skills!.Content);

        Assert.Matches("^[A-F0-9]{64}$", skills.Version);
        Assert.NotEmpty(weapons!.Content);
        Assert.NotNull(skillsXml.SelectSingleNode("/chummer/skills/skill[name='Aeronautics Mechanic']"));
        Assert.NotNull(LoadXml(weapons.Content).SelectSingleNode("/chummer/categories/category[.='Blades']"));
    }

    [Fact]
    public async Task ReturnsTranslationsForRequestedCultureWithoutLeakingAcrossCultures()
    {
        using var provider = CreateProvider(out _);
        var catalog = provider.GetRequiredService<IRuleCatalog>();

        var english = await catalog.GetAsync("skills.xml", "en-us", CancellationToken.None);
        var german = await catalog.GetAsync("skills.xml", "de", CancellationToken.None);
        var englishXml = LoadXml(english!.Content);
        var germanXml = LoadXml(german!.Content);

        Assert.Null(englishXml.SelectSingleNode("/chummer/skills/skill[name='Aeronautics Mechanic']/translate"));
        Assert.Equal(
            "Luftfahrtmechanik",
            germanXml.SelectSingleNode("/chummer/skills/skill[name='Aeronautics Mechanic']/translate")?.InnerText);
        Assert.Equal(
            "Raumfahrzeuge",
            germanXml.SelectSingleNode("/chummer/skills/skill[name='Aeronautics Mechanic']/specs/spec[@translate='Raumfahrzeuge']")?.Attributes?["translate"]?.Value);
        Assert.NotEqual(english.Version, german.Version);
    }

    [Fact]
    public async Task MissingCatalogThrowsHelpfulError()
    {
        using var provider = CreateProvider(out _);
        var catalog = provider.GetRequiredService<IRuleCatalog>();

        var exception = await Assert.ThrowsAsync<FileNotFoundException>(
            () => catalog.GetAsync("missing-catalog.xml", "en-us", CancellationToken.None));

        Assert.Contains("Regelkatalog 'missing-catalog.xml'", exception.Message);
        Assert.Contains("Datenverzeichnis", exception.Message);
    }

    [Fact]
    public async Task RejectsPathTraversalInCatalogName()
    {
        using var provider = CreateProvider(out _);
        var catalog = provider.GetRequiredService<IRuleCatalog>();

        await Assert.ThrowsAsync<ArgumentException>(
            () => catalog.GetAsync("..\\secrets.xml", "en-us", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(
            () => catalog.GetAsync("../secrets.xml", "en-us", CancellationToken.None));
    }

    private static ServiceProvider CreateProvider(out string repositoryRoot)
    {
        repositoryRoot = FindRepositoryRoot();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Rules:DataDirectory"] = "bin/data"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddChummerRuleCatalog(configuration, new TestHostEnvironment(repositoryRoot));
        return services.BuildServiceProvider();
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "bin", "data")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root containing bin/data could not be located.");
    }

    private static XmlDocument LoadXml(string content)
    {
        var document = new XmlDocument { XmlResolver = null };
        document.LoadXml(content);
        return document;
    }

    private sealed class TestHostEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = nameof(ChummerGen);
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
