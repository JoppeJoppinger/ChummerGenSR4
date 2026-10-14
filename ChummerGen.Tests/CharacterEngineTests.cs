using System.Xml.Linq;
using ChummerGen.Domain.Abstractions;
using ChummerGen.Domain.Character;
using ChummerGen.Domain.Chum;
using ChummerGen.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ChummerGen.Tests;

public sealed class CharacterEngineTests
{
    [Fact]
    public async Task RunsRegisteredModuleAndReturnsItsValidationIssues()
    {
        const string xml = "<character><name>Vorher</name><karma>12</karma><extension><nested>erhalten</nested></extension></character>";
        var module = new TestModule();
        var services = new ServiceCollection();
        services.AddChummerCharacterEngine();
        services.AddSingleton<ICharacterModule>(module);
        services.AddSingleton<IRuleCatalog, EmptyRuleCatalog>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<CharacterEngine>();
        var rules = scope.ServiceProvider.GetRequiredService<IRuleCatalog>();
        var session = new CharacterSession(ChumSerializer.Parse(xml));

        var result = await engine.RunAsync(session, rules);

        Assert.Equal(1, result.ModulesExecuted);
        Assert.Equal("Test", Assert.Single(engine.ModuleNames));
        Assert.Equal(1, module.RecalculationCount);
        Assert.Equal(1, module.ValidationCount);
        Assert.Equal(13, session.Model.Karma);
        Assert.Contains(result.Issues, issue => issue.Code == "test-warning");
    }

    [Fact]
    public void ModelEditsRoundtripAndKeepUnmodeledXml()
    {
        var session = new CharacterSession(ChumSerializer.Parse(
            "<character><name>Alt</name><attributes><attribute><name>AGI</name><base>3</base></attribute></attributes><unknown flag=\"keep\"><nested>inhalt</nested></unknown></character>"));

        session.Model.Name = "Neu";
        session.Model.SetAttributeValue("AGI", "4");
        session.Model.InitiativeTotal = 9;
        session.Model.AddItem("skills", new XElement("skill", new XElement("name", "Schleichen")));
        var restored = ChumSerializer.Parse(session.Serialize());

        Assert.Equal("Neu", restored.Name);
        Assert.Equal("4", restored.Xml.Root?.Element("attributes")?.Element("attribute")?.Element("base")?.Value);
        Assert.Equal("9", restored.Xml.Root?.Element("init")?.Element("total")?.Value);
        Assert.Equal("inhalt", restored.Xml.Root?.Element("unknown")?.Element("nested")?.Value);
        Assert.True(XNode.DeepEquals(session.Xml.Root, restored.Xml.Root));
    }

    private sealed class TestModule : ICharacterModule
    {
        public string Name => "Test";
        public int RecalculationCount { get; private set; }
        public int ValidationCount { get; private set; }

        public Task RecalculateAsync(CharacterSession session, IRuleCatalog ruleCatalog, CancellationToken cancellationToken = default)
        {
            RecalculationCount++;
            session.Model.Karma = (session.Model.Karma ?? 0) + 1;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CharacterValidationIssue>> ValidateAsync(CharacterSession session, IRuleCatalog ruleCatalog, CancellationToken cancellationToken = default)
        {
            ValidationCount++;
            IReadOnlyList<CharacterValidationIssue> issues =
            [new CharacterValidationIssue(Name, "test-warning", "Testhinweis", CharacterIssueSeverity.Warning)];
            return Task.FromResult(issues);
        }
    }

    private sealed class EmptyRuleCatalog : IRuleCatalog
    {
        public Task<RuleCatalogDocument?> GetAsync(string catalogName, string culture, CancellationToken cancellationToken) =>
            Task.FromResult<RuleCatalogDocument?>(null);
    }
}