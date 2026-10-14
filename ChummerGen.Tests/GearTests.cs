using System.Xml.Linq;
using ChummerGen.Domain.Abstractions;
using ChummerGen.Domain.Character;
using ChummerGen.Domain.Character.Gear;
using ChummerGen.Domain.Chum;
using ChummerGen.Domain.Models;

namespace ChummerGen.Tests;

public sealed class GearTests
{
    private const string SampleGearXml = """
        <character>
            <name>Test Runner</name>
            <maxavail>12</maxavail>
            <gears>
                <gear>
                    <guid>63c2e3de-76cd-4781-b110-71c80dbc1a2f</guid>
                    <name>Hermes Ikon</name>
                    <category>Commlink</category>
                    <capacity>4</capacity>
                    <maxrating>0</maxrating>
                    <rating>0</rating>
                    <qty>1</qty>
                    <avail>0</avail>
                    <cost>3000</cost>
                    <equipped>True</equipped>
                    <source>SR4</source>
                    <page>316</page>
                    <response>4</response>
                    <signal>3</signal>
                    <children>
                        <gear>
                            <guid>04fd2e0f-225b-4920-8dfb-c403e2e26989</guid>
                            <name>Redcap Nix</name>
                            <category>Commlink Operating System</category>
                            <capacity>[1]</capacity>
                            <maxrating>0</maxrating>
                            <rating>0</rating>
                            <qty>1</qty>
                            <avail>0</avail>
                            <cost>400</cost>
                            <equipped>True</equipped>
                            <source>SR4</source>
                            <page>317</page>
                            <firewall>1</firewall>
                            <system>2</system>
                            <children />
                        </gear>
                    </children>
                </gear>
                <gear>
                    <guid>a1b2c3d4-0000-0000-0000-000000000001</guid>
                    <name>Fake SIN</name>
                    <category>ID/Credsticks</category>
                    <minrating>1</minrating>
                    <maxrating>6</maxrating>
                    <rating>4</rating>
                    <qty>1</qty>
                    <avail>Rating * 3</avail>
                    <cost>Rating * 1000</cost>
                    <children />
                </gear>
                <gear>
                    <guid>a1b2c3d4-0000-0000-0000-000000000002</guid>
                    <name>Ammo: Regular Ammo</name>
                    <category>Ammunition</category>
                    <rating>0</rating>
                    <qty>100</qty>
                    <costfor>10</costfor>
                    <cost>20</cost>
                    <avail>2R</avail>
                    <children />
                </gear>
            </gears>
            <unrelatedNode key="preserveMe">
                <childTag>UntouchedData</childTag>
            </unrelatedNode>
        </character>
        """;

    [Fact]
    public void ParsesGearItemsAndCalculatesCostsAndAvail()
    {
        var session = new CharacterSession(ChumSerializer.Parse(SampleGearXml));
        var items = GearModule.GetGearItems(session);

        Assert.Equal(3, items.Count);

        var commlink = items[0];
        Assert.Equal("Hermes Ikon", commlink.Name);
        Assert.Equal("Commlink", commlink.Category);
        Assert.Single(commlink.Children);
        Assert.Equal("Redcap Nix", commlink.Children[0].Name);
        Assert.Equal(3400, commlink.TotalCost); // 3000 + 400
        Assert.Equal(3, commlink.CapacityRemaining); // base 4 - [1]*1

        var sin = items[1];
        Assert.Equal("Fake SIN", sin.Name);
        Assert.Equal(4000, sin.TotalCost); // Rating 4 * 1000
        Assert.Equal(12, sin.CalculateAvailValue()); // Rating 4 * 3 = 12

        var ammo = items[2];
        Assert.Equal("Ammo: Regular Ammo", ammo.Name);
        Assert.Equal(200, ammo.TotalCost); // (20 * 100) / 10 = 200
        Assert.Equal("2R", ammo.TotalAvail);
        Assert.Equal(2, ammo.CalculateAvailValue());

        var totalCost = GearModule.CalculateTotalGearCost(session);
        Assert.Equal(7600, totalCost); // 3400 + 4000 + 200
    }

