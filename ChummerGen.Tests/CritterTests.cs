using System.Xml.Linq;
using ChummerGen.Domain.Abstractions;
using ChummerGen.Domain.Character;
using ChummerGen.Domain.Character.Critters;
using ChummerGen.Domain.Chum;
using ChummerGen.Domain.Models;

namespace ChummerGen.Tests;

public sealed class CritterTests
{
    private const string SampleCritterXml = """
        <character>
            <appversion>467</appversion>
            <name>Warform Cheetah</name>
            <metatype>Cheetah</metatype>
            <iscritter>True</iscritter>
            <critter>True</critter>
            <mutantcritterbaseskills>2</mutantcritterbaseskills>
            <unknown_root_flag value="preserved" />
            <critterpowers>
                <critterpower>
                    <guid>9153dcb8-90d8-4ea1-847b-8761ce78dbce</guid>
                    <name>Natural Weapon</name>
                    <extra>Claws/Bite: DV 5P, AP 0</extra>
                    <category>Mundane</category>
                    <type>P</type>
                    <action>Complex</action>
                    <range>Touch</range>
                    <duration>Instant</duration>
                    <source>RW</source>
                    <page>206</page>
                    <points>0</points>
                    <counttowardslimit>True</counttowardslimit>
                    <bonus><selecttext /><nested_bonus_tag keep="yes" /></bonus>
                    <notes>Sehr scharf</notes>
                    <custom_extension_node>DoNotDrop</custom_extension_node>
                </critterpower>
                <critterpower>
                    <guid>a2222222-2222-2222-2222-222222222222</guid>
                    <name>Allergy</name>
                    <extra>Silver</extra>
                    <category>Weakness</category>
                    <type>P</type>
                    <action>Auto</action>
                    <range>Self</range>
                    <duration>Always</duration>
                    <source>SR4</source>
                    <page>290</page>
                    <points>0</points>
                    <counttowardslimit>False</counttowardslimit>
                    <notes>Empfindlich gegen Silber</notes>
                </critterpower>
                <critterpower>
                    <guid>b3333333-3333-3333-3333-333333333333</guid>
                    <name>Concealment</name>
                    <category>Paranormal</category>
                    <type>M</type>
                    <action>Simple</action>
                    <range>LOS</range>
                    <duration>Sustained</duration>
                    <source>RW</source>
                    <page>208</page>
                    <points>1.5</points>
                    <counttowardslimit>True</counttowardslimit>
                    <notes />
                </critterpower>
            </critterpowers>
        </character>
        """;

    [Fact]
    public void LoadsCritterCharacterFromLegacyXml()
    {
        var doc = ChumSerializer.Parse(SampleCritterXml);
        var model = new CritterCharacterModel(doc);

        Assert.True(model.IsCritter);
        Assert.True(model.CritterEnabled);
        Assert.True(model.HasCritterData);
        Assert.Equal(2, model.MutantCritterBaseSkills);
        Assert.Equal(3, model.Powers.Count);

        var first = model.Powers[0];
        Assert.Equal("9153dcb8-90d8-4ea1-847b-8761ce78dbce", first.Guid);
        Assert.Equal("Natural Weapon", first.Name);
        Assert.Equal("Claws/Bite: DV 5P, AP 0", first.Extra);
        Assert.Equal("Natural Weapon (Claws/Bite: DV 5P, AP 0)", first.DisplayName);
        Assert.Equal("Mundane", first.Category);
        Assert.Equal("P", first.Type);
        Assert.Equal("Complex", first.Action);
        Assert.Equal("Touch", first.Range);
        Assert.Equal("Instant", first.Duration);
        Assert.Equal("RW", first.Source);
        Assert.Equal("206", first.Page);
        Assert.Equal(0.0, first.Points);
        Assert.True(first.CountTowardsLimit);
        Assert.Equal("Sehr scharf", first.Notes);
        Assert.False(first.IsWeakness);
    }

    [Fact]
    public void DistinguishesCritterPowersAndWeaknesses()
    {
        var doc = ChumSerializer.Parse(SampleCritterXml);
        var model = new CritterCharacterModel(doc);

        Assert.Equal(2, model.ActivePowers.Count);
        Assert.Single(model.Weaknesses);
        Assert.Equal("Allergy", model.Weaknesses[0].Name);
        Assert.True(model.Weaknesses[0].IsWeakness);
        Assert.Equal(2, model.CountTowardsLimitTotal);
        Assert.Equal(1.5, model.TotalPowerPoints);
    }

    [Fact]
    public void PreservesUnknownXmlAndBonusWhenEditing()
    {
        var session = new CharacterSession(ChumSerializer.Parse(SampleCritterXml));
        var model = new CritterCharacterModel(session);

        var power = model.FindPower("9153dcb8-90d8-4ea1-847b-8761ce78dbce")!;
        power.Notes = "Aktualisierte Notiz";
        power.CountTowardsLimit = false;

        var serialized = session.Serialize();
        var reloadedDoc = ChumSerializer.Parse(serialized);
        var reloadedModel = new CritterCharacterModel(reloadedDoc);

        var reloadedPower = reloadedModel.FindPower("9153dcb8-90d8-4ea1-847b-8761ce78dbce")!;
        Assert.Equal("Aktualisierte Notiz", reloadedPower.Notes);
        Assert.False(reloadedPower.CountTowardsLimit);

        // Verify unmodeled/bonus XML preservation
        var bonus = reloadedPower.Element.Element("bonus");
        Assert.NotNull(bonus);
        Assert.NotNull(bonus.Element("nested_bonus_tag"));
        Assert.Equal("yes", (string?)bonus.Element("nested_bonus_tag")?.Attribute("keep"));

        var customChild = reloadedPower.Element.Element("custom_extension_node");
        Assert.NotNull(customChild);
        Assert.Equal("DoNotDrop", customChild.Value);

        var unknownRoot = reloadedDoc.Xml.Root?.Element("unknown_root_flag");
        Assert.NotNull(unknownRoot);
        Assert.Equal("preserved", (string?)unknownRoot.Attribute("value"));
    }

    [Fact]
    public void SafelyAddsAndRemovesCritterPower()
    {
        var session = new CharacterSession(ChumSerializer.Parse("<character><name>Runner</name></character>"));
        var model = new CritterCharacterModel(session);

        Assert.False(model.HasCritterData);

        var newPower = CritterPower.Create(
            name: "Regeneration",
            category: "Paranormal",
            type: "P",
            action: "Auto",
            range: "Self",
            duration: "Always",
            source: "RW",
            page: "212",
            extra: "",
            points: 0,
            countTowardsLimit: true,
            notes: "Heilt schnell");

        model.AddPower(newPower);

        Assert.True(model.CritterEnabled);
        Assert.True(model.HasCritterData);
        Assert.Single(model.Powers);
        Assert.Equal("Regeneration", model.Powers[0].Name);

        var removed = model.RemovePower(newPower.Guid);
        Assert.True(removed);
        Assert.Empty(model.Powers);
    }

    [Fact]
    public void HandlesStandardMetahumanWithoutCritterData()
    {
        const string standardRunnerXml = "<character><name>Normal Metahuman</name><metatype>Human</metatype></character>";
        var session = new CharacterSession(ChumSerializer.Parse(standardRunnerXml));
        var model = new CritterCharacterModel(session);

        Assert.False(model.IsCritter);
        Assert.False(model.CritterEnabled);
        Assert.False(model.HasCritterData);
        Assert.Empty(model.Powers);
        Assert.Equal(0, model.CountTowardsLimitTotal);
        Assert.Equal(0.0, model.TotalPowerPoints);

        // Can safely activate without corrupting anything
        model.CritterEnabled = true;
        Assert.True(model.CritterEnabled);
        Assert.True(model.HasCritterData);

        var reloaded = ChumSerializer.Parse(session.Serialize());
        Assert.Equal("True", reloaded.Xml.Root?.Element("critter")?.Value);
    }

    [Fact]
    public async Task CritterModule_ValidatesAndReportsIssues()
    {
        var invalidXml = """
            <character>
                <iscritter>True</iscritter>
                <critterpowers>
                    <critterpower>
                        <guid>bad-1</guid>
                        <name></name>
                        <points>-2.0</points>
                    </critterpower>
                </critterpowers>
            </character>
            """;

        var session = new CharacterSession(ChumSerializer.Parse(invalidXml));
        var module = new CritterModule();
        var rules = new EmptyRuleCatalog();

        await module.RecalculateAsync(session, rules);
        var issues = await module.ValidateAsync(session, rules);

        Assert.Contains(issues, i => i.Code == "critter-power-missing-name" && i.Severity == CharacterIssueSeverity.Error);
        Assert.Contains(issues, i => i.Code == "critter-power-negative-points" && i.Severity == CharacterIssueSeverity.Warning);
        Assert.Contains(issues, i => i.Code == "critter-missing-metatype" && i.Severity == CharacterIssueSeverity.Warning);
    }

    [Fact]
    public void LegacySaveFile_WarformCheetahs_LoadsCorrectly()
    {
        const string filePath = @"E:\Github\ChummerGen\bin\saves\Horizon Adventures\Columbian Subterfuge\Warform Cheetahs.chum";
        if (!File.Exists(filePath))
            return;

        var doc = ChumSerializer.LoadFile(filePath);
        var model = new CritterCharacterModel(doc);

        Assert.True(model.IsCritter);
        Assert.True(model.CritterEnabled);
        Assert.NotEmpty(model.Powers);
        Assert.Contains(model.Powers, p => p.Name == "Natural Weapon");
    }

    private sealed class EmptyRuleCatalog : IRuleCatalog
    {
        public Task<RuleCatalogDocument?> GetAsync(string catalogName, string culture, CancellationToken cancellationToken) =>
            Task.FromResult<RuleCatalogDocument?>(null);
    }
}
