using ChummerGen.Domain.Character;
using ChummerGen.Domain.Chum;
using ChummerGen.Infrastructure.Localization;
using ChummerGen.Web.Components.Characters;
using ChummerGen.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using System.Xml.Linq;

namespace ChummerGen.Tests;

public sealed class CoreTabLocalizationTests
{
    private static readonly string[] Cultures = ["de", "en-us", "fr", "jp"];

    private static readonly string[] RequiredKeys =
    [
        "Tab_Common",
        "Tab_CharacterInfo",
        "Tab_Skills",
        "Tab_Gear",
        "Tab_Cyberware",
        "Tab_OtherInfo",
        "Tab_Critter",
        "Label_Attributes",
        "Label_Base",
        "Label_Augmented",
        "Label_MetatypeLimits",
        "Label_SkillGroups",
        "Label_ActiveSkills",
        "Label_KnowledgeSkills",
        "Label_Options_Qualities",
        "Button_AddSkill",
        "Button_AddExoticSkill",
        "String_BP",
        "Label_Name",
        "Label_Alias",
        "Label_SummaryMetatype",
        "Label_Sex",
        "Label_Age",
        "String_Karma",
        "Label_SummaryNuyen",
        "Label_SummaryPrimaryAttributes",
        "Label_SummarySpecialAttributes",
        "Label_DicePool",
        "String_AttributeBODLong",
        "String_AttributeAGILong",
        "String_AttributeREALong",
        "String_AttributeSTRLong",
        "String_AttributeCHALong",
        "String_AttributeINTLong",
        "String_AttributeLOGLong",
        "String_AttributeWILLong",
        "String_AttributeEDGLong",
        "String_AttributeMAGLong",
        "String_AttributeRESLong",
        "String_AttributeBODShort",
        "String_AttributeAGIShort",
        "String_AttributeREAShort",
        "String_AttributeSTRShort",
        "String_AttributeCHAShort",
        "String_AttributeINTShort",
        "String_AttributeLOGShort",
        "String_AttributeWILShort",
        "String_AttributeEDGShort",
        "String_AttributeMAGShort",
        "String_AttributeRESShort",
        "Label_OtherIP",
        "Label_OtherCmposure",
        "Label_OtherJudgeIntention",
        "Label_OtherLiftAndCarry",
        "Label_OtherMemory",
        "String_AttributeINILong",
        "String_ExpenseAttribute",
        "String_ExpenseActiveSkill",
        "String_ExpenseSkillGroup",
        "String_ExpenseSpecialization",
        "String_Rating",
        "Label_Category",
        "String_SkillFilterRatingAboveZero",
        "Menu_FileExport",
        "Title_CareerMode"
    ];

    private static readonly (string Key, string De, string En, string Fr, string Jp)[] VerifiedExamples =
    [
        ("Tab_Common", "Allgemein", "Common", "Commun", "共通"),
        ("Tab_Skills", "Fertigkeiten", "Skills", "Compétences", "技能"),
        ("Label_Attributes", "Attribute", "Attributes", "Attribut", "能力値"),
        ("Label_Base", "Basis", "Base", "Base", "自然"),
        ("Label_Augmented", "(Verst.)", "(Aug)", "(Augmenté)", "(強化)"),
        ("Label_MetatypeLimits", "Metatyp Begrenzung", "Metatype Limits", "Limites", "メタタイプ上限"),
        ("Label_SkillGroups", "Fertigkeitsgruppen", "Skill Groups", "Groupes de Compétences", "技能グループ"),
        ("Label_ActiveSkills", "Aktionsfertigkeiten", "Active Skills", "Compétences actives", "能動技能"),
        ("Label_KnowledgeSkills", "Wissensfertigkeiten", "Knowledge Skills", "Compétences de connaissance", "知識技能"),
        ("Button_AddSkill", "Fertigkeit hinzufügen", "&Add Skill", "&Ajout Comp.", "技能を追加 (&A)"),
        ("Button_AddExoticSkill", "Exotische Fertigkeit hinzufügen", "Add Exotic Skill", "Ajout Comp. Exotique", "特殊技能を追加"),
        ("String_BP", "GP", "BP", "PC", "BP")
    ];

    [Theory]
    [InlineData("de")]
    [InlineData("en-us")]
    [InlineData("fr")]
    [InlineData("jp")]
    public async Task CoreTabsRenderTheSelectedCultureFromTheLanguageFiles(string culture)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var localization = new ChummerLocalizationService(Path.Combine(repositoryRoot, "bin", "lang"), cache);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IChummerLocalizationAccessor>(new XmlLocalizationAccessor(localization, culture));
        using var serviceProvider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(serviceProvider, serviceProvider.GetRequiredService<ILoggerFactory>());
        var session = CreateSession();

