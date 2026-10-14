using System.Xml.Linq;
using ChummerGen.Domain.Abstractions;
using ChummerGen.Domain.Character;
using ChummerGen.Domain.Character.Qualities;
using ChummerGen.Domain.Chum;
using ChummerGen.Domain.Models;

namespace ChummerGen.Tests;

public sealed class QualitiesTests
{
    private const string SampleChumXml = """
        <character>
          <appversion>467</appversion>
          <name>Shadowrunner</name>
          <metatype>Troll</metatype>
          <qualities>
            <quality>
              <guid>11111111-1111-1111-1111-111111111111</guid>
              <name>Thermographic Vision</name>
              <extra></extra>
              <bp>5</bp>
              <contributetolimit>True</contributetolimit>
              <print>True</print>
              <qualitytype>Positive</qualitytype>
              <qualitysource>Metatype</qualitysource>
              <source>SR4</source>
              <page>90</page>
              <notes>Racial trait</notes>
            </quality>
            <quality>
              <guid>22222222-2222-2222-2222-222222222222</guid>
              <name>Toughness</name>
              <extra></extra>
              <bp>10</bp>
              <contributetolimit>True</contributetolimit>
              <print>True</print>
              <qualitytype>Positive</qualitytype>
              <qualitysource>Selected</qualitysource>
              <source>SR4</source>
              <page>91</page>
              <notes>Purchased</notes>
            </quality>
            <quality>
              <guid>33333333-3333-3333-3333-333333333333</guid>
              <name>Distinctive Style</name>
              <extra></extra>
              <bp>-5</bp>
              <contributetolimit>True</contributetolimit>
              <print>True</print>
              <qualitytype>Negative</qualitytype>
              <qualitysource>Selected</qualitysource>
              <source>SR4</source>
              <page>95</page>
              <notes>Cyber mohawk</notes>
            </quality>
          </qualities>
          <improvements>
            <improvement>
              <sourcename>22222222-2222-2222-2222-222222222222</sourcename>
              <min>0</min>
              <max>0</max>
              <aug>0</aug>
              <augmax>0</augmax>
              <val>1</val>
              <rating>1</rating>
              <improvementttype>DamageResistance</improvementttype>
              <improvementsource>Quality</improvementsource>
              <custom>False</custom>
              <addtorating>False</addtorating>
              <enabled>True</enabled>
              <order>0</order>
            </improvement>
            <improvement>
              <sourcename>other-unrelated-guid</sourcename>
              <min>0</min>
              <max>0</max>
              <val>2</val>
              <improvementttype>CyberwareGrade</improvementttype>
              <improvementsource>Cyberware</improvementsource>
            </improvement>
          </improvements>
          <custom_unrelated_tag flag="keep">
            <nested_value>untouched_payload</nested_value>
          </custom_unrelated_tag>
        </character>
        """;

    [Fact]
    public void Manager_ParsesPositiveAndNegativeQualities_AndImprovements()
    {
        var doc = ChumSerializer.Parse(SampleChumXml);
        var session = new CharacterSession(doc);
        var manager = new QualitiesManager(session);

        var all = manager.GetAllQualities();
        Assert.Equal(3, all.Count);

        var positive = manager.GetPositiveQualities();
        Assert.Equal(2, positive.Count);

        var negative = manager.GetNegativeQualities();
        Assert.Single(negative);
        Assert.Equal("Distinctive Style", negative[0].Name);
        Assert.Equal(-5, negative[0].Bp);

        var imps = manager.GetImprovementsForQuality("22222222-2222-2222-2222-222222222222");
        Assert.Single(imps);
        Assert.Equal("DamageResistance", imps[0].ImprovementType);
        Assert.Equal(1, imps[0].Val);
    }

    [Fact]
    public void Manager_CalculatesSummary_ExcludingMetatypeFromBpLimits()
    {
        var doc = ChumSerializer.Parse(SampleChumXml);
        var session = new CharacterSession(doc);
        var manager = new QualitiesManager(session);

        var summary = manager.CalculateSummary();

        // Metatype quality (Thermographic Vision, 5 BP) must not contribute to selected BP total
        // Selected positive: Toughness (10 BP)
        Assert.Equal(10, summary.PositiveBpTotal);
        // Selected negative: Distinctive Style (-5 BP)
        Assert.Equal(-5, summary.NegativeBpTotal);
        Assert.Equal(5, summary.NetBpTotal);

        // Karma multiplier default is 2
        Assert.Equal(20, summary.PositiveKarmaTotal);
        Assert.Equal(-10, summary.NegativeKarmaTotal);
        Assert.Equal(10, summary.NetKarmaTotal);

        Assert.Equal(2, summary.CountPositive);
        Assert.Equal(1, summary.CountNegative);
        Assert.Equal(1, summary.CountMetatype);
    }

