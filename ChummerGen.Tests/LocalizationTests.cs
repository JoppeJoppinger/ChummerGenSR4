using ChummerGen.Infrastructure.Localization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ChummerGen.Tests;

public sealed class LocalizationTests
{
    private readonly IChummerLocalizationService _service;
    private readonly string _repoRoot;

    public LocalizationTests()
    {
        var provider = CreateProvider(out _repoRoot);
        _service = provider.GetRequiredService<IChummerLocalizationService>();
    }

    [Fact]
    public void DiscoversSupportedCulturesFromRealFiles()
    {
        var cultures = _service.GetSupportedCultures();

        Assert.Contains("en-us", cultures);
        Assert.Contains("de", cultures);
        Assert.Contains("fr", cultures);
        Assert.Contains("jp", cultures);

        Assert.True(_service.IsCultureSupported("en-us"));
        Assert.True(_service.IsCultureSupported("en-US"));
        Assert.True(_service.IsCultureSupported("en"));
        Assert.True(_service.IsCultureSupported("de"));
        Assert.True(_service.IsCultureSupported("de-DE"));
        Assert.True(_service.IsCultureSupported("fr"));
        Assert.True(_service.IsCultureSupported("fr-FR"));
        Assert.True(_service.IsCultureSupported("jp"));
        Assert.True(_service.IsCultureSupported("ja"));
        Assert.True(_service.IsCultureSupported("ja-JP"));

        Assert.False(_service.IsCultureSupported("es"));
        Assert.False(_service.IsCultureSupported("it"));
        Assert.False(_service.IsCultureSupported(""));
        Assert.False(_service.IsCultureSupported(null!));
    }

    [Fact]
    public void ComputesDeterministicSha256VersionPerCulture()
    {
        var enVersion = _service.GetVersion("en-us");
        var deVersion = _service.GetVersion("de");
        var frVersion = _service.GetVersion("fr");

        Assert.Matches("^[A-F0-9]{64}$", enVersion);
        Assert.Matches("^[A-F0-9]{64}$", deVersion);
        Assert.Matches("^[A-F0-9]{64}$", frVersion);

        // Version should be distinct across cultures because files and content differ
        Assert.NotEqual(enVersion, deVersion);
        Assert.NotEqual(deVersion, frVersion);

        // Repeated call returns identical deterministic hash
        Assert.Equal(deVersion, _service.GetVersion("de"));
        Assert.Equal(deVersion, _service.GetVersion("de-DE"));
    }

    [Fact]
    public void RetrievesExactUiStringsForKnownCultures()
    {
        // String_BP
        Assert.Equal("BP", _service.GetRequiredString("String_BP", "en-us"));
        Assert.Equal("GP", _service.GetRequiredString("String_BP", "de"));
        Assert.Equal("PC", _service.GetRequiredString("String_BP", "fr"));
        Assert.Equal("BP", _service.GetRequiredString("String_BP", "jp"));

        // String_Karma
        Assert.Equal("Karma", _service.GetRequiredString("String_Karma", "en-us"));
        Assert.Equal("Karma", _service.GetRequiredString("String_Karma", "de"));
        Assert.Equal("Karma", _service.GetRequiredString("String_Karma", "fr"));
        Assert.Equal("カルマ", _service.GetRequiredString("String_Karma", "jp"));

        // String_Page
        Assert.Equal("page", _service.GetRequiredString("String_Page", "en-us"));
        Assert.Equal("Seite", _service.GetRequiredString("String_Page", "de"));
        Assert.Equal("page", _service.GetRequiredString("String_Page", "fr"));
        Assert.Equal("page", _service.GetRequiredString("String_Page", "jp"));

        // String_AttributeBODShort & Long
        Assert.Equal("BOD", _service.GetRequiredString("String_AttributeBODShort", "en-us"));
        Assert.Equal("KON", _service.GetRequiredString("String_AttributeBODShort", "de"));
        Assert.Equal("Konstitution", _service.GetRequiredString("String_AttributeBODLong", "de"));
        Assert.Equal("Body", _service.GetRequiredString("String_AttributeBODLong", "en-us"));
    }

    [Fact]
    public void UnescapesNewlineInUiStrings()
    {
        Assert.True(_service.TryGetString("Message_RemoveCharacterAssociation", "en-us", out var val));
        Assert.NotNull(val);
        Assert.Contains("\n", val);
        Assert.DoesNotContain("\\n", val);
    }

    [Fact]
    public void TryGetStringReturnsFalseOnMissingKey()
    {
        Assert.False(_service.TryGetString("NonExistent_Key_XYZ", "en-us", out var val));
        Assert.Null(val);

        Assert.False(_service.TryGetString("NonExistent_Key_XYZ", "de", out var valDe));
        Assert.Null(valDe);
    }