        var rendered = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var tabs = await renderer.RenderComponentAsync<CharacterTabs>(ParameterView.Empty);
            var info = await renderer.RenderComponentAsync<InfoTab>(
                ParameterView.FromDictionary(new Dictionary<string, object?> { ["Session"] = session }));
            var attributes = await renderer.RenderComponentAsync<AttributesTab>(
                ParameterView.FromDictionary(new Dictionary<string, object?> { ["Session"] = session }));
            var skills = await renderer.RenderComponentAsync<SkillsTab>(
                ParameterView.FromDictionary(new Dictionary<string, object?> { ["Session"] = session }));
            return string.Join('\n', tabs.ToHtmlString(), info.ToHtmlString(), attributes.ToHtmlString(), skills.ToHtmlString());
        });

        foreach (var key in RenderedKeys)
            Assert.Contains(HtmlEncoder.Default.Encode(localization.GetRequiredString(key, culture)), rendered);
    }

    [Theory]
    [InlineData("de", 0)]
    [InlineData("en-us", 1)]
    [InlineData("fr", 2)]
    [InlineData("jp", 3)]
    public void RequiredKeysAndVerifiedExamplesContainActualXmlValues(string culture, int cultureIndex)
    {
        var xmlPath = Path.Combine(FindRepositoryRoot(), "bin", "lang", $"{culture}.xml");
        Assert.True(File.Exists(xmlPath), $"Language file {xmlPath} must exist.");
        var values = XDocument.Load(xmlPath)
            .Root?
            .Element("strings")?
            .Elements("string")
            .GroupBy(
                element => element.Element("key")?.Value ?? string.Empty,
                element => element.Element("text")?.Value ?? string.Empty,
                StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal)
            ?? throw new InvalidOperationException($"Invalid XML format in {xmlPath}");

        foreach (var key in RequiredKeys.Concat(RenderedKeys).Distinct(StringComparer.Ordinal))
        {
            Assert.True(values.TryGetValue(key, out var value), $"Key '{key}' must exist in '{culture}'.");
            Assert.False(string.IsNullOrWhiteSpace(value), $"Key '{key}' must have a value in '{culture}'.");
        }

        foreach (var example in VerifiedExamples)
        {
            var expected = cultureIndex switch
            {
                0 => example.De,
                1 => example.En,
                2 => example.Fr,
                _ => example.Jp
            };
            Assert.Equal(expected, values[example.Key]);
        }
    }

    private static readonly string[] RenderedKeys =
    [
        "Tab_CharacterInfo",
        "Label_Attributes",
        "Tab_Skills",
        "Tab_Gear",
        "Tab_Cyberware",
        "Label_Options_Qualities",
        "Title_CareerMode",
        "Tab_Critter",
        "Menu_FileExport",
        "Label_Name",
        "Label_Alias",
        "Label_SummaryMetatype",
        "Label_Sex",
        "Label_Age",
        "Tab_OtherInfo",
        "String_Karma",
        "Label_SummaryNuyen",
        "String_BP",
        "Label_SummaryPrimaryAttributes",
        "Label_SummarySpecialAttributes",
        "String_ExpenseAttribute",
        "Label_Base",
        "Label_MetatypeLimits",
        "Label_Augmented",
        "Label_DicePool",
        "String_AttributeINILong",
        "Label_OtherIP",
        "Label_OtherCmposure",
        "Label_OtherJudgeIntention",
        "Label_OtherLiftAndCarry",
        "Label_OtherMemory",
        "String_AttributeWILShort",
        "String_AttributeCHAShort",
        "String_AttributeINTShort",
        "String_AttributeBODShort",
        "String_AttributeSTRShort",
        "String_AttributeLOGShort",
        "String_AttributeREAShort",
        "String_ExpenseActiveSkill",
        "String_ExpenseSkillGroup",
        "String_Rating",
        "String_ExpenseSpecialization",
        "Label_SkillGroups",
        "Label_ActiveSkills",
        "Label_Category",
        "String_SkillFilterRatingAboveZero"
    ];

    private static CharacterSession CreateSession() =>
        new(ChumSerializer.Parse("""
            <character>
              <name>Test</name>
              <alias>Runner</alias>
              <attributes>
                <attribute>
                  <name>BOD</name><metatypemin>1</metatypemin><metatypemax>6</metatypemax>
                  <metatypeaugmax>9</metatypeaugmax><value>3</value><base>3</base>
                  <augmodifier>0</augmodifier><totalvalue>3</totalvalue>
                </attribute>
              </attributes>
              <skills>
                <skill>
                  <name>Unarmed Combat</name><skillgroup>Close Combat</skillgroup>
                  <skillcategory>Combat</skillcategory><rating>1</rating><ratingmax>6</ratingmax>
                  <attribute>AGI</attribute><totalvalue>4</totalvalue>
                </skill>
              </skills>
              <skillgroups>
                <skillgroup><name>Close Combat</name><rating>1</rating><ratingmax>6</ratingmax><broken>false</broken></skillgroup>
              </skillgroups>
            </character>
            """));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "bin", "lang")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Repository root containing bin/lang could not be located.");
    }

    private sealed class XmlLocalizationAccessor(IChummerLocalizationService localization, string culture) : IChummerLocalizationAccessor
    {
        public IReadOnlyList<string> GetSupportedCultures() => Cultures;

        public Task<string> GetCultureAsync(CancellationToken cancellationToken = default) => Task.FromResult(culture);

        public Task<string> GetRequiredStringAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(localization.GetRequiredString(key, culture));

        public Task SetCultureAsync(string culture, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