    [Fact]
    public void SafeEditingPreservesXmlStructureAndUnrelatedNodes()
    {
        var session = new CharacterSession(ChumSerializer.Parse(SampleGearXml));
        var items = GearModule.GetGearItems(session);

        var commlink = items[0];
        commlink.Quantity = 2;
        commlink.Notes = "Custom Commlink Notes";
        commlink.Location = "Tasche";
        commlink.DiscountCost = true;

        var serialized = session.Serialize();
        var roundtripped = ChumSerializer.Parse(serialized);

        var unrelated = roundtripped.Xml.Root?.Element("unrelatedNode");
        Assert.NotNull(unrelated);
        Assert.Equal("preserveMe", unrelated.Attribute("key")?.Value);
        Assert.Equal("UntouchedData", unrelated.Element("childTag")?.Value);

        var reloadedItems = GearModule.GetGearItems(new CharacterSession(roundtripped));
        var reloadedCommlink = reloadedItems[0];

        Assert.Equal(2, reloadedCommlink.Quantity);
        Assert.Equal("Custom Commlink Notes", reloadedCommlink.Notes);
        Assert.Equal("Tasche", reloadedCommlink.Location);
        Assert.True(reloadedCommlink.DiscountCost);
        Assert.Single(reloadedCommlink.Children);
        Assert.Equal("Redcap Nix", reloadedCommlink.Children[0].Name);
    }

    [Fact]
    public async Task ValidatesAvailabilityAndCapacityLimits()
    {
        const string invalidXml = """
            <character>
                <maxavail>10</maxavail>
                <gears>
                    <gear>
                        <name>Small Pouch</name>
                        <capacity>2</capacity>
                        <avail>4</avail>
                        <qty>1</qty>
                        <children>
                            <gear>
                                <name>Bulky Device</name>
                                <capacity>[3]</capacity>
                                <avail>5</avail>
                                <qty>1</qty>
                                <children />
                            </gear>
                        </children>
                    </gear>
                    <gear>
                        <name>Restricted Military Gear</name>
                        <avail>14F</avail>
                        <qty>1</qty>
                        <children />
                    </gear>
                </gears>
            </character>
            """;

        var session = new CharacterSession(ChumSerializer.Parse(invalidXml));
        var module = new GearModule();
        var catalog = new EmptyRuleCatalog();

        var issues = await module.ValidateAsync(session, catalog);

        Assert.Contains(issues, i => i.Code == "gear-capacity-exceeded");
        Assert.Contains(issues, i => i.Code == "gear-avail-exceeded");
    }

    [Fact]
    public void RealStreetSamuraiChumFileParsesAndCalculatesCorrectly()
    {
        var chumPath = @"E:\Github\ChummerGen\bin\saves\examples\Street Samurai.chum";
        Assert.True(File.Exists(chumPath));

        var session = new CharacterSession(ChumSerializer.Parse(File.ReadAllText(chumPath)));
        var items = GearModule.GetGearItems(session);

        Assert.Equal(16, items.Count);

        var commlink = items.First(i => i.Name == "Hermes Ikon");
        Assert.Single(commlink.Children);
        Assert.Equal("Redcap Nix", commlink.Children[0].Name);
        Assert.Equal(3400, commlink.TotalCost);

        var explosiveAmmo = items.First(i => i.Name == "Ammo: Explosive Rounds");
        Assert.Equal(200, explosiveAmmo.Quantity);
        Assert.Equal(1000, explosiveAmmo.TotalCost); // 200 qty * 50 cost / 10 costfor = 1000

        var total = GearModule.CalculateTotalGearCost(session);
        Assert.True(total > 30000);
    }

    private sealed class EmptyRuleCatalog : IRuleCatalog
    {
        public Task<RuleCatalogDocument?> GetAsync(string catalogName, string culture, CancellationToken cancellationToken) =>
            Task.FromResult<RuleCatalogDocument?>(null);
    }
}