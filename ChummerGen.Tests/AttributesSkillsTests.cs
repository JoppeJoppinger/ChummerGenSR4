using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using ChummerGen.Domain.Abstractions;
using ChummerGen.Domain.Character;
using ChummerGen.Domain.Character.AttributesSkills;
using ChummerGen.Domain.Chum;
using ChummerGen.Domain.Models;
using Xunit;

namespace ChummerGen.Tests;

public sealed class AttributesSkillsTests
{
    private sealed class DummyRuleCatalog : IRuleCatalog
    {
        public Task<RuleCatalogDocument?> GetAsync(string catalogName, string culture, CancellationToken cancellationToken) =>
            Task.FromResult<RuleCatalogDocument?>(null);
    }

    private const string SampleChumXml = """
        <character>
          <appversion>467</appversion>
          <name>Street Sam</name>
          <metatype>Human</metatype>
          <buildpoints>400</buildpoints>
          <attributes>
            <attribute>
              <name>BOD</name>
              <metatypemin>1</metatypemin>
              <metatypemax>6</metatypemax>
              <metatypeaugmax>9</metatypeaugmax>
              <value>4</value>
              <augmodifier>0</augmodifier>
              <totalvalue>4</totalvalue>
            </attribute>
            <attribute>
              <name>AGI</name>
              <metatypemin>1</metatypemin>
              <metatypemax>6</metatypemax>
              <metatypeaugmax>9</metatypeaugmax>
              <value>5</value>
              <augmodifier>2</augmodifier>
              <totalvalue>7</totalvalue>
            </attribute>
            <attribute>
              <name>REA</name>
              <metatypemin>1</metatypemin>
              <metatypemax>6</metatypemax>
              <metatypeaugmax>9</metatypeaugmax>
              <value>3</value>
              <augmodifier>2</augmodifier>
              <totalvalue>5</totalvalue>
            </attribute>
            <attribute>
              <name>STR</name>
              <metatypemin>1</metatypemin>
              <metatypemax>6</metatypemax>
              <metatypeaugmax>9</metatypeaugmax>
              <value>3</value>
              <augmodifier>1</augmodifier>
              <totalvalue>4</totalvalue>
            </attribute>
            <attribute>
              <name>CHA</name>
              <metatypemin>1</metatypemin>
              <metatypemax>6</metatypemax>
              <metatypeaugmax>9</metatypeaugmax>
              <value>2</value>
              <augmodifier>0</augmodifier>
              <totalvalue>2</totalvalue>
            </attribute>
            <attribute>
              <name>INT</name>
              <metatypemin>1</metatypemin>
              <metatypemax>6</metatypemax>
              <metatypeaugmax>9</metatypeaugmax>
              <value>4</value>
              <augmodifier>0</augmodifier>
              <totalvalue>4</totalvalue>
            </attribute>
            <attribute>
              <name>LOG</name>
              <metatypemin>1</metatypemin>
              <metatypemax>6</metatypemax>
              <metatypeaugmax>9</metatypeaugmax>
              <value>3</value>
              <augmodifier>0</augmodifier>
              <totalvalue>3</totalvalue>
            </attribute>
            <attribute>
              <name>WIL</name>
              <metatypemin>1</metatypemin>
              <metatypemax>6</metatypemax>
              <metatypeaugmax>9</metatypeaugmax>
              <value>4</value>
              <augmodifier>0</augmodifier>
              <totalvalue>4</totalvalue>
            </attribute>
            <attribute>
              <name>EDG</name>
              <metatypemin>2</metatypemin>
              <metatypemax>7</metatypemax>
              <metatypeaugmax>7</metatypeaugmax>
              <value>3</value>
              <augmodifier>0</augmodifier>
              <totalvalue>3</totalvalue>
            </attribute>
          </attributes>
          <init>
            <base>9</base>
            <total>9</total>
          </init>
          <ip>
            <base>1</base>
            <total>3</total>
          </ip>
          <skillgroups>
            <skillgroup>
              <name>Athletics</name>
              <rating>2</rating>
              <ratingmax>6</ratingmax>
              <broken>False</broken>
            </skillgroup>
          </skillgroups>
          <skills>
            <skill>
              <name>Running</name>
              <skillgroup>Athletics</skillgroup>
              <skillcategory>Physical Active</skillcategory>
              <grouped>True</grouped>
              <default>True</default>
              <rating>2</rating>
              <ratingmax>6</ratingmax>
              <knowledge>False</knowledge>
              <exotic>False</exotic>
              <spec></spec>
              <allowdelete>False</allowdelete>
              <attribute>STR</attribute>
              <totalvalue>6</totalvalue>
            </skill>
            <skill>
              <name>Pistols</name>
              <skillgroup>Firearms</skillgroup>
              <skillcategory>Combat Active</skillcategory>
              <grouped>False</grouped>
              <default>True</default>
              <rating>4</rating>
              <ratingmax>6</ratingmax>
              <knowledge>False</knowledge>
              <exotic>False</exotic>
              <spec>Semi-Automatics</spec>
              <allowdelete>False</allowdelete>
              <attribute>AGI</attribute>
              <totalvalue>11</totalvalue>
            </skill>
            <skill>
              <name>Locksmith</name>
              <skillgroup></skillgroup>
              <skillcategory>Technical Active</skillcategory>
              <grouped>False</grouped>
              <default>False</default>
              <rating>0</rating>
              <ratingmax>6</ratingmax>
              <knowledge>False</knowledge>
              <exotic>False</exotic>
              <spec></spec>
              <allowdelete>False</allowdelete>
              <attribute>AGI</attribute>
              <totalvalue>0</totalvalue>
            </skill>
            <skill>
              <name>Perception</name>
              <skillgroup></skillgroup>
              <skillcategory>Physical Active</skillcategory>
              <grouped>False</grouped>
              <default>True</default>
              <rating>0</rating>
              <ratingmax>6</ratingmax>
              <knowledge>False</knowledge>
              <exotic>False</exotic>
              <spec></spec>
              <allowdelete>False</allowdelete>
              <attribute>INT</attribute>
              <totalvalue>3</totalvalue>
            </skill>
          </skills>
        </character>
        """;

    private static CharacterSession CreateSession(string xml = SampleChumXml)
    {
        var doc = ChumSerializer.Parse(xml);
        return new CharacterSession(doc);
    }

    [Fact]
    public void Manager_Loads_Attributes_Correctly()
    {
        var session = CreateSession();
        var manager = new AttributesSkillsManager(session);

        var attrs = manager.GetAttributes();
        Assert.Equal(9, attrs.Count);

        var agi = manager.GetAttribute("AGI");
        Assert.NotNull(agi);
        Assert.Equal(5, agi.BaseValue);
        Assert.Equal(2, agi.AugModifier);
        Assert.Equal(7, agi.TotalValue);
    }

    [Fact]
    public void DerivedAttributes_Calculate_Correctly()
    {
        var session = CreateSession();
        var manager = new AttributesSkillsManager(session);

        var derived = manager.GetDerivedAttributes();

        // WIL (4) + CHA (2) = 6
        Assert.Equal(6, derived.Composure);
        // INT (4) + CHA (2) = 6
        Assert.Equal(6, derived.JudgeIntentions);
        // STR (4 total) + BOD (4 total) = 8
        Assert.Equal(8, derived.LiftAndCarry);
        // LOG (3) + WIL (4) = 7
        Assert.Equal(7, derived.Memory);
        // Base Init: REA (5 total) + INT (4 total) = 9
        Assert.Equal(9, derived.InitiativeBase);
        Assert.Equal(9, derived.InitiativeTotal);
        Assert.Equal(3, derived.InitiativePasses);
    }

    [Fact]
    public void Attribute_Update_Updates_Xml_And_Recalculates()
    {
        var session = CreateSession();
        var manager = new AttributesSkillsManager(session);

        manager.UpdateAttributeValue("BOD", 5);

        var bod = manager.GetAttribute("BOD");
        Assert.NotNull(bod);
        Assert.Equal(5, bod.BaseValue);
        Assert.Equal(5, bod.TotalValue);

        // Check lift and carry after BOD increase (BOD 5 + STR 4 = 9)
        var derived = manager.GetDerivedAttributes();
        Assert.Equal(9, derived.LiftAndCarry);

        // Verify XML serialization preserves unknown and known tags
        var serialized = session.Serialize();
        Assert.Contains("<name>BOD</name>", serialized);
        Assert.Contains("<value>5</value>", serialized);
    }

    [Fact]
    public void Skill_Defaulting_DicePool_Calculates_Accurately()
    {
        var session = CreateSession();
        var manager = new AttributesSkillsManager(session);

        // Perception: Rating 0, Default True, Attribute INT (4). Pool = 4 - 1 = 3.
        int percPool = manager.CalculateSkillTotal(0, "INT", canDefault: true);
        Assert.Equal(3, percPool);

        // Locksmith: Rating 0, Default False, Attribute AGI (7). Pool = 0.
        int lockPool = manager.CalculateSkillTotal(0, "AGI", canDefault: false);
        Assert.Equal(0, lockPool);

        // Pistols: Rating 4, Attribute AGI (7). Pool = 4 + 7 = 11.
        int pistolPool = manager.CalculateSkillTotal(4, "AGI", canDefault: true);
        Assert.Equal(11, pistolPool);
    }

    [Fact]
    public void SkillGroup_Update_Propagates_To_Grouped_Skills()
    {
        var session = CreateSession();
        var manager = new AttributesSkillsManager(session);

        manager.UpdateSkillGroupRating("Athletics", 3);

        var running = manager.GetSkills().First(s => s.Name == "Running");
        Assert.Equal(3, running.Rating);
        // STR total = 4. 3 + 4 = 7.
        Assert.Equal(7, running.TotalValue);

        var group = manager.GetSkillGroups().First(g => g.Name == "Athletics");
        Assert.Equal(3, group.Rating);
    }

    [Fact]
    public void Skill_Specialization_Updates_And_Summarizes_Bp()
    {
        var session = CreateSession();
        var manager = new AttributesSkillsManager(session);

        manager.UpdateSkillSpecialization("Perception", "Visual");

        var perc = manager.GetSkills().First(s => s.Name == "Perception");
        Assert.Equal("Visual", perc.Specialization);

        var summary = manager.CalculateSkillsSummary();
        // Athletics group rating 2 = 20 BP
        Assert.Equal(20, summary.SkillGroupsBp);
        // Pistols rating 4 = 16 BP
        Assert.Equal(16, summary.ActiveSkillsBp);
        // Pistols spec + Perception spec = 2 * 2 = 4 BP
        Assert.Equal(4, summary.SpecializationsBp);
        Assert.Equal(40, summary.TotalBp);
    }

    [Fact]
    public async Task Module_Validates_Attribute_And_Skill_Limits()
    {
        var session = CreateSession();
        var module = new AttributesSkillsModule();
        var catalog = new DummyRuleCatalog();

        var issues = await module.ValidateAsync(session, catalog);
        Assert.Empty(issues);

        // Set AGI above max
        var manager = new AttributesSkillsManager(session);
        manager.UpdateAttributeValue("AGI", 7); // Max is 6

        var issuesWithMax = await module.ValidateAsync(session, catalog);
        Assert.Contains(issuesWithMax, i => i.Code == "ATTR_ABOVE_MAX");
    }

    [Fact]
    public void StreetSamurai_Example_Loads_And_Validates()
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "bin", "saves", "examples", "Street Samurai.chum");
        if (!File.Exists(filePath))
            return;

        var doc = ChumSerializer.LoadFile(filePath);
        var session = new CharacterSession(doc);
        var manager = new AttributesSkillsManager(session);

        var attrs = manager.GetAttributes();
        Assert.True(attrs.Count >= 8);

        var skills = manager.GetSkills();
        Assert.True(skills.Count > 10);

        var derived = manager.GetDerivedAttributes();
        Assert.True(derived.InitiativeTotal > 0);
        Assert.True(derived.Composure > 0);
    }
}