    [Fact]
    public void GetRequiredStringThrowsExplicitExceptionOnMissingKey()
    {
        var ex = Assert.Throws<LocalizationKeyNotFoundException>(
            () => _service.GetRequiredString("NonExistent_Key_XYZ", "de"));

        Assert.Equal("NonExistent_Key_XYZ", ex.Key);
        Assert.Equal("de", ex.Culture);
        Assert.Contains("NonExistent_Key_XYZ", ex.Message);
        Assert.Contains("de", ex.Message);
    }

    [Fact]
    public void ThrowsExplicitExceptionOnUnsupportedCulture()
    {
        var ex = Assert.Throws<CultureNotSupportedException>(
            () => _service.GetRequiredString("String_BP", "es-ES"));

        Assert.Equal("es-ES", ex.Culture);
        Assert.Contains("es-ES", ex.Message);

        Assert.Throws<CultureNotSupportedException>(
            () => _service.TranslateData("Blades", "es-ES"));
    }

    [Fact]
    public void NeverSubstitutesFallbackLanguageSilently()
    {
        // String_BP is "GP" in German. In Spanish, it must throw CultureNotSupportedException, NOT silently return "BP" or "GP"
        Assert.Throws<CultureNotSupportedException>(() => _service.GetRequiredString("String_BP", "es-ES"));

        // If a key doesn't exist in German, it must throw LocalizationKeyNotFoundException, NOT fallback to English
        Assert.Throws<LocalizationKeyNotFoundException>(() => _service.GetRequiredString("CompletelyUnknownStringKey", "de"));
    }

    [Fact]
    public void TranslatesDataLabelsFromDataXmlFile()
    {
        // Weapons categories
        Assert.Equal("Klingenwaffen", _service.TranslateData("Blades", "de"));
        Assert.Equal("Armes tranchantes", _service.TranslateData("Blades", "fr"));
        Assert.Equal("刀剣", _service.TranslateData("Blades", "jp"));

        // Weapon name
        Assert.Equal("Katana", _service.TranslateData("Katana", "de"));
        Assert.Equal("Katana", _service.TranslateData("Katana", "fr"));

        // Skill groups
        Assert.Equal("Feuerwaffen", _service.TranslateData("Firearms", "de"));
        Assert.Equal("Nahkampf", _service.TranslateData("Close Combat", "de"));
        Assert.Equal("Biotech", _service.TranslateData("Biotech", "de"));

        // Skills
        Assert.Equal("Luftfahrtmechanik", _service.TranslateData("Aeronautics Mechanic", "de"));

        // Specializations
        Assert.Equal("Raumfahrzeuge", _service.TranslateData("Aerospace", "de"));

        // Metavariants
        Assert.Equal("Oger", _service.TranslateData("Ogre", "de"));
        Assert.Equal("Zyklop", _service.TranslateData("Cyclops", "de"));

        // Mentors
        Assert.Equal("Der Widersacher", _service.TranslateData("Adversary", "de"));
        Assert.Equal("Bär", _service.TranslateData("Bear", "de"));

        // Martial Arts Advantages
        Assert.Equal("+1 Würfel für Volles Ausweichen", _service.TranslateData("+1 die for Full Dodge", "de"));

        // Attribute short codes (BOD -> KON for de, BOD -> BOD for en-us)
        Assert.Equal("KON", _service.TranslateData("BOD", "de"));
        Assert.Equal("GES", _service.TranslateData("AGI", "de"));
        Assert.Equal("BOD", _service.TranslateData("BOD", "en-us"));
    }

    [Fact]
    public void EnglishDataTranslationReturnsOriginalText()
    {
        Assert.Equal("Blades", _service.TranslateData("Blades", "en-us"));
        Assert.Equal("Katana", _service.TranslateData("Katana", "en-us"));
        Assert.Equal("Aeronautics Mechanic", _service.TranslateData("Aeronautics Mechanic", "en-us"));
    }

    [Fact]
    public void TryTranslateDataReturnsFalseWhenDataTranslationMissing()
    {
        var found = _service.TryTranslateData("NonExistentItemName_99999", "de", out var translated);
        Assert.False(found);
        Assert.Null(translated);

        // TranslateData returns original text if not found
        var fallback = _service.TranslateData("NonExistentItemName_99999", "de");
        Assert.Equal("NonExistentItemName_99999", fallback);
    }

    [Fact]
    public void RejectsPathTraversalInCulture()
    {
        Assert.Throws<ArgumentException>(() => _service.GetVersion("../secrets"));
        Assert.Throws<ArgumentException>(() => _service.GetRequiredString("String_BP", "..\\..\\windows\\system32"));
        Assert.Throws<ArgumentException>(() => _service.TranslateData("Blades", "../lang/de"));
        Assert.Throws<ArgumentException>(() => _service.TranslateData("Blades", "de/../../"));
    }

    private static ServiceProvider CreateProvider(out string repoRoot)
    {
        repoRoot = FindRepositoryRoot();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Localization:LanguageDirectory"] = "bin/lang"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddChummerLocalization(configuration, new TestHostEnvironment(repoRoot));
        return services.BuildServiceProvider();
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

    private sealed class TestHostEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = nameof(ChummerGen);
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