    [Fact]
    public void Manager_UpdatesNotesAndPrint_PreservingUnrelatedXml()
    {
        var doc = ChumSerializer.Parse(SampleChumXml);
        var session = new CharacterSession(doc);
        var manager = new QualitiesManager(session);

        var updatedNotes = manager.UpdateNotes("22222222-2222-2222-2222-222222222222", "Updated custom notes");
        var updatedPrint = manager.UpdatePrint("22222222-2222-2222-2222-222222222222", false);

        Assert.True(updatedNotes);
        Assert.True(updatedPrint);

        var reloaded = ChumSerializer.Parse(session.Serialize());
        var qualElem = reloaded.Xml.Root?.Element("qualities")?.Elements("quality")
            .FirstOrDefault(q => q.Element("guid")?.Value == "22222222-2222-2222-2222-222222222222");

        Assert.NotNull(qualElem);
        Assert.Equal("Updated custom notes", qualElem.Element("notes")?.Value);
        Assert.Equal("False", qualElem.Element("print")?.Value);

        // Unrelated XML is preserved intact
        var customTag = reloaded.Xml.Root?.Element("custom_unrelated_tag");
        Assert.NotNull(customTag);
        Assert.Equal("untouched_payload", customTag.Element("nested_value")?.Value);
        Assert.Equal("keep", customTag.Attribute("flag")?.Value);
    }

    [Fact]
    public void Manager_DeleteQuality_RemovesQualityAndLinkedImprovements_RejectsMetatype()
    {
        var doc = ChumSerializer.Parse(SampleChumXml);
        var session = new CharacterSession(doc);
        var manager = new QualitiesManager(session);

        // Metatype quality cannot be deleted
        var deletedMeta = manager.DeleteQuality("11111111-1111-1111-1111-111111111111");
        Assert.False(deletedMeta);

        // Selected quality can be deleted
        var deletedSelected = manager.DeleteQuality("22222222-2222-2222-2222-222222222222");
        Assert.True(deletedSelected);

        // Quality is removed
        Assert.Null(manager.GetAllQualities().FirstOrDefault(q => q.Guid == "22222222-2222-2222-2222-222222222222"));

        // Linked improvement is removed, unrelated cyberware improvement remains
        var remainingImps = manager.GetAllImprovements();
        Assert.Single(remainingImps);
        Assert.Equal("CyberwareGrade", remainingImps[0].ImprovementType);

        // Unrelated XML still preserved
        Assert.Equal("untouched_payload", session.Xml.Root?.Element("custom_unrelated_tag")?.Element("nested_value")?.Value);
    }

    [Fact]
    public async Task QualitiesModule_Validates35BpLimit()
    {
        // Character with 40 BP positive qualities
        const string xml = """
            <character>
              <qualities>
                <quality>
                  <guid>11111111-1111-1111-1111-111111111111</guid>
                  <name>Quality 1</name>
                  <bp>20</bp>
                  <qualitytype>Positive</qualitytype>
                  <qualitysource>Selected</qualitysource>
                </quality>
                <quality>
                  <guid>22222222-2222-2222-2222-222222222222</guid>
                  <name>Quality 2</name>
                  <bp>25</bp>
                  <qualitytype>Positive</qualitytype>
                  <qualitysource>Selected</qualitysource>
                </quality>
                <quality>
                  <guid>33333333-3333-3333-3333-333333333333</guid>
                  <name>Flaw 1</name>
                  <bp>-40</bp>
                  <qualitytype>Negative</qualitytype>
                  <qualitysource>Selected</qualitysource>
                </quality>
              </qualities>
            </character>
            """;

        var session = new CharacterSession(ChumSerializer.Parse(xml));
        var module = new QualitiesModule();
        var emptyCatalog = new DummyRuleCatalog();

        var issues = await module.ValidateAsync(session, emptyCatalog);

        Assert.Equal(2, issues.Count);
        Assert.Contains(issues, i => i.Code == "qualities-positive-bp-limit");
        Assert.Contains(issues, i => i.Code == "qualities-negative-bp-limit");
    }

    [ReferenceChumFact]
    public void ReferenceFile_QualitiesAndImprovements_LoadAccurately()
    {
        var doc = ChumSerializer.LoadFile(ChumRoundtripTests.ReferenceFilePathForTest);
        var session = new CharacterSession(doc);
        var manager = new QualitiesManager(session);

        var qualities = manager.GetAllQualities();
        Assert.Equal(12, qualities.Count);

        var positive = manager.GetPositiveQualities();
        var negative = manager.GetNegativeQualities();
        Assert.Equal(6, positive.Count);
        Assert.Equal(6, negative.Count);

        var improvements = manager.GetQualityImprovements();
        Assert.Equal(7, improvements.Count);

        var summary = manager.CalculateSummary();
        Assert.Equal(35, summary.PositiveBpTotal);
        Assert.Equal(-30, summary.NegativeBpTotal);
        Assert.Equal(5, summary.NetBpTotal);
    }

    private sealed class DummyRuleCatalog : IRuleCatalog
    {
        public Task<RuleCatalogDocument?> GetAsync(string catalogName, string culture, CancellationToken cancellationToken) =>
            Task.FromResult<RuleCatalogDocument?>(null);
    }
}
