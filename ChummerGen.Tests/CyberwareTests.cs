using System.Xml.Linq;
using ChummerGen.Domain.Abstractions;
using ChummerGen.Domain.Character;
using ChummerGen.Domain.Character.Cyberware;
using ChummerGen.Domain.Chum;
using ChummerGen.Domain.Models;

namespace ChummerGen.Tests;

public sealed class CyberwareTests
{
    // Synthesized from the real cyberware.xml/bioware.xml schema (grades, categories, cyberware/bioware nodes).
    private const string CyberwareCatalogXml = """
        <chummer>
          <grades>
            <grade><name>Standard</name><ess>1</ess><cost>1</cost><avail>0</avail><source>SR4</source></grade>
            <grade><name>Alphaware</name><ess>0.8</ess><cost>2</cost><avail>0</avail><source>SR4</source></grade>
            <grade><name>Standard (Second-Hand)</name><ess>1.2</ess><cost>0.5</cost><avail>-1</avail><source>SR4</source></grade>
          </grades>
          <categories><category>Earware</category><category>Headware</category></categories>
          <cyberwares>
            <cyberware>
              <name>Cyberears</name><category>Earware</category><rating>4</rating><minrating>1</minrating>
              <ess>(Rating * 0.1) + 0.1</ess><capacity>Rating * 4</capacity><avail>FixedValues(0,4,6,8)</avail>
              <cost>FixedValues(500,750,1000,1500)</cost><subsystems>Earware,Nanocybernetics,</subsystems>
              <source>SR4</source><page>341</page>
            </cyberware>
            <cyberware>
              <name>Audio Enhancement</name><category>Earware</category><rating>3</rating>
              <ess>0.1</ess><capacity>[Rating]</capacity><avail>Rating * 3</avail><cost>Rating * 1500</cost>
              <source>SR4</source><page>341</page>
            </cyberware>
            <cyberware>
              <name>Sound Link</name><category>Earware</category>
              <ess>0.1</ess><capacity>[*]</capacity><avail>0</avail><cost>250</cost><source>SR4</source><page>341</page>
            </cyberware>
            <cyberware>
              <name>Spatial Recognizer</name><category>Earware</category>
              <ess>0.1</ess><capacity>[2]</capacity><avail>+2R</avail><cost>750</cost><source>SR4</source><page>341</page>
            </cyberware>
            <cyberware>
              <name>Commlink</name><category>Headware</category>
              <ess>0.2</ess><capacity>[2]</capacity><avail>0</avail><cost>2000</cost>
              <allowgear><gearcategory>Commlink</gearcategory></allowgear><source>SR4</source><page>338</page>
            </cyberware>
            <cyberware>
              <name>Attention Coprocessor</name><category>Headware</category><rating>3</rating>
              <ess>0.3</ess><capacity>0</capacity><avail>8</avail><cost>Rating * 3000</cost>
              <bonus><adapsin /></bonus>
              <source>AU</source><page>36</page>
            </cyberware>
            <cyberware>
              <name>Gill Thing</name><category>Headware</category>
              <ess>0.1</ess><capacity>0</capacity><avail>0</avail><cost>Variable(100-500)</cost><source>SR4</source><page>1</page>
            </cyberware>
            <cyberware>
              <name>Hand Razors</name><category>Headware</category>
              <ess>0.1</ess><capacity>0</capacity><avail>0</avail><cost>100</cost><addweapon>Hand Razors</addweapon>
              <source>SR4</source><page>1</page>
            </cyberware>
            <cyberware>
              <name>Broken Entry</name><category>Headware</category><ess>0.1</ess><source>SR4</source>
            </cyberware>
          </cyberwares>
        </chummer>
        """;

    private const string BiowareCatalogXml = """
        <chummer>
          <grades>
            <grade><name>Standard</name><ess>1</ess><cost>1</cost><avail>0</avail><source>SR4</source></grade>
            <grade><name>Betaware</name><ess>0.7</ess><cost>4</cost><avail>0</avail><source>SR4</source></grade>
          </grades>
          <biowares>
            <bioware>
              <name>Muscle Toner</name><category>Basic</category><rating>4</rating>
              <ess>Rating * 0.2</ess><capacity>0</capacity><avail>Rating * 4R</avail><cost>Rating * 8000</cost>
              <source>SR4</source><page>81</page>
            </bioware>
            <bioware>
              <name>Cat's Eyes</name><category>Genetech: Cultured</category>
              <ess>0.2</ess><capacity>0</capacity><avail>8</avail><cost>5000</cost><forcegrade>Betaware</forcegrade>
              <source>SR4</source><page>1</page>
            </bioware>
          </biowares>
        </chummer>
        """;

    private const string CharacterXml = """
        <character>
          <name>Test</name>
          <improvements>
            <improvement><improvedname>X</improvedname><sourcename>aaaaaaaa-0000-0000-0000-000000000003</sourcename><improvementsource>Cyberware</improvementsource></improvement>
            <improvement><improvedname>Y</improvedname><sourcename>unrelated</sourcename><improvementsource>Quality</improvementsource></improvement>
          </improvements>
          <cyberwares>
            <cyberware>
              <guid>aaaaaaaa-0000-0000-0000-000000000001</guid><name>Cyberears</name><category>Earware</category><limbslot />
              <ess>(Rating * 0.1) + 0.1</ess><capacity>Rating * 4</capacity><avail>FixedValues(0,4,6,8)</avail>
              <cost>FixedValues(500,750,1000,1500)</cost><source>SR4</source><page>341</page>
              <rating>1</rating><minrating>1</minrating><maxrating>4</maxrating><subsystems>Earware,Nanocybernetics,</subsystems>
              <grade>Standard</grade><location /><suite>False</suite><essdiscount>0</essdiscount><forcegrade /><bonus />
              <improvementsource>Cyberware</improvementsource>
              <unknownNode keep="yes">stay</unknownNode>
              <children>
                <cyberware>
                  <guid>aaaaaaaa-0000-0000-0000-000000000002</guid><name>Sound Link</name><category>Earware</category>
                  <ess>0.1</ess><capacity>[*]</capacity><avail>0</avail><cost>250</cost><source>SR4</source><page>341</page>
                  <rating>0</rating><minrating>0</minrating><maxrating>0</maxrating><subsystems /><grade>Standard</grade>
                  <location /><suite>False</suite><essdiscount>0</essdiscount><forcegrade /><bonus />
                  <improvementsource>Cyberware</improvementsource><children /><notes /><discountedcost>False</discountedcost>
                </cyberware>
                <cyberware>
                  <guid>aaaaaaaa-0000-0000-0000-000000000003</guid><name>Audio Enhancement</name><category>Earware</category>
                  <ess>0.1</ess><capacity>[Rating]</capacity><avail>Rating * 3</avail><cost>Rating * 1500</cost><source>SR4</source><page>341</page>
                  <rating>2</rating><minrating>1</minrating><maxrating>3</maxrating><subsystems /><grade>Standard</grade>
                  <location /><suite>False</suite><essdiscount>0</essdiscount><forcegrade />
                  <bonus><initiative>1</initiative></bonus>
                  <improvementsource>Cyberware</improvementsource><children /><notes /><discountedcost>False</discountedcost>
                </cyberware>
              </children>
              <notes /><discountedcost>False</discountedcost>
            </cyberware>
            <cyberware>
              <guid>aaaaaaaa-0000-0000-0000-000000000004</guid><name>Muscle Toner</name><category>Basic</category>
              <ess>Rating * 0.2</ess><capacity>0</capacity><avail>Rating * 4R</avail><cost>Rating * 8000</cost><source>SR4</source><page>81</page>
              <rating>2</rating><minrating>1</minrating><maxrating>4</maxrating><subsystems /><grade>Betaware</grade>
              <location /><suite>False</suite><essdiscount>0</essdiscount><forcegrade /><bonus />
              <improvementsource>Bioware</improvementsource><children /><notes /><discountedcost>False</discountedcost>
            </cyberware>
          </cyberwares>
          <unrelatedNode key="preserveMe"><childTag>UntouchedData</childTag></unrelatedNode>
        </character>
        """;

    private static CyberwareCatalog Catalog => CyberwareCatalog.FromXml(CyberwareCatalogXml, BiowareCatalogXml);

    private static CharacterSession NewSession(string xml = CharacterXml) => new(ChumSerializer.Parse(xml));

    [Fact]
    public void CalculatesEssenceCostAvailAndCapacityLikeLegacy()
    {
        var manager = new CyberwareManager(NewSession(), Catalog);
        var items = manager.GetItems();
        Assert.Equal(2, items.Count);

        var ears = items[0];
        var grades = manager.GradesFor(ears);
        Assert.Equal(0.2m, ears.CalculatedEssence(grades));
        Assert.Equal("0", ears.TotalAvail(grades));
        Assert.Equal("4", ears.CalculatedCapacity);
        Assert.Equal(2, ears.CapacityRemaining); // 4 - [*]0 - [Rating 2]
        // 500 own + [*] plugin included in price + Audio Enhancement 2 * 1500
        Assert.Equal(3500, ears.TotalCost(grades));

        var toner = items[1];
        Assert.Equal(CyberwareSource.Bioware, toner.SourceType);
        var bioGrades = manager.GradesFor(toner);
        Assert.Equal(0.28m, toner.CalculatedEssence(bioGrades)); // 0.4 * 0.7
        Assert.Equal(64000, toner.TotalCost(bioGrades)); // 16000 * 4
        Assert.Equal("8R", toner.TotalAvail(bioGrades));
    }

    [Fact]
    public void EssenceSummaryHalvesTheSmallerOfCyberwareAndBioware()
    {
        var summary = new CyberwareManager(NewSession(), Catalog).EssenceSummary();
        Assert.Equal(0.1m, summary.Cyberware);
        Assert.Equal(0.28m, summary.Bioware);
        Assert.Equal(0.38m, summary.Total);
    }

    [Fact]
    public void GradeChangeAffectsEssenceCostAndAvail()
    {
        var manager = new CyberwareManager(NewSession(), Catalog);
        var ears = manager.GetItems()[0];
        Assert.True(manager.SetGrade(ears, "Alphaware").Success);

        var grades = Catalog.CyberwareGrades;
        Assert.Equal(0.16m, ears.CalculatedEssence(grades));
        Assert.Equal(2, ears.Children.Count);
        Assert.All(ears.Children, c => Assert.Equal("Alphaware", c.GradeName));
        Assert.Equal(7000, ears.TotalCost(grades)); // 500*2 + Audio Enhancement 3000*2, [*] plugin included
        Assert.False(manager.SetGrade(ears, "Nonexistent").Success);
    }

    [Fact]
    public void SecondHandGradeUsesNegativeAvailModifierAndFloorsAtZero()
    {
        var session = NewSession();
        var manager = new CyberwareManager(session, Catalog);
        var toner = manager.GetItems()[1];
        toner.GradeName = "Standard";
        var (value, _) = toner.CalculateAvail(manager.GradesFor(toner));
        Assert.Equal(8, value);

        var ears = manager.GetItems()[0];
        ears.GradeName = "Standard (Second-Hand)";
        ears.SetText("avail", "0");
        Assert.Equal("0", ears.TotalAvail(Catalog.CyberwareGrades));
    }

    [Fact]
    public void AddsCatalogItemWithRealSchemaFieldsAndPreservesRestOfXml()
    {
        var session = NewSession();
        var manager = new CyberwareManager(session, Catalog);

        var result = manager.Add(CyberwareSource.Cyberware, "Commlink", "Alphaware", 0);
        Assert.True(result.Success, result.Error);
        var added = result.Item!;
        Assert.Equal("Headware", added.Category);
        Assert.Equal("Alphaware", added.GradeName);
        Assert.Equal("Cyberware", added.GetText("improvementsource"));
        Assert.NotNull(added.Element.Element("allowgear"));
        Assert.NotNull(added.Element.Element("children"));
        Assert.True(System.Guid.TryParse(added.Guid, out _));

        var roundtrip = ChumSerializer.Parse(session.Serialize());
        Assert.Equal("UntouchedData", roundtrip.Xml.Root!.Element("unrelatedNode")!.Element("childTag")!.Value);
        Assert.Equal("yes", roundtrip.Xml.Root.Element("cyberwares")!.Elements("cyberware").First()
            .Element("unknownNode")!.Attribute("keep")!.Value);
        Assert.Equal(3, new CyberwareManager(new CharacterSession(roundtrip), Catalog).GetItems().Count);
    }

    [Fact]
    public void AddsBiowareAndHonoursForcedAndLockedGrades()
    {
        var manager = new CyberwareManager(NewSession(), Catalog);
        var result = manager.Add(CyberwareSource.Bioware, "Cat's Eyes", "Standard", 0);
        Assert.True(result.Success, result.Error);
        Assert.Equal("Betaware", result.Item!.GradeName);
        Assert.Equal(CyberwareSource.Bioware, result.Item.SourceType);

        var toner = manager.Add(CyberwareSource.Bioware, "Muscle Toner", "Betaware", 3);
        Assert.True(toner.Success, toner.Error);
        Assert.Equal(3, toner.Item!.Rating);
        Assert.Equal(4, toner.Item.MaxRating);
    }

    [Theory]
    [InlineData("Muscle Toner", 0)]
    [InlineData("Muscle Toner", 5)]
    public void AddRejectsRatingOutsideCatalogRange(string name, int rating)
    {
        var manager = new CyberwareManager(NewSession(), Catalog);
        Assert.False(manager.Add(CyberwareSource.Bioware, name, "Standard", rating).Success);
    }

    [Theory]
    [InlineData("Attention Coprocessor")]
    [InlineData("Gill Thing")] // without variableCost, still refused!
    [InlineData("Hand Razors")]
    [InlineData("Broken Entry")]
    [InlineData("Missing Item")]
    public void AddRefusesEntriesThatNeedUnportedBehaviour(string name)
    {
        var session = NewSession();
        var manager = new CyberwareManager(session, Catalog);
        var before = session.Serialize();
        var result = manager.Add(CyberwareSource.Cyberware, name, "Standard", 3);
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Equal(before, session.Serialize());
    }

    [Fact]
    public void AddVariableCostValidatesRangeAndStoresSelectedValue()
    {
        var session = NewSession();
        var manager = new CyberwareManager(session, Catalog);

        // Missing variable cost is refused
        var missingCost = manager.Add(CyberwareSource.Cyberware, "Gill Thing", "Standard", 0);
        Assert.False(missingCost.Success);
        Assert.Contains("zwischen 100 und 500", missingCost.Error);

        // Below minimum is refused
        var tooLow = manager.Add(CyberwareSource.Cyberware, "Gill Thing", "Standard", 0, variableCost: 50);
        Assert.False(tooLow.Success);
        Assert.Contains("außerhalb des zulässigen Bereichs", tooLow.Error);

        // Above maximum is refused
        var tooHigh = manager.Add(CyberwareSource.Cyberware, "Gill Thing", "Standard", 0, variableCost: 600);
        Assert.False(tooHigh.Success);
        Assert.Contains("außerhalb des zulässigen Bereichs", tooHigh.Error);

        // Valid cost succeeds and persists exact cost in XML
        var valid = manager.Add(CyberwareSource.Cyberware, "Gill Thing", "Alphaware", 0, variableCost: 350);
        Assert.True(valid.Success, valid.Error);
        Assert.NotNull(valid.Item);
        Assert.Equal("350", valid.Item.CostExpression);
        Assert.Equal("350", valid.Item.Element.Element("cost")?.Value);
        // Alphaware has cost multiplier 2 -> 350 * 2 = 700
        Assert.Equal(700, valid.Item.OwnCost(Catalog.CyberwareGrades));

        // Unknown / unported nodes check still preserved on item
        Assert.Equal(0, valid.Item.Rating);
    }
    [Fact]
    public void AddPluginValidatesSubsystemsAndCapacityAndInheritsGrade()
    {
        var manager = new CyberwareManager(NewSession(), Catalog);
        var ears = manager.GetItems()[0];
        Assert.Equal(2, ears.CapacityRemaining);

        var link = manager.Add(CyberwareSource.Cyberware, "Sound Link", "Alphaware", 0, ears.Guid);
        Assert.True(link.Success, link.Error);
        Assert.Equal(ears.GradeName, link.Item!.GradeName);
        Assert.Same(ears.Element, link.Item.Element.Parent!.Parent);

        var tooBig = manager.Add(CyberwareSource.Cyberware, "Spatial Recognizer", "Standard", 0, ears.Guid);
        Assert.True(tooBig.Success, tooBig.Error); // [2] fits exactly
        var overflow = manager.Add(CyberwareSource.Cyberware, "Audio Enhancement", "Standard", 1, ears.Guid);
        Assert.False(overflow.Success);
        Assert.Contains("Kapazität", overflow.Error);

        var wrongCategory = manager.Add(CyberwareSource.Cyberware, "Commlink", "Standard", 0, ears.Guid);
        Assert.False(wrongCategory.Success);
    }

    [Fact]
    public void RatingEditsAreRefusedWhenBonusesWouldNeedRebuilding()
    {
        var manager = new CyberwareManager(NewSession(), Catalog);
        var ears = manager.GetItems()[0];
        var plugin = ears.Children[1];
        Assert.False(manager.SetRating(plugin, 3).Success);
        Assert.Equal(2, plugin.Rating);

        Assert.False(manager.SetRating(ears, 3).Success); // plugin has linked improvements

        var toner = manager.GetItems()[1];
        Assert.False(manager.SetRating(toner, 9).Success);
        Assert.True(manager.SetRating(toner, 3).Success);
        Assert.Equal(3, toner.Rating);
        Assert.Equal(96000, toner.OwnCost(Catalog.BiowareGrades)); // 24000 * 4
    }

    [Fact]
    public void RemoveDeletesSubtreeAndLinkedImprovementsOnly()
    {
        var session = NewSession();
        var manager = new CyberwareManager(session, Catalog);
        Assert.True(manager.Remove("aaaaaaaa-0000-0000-0000-000000000001").Success);

        var root = session.Xml.Root!;
        Assert.Single(root.Element("cyberwares")!.Elements("cyberware"));
        var remaining = root.Element("improvements")!.Elements("improvement").ToList();
        Assert.Single(remaining);
        Assert.Equal("unrelated", remaining[0].Element("sourcename")!.Value);
        Assert.NotNull(root.Element("unrelatedNode"));
        Assert.False(manager.Remove("missing").Success);
    }

    private const string WeaponsXml = """
        <chummer>
          <weapons>
            <weapon>
              <name>Hand Razors</name><category>Cyberware Melee Weapons</category><type>Melee</type><reach>0</reach>
              <damage>(STR/2)P</damage><ap>-</ap><mode>0</mode><rc>0</rc><ammo>0</ammo><conceal>0</conceal><avail>0</avail>
              <cost>100</cost><source>SR4</source><page>1</page>
            </weapon>
            <weapon>
              <name>Hand Razors</name><category>Melee</category><type>Melee</type><reach>0</reach>
              <damage>X</damage><ap>-</ap><mode>0</mode><rc>0</rc><ammo>0</ammo><avail>0</avail><cost>1</cost><source>X</source><page>1</page>
            </weapon>
            <weapon>
              <name>Taser</name><category>Cyberware Tasers</category><type>Ranged</type><reach>0</reach>
              <damage>6S(e)</damage><ap>-half</ap><mode>SA</mode><rc>0</rc><ammo>4(m)</ammo><conceal>0</conceal><avail>0</avail>
              <cost>1000</cost><mods><mod>Smartgun System</mod></mods><range>Tasers</range><source>AU</source><page>43</page>
            </weapon>
            <weapon>
              <name>Nested</name><category>Cyberware Tasers</category><type>Ranged</type><reach>0</reach>
              <damage>1</damage><ap>-</ap><mode>SA</mode><rc>0</rc><ammo>1</ammo><avail>0</avail><cost>1</cost>
              <underbarrel>Taser</underbarrel><source>AU</source><page>1</page>
            </weapon>
          </weapons>
          <mods>
            <mod><name>Smartgun System</name><category>Weapon Mod</category><rating>0</rating><slots>1</slots><avail>6R</avail><cost>Weapon Cost</cost><source>AR</source><page>153</page></mod>
          </mods>
        </chummer>
        """;

    private const string LinkedWeaponEntries = """
          <cyberware><name>Hand Razors</name><category>Headware</category><ess>0.1</ess><capacity>0</capacity><avail>0</avail><cost>100</cost><addweapon>Hand Razors</addweapon><source>SR4</source><page>1</page></cyberware>
          <cyberware><name>Taser Implant</name><category>Headware</category><ess>0.1</ess><capacity>0</capacity><avail>0</avail><cost>100</cost><addweapon>Taser</addweapon><source>SR4</source><page>1</page></cyberware>
          <cyberware><name>Nested Implant</name><category>Headware</category><ess>0.1</ess><capacity>0</capacity><avail>0</avail><cost>100</cost><addweapon>Nested</addweapon><source>SR4</source><page>1</page></cyberware>
          <cyberware><name>Ghost Implant</name><category>Headware</category><ess>0.1</ess><capacity>0</capacity><avail>0</avail><cost>100</cost><addweapon>Nope</addweapon><source>SR4</source><page>1</page></cyberware>
        """;

    private static CyberwareCatalog WeaponCatalog => CyberwareCatalog.FromXml(
        CyberwareCatalogXml.Replace("</cyberwares>", LinkedWeaponEntries + "</cyberwares>"), BiowareCatalogXml,
        WeaponsXml);

    private static CharacterSession SessionWithUnrelatedWeapon()
    {
        var session = NewSession();
        session.Xml.Root!.Add(new XElement("weapons", new XElement("weapon",
            new XElement("guid", "cccccccc-0000-0000-0000-000000000001"), new XElement("name", "Ares Predator"))));
        return session;
    }

    [Fact]
    public void AddCreatesLinkedCyberwareWeaponWithLegacySchema()
    {
        var session = SessionWithUnrelatedWeapon();
        var manager = new CyberwareManager(session, WeaponCatalog);
        var result = manager.Add(CyberwareSource.Cyberware, "Hand Razors", "Standard", 0);
        Assert.True(result.Success, result.Error);

        var weapons = session.Xml.Root!.Element("weapons")!.Elements("weapon").ToList();
        Assert.Equal(2, weapons.Count);
        var weapon = weapons[1];
        Assert.Equal("Cyberware Melee Weapons", weapon.Element("category")!.Value);
        Assert.Equal(result.Item!.Element.Element("weaponguid")!.Value, weapon.Element("guid")!.Value);
        Assert.Equal("improvementsource", result.Item.Element.Element("weaponguid")!.PreviousNode is XElement p ? p.Name.LocalName : "");
        Assert.Equal(["guid", "name", "category", "type", "spec", "spec2", "reach", "damage", "ap", "mode", "rc", "ammo", "ammocategory",
            "ammoremaining", "ammoremaining2", "ammoremaining3", "ammoremaining4", "ammoloaded", "ammoloaded2", "ammoloaded3", "ammoloaded4",
            "conceal", "avail", "cost", "useskill", "range", "rangemultiply", "fullburst", "suppressive", "source", "page", "weaponname",
            "included", "installed", "requireammo", "location", "notes", "discountedcost"],
            weapon.Elements().Select(e => e.Name.LocalName).ToArray());
        Assert.Equal("100", weapon.Element("cost")!.Value);
        Assert.Equal("10", weapon.Element("fullburst")!.Value);
        Assert.Equal("20", weapon.Element("suppressive")!.Value);
        Assert.Equal("True", weapon.Element("installed")!.Value);
        Assert.Equal("00000000-0000-0000-0000-000000000000", weapon.Element("ammoloaded")!.Value);
    }

    [Fact]
    public void AddCreatesIncludedWeaponModsFromCatalog()
    {
        var session = NewSession();
        var manager = new CyberwareManager(session, WeaponCatalog);
        Assert.True(manager.Add(CyberwareSource.Cyberware, "Taser Implant", "Standard", 0).Success);

        var weapon = session.Xml.Root!.Element("weapons")!.Element("weapon")!;
        Assert.Equal("1", weapon.Element("rangemultiply")!.Value);
        var mod = weapon.Element("weaponmods")!.Element("weaponmod")!;
        Assert.Equal("Smartgun System", mod.Element("name")!.Value);
        Assert.Equal("True", mod.Element("included")!.Value);
        Assert.Equal("Weapon Cost", mod.Element("cost")!.Value);
        Assert.Equal("0", mod.Element("rating")!.Value);
    }

    [Fact]
    public void RemoveDeletesOnlyLinkedWeaponAndKeepsUnrelatedOnes()
    {
        var session = SessionWithUnrelatedWeapon();
        session.Xml.Root!.Element("weapons")!.Add(new XElement("weapon", new XElement("guid", "cccccccc-0000-0000-0000-000000000002"), new XElement("name", "Orphan")));
        var manager = new CyberwareManager(session, WeaponCatalog);
        var added = manager.Add(CyberwareSource.Cyberware, "Hand Razors", "Standard", 0).Item!;
        var other = manager.Add(CyberwareSource.Cyberware, "Taser Implant", "Standard", 0).Item!;
        Assert.Equal(4, session.Xml.Root.Element("weapons")!.Elements("weapon").Count());

        Assert.True(manager.Remove(added.Guid).Success);
        var names = session.Xml.Root.Element("weapons")!.Elements("weapon").Select(w => w.Element("name")!.Value).ToList();
        Assert.Equal(["Ares Predator", "Orphan", "Taser"], names);
        Assert.Equal(other.Element.Element("weaponguid")!.Value, session.Xml.Root.Element("weapons")!.Elements("weapon").Last().Element("guid")!.Value);
        Assert.NotNull(session.Xml.Root.Element("unrelatedNode"));
    }

    [Fact]
    public void RemoveWithMissingWeaponGuidLeavesWeaponsUntouched()
    {
        var session = SessionWithUnrelatedWeapon();
        session.Xml.Root!.Element("cyberwares")!.Elements("cyberware").Last()
            .Element("improvementsource")!.AddAfterSelf(new XElement("weaponguid", "bbbbbbbb-0000-0000-0000-000000000001"));
        var manager = new CyberwareManager(session, Catalog);
        Assert.True(manager.Remove("aaaaaaaa-0000-0000-0000-000000000004").Success);
        Assert.Single(session.Xml.Root.Element("weapons")!.Elements("weapon"));
    }

    [Theory]
    [InlineData("Nested Implant")] // underbarrel weapons not ported
    [InlineData("Ghost Implant")] // weapon missing in catalog
    public void AddRefusesUnsupportedLinkedWeaponsWithoutChangingDocument(string name)
    {
        var session = SessionWithUnrelatedWeapon();
        var before = session.Serialize();
        var result = new CyberwareManager(session, WeaponCatalog).Add(CyberwareSource.Cyberware, name, "Standard", 0);
        Assert.False(result.Success);
        Assert.Equal(before, session.Serialize());
    }
    [Fact]
    public void EditingFieldsPreservesBonusAndUnknownNodes()
    {
        var session = NewSession();
        var manager = new CyberwareManager(session, Catalog);
        var plugin = manager.GetItems()[0].Children[1];
        plugin.Location = "Links";
        plugin.Notes = "Hinweis";
        plugin.DiscountCost = true;
        plugin.EssenceDiscount = 10;

        var reloaded = new CyberwareManager(new CharacterSession(ChumSerializer.Parse(session.Serialize())), Catalog)
            .GetItems()[0].Children[1];
        Assert.Equal("Links", reloaded.Location);
        Assert.Equal("Hinweis", reloaded.Notes);
        Assert.True(reloaded.DiscountCost);
        Assert.Equal(10, reloaded.EssenceDiscount);
        Assert.Equal("1", reloaded.Element.Element("bonus")!.Element("initiative")!.Value);
        Assert.Equal(0.09m, reloaded.CalculatedEssence(Catalog.CyberwareGrades)); // 0.1 * (1 - 0.1)
        Assert.Equal(2700, reloaded.OwnCost(Catalog.CyberwareGrades)); // 3000 * 0.9
    }

    [Fact]
    public async Task ModuleReportsCapacityOverflowAndRatingRange()
    {
        var session = NewSession();
        var ears = session.Xml.Root!.Descendants("cyberware").First(e => e.Element("name")!.Value == "Cyberears");
        ears.Element("capacity")!.Value = "1";
        ears.Descendants("cyberware").Last().Element("rating")!.Value = "9";

        var issues = await new CyberwareModule().ValidateAsync(session, new StubCatalog());
        Assert.Contains(issues, i => i.Code == "cyberware-capacity-exceeded");
        Assert.Contains(issues, i => i.Code == "cyberware-rating-range");
    }

    [Fact]
    public void WorksWithRealInstalledCatalogsWhenPresent()
    {
        var data = FindDirectory(Path.Combine("bin", "data"));
        if (data is null || !File.Exists(Path.Combine(data, "cyberware.xml")) || !File.Exists(Path.Combine(data, "bioware.xml")))
            return;

        var catalog = CyberwareCatalog.FromXml(
            File.ReadAllText(Path.Combine(data, "cyberware.xml")),
            File.ReadAllText(Path.Combine(data, "bioware.xml")));
        Assert.Contains(catalog.CyberwareGrades.Grades, g => g.Name == "Alphaware");

        var session = new CharacterSession(ChumSerializer.Parse("<character />"));
        var manager = new CyberwareManager(session, catalog);
        var added = 0;
        foreach (var source in Enum.GetValues<CyberwareSource>())
        {
            foreach (var entry in catalog.Entries(source))
            {
                if (!string.IsNullOrEmpty(entry.Capacity) && entry.Capacity.Contains('[') && !entry.Capacity.Contains("/["))
                    continue;
                if (manager.GetAddBlocker(entry) is not null)
                    continue;
                var rating = entry.Node.Element("rating") is { } r ? int.Parse(r.Value) : 0;
                int? variableCost = null;
                if (entry.TryGetVariableCostRange(out var minC, out _))
                    variableCost = minC;
                var result = manager.Add(source, entry.Name, "Standard", rating, variableCost: variableCost,
                    side: manager.RequiresSide(entry) ? "Left" : null);
                Assert.True(result.Success, $"{entry.Name}: {result.Error}");
                var item = result.Item!;
                var grades = manager.GradesFor(item);
                _ = item.TotalCost(grades);
                _ = item.CalculatedEssence(grades);
                _ = item.TotalAvail(grades);
                _ = item.CalculatedCapacity;
                added++;
            }
        }

        Assert.True(added > 50);
        Assert.Equal(added, manager.GetItems().Count);
    }

    [Fact]
    public void LocalCharacterFileCharacterizationWhenPresent()
    {
        var saves = FindDirectory("bin\\saves");
        var data = FindDirectory(Path.Combine("bin", "data"));
        if (saves is null || data is null)
            return;

        var file = Directory.EnumerateFiles(saves, "*.chum", SearchOption.AllDirectories)
            .FirstOrDefault(f => File.ReadAllText(f).Contains("<cyberwares><cyberware>", StringComparison.Ordinal));
        if (file is null)
            return;

        var catalog = CyberwareCatalog.FromXml(
            File.ReadAllText(Path.Combine(data, "cyberware.xml")),
            File.ReadAllText(Path.Combine(data, "bioware.xml")));
        var session = new CharacterSession(ChumSerializer.Parse(File.ReadAllText(file)));
        var before = session.Xml.ToString(SaveOptions.DisableFormatting);
        var manager = new CyberwareManager(session, catalog);

        var items = manager.GetItems();
        Assert.NotEmpty(items);
        Assert.All(manager.GetAllItems(), i =>
        {
            Assert.False(string.IsNullOrEmpty(i.Guid));
            Assert.True(i.CalculatedEssence(manager.GradesFor(i)) >= 0);
            Assert.True(i.TotalCost(manager.GradesFor(i)) >= 0);
        });
        Assert.True(manager.EssenceSummary().Total >= 0);
        Assert.Equal(before, session.Xml.ToString(SaveOptions.DisableFormatting));
    }

    [Theory]
    [InlineData("Reflex Recorder (Skill)", "selectskill")]
    [InlineData("Reflex Recorder (Skill Group)", "selectskillgroup")]
    [InlineData("Enhanced Protein Exchange, Adapsin", "adapsin")]
    [InlineData("Cranial Containment Unit", "conditionmonitor")]
    [InlineData("Chemical Gland", "selecttext")]
    [InlineData("Genetic Optimization", "selectattribute")]
    public void UnsupportedBonusTypesFailClosedAccuratelyAndDoNotMutateXml(string itemName, string expectedBonusTags)
    {
        var data = FindDirectory(Path.Combine("bin", "data"));
        if (data is null || !File.Exists(Path.Combine(data, "cyberware.xml")) || !File.Exists(Path.Combine(data, "bioware.xml")))
            return;

        var catalog = CyberwareCatalog.FromXml(
            File.ReadAllText(Path.Combine(data, "cyberware.xml")),
            File.ReadAllText(Path.Combine(data, "bioware.xml")));

        var entry = catalog.Entries(CyberwareSource.Cyberware).FirstOrDefault(e => e.Name == itemName)
            ?? catalog.Entries(CyberwareSource.Bioware).FirstOrDefault(e => e.Name == itemName);
        Assert.NotNull(entry);

        var session = NewSession();
        var beforeXml = session.Serialize();
        var manager = new CyberwareManager(session, catalog);

        var blocker = manager.GetAddBlocker(entry);
        Assert.NotNull(blocker);
        foreach (var tag in expectedBonusTags.Split(',', StringSplitOptions.TrimEntries))
            Assert.Contains(tag, blocker);

        var rating = entry.Node.Element("rating") is { } r ? int.Parse(r.Value) : 0;
        var result = manager.Add(entry.Source, entry.Name, "Standard", rating);
        Assert.False(result.Success);
        Assert.Equal(blocker, result.Error);
        Assert.Equal(beforeXml, session.Serialize());
    }

    [Fact]
    public void RatingThresholdOffsetBonusMatchesRealBiowareCatalogAndRecalculates()
    {
        var data = FindDirectory(Path.Combine("bin", "data"));
        if (data is null)
            return;

        var catalog = CyberwareCatalog.FromXml(
            File.ReadAllText(Path.Combine(data, "cyberware.xml")),
            File.ReadAllText(Path.Combine(data, "bioware.xml")));
        foreach (var name in new[] { "Damage Compensators", "Damage Compensators (2050)" })
        {
            var entry = Assert.Single(catalog.Entries(CyberwareSource.Bioware), e => e.Name == name);
            var session = NewSession();
            var manager = new CyberwareManager(session, catalog);
            Assert.Null(manager.GetAddBlocker(entry));

            var added = manager.Add(CyberwareSource.Bioware, name, "Standard", 4);
            Assert.True(added.Success, added.Error);
            var item = Assert.IsType<CyberwareItem>(added.Item);
            var root = session.Xml.Root!;
            var improvement = Assert.Single(root.Element("improvements")!.Elements("improvement"), i => i.Element("sourcename")?.Value == item.Guid);
            Assert.Equal("CMThresholdOffset", improvement.Element("improvementttype")!.Value);
            Assert.Equal(string.Empty, improvement.Element("unique")!.Value);
            Assert.Equal("4", improvement.Element("val")!.Value);
            Assert.Equal(item.Guid, improvement.Element("sourcename")!.Value);
            Assert.Equal("Bioware", improvement.Element("improvementsource")!.Value);

            var changed = manager.SetRating(item, 7);
            Assert.True(changed.Success, changed.Error);
            Assert.Equal("7", improvement.Element("val")!.Value);

            Assert.True(manager.Remove(item.Guid).Success);
            Assert.DoesNotContain(root.Element("improvements")!.Elements("improvement"), i => i.Element("sourcename")?.Value == item.Guid);
        }
    }

    [Fact]
    public void RatingArmorBonusMatchesRealCatalogAndRecalculates()
    {
        var data = FindDirectory(Path.Combine("bin", "data"));
        if (data is null)
            return;

        var catalog = CyberwareCatalog.FromXml(
            File.ReadAllText(Path.Combine(data, "cyberware.xml")),
            File.ReadAllText(Path.Combine(data, "bioware.xml")));
        foreach (var (name, expectedImpactAtTwo) in new[] { ("Dermal Plating", 2), ("Dermal Sheath", 3) })
        {
            var entry = Assert.Single(catalog.Entries(CyberwareSource.Cyberware), e => e.Name == name);
            var session = NewSession();
            var manager = new CyberwareManager(session, catalog);
            Assert.Null(manager.GetAddBlocker(entry));

            var added = manager.Add(CyberwareSource.Cyberware, name, "Standard", 2);
            Assert.True(added.Success, added.Error);
            var item = Assert.IsType<CyberwareItem>(added.Item);
            var improvements = session.Xml.Root!.Element("improvements")!.Elements("improvement")
                .Where(i => i.Element("sourcename")?.Value == item.Guid).ToList();
            Assert.Equal(2, improvements.Count);
            Assert.Contains(improvements, i => i.Element("improvementttype")?.Value == "BallisticArmor" && i.Element("val")?.Value == "2");
            Assert.Contains(improvements, i => i.Element("improvementttype")?.Value == "ImpactArmor" && i.Element("val")?.Value == expectedImpactAtTwo.ToString());

            var changed = manager.SetRating(item, 3);
            Assert.True(changed.Success, changed.Error);
            Assert.Contains(improvements, i => i.Element("improvementttype")?.Value == "BallisticArmor" && i.Element("val")?.Value == "3");
            Assert.Contains(improvements, i => i.Element("improvementttype")?.Value == "ImpactArmor" && i.Element("val")?.Value == (name == "Dermal Sheath" ? "4" : "3"));

            Assert.True(manager.Remove(item.Guid).Success);
            Assert.DoesNotContain(session.Xml.Root!.Element("improvements")!.Elements("improvement"),
                i => i.Element("sourcename")?.Value == item.Guid);
        }
    }

    [Fact]
    public void NumericArmorBonusMatchesRealBiowareCatalog()
    {
        var data = FindDirectory(Path.Combine("bin", "data"));
        if (data is null)
            return;

        var catalog = CyberwareCatalog.FromXml(
            File.ReadAllText(Path.Combine(data, "cyberware.xml")),
            File.ReadAllText(Path.Combine(data, "bioware.xml")));
        var entry = Assert.Single(catalog.Entries(CyberwareSource.Bioware), e => e.Name == "Carapace");
        var session = NewSession();
        var manager = new CyberwareManager(session, catalog);

        var added = manager.Add(CyberwareSource.Bioware, entry.Name, "Standard", 0);
        Assert.True(added.Success, added.Error);
        var item = Assert.IsType<CyberwareItem>(added.Item);
        var improvements = session.Xml.Root!.Element("improvements")!.Elements("improvement")
            .Where(i => i.Element("sourcename")?.Value == item.Guid).ToList();
        Assert.Equal(2, improvements.Count);
        Assert.Contains(improvements, i => i.Element("improvementttype")?.Value == "BallisticArmor" && i.Element("val")?.Value == "1");
        Assert.Contains(improvements, i => i.Element("improvementttype")?.Value == "ImpactArmor" && i.Element("val")?.Value == "1");
    }

    [Fact]
    public void NumericDamageResistanceBonusMatchesRealCyberwareCatalog()
    {
        var data = FindDirectory(Path.Combine("bin", "data"));
        if (data is null)
            return;

        var catalog = CyberwareCatalog.FromXml(
            File.ReadAllText(Path.Combine(data, "cyberware.xml")),
            File.ReadAllText(Path.Combine(data, "bioware.xml")),
            File.ReadAllText(Path.Combine(data, "weapons.xml")));
        var entry = Assert.Single(catalog.Entries(CyberwareSource.Cyberware), e => e.Name == "Bone Lacing (Plastic)");
        var session = NewSession();
        var manager = new CyberwareManager(session, catalog);
        Assert.Null(manager.GetAddBlocker(entry));

        var added = manager.Add(CyberwareSource.Cyberware, entry.Name, "Standard", 0);
        Assert.True(added.Success, added.Error);
        var item = Assert.IsType<CyberwareItem>(added.Item);
        var improvement = Assert.Single(session.Xml.Root!.Element("improvements")!.Elements("improvement"),
            i => i.Element("sourcename")?.Value == item.Guid);
        Assert.Equal("DamageResistance", improvement.Element("improvementttype")!.Value);
        Assert.Equal("damageresistance", improvement.Element("unique")!.Value);
        Assert.Equal("1", improvement.Element("val")!.Value);
        Assert.Equal("Cyberware", improvement.Element("improvementsource")!.Value);

        Assert.True(manager.Remove(item.Guid).Success);
        Assert.DoesNotContain(session.Xml.Root!.Element("improvements")!.Elements("improvement"),
            i => i.Element("sourcename")?.Value == item.Guid);
    }

    [Fact]
    public void CombinedArmorAndDamageResistanceBonusMatchesRealCatalog()
    {
        var data = FindDirectory(Path.Combine("bin", "data"));
        if (data is null)
            return;

        var catalog = CyberwareCatalog.FromXml(
            File.ReadAllText(Path.Combine(data, "cyberware.xml")),
            File.ReadAllText(Path.Combine(data, "bioware.xml")));
        var entry = Assert.Single(catalog.Entries(CyberwareSource.Cyberware), e => e.Name == "Bone Lacing (Kevlar)");
        var session = NewSession();
        var manager = new CyberwareManager(session, catalog);
        Assert.Null(manager.GetAddBlocker(entry));

        var added = manager.Add(CyberwareSource.Cyberware, entry.Name, "Standard", 0);
        Assert.True(added.Success, added.Error);
        var item = Assert.IsType<CyberwareItem>(added.Item);
        var improvements = session.Xml.Root!.Element("improvements")!.Elements("improvement")
            .Where(i => i.Element("sourcename")?.Value == item.Guid).ToList();
        Assert.Equal(2, improvements.Count);
        Assert.Contains(improvements, i => i.Element("improvementttype")?.Value == "BallisticArmor" && i.Element("val")?.Value == "1");
        Assert.Contains(improvements, i => i.Element("improvementttype")?.Value == "DamageResistance" && i.Element("val")?.Value == "1");

        Assert.True(manager.Remove(item.Guid).Success);
        Assert.DoesNotContain(session.Xml.Root!.Element("improvements")!.Elements("improvement"),
            i => i.Element("sourcename")?.Value == item.Guid);
    }

    [Fact]
    public void ArmorAndDamageResistanceWithAdditionalUnknownBonusRemainsBlocked()
    {
        const string catalogXml = """
            <chummer>
              <grades><grade><name>Standard</name><ess>1</ess><cost>1</cost><avail>0</avail><source>SR4</source></grade></grades>
              <categories><category>Bodyware</category></categories>
              <cyberwares>
                <cyberware><name>Mixed Bonuses</name><category>Bodyware</category><ess>0</ess><capacity>0</capacity><avail>0</avail><cost>100</cost><source>SR4</source><page>1</page>
                  <bonus><armor><b>1</b></armor><damageresistance>1</damageresistance><initiativepass>1</initiativepass></bonus></cyberware>
              </cyberwares>
            </chummer>
            """;
        var catalog = CyberwareCatalog.FromXml(catalogXml, BiowareCatalogXml);
        var session = NewSession();
        var before = session.Serialize();
        var manager = new CyberwareManager(session, catalog);

        var result = manager.Add(CyberwareSource.Cyberware, "Mixed Bonuses", "Standard", 0);
        Assert.False(result.Success);
        Assert.Contains("initiativepass", result.Error);
        Assert.Equal(before, session.Serialize());
    }

    [Fact]
    public void CombinedRatingArmorAndDamageResistanceRecalculatesTogether()
    {
        const string catalogXml = """
            <chummer>
              <grades><grade><name>Standard</name><ess>1</ess><cost>1</cost><avail>0</avail><source>SR4</source></grade></grades>
              <categories><category>Bodyware</category></categories>
              <cyberwares>
                <cyberware><name>Rated Bone Lacing</name><category>Bodyware</category><rating>3</rating><ess>0</ess><capacity>0</capacity><avail>0</avail><cost>100</cost><source>SR4</source><page>1</page>
                  <bonus><armor><b>Rating</b><i>Rating+1</i></armor><damageresistance>Rating * 2</damageresistance></bonus></cyberware>
              </cyberwares>
            </chummer>
            """;
        var catalog = CyberwareCatalog.FromXml(catalogXml, BiowareCatalogXml);
        var session = NewSession();
        var manager = new CyberwareManager(session, catalog);

        var added = manager.Add(CyberwareSource.Cyberware, "Rated Bone Lacing", "Standard", 2);
        Assert.True(added.Success, added.Error);
        var item = Assert.IsType<CyberwareItem>(added.Item);
        var improvements = session.Xml.Root!.Element("improvements")!.Elements("improvement")
            .Where(i => i.Element("sourcename")?.Value == item.Guid).ToList();
        Assert.Equal(3, improvements.Count);

        var changed = manager.SetRating(item, 3);
        Assert.True(changed.Success, changed.Error);
        Assert.Contains(improvements, i => i.Element("improvementttype")?.Value == "BallisticArmor" && i.Element("val")?.Value == "3");
        Assert.Contains(improvements, i => i.Element("improvementttype")?.Value == "ImpactArmor" && i.Element("val")?.Value == "4");
        Assert.Contains(improvements, i => i.Element("improvementttype")?.Value == "DamageResistance" && i.Element("val")?.Value == "6");
    }

    [Fact]
    public void BoneDensityAugmentationMatchesRealBiowareBonusAndRecalculates()
    {
        var data = FindDirectory(Path.Combine("bin", "data"));
        if (data is null)
            return;

        var catalog = CyberwareCatalog.FromXml(
            File.ReadAllText(Path.Combine(data, "cyberware.xml")),
            File.ReadAllText(Path.Combine(data, "bioware.xml")));
        var entry = Assert.Single(catalog.Entries(CyberwareSource.Bioware), e => e.Name == "Bone Density Augmentation");
        var session = NewSession();
        var manager = new CyberwareManager(session, catalog);
        Assert.Null(manager.GetAddBlocker(entry));

        var added = manager.Add(CyberwareSource.Bioware, entry.Name, "Standard", 3);
        Assert.True(added.Success, added.Error);
        var item = Assert.IsType<CyberwareItem>(added.Item);
        var improvements = session.Xml.Root!.Element("improvements")!.Elements("improvement")
            .Where(i => i.Element("sourcename")?.Value == item.Guid).ToList();
        Assert.Equal(3, improvements.Count);
        Assert.Contains(improvements, i => i.Element("improvementttype")?.Value == "DamageResistance"
            && i.Element("unique")?.Value == "damageresistance" && i.Element("val")?.Value == "3");
        Assert.Contains(improvements, i => i.Element("improvementttype")?.Value == "UnarmedDV"
            && i.Element("unique")?.Value == string.Empty && i.Element("val")?.Value == "2");
        Assert.Contains(improvements, i => i.Element("improvementttype")?.Value == "UnarmedDVPhysical"
            && i.Element("unique")?.Value == string.Empty && i.Element("val")?.Value == "0");

        var changed = manager.SetRating(item, 4);
        Assert.True(changed.Success, changed.Error);
        Assert.Contains(improvements, i => i.Element("improvementttype")?.Value == "DamageResistance" && i.Element("val")?.Value == "4");
        Assert.Contains(improvements, i => i.Element("improvementttype")?.Value == "UnarmedDV" && i.Element("val")?.Value == "3");

        Assert.True(manager.Remove(item.Guid).Success);
        Assert.DoesNotContain(session.Xml.Root!.Element("improvements")!.Elements("improvement"),
            i => i.Element("sourcename")?.Value == item.Guid);
    }

    [Fact]
    public void UnsupportedBoneDensityExpressionFailsClosedWithoutMutation()
    {
        const string catalogXml = """
            <chummer>
              <grades><grade><name>Standard</name><ess>1</ess><cost>1</cost><avail>0</avail><source>SR4</source></grade></grades>
              <categories><category>Basic</category></categories>
              <biowares>
                <bioware><name>Unknown Bone Density</name><category>Basic</category><rating>3</rating><ess>0</ess><capacity>0</capacity><avail>0</avail><cost>100</cost><source>SR4</source><page>1</page>
                  <bonus><damageresistance>Rating</damageresistance><unarmeddv>BOD</unarmeddv><unarmeddvphysical /></bonus></bioware>
              </biowares>
            </chummer>
            """;
        var catalog = CyberwareCatalog.FromXml(CyberwareCatalogXml, catalogXml);
        var session = NewSession();
        var before = session.Serialize();
        var manager = new CyberwareManager(session, catalog);

        var result = manager.Add(CyberwareSource.Bioware, "Unknown Bone Density", "Standard", 2);
        Assert.False(result.Success);
        Assert.Contains("unarmeddv", result.Error);
        Assert.Equal(before, session.Serialize());
    }

    [Fact]
    public void MatrixInitiativePassAddMatchesRealCyberwareCatalog()
    {
        var data = FindDirectory(Path.Combine("bin", "data"));
        if (data is null)
            return;

        var catalog = CyberwareCatalog.FromXml(
            File.ReadAllText(Path.Combine(data, "cyberware.xml")),
            File.ReadAllText(Path.Combine(data, "bioware.xml")));
        var entry = Assert.Single(catalog.Entries(CyberwareSource.Cyberware), e => e.Name == "Simsense Booster");
        var session = NewSession();
        var manager = new CyberwareManager(session, catalog);
        Assert.Null(manager.GetAddBlocker(entry));

        var added = manager.Add(CyberwareSource.Cyberware, entry.Name, "Standard", 0);
        Assert.True(added.Success, added.Error);
        var item = Assert.IsType<CyberwareItem>(added.Item);
        var improvement = Assert.Single(session.Xml.Root!.Element("improvements")!.Elements("improvement"),
            i => i.Element("sourcename")?.Value == item.Guid);
        Assert.Equal("MatrixInitiativePass", improvement.Element("improvementttype")!.Value);
        Assert.Equal(string.Empty, improvement.Element("unique")!.Value);
        Assert.Equal("1", improvement.Element("val")!.Value);
        Assert.Equal("Cyberware", improvement.Element("improvementsource")!.Value);

        Assert.True(manager.Remove(item.Guid).Success);
        Assert.DoesNotContain(session.Xml.Root!.Element("improvements")!.Elements("improvement"),
            i => i.Element("sourcename")?.Value == item.Guid);
    }

    [Fact]
    public void MatrixInitiativePassMatchesRealCyberwareCatalog()
    {
        var data = FindDirectory(Path.Combine("bin", "data"));
        if (data is null)
            return;

        var catalog = CyberwareCatalog.FromXml(
            File.ReadAllText(Path.Combine(data, "cyberware.xml")),
            File.ReadAllText(Path.Combine(data, "bioware.xml")));
        var entry = Assert.Single(catalog.Entries(CyberwareSource.Cyberware), e => e.Name == "Simrig");
        var session = NewSession();
        var manager = new CyberwareManager(session, catalog);
        Assert.Null(manager.GetAddBlocker(entry));

        var added = manager.Add(CyberwareSource.Cyberware, entry.Name, "Standard", 0);
        Assert.True(added.Success, added.Error);
        var item = Assert.IsType<CyberwareItem>(added.Item);
        var improvement = Assert.Single(session.Xml.Root!.Element("improvements")!.Elements("improvement"),
            i => i.Element("sourcename")?.Value == item.Guid);
        Assert.Equal("MatrixInitiativePass", improvement.Element("improvementttype")!.Value);
        Assert.Equal("matrixinitiativepass", improvement.Element("unique")!.Value);
        Assert.Equal("1", improvement.Element("val")!.Value);
        Assert.Equal("Cyberware", improvement.Element("improvementsource")!.Value);

        Assert.True(manager.Remove(item.Guid).Success);
        Assert.DoesNotContain(session.Xml.Root!.Element("improvements")!.Elements("improvement"),
            i => i.Element("sourcename")?.Value == item.Guid);
    }

    [Fact]
    public void MatrixInitiativePassRatingBonusRecalculatesAndRemovesLinkedImprovement()
    {
        const string catalogXml = """
            <chummer>
              <grades><grade><name>Standard</name><ess>1</ess><cost>1</cost><avail>0</avail><source>SR4</source></grade></grades>
              <categories><category>Headware</category></categories>
              <cyberwares>
                <cyberware><name>Rated Simrig</name><category>Headware</category><rating>3</rating><ess>0</ess><capacity>0</capacity><avail>0</avail><cost>100</cost><source>SR4</source><page>1</page>
                  <bonus><matrixinitiativepass>Rating + 1</matrixinitiativepass></bonus></cyberware>
              </cyberwares>
            </chummer>
            """;
        var catalog = CyberwareCatalog.FromXml(catalogXml, BiowareCatalogXml);
        var session = NewSession();
        var manager = new CyberwareManager(session, catalog);

        var added = manager.Add(CyberwareSource.Cyberware, "Rated Simrig", "Standard", 2);
        Assert.True(added.Success, added.Error);
        var item = Assert.IsType<CyberwareItem>(added.Item);
        var improvement = Assert.Single(session.Xml.Root!.Element("improvements")!.Elements("improvement"),
            i => i.Element("sourcename")?.Value == item.Guid);
        Assert.Equal("3", improvement.Element("val")!.Value);

        var changed = manager.SetRating(item, 3);
        Assert.True(changed.Success, changed.Error);
        Assert.Equal("4", improvement.Element("val")!.Value);

        Assert.True(manager.Remove(item.Guid).Success);
        Assert.DoesNotContain(session.Xml.Root!.Element("improvements")!.Elements("improvement"),
            i => i.Element("sourcename")?.Value == item.Guid);
    }

    [Theory]
    [InlineData(CyberwareSource.Cyberware, "Smartlink", "Smartlink", "smartlink", "0")]
    [InlineData(CyberwareSource.Cyberware, "Smartlink (2050)", "Smartlink", "smartlink", "0")]
    [InlineData(CyberwareSource.Cyberware, "Cyberskates", "MovementPercent", "", "25")]
    [InlineData(CyberwareSource.Cyberware, "Skimmer Discs", "MovementPercent", "", "50")]
    [InlineData(CyberwareSource.Cyberware, "Waterjets", "SwimPercent", "", "100")]
    [InlineData(CyberwareSource.Cyberware, "Obvious Torso", "PhysicalCM", "", "1")]
    [InlineData(CyberwareSource.Bioware, "Enhanced Protein Exchange, Hyper-Glucagon", "LifestyleCost", "", "10")]
    [InlineData(CyberwareSource.Bioware, "Enhanced Protein Exchange, Vegsin", "LifestyleCost", "", "-10")]
    public void SimpleBonusMatchesRealCatalog(CyberwareSource source, string name, string type, string unique, string value)
    {
        var data = FindDirectory(Path.Combine("bin", "data"));
        if (data is null)
            return;

        var catalog = CyberwareCatalog.FromXml(
            File.ReadAllText(Path.Combine(data, "cyberware.xml")),
            File.ReadAllText(Path.Combine(data, "bioware.xml")),
            File.ReadAllText(Path.Combine(data, "weapons.xml")));
        var entry = Assert.Single(catalog.Entries(source), e => e.Name == name);
        var session = NewSession();
        var manager = new CyberwareManager(session, catalog);
        var blocker = manager.GetAddBlocker(entry);
        if (blocker is not null)
        {
            // Entries with unrelated blockers (e.g. selectside) must stay blocked; only the bonus shape is under test here.
            Assert.DoesNotContain(type, blocker);
            return;
        }

        var added = manager.Add(source, name, "Standard", 0);
        Assert.True(added.Success, added.Error);
        var item = Assert.IsType<CyberwareItem>(added.Item);
        var improvement = Assert.Single(session.Xml.Root!.Element("improvements")!.Elements("improvement"),
            i => i.Element("sourcename")?.Value == item.Guid);
        Assert.Equal(type, improvement.Element("improvementttype")!.Value);
        Assert.Equal(unique, improvement.Element("unique")!.Value);
        Assert.Equal(value, improvement.Element("val")!.Value);

        Assert.True(manager.Remove(item.Guid).Success);
        Assert.DoesNotContain(session.Xml.Root!.Element("improvements")!.Elements("improvement"),
            i => i.Element("sourcename")?.Value == item.Guid);
    }

    [Theory]
    [InlineData(CyberwareSource.Cyberware, "Orientation System", "Skill", "Navigation", "", "2", 0)]
    [InlineData(CyberwareSource.Cyberware, "Datajack", "SkillsoftAccess", "", "", "0", 0)]
    [InlineData(CyberwareSource.Cyberware, "Attention Coprocessor", "Skill", "Perception", "", "2", 2)]
    [InlineData(CyberwareSource.Cyberware, "Encephalon Rating 1", "SkillAttribute", "LOG", "", "1", 0)]
    [InlineData(CyberwareSource.Bioware, "False Front", "Skill", "Disguise", "precedence2", "2", 2)]
    [InlineData(CyberwareSource.Bioware, "Synthacardium", "SkillGroup", "Athletics", "", "2", 2)]
    [InlineData(CyberwareSource.Bioware, "Tailored Pheromones", "SkillCategory", "Social Active", "", "2", 2)]
    [InlineData(CyberwareSource.Cyberware, "Reaction Enhancers", "Attribute", "REA", "precedence1", "2", 2)]
    [InlineData(CyberwareSource.Bioware, "Cerebral Booster", "Attribute", "LOG", "", "2", 2)]
    [InlineData(CyberwareSource.Bioware, "Enhanced Articulation", "EnhancedArticulation", "", "", "1", 0)]
    public void SkillBonusMatchesRealCatalog(CyberwareSource source, string name, string type, string improved, string unique, string value, int rating)
    {
        var data = FindDirectory(Path.Combine("bin", "data"));
        if (data is null)
            return;

        var catalog = CyberwareCatalog.FromXml(
            File.ReadAllText(Path.Combine(data, "cyberware.xml")),
            File.ReadAllText(Path.Combine(data, "bioware.xml")),
            File.ReadAllText(Path.Combine(data, "weapons.xml")));
        var entry = Assert.Single(catalog.Entries(source), e => e.Name == name);
        var session = NewSession();
        var manager = new CyberwareManager(session, catalog);
        Assert.Null(manager.GetAddBlocker(entry));

        var added = manager.Add(source, name, "Standard", rating);
        Assert.True(added.Success, added.Error);
        var item = Assert.IsType<CyberwareItem>(added.Item);
        var improvement = Assert.Single(session.Xml.Root!.Element("improvements")!.Elements("improvement"),
            i => i.Element("sourcename")?.Value == item.Guid);
        Assert.Equal(type, improvement.Element("improvementttype")!.Value);
        Assert.Equal(improved, improvement.Element("improvedname")!.Value);
        Assert.Equal(unique, improvement.Element("unique")!.Value);
        var valueName = type == "Attribute" ? "aug" : "val";
        Assert.Equal(value, improvement.Element(valueName)!.Value);
        Assert.Equal("False", improvement.Element("addtorating")!.Value);

        if (rating > 0)
        {
            Assert.True(manager.SetRating(item, rating + 1).Success);
            Assert.Equal((int.Parse(value) + 1).ToString(), improvement.Element(valueName)!.Value);
        }

        Assert.True(manager.Remove(item.Guid).Success);
        Assert.DoesNotContain(session.Xml.Root!.Element("improvements")!.Elements("improvement"),
            i => i.Element("sourcename")?.Value == item.Guid);
    }

    [Fact]
    public void SkillBonusWithMaxOrMultipleSkillsStaysBlocked()
    {
        const string catalogXml = """
            <chummer>
              <grades><grade><name>Standard</name><ess>1</ess><cost>1</cost><avail>0</avail><source>SR4</source></grade></grades>
              <categories><category>Headware</category></categories>
              <cyberwares>
                <cyberware><name>With Max</name><category>Headware</category><ess>0</ess><capacity>0</capacity><avail>0</avail><cost>1</cost><source>SR4</source><page>1</page>
                  <bonus><specificskill><name>Dodge</name><bonus>1</bonus><max>2</max></specificskill></bonus></cyberware>
                <cyberware><name>Two Skills</name><category>Headware</category><ess>0</ess><capacity>0</capacity><avail>0</avail><cost>1</cost><source>SR4</source><page>1</page>
                  <bonus><specificskill><name>Dodge</name><bonus>1</bonus></specificskill><specificskill><name>Swimming</name><bonus>1</bonus></specificskill></bonus></cyberware>
                <cyberware><name>Applied</name><category>Headware</category><ess>0</ess><capacity>0</capacity><avail>0</avail><cost>1</cost><source>SR4</source><page>1</page>
                  <bonus><specificskill><name>Dodge</name><bonus>1</bonus><applytorating>yes</applytorating></specificskill></bonus></cyberware>
              </cyberwares>
            </chummer>
            """;
        var catalog = CyberwareCatalog.FromXml(catalogXml, BiowareCatalogXml);
        var session = NewSession();
        var manager = new CyberwareManager(session, catalog);

        var before = session.Serialize();
        Assert.False(manager.Add(CyberwareSource.Cyberware, "With Max", "Standard", 0).Success);
        Assert.Equal(before, session.Serialize());

        var two = manager.Add(CyberwareSource.Cyberware, "Two Skills", "Standard", 0);
        Assert.True(two.Success, two.Error);
        Assert.Equal(2, session.Xml.Root!.Element("improvements")!.Elements("improvement")
            .Count(i => i.Element("sourcename")?.Value == two.Item!.Guid));

        var applied = manager.Add(CyberwareSource.Cyberware, "Applied", "Standard", 0);
        Assert.True(applied.Success, applied.Error);
        Assert.Equal("True", Assert.Single(session.Xml.Root!.Element("improvements")!.Elements("improvement"),
            i => i.Element("sourcename")?.Value == applied.Item!.Guid).Element("addtorating")!.Value);
    }

    [Theory]
    [InlineData("Sim Module", "1")]
    [InlineData("Sim Module (Hot-Sim Modified)", "2")]
    public void SimModuleCombinesMatrixPassAndSkillsoftAccess(string name, string pass)
    {
        var data = FindDirectory(Path.Combine("bin", "data"));
        if (data is null)
            return;

        var session = NewSession();
        var catalog = CyberwareCatalog.FromXml(
            File.ReadAllText(Path.Combine(data, "cyberware.xml")),
            File.ReadAllText(Path.Combine(data, "bioware.xml")));
        var manager = new CyberwareManager(session, catalog);
        var added = manager.Add(CyberwareSource.Cyberware, name, "Standard", 0);
        Assert.True(added.Success, added.Error);
        var own = session.Xml.Root!.Element("improvements")!.Elements("improvement")
            .Where(i => i.Element("sourcename")?.Value == added.Item!.Guid).ToList();
        Assert.Equal(pass, Assert.Single(own, i => i.Element("improvementttype")!.Value == "MatrixInitiativePass").Element("val")!.Value);
        Assert.Single(own, i => i.Element("improvementttype")!.Value == "SkillsoftAccess");
        Assert.True(manager.Remove(added.Item!.Guid).Success);
        Assert.DoesNotContain(session.Xml.Root!.Element("improvements")!.Elements("improvement"),
            i => i.Element("sourcename")?.Value == added.Item!.Guid);
    }

    [Theory]
    [InlineData("Move-by-Wire System", 1, "2")]
    [InlineData("Move-by-Wire System", 3, "5")]
    [InlineData("Stirrup Interface", 2, "4")]
    public void SkillwireFixedValuesMatchRealCatalogAndRecalculate(string name, int rating, string expected)
    {
        var data = FindDirectory(Path.Combine("bin", "data"));
        if (data is null)
            return;

        var session = NewSession();
        var manager = new CyberwareManager(session, CyberwareCatalog.FromXml(
            File.ReadAllText(Path.Combine(data, "cyberware.xml")),
            File.ReadAllText(Path.Combine(data, "bioware.xml"))));
        var added = manager.Add(CyberwareSource.Cyberware, name, "Standard", rating);
        Assert.True(added.Success, added.Error);
        var item = added.Item!;
        List<XElement> Own() => session.Xml.Root!.Element("improvements")!.Elements("improvement")
            .Where(i => i.Element("sourcename")?.Value == item.Guid).ToList();

        Assert.Equal(expected, Assert.Single(Own(), i => i.Element("improvementttype")!.Value == "Skillwire").Element("val")!.Value);
        Assert.True(manager.SetRating(item, 1).Success);
        Assert.Equal("2", Assert.Single(Own(), i => i.Element("improvementttype")!.Value == "Skillwire").Element("val")!.Value);
        Assert.True(manager.Remove(item.Guid).Success);
        Assert.Empty(Own());
    }

    [Theory]
    [InlineData("Single Cybereye", CyberwareSource.Cyberware, 0)]
    [InlineData("Obvious Full Arm", CyberwareSource.Cyberware, 1)]
    [InlineData("Synthetic Full Arm", CyberwareSource.Cyberware, 1)]
    public void SelectSideBonusStoresLocationAndKeepsPhysicalMonitorImprovement(string name, CyberwareSource source, int improvements)
    {
        var data = FindDirectory(Path.Combine("bin", "data"));
        if (data is null)
            return;

        var session = NewSession();
        var manager = new CyberwareManager(session, CyberwareCatalog.FromXml(
            File.ReadAllText(Path.Combine(data, "cyberware.xml")),
            File.ReadAllText(Path.Combine(data, "bioware.xml"))));
        var entry = manager.Catalog.Find(source, name)!;
        Assert.True(manager.RequiresSide(entry));
        Assert.Null(manager.GetAddBlocker(entry));

        var before = session.Serialize();
        Assert.False(manager.Add(source, name, "Standard", entry.Node.Element("rating") is null ? 0 : 1).Success);
        Assert.Equal(before, session.Serialize());

        var added = manager.Add(source, name, "Standard", entry.Node.Element("rating") is null ? 0 : 1, null, null, "Left");
        Assert.True(added.Success, added.Error);
        Assert.Equal("Left", added.Item!.Location);
        Assert.Equal(improvements, session.Xml.Root!.Element("improvements")?.Elements("improvement")
            .Count(i => i.Element("sourcename")?.Value == added.Item.Guid) ?? 0);
        Assert.True(manager.Remove(added.Item.Guid).Success);
    }

    [Fact]
    public void SuiteWithFullArmsNeedsOneSidePerArm()
    {
        var data = FindDirectory(Path.Combine("bin", "data"));
        if (data is null)
            return;

        var session = NewSession();
        var manager = new CyberwareManager(session, CyberwareCatalog.FromXml(
            File.ReadAllText(Path.Combine(data, "cyberware.xml")),
            File.ReadAllText(Path.Combine(data, "bioware.xml")),
            File.ReadAllText(Path.Combine(data, "weapons.xml"))));
        var sided = manager.ListSuites(CyberwareSource.Cyberware)
            .Where(s => s.Blocker is null && manager.CountSuiteSides(s) > 0).ToList();
        Assert.NotEmpty(sided);

        var successes = 0;
        foreach (var suite in sided)
        {
            var needed = manager.CountSuiteSides(suite);
            var before = session.Serialize();
            Assert.False(manager.ApplySuite(CyberwareSource.Cyberware, suite.Name).Success);
            Assert.False(manager.ApplySuite(CyberwareSource.Cyberware, suite.Name, Enumerable.Repeat("Left", needed + 1).ToList()).Success);
            Assert.Equal(before, session.Serialize());

            var applied = manager.ApplySuite(CyberwareSource.Cyberware, suite.Name, Enumerable.Repeat("Right", needed).ToList());
            if (applied.Success)
            {
                successes++;
                Assert.Contains(session.Xml.Root!.Element("cyberwares")!.Descendants("cyberware"),
                    c => c.Element("location")?.Value == "Right");
            }
            else
                Assert.Equal(before, session.Serialize());
        }
        Assert.True(successes > 0);
    }

    [Theory]
    [InlineData("Wired Reflexes", 1, "1", "1")]
    [InlineData("Wired Reflexes", 3, "3", "3")]
    [InlineData("Boosted Reflexes", 1, "1", "0")]
    [InlineData("Boosted Reflexes", 3, "3", "1")]
    public void MultiTagReflexBonusesMatchRealCatalogAndRecalculate(string name, int rating, string first, string second)
    {
        var data = FindDirectory(Path.Combine("bin", "data"));
        if (data is null)
            return;

        var session = NewSession();
        var manager = new CyberwareManager(session, CyberwareCatalog.FromXml(
            File.ReadAllText(Path.Combine(data, "cyberware.xml")),
            File.ReadAllText(Path.Combine(data, "bioware.xml"))));
        var added = manager.Add(CyberwareSource.Cyberware, name, "Standard", rating);
        Assert.True(added.Success, added.Error);
        var item = added.Item!;
        List<XElement> Own() => session.Xml.Root!.Element("improvements")!.Elements("improvement")
            .Where(i => i.Element("sourcename")?.Value == item.Guid).ToList();

        var own = Own();
        Assert.Equal(2, own.Count);
        if (name == "Wired Reflexes")
        {
            Assert.Equal(first, own.Single(i => i.Element("improvementttype")!.Value == "InitiativePass").Element("val")!.Value);
            var attribute = own.Single(i => i.Element("improvementttype")!.Value == "Attribute");
            Assert.Equal("REA", attribute.Element("improvedname")!.Value);
            Assert.Equal("precedence1", attribute.Element("unique")!.Value);
            Assert.Equal(second, attribute.Element("aug")!.Value);
        }
        else
        {
            Assert.Equal(first, own.Single(i => i.Element("improvementttype")!.Value == "Initiative").Element("val")!.Value);
            Assert.Equal(second, own.Single(i => i.Element("improvementttype")!.Value == "InitiativePass").Element("val")!.Value);
        }

        if (rating < 3)
        {
            Assert.True(manager.SetRating(item, 3).Success);
            Assert.Equal(2, Own().Count);
        }

        Assert.True(manager.Remove(item.Guid).Success);
        Assert.Empty(Own());
    }

    [Theory]
    [InlineData("<specificattribute><name>ESS</name><val>1</val></specificattribute>")]
    [InlineData("<specificattribute><name>AGI</name><val>1</val><max>2</max></specificattribute>")]
    [InlineData("<specificattribute><name>AGI</name><val>1</val><aug>1</aug></specificattribute>")]
    [InlineData("<specificattribute><name>AGI</name><val>1</val><affectbase /></specificattribute>")]
    [InlineData("<specificattribute><name>AGI</name><name>REA</name><val>1</val></specificattribute>")]
    [InlineData("<specificattribute><name>XYZ</name><val>1</val></specificattribute>")]
    public void SpecificAttributeVariantsStayBlocked(string bonusXml)
    {
        var catalogXml = $$"""
            <chummer>
              <grades><grade><name>Standard</name><ess>1</ess><cost>1</cost><avail>0</avail><source>SR4</source></grade></grades>
              <categories><category>Headware</category></categories>
              <cyberwares>
                <cyberware><name>Odd</name><category>Headware</category><rating>0</rating><ess>0</ess><capacity>0</capacity><avail>0</avail><cost>1</cost><source>SR4</source><page>1</page>
                  <bonus>{{bonusXml}}</bonus></cyberware>
              </cyberwares>
            </chummer>
            """;
        var session = NewSession();
        var before = session.Xml.ToString();
        var result = new CyberwareManager(session, CyberwareCatalog.FromXml(catalogXml, BiowareCatalogXml))
            .Add(CyberwareSource.Cyberware, "Odd", "Standard", 1);
        Assert.False(result.Success);
        Assert.Equal(before, session.Xml.ToString());
    }

    [Fact]
    public void InitiativeBonusesCreateLegacyImprovementsAndRecalculate()
    {
        const string catalogXml = """
            <chummer>
              <grades><grade><name>Standard</name><ess>1</ess><cost>1</cost><avail>0</avail><source>SR4</source></grade></grades>
              <categories><category>Headware</category></categories>
              <cyberwares>
                <cyberware><name>Ini</name><category>Headware</category><rating>3</rating><ess>0</ess><capacity>0</capacity><avail>0</avail><cost>1</cost><source>SR4</source><page>1</page>
                  <bonus><initiative>Rating</initiative></bonus></cyberware>
                <cyberware><name>Pass</name><category>Headware</category><rating>3</rating><ess>0</ess><capacity>0</capacity><avail>0</avail><cost>1</cost><source>SR4</source><page>1</page>
                  <bonus><initiativepass>Rating</initiativepass></bonus></cyberware>
              </cyberwares>
            </chummer>
            """;
        var catalog = CyberwareCatalog.FromXml(catalogXml, BiowareCatalogXml);
        var session = NewSession();
        var manager = new CyberwareManager(session, catalog);

        foreach (var (name, type, unique) in new[] { ("Ini", "Initiative", ""), ("Pass", "InitiativePass", "initiativepass") })
        {
            var added = manager.Add(CyberwareSource.Cyberware, name, "Standard", 1);
            Assert.True(added.Success, added.Error);
            var improvement = Assert.Single(session.Xml.Root!.Element("improvements")!.Elements("improvement"),
                i => i.Element("sourcename")?.Value == added.Item!.Guid);
            Assert.Equal(type, improvement.Element("improvementttype")!.Value);
            Assert.Equal(unique, improvement.Element("unique")!.Value);
            Assert.Equal("1", improvement.Element("val")!.Value);
            Assert.True(manager.SetRating(added.Item!, 3).Success);
            Assert.Equal("3", improvement.Element("val")!.Value);
        }
    }

    [Fact]
    public void SkillwireRatingBonusRecalculatesAndMixedFormsStayBlocked()
    {
        const string catalogXml = """
            <chummer>
              <grades><grade><name>Standard</name><ess>1</ess><cost>1</cost><avail>0</avail><source>SR4</source></grade></grades>
              <categories><category>Headware</category></categories>
              <cyberwares>
                <cyberware><name>Rated Wire</name><category>Headware</category><rating>6</rating><ess>0</ess><capacity>0</capacity><avail>0</avail><cost>100</cost><source>SR4</source><page>1</page>
                  <bonus><skillwire>Rating</skillwire></bonus></cyberware>
                <cyberware><name>Fixed Wire</name><category>Headware</category><rating>3</rating><ess>0</ess><capacity>0</capacity><avail>0</avail><cost>100</cost><source>SR4</source><page>1</page>
                  <bonus><skillwire>FixedValues(2,4,5)</skillwire></bonus></cyberware>
                <cyberware><name>Two Tags</name><category>Headware</category><ess>0</ess><capacity>0</capacity><avail>0</avail><cost>100</cost><source>SR4</source><page>1</page>
                  <bonus><smartlink /><adapsin /></bonus></cyberware>
              </cyberwares>
            </chummer>
            """;
        var catalog = CyberwareCatalog.FromXml(catalogXml, BiowareCatalogXml);
        var session = NewSession();
        var manager = new CyberwareManager(session, catalog);

        var added = manager.Add(CyberwareSource.Cyberware, "Rated Wire", "Standard", 2);
        Assert.True(added.Success, added.Error);
        var item = Assert.IsType<CyberwareItem>(added.Item);
        var improvement = Assert.Single(session.Xml.Root!.Element("improvements")!.Elements("improvement"),
            i => i.Element("sourcename")?.Value == item.Guid);
        Assert.Equal("Skillwire", improvement.Element("improvementttype")!.Value);
        Assert.Equal("2", improvement.Element("val")!.Value);
        Assert.True(manager.SetRating(item, 5).Success);
        Assert.Equal("5", improvement.Element("val")!.Value);

        Assert.True(manager.Add(CyberwareSource.Cyberware, "Fixed Wire", "Standard", 1).Success);
        var before = session.Serialize();
        Assert.False(manager.Add(CyberwareSource.Cyberware, "Two Tags", "Standard", 0).Success);
        Assert.Equal(before, session.Serialize());
    }

    [Fact]
    public void RatingDamageResistanceBonusRecalculatesUsingLegacyRatingValue()
    {
        const string catalogXml = """
            <chummer>
              <grades><grade><name>Standard</name><ess>1</ess><cost>1</cost><avail>0</avail><source>SR4</source></grade></grades>
              <categories><category>Bodyware</category></categories>
              <cyberwares>
                <cyberware><name>Rated Resistance</name><category>Bodyware</category><rating>3</rating><ess>0</ess><capacity>0</capacity><avail>0</avail><cost>100</cost><source>SR4</source><page>1</page>
                  <bonus><damageresistance>Rating * 2</damageresistance></bonus></cyberware>
              </cyberwares>
            </chummer>
            """;
        var catalog = CyberwareCatalog.FromXml(catalogXml, BiowareCatalogXml);
        var session = NewSession();
        var manager = new CyberwareManager(session, catalog);

        var added = manager.Add(CyberwareSource.Cyberware, "Rated Resistance", "Standard", 2);
        Assert.True(added.Success, added.Error);
        var item = Assert.IsType<CyberwareItem>(added.Item);
        var improvement = Assert.Single(session.Xml.Root!.Element("improvements")!.Elements("improvement"),
            i => i.Element("improvementttype")?.Value == "DamageResistance");
        Assert.Equal("4", improvement.Element("val")!.Value);

        var changed = manager.SetRating(item, 3);
        Assert.True(changed.Success, changed.Error);
        Assert.Equal("6", improvement.Element("val")!.Value);
    }

    [Fact]
    public void PreservesExistingLinkedImprovementsWhenRemovingOtherSources()
    {
        var session = NewSession();
        var root = session.Xml.Root!;
        var imps = root.Element("improvements")!;

        // Add various improvement sources to ensure manager.Remove does not purge them
        imps.Add(new XElement("improvement",
            new XElement("improvedname", "Athletics"),
            new XElement("sourcename", "some-quality-guid"),
            new XElement("improvementsource", "Quality")));
        imps.Add(new XElement("improvement",
            new XElement("improvedname", "Spellcasting"),
            new XElement("sourcename", "some-power-guid"),
            new XElement("improvementsource", "Power")));
        imps.Add(new XElement("improvement",
            new XElement("improvedname", "DamageResistance"),
            new XElement("sourcename", "other-armor-guid"),
            new XElement("improvementsource", "Armor")));

        var manager = new CyberwareManager(session, Catalog);

        // Remove Cyberears (aaaaaaaa-0000-0000-0000-000000000001) which has child Audio Enhancement (000000000003)
        var removeResult = manager.Remove("aaaaaaaa-0000-0000-0000-000000000001");
        Assert.True(removeResult.Success);

        var remainingImps = imps.Elements("improvement").ToList();
        Assert.Equal(4, remainingImps.Count); // 3 non-cyberware + 1 pre-existing unrelated Quality improvement
        Assert.Contains(remainingImps, i => i.Element("sourcename")?.Value == "unrelated" && i.Element("improvementsource")?.Value == "Quality");
        Assert.Contains(remainingImps, i => i.Element("sourcename")?.Value == "some-quality-guid" && i.Element("improvementsource")?.Value == "Quality");
        Assert.Contains(remainingImps, i => i.Element("sourcename")?.Value == "some-power-guid" && i.Element("improvementsource")?.Value == "Power");
        Assert.Contains(remainingImps, i => i.Element("sourcename")?.Value == "other-armor-guid" && i.Element("improvementsource")?.Value == "Armor");
        Assert.DoesNotContain(remainingImps, i => i.Element("sourcename")?.Value == "aaaaaaaa-0000-0000-0000-000000000003");
    }
    private const string CyberwareSuitesXml = """
          <suites>
            <suite><name>Ear Set</name><grade>Alphaware</grade><cyberwares>
              <cyberware><name>Cyberears</name><rating>2</rating><cyberwares>
                <cyberware><name>Audio Enhancement</name><rating>1</rating></cyberware>
                <cyberware><name>Sound Link</name></cyberware>
              </cyberwares></cyberware>
              <cyberware><name>Commlink</name></cyberware>
            </cyberwares></suite>
            <suite><name>Razor Set</name><grade>Nonsense</grade><cyberwares>
              <cyberware><name>Hand Razors</name></cyberware><cyberware><name>Commlink</name></cyberware>
            </cyberwares></suite>
            <suite><name>Second Hand Set</name><grade>StandardSecondHand</grade><cyberwares><cyberware><name>Commlink</name></cyberware></cyberwares></suite>
            <suite><name>Razor Broken Set</name><grade>Standard</grade><cyberwares>
              <cyberware><name>Hand Razors</name></cyberware>
              <cyberware><name>Cyberears</name><rating>2</rating><cyberwares><cyberware><name>Commlink</name></cyberware></cyberwares></cyberware>
            </cyberwares></suite>
            <suite><name>Range Set</name><grade>Standard</grade><cyberwares>
              <cyberware><name>Commlink</name></cyberware>
              <cyberware><name>Cyberears</name><rating>1</rating><cyberwares><cyberware><name>Audio Enhancement</name><rating>9</rating></cyberware></cyberwares></cyberware>
            </cyberwares></suite>
            <suite><name>Bonus Set</name><grade>Standard</grade><cyberwares><cyberware><name>Attention Coprocessor</name><rating>1</rating></cyberware></cyberwares></suite>
            <suite><name>Variable Set</name><grade>Standard</grade><cyberwares><cyberware><name>Gill Thing</name></cyberware></cyberwares></suite>
            <suite><name>Override Set</name><grade>Standard</grade><cyberwares><cyberware><name>Commlink</name><grade>Alphaware</grade></cyberware></cyberwares></suite>
            <suite><name>Select Set</name><grade>Standard</grade><cyberwares><cyberware><name select="Left">Commlink</name></cyberware></cyberwares></suite>
            <suite><name>Missing Grade Set</name><grade>Deltaware</grade><cyberwares><cyberware><name>Commlink</name></cyberware></cyberwares></suite>
          </suites>
        """;

    private const string BiowareSuitesXml = """
          <suites>
            <suite><name>Toner Set</name><grade>Betaware</grade><biowares><bioware><name>Muscle Toner</name><rating>2</rating></bioware></biowares></suite>
          </suites>
        """;

    private static CyberwareCatalog SuiteCatalog => CyberwareCatalog.FromXml(
        CyberwareCatalogXml.Replace("</cyberwares>", LinkedWeaponEntries + "</cyberwares>").Replace("</chummer>", CyberwareSuitesXml + "</chummer>"),
        BiowareCatalogXml.Replace("</chummer>", BiowareSuitesXml + "</chummer>"),
        WeaponsXml);

    [Theory]
    [InlineData(CyberwareSource.Cyberware, "Muscle Replacement", 2, 2)]
    [InlineData(CyberwareSource.Cyberware, "Encephalon Rating 2", 0, 3)]
    [InlineData(CyberwareSource.Bioware, "Suprathyroid Gland", 0, 4)]
    [InlineData(CyberwareSource.Bioware, "Mnemonic Enhancer", 2, 4)]
    public void RepeatedListTagsMatchRealCatalog(CyberwareSource source, string name, int rating, int count)
    {
        var data = FindDirectory(Path.Combine("bin", "data"));
        if (data is null)
            return;

        var session = NewSession();
        var manager = new CyberwareManager(session, CyberwareCatalog.FromXml(
            File.ReadAllText(Path.Combine(data, "cyberware.xml")),
            File.ReadAllText(Path.Combine(data, "bioware.xml"))));
        var added = manager.Add(source, name, "Standard", rating);
        Assert.True(added.Success, added.Error);
        var item = added.Item!;
        List<XElement> Own() => session.Xml.Root!.Element("improvements")!.Elements("improvement")
            .Where(i => i.Element("sourcename")?.Value == item.Guid).ToList();
        Assert.Equal(count, Own().Count);
        if (rating > 0)
        {
            Assert.True(manager.SetRating(item, 3).Success);
            Assert.All(Own(), i => Assert.Equal("3", i.Element(i.Element("improvementttype")!.Value == "Attribute" ? "aug" : "val")!.Value));
        }

        Assert.True(manager.Remove(item.Guid).Success);
        Assert.Empty(Own());
    }

    [Fact]
    public void ListSuitesReturnsParsedSuitesWithResolvedGradeAndBlockers()
    {
        var manager = new CyberwareManager(NewSession(), SuiteCatalog);
        var suites = manager.ListSuites(CyberwareSource.Cyberware);
        Assert.Equal(10, suites.Count);
        var ear = suites[0];
        Assert.Equal(("Ear Set", "Alphaware", "Alphaware", null), (ear.Name, ear.GradeText, ear.GradeName, ear.Blocker));
        Assert.Equal(["Cyberears", "Commlink"], ear.Items.Select(i => i.Name));
        Assert.Equal(2, ear.Items[0].Rating);
        Assert.Equal(["Audio Enhancement", "Sound Link"], ear.Items[0].Children.Select(i => i.Name));
        Assert.Equal("Standard", suites[1].GradeName);
        Assert.Equal("Standard (Second-Hand)", suites[2].GradeName);
        Assert.Contains("Attention Coprocessor", suites[5].Blocker);
        Assert.Contains("variable", suites[6].Blocker);
        Assert.Contains("grade", suites[7].Blocker);
        Assert.Contains("select", suites[8].Blocker);
        Assert.Contains("Deltaware", suites[9].Blocker);
        Assert.Equal(["Toner Set"], manager.ListSuites(CyberwareSource.Bioware).Select(s => s.Name));
        Assert.Empty(new CyberwareManager(NewSession(), Catalog).ListSuites(CyberwareSource.Cyberware));
    }

    [Theory]
    [InlineData("AlphawareAdapsin", "Alphaware (Adapsin)")]
    [InlineData("StandardSecondHandAdapsin", "Standard (Second-Hand) (Adapsin)")]
    [InlineData("AlphawareSecondHand", "Alphaware (Second-Hand)")]
    [InlineData("Deltaware", "Deltaware")]
    [InlineData("garbage", "Standard")]
    public void SuiteGradeResolutionMatchesLegacyMapping(string text, string expected) =>
        Assert.Equal(expected, CyberwareManager.ResolveSuiteGradeName(text));

    [Fact]
    public void ApplySuiteAddsNestedItemsWithSuiteGradeAndSuiteFlag()
    {
        var session = NewSession();
        var manager = new CyberwareManager(session, SuiteCatalog);
        var originalFirst = session.Xml.Root!.Element("cyberwares")!.Elements("cyberware").First().ToString();
        var result = manager.ApplySuite(CyberwareSource.Cyberware, "Ear Set");
        Assert.True(result.Success, result.Error);

        var items = manager.GetItems();
        Assert.Equal(4, items.Count);
        Assert.Equal(originalFirst, items[0].Element.ToString());
        var ears = items[2];
        var commlink = items[3];
        Assert.Equal(("Cyberears", 2, "Alphaware", true), (ears.Name, ears.Rating, ears.GradeName, ears.Suite));
        Assert.Equal(["Audio Enhancement", "Sound Link"], ears.Children.Select(c => c.Name));
        Assert.All(ears.Children, c => Assert.Equal(("Alphaware", true), (c.GradeName, c.Suite)));
        Assert.Equal(1, ears.Children[0].Rating);
        Assert.Equal(("Commlink", "Alphaware", true), (commlink.Name, commlink.GradeName, commlink.Suite));
        Assert.Equal(["True"], ears.Element.DescendantsAndSelf("suite").Select(e => e.Value).Distinct());
        Assert.Equal(["guid", "name"], ears.Element.Elements().Take(2).Select(e => e.Name.LocalName));
        Assert.Equal(4, ChumSerializer.Parse(session.Serialize()).Xml.Root!.Element("cyberwares")!.Elements("cyberware").Count());
    }

    [Fact]
    public void ApplySuiteUsesGradeFromSecondHandAlias()
    {
        var session = NewSession();
        var result = new CyberwareManager(session, SuiteCatalog).ApplySuite(CyberwareSource.Cyberware, "Second Hand Set");
        Assert.True(result.Success, result.Error);
        Assert.Equal("Standard (Second-Hand)", new CyberwareManager(session, SuiteCatalog).GetItems()[^1].GradeName);
    }

    [Fact]
    public void ApplySuiteAddsBiowareSuite()
    {
        var session = NewSession();
        var manager = new CyberwareManager(session, SuiteCatalog);
        var result = manager.ApplySuite(CyberwareSource.Bioware, "Toner Set");
        Assert.True(result.Success, result.Error);
        var added = manager.GetItems()[^1];
        Assert.Equal(("Muscle Toner", 2, "Betaware", true, CyberwareSource.Bioware), (added.Name, added.Rating, added.GradeName, added.Suite, added.SourceType));
        Assert.Equal("Bioware", added.Element.Element("improvementsource")!.Value);
    }

    [Fact]
    public void ApplySuiteKeepsLinkedWeaponAssociation()
    {
        var session = SessionWithUnrelatedWeapon();
        var manager = new CyberwareManager(session, SuiteCatalog);
        Assert.True(manager.ApplySuite(CyberwareSource.Cyberware, "Razor Set").Success);
        var razors = manager.GetItems().Single(i => i.Name == "Hand Razors");
        var weapons = session.Xml.Root!.Element("weapons")!.Elements("weapon").ToList();
        Assert.Equal(2, weapons.Count);
        Assert.Equal("cccccccc-0000-0000-0000-000000000001", weapons[0].Element("guid")!.Value);
        Assert.Equal(razors.Element.Element("weaponguid")!.Value, weapons[1].Element("guid")!.Value);
        Assert.True(razors.Suite);
    }

    [Theory]
    [InlineData("Razor Broken Set")] // fails on the plugin of the second piece after the first (with weapon) succeeded
    [InlineData("Range Set")]
    [InlineData("Bonus Set")]
    [InlineData("Variable Set")]
    [InlineData("Override Set")]
    [InlineData("Select Set")]
    [InlineData("Missing Grade Set")]
    [InlineData("Unknown Set")]
    public void ApplySuiteFailureLeavesDocumentUnchanged(string suite)
    {
        var session = SessionWithUnrelatedWeapon();
        var manager = new CyberwareManager(session, SuiteCatalog);
        var before = session.Serialize();
        var result = manager.ApplySuite(CyberwareSource.Cyberware, suite);
        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Equal(before, session.Serialize());
    }

    [Fact]
    public void ApplySuiteRollsBackWhenAConstituentFailsDuringStaging()
    {
        var session = SessionWithUnrelatedWeapon();
        var manager = new CyberwareManager(session, SuiteCatalog);
        var before = session.Serialize();
        var result = manager.ApplySuite(CyberwareSource.Cyberware, "Razor Broken Set");
        Assert.False(result.Success);
        Assert.Contains("Razor Broken Set", result.Error);
        Assert.Contains("Commlink", result.Error);
        Assert.Equal(before, session.Serialize());
        Assert.Single(session.Xml.Root!.Element("weapons")!.Elements("weapon"));
    }
    private const string EnableCatalogXml = """
        <chummer>
          <grades><grade><name>Standard</name><ess>1</ess><cost>1</cost><avail>0</avail><source>SR4</source></grade></grades>
          <categories><category>Headware</category></categories>
          <cyberwares>
            <cyberware><name>Mag Port</name><category>Headware</category><ess>0.1</ess><capacity>0</capacity><avail>0</avail><cost>100</cost><source>SR4</source><page>1</page>
              <bonus><addattribute><name>MAG</name></addattribute></bonus></cyberware>
            <cyberware><name>Res Port</name><category>Headware</category><rating>3</rating><ess>0.1</ess><capacity>0</capacity><avail>0</avail><cost>100</cost><source>SR4</source><page>1</page>
              <bonus><addattribute><name>RES</name></addattribute></bonus></cyberware>
            <cyberware><name>Edge Port</name><category>Headware</category><ess>0.1</ess><capacity>0</capacity><avail>0</avail><cost>100</cost><source>SR4</source><page>1</page>
              <bonus><addattribute><name>EDG</name></addattribute></bonus></cyberware>
            <cyberware><name>Mixed Port</name><category>Headware</category><ess>0.1</ess><capacity>0</capacity><avail>0</avail><cost>100</cost><source>SR4</source><page>1</page>
              <bonus><addattribute><name>MAG</name></addattribute><initiative>1</initiative></bonus></cyberware>
            <cyberware><name>Unique Port</name><category>Headware</category><ess>0.1</ess><capacity>0</capacity><avail>0</avail><cost>100</cost><source>SR4</source><page>1</page>
              <bonus unique="x"><addattribute><name>MAG</name></addattribute></bonus></cyberware>
          </cyberwares>
        </chummer>
        """;

    private static CyberwareManager EnableManager(CharacterSession session) =>
        new(session, CyberwareCatalog.FromXml(EnableCatalogXml, BiowareCatalogXml));

    [Fact]
    public void AddEnableAttributeBonusWritesLegacyImprovementAndFlagAtomically()
    {
        var session = NewSession();
        var manager = EnableManager(session);
        var result = manager.Add(CyberwareSource.Cyberware, "Mag Port", "Standard", 0);
        Assert.True(result.Success, result.Error);

        var root = session.Xml.Root!;
        Assert.Equal("True", root.Element("magenabled")!.Value);
        var imp = Assert.Single(root.Element("improvements")!.Elements("improvement"), i => i.Element("unique")?.Value == "enableattribute");
        Assert.Equal(
            new[] { "unique", "improvedname", "sourcename", "min", "max", "aug", "augmax", "val", "rating", "exclude", "improvementttype", "improvementsource", "custom", "customname", "customid", "customgroup", "addtorating", "enabled", "order", "notes" },
            imp.Elements().Select(e => e.Name.LocalName).ToArray());
        Assert.Equal("MAG", imp.Element("improvedname")!.Value);
        Assert.Equal(result.Item!.Guid, imp.Element("sourcename")!.Value);
        Assert.Equal("Attribute", imp.Element("improvementttype")!.Value);
        Assert.Equal("Cyberware", imp.Element("improvementsource")!.Value);
        Assert.Equal("0", imp.Element("val")!.Value);
        Assert.Equal("0", imp.Element("rating")!.Value);
        Assert.Equal("True", imp.Element("enabled")!.Value);
        Assert.Equal("MAG", result.Item.Element.Element("bonus")!.Element("addattribute")!.Element("name")!.Value);
    }

    [Fact]
    public void EnableAttributeRemoveKeepsFlagWhileAnotherSourceNeedsIt()
    {
        var session = NewSession();
        var manager = EnableManager(session);
        var a = manager.Add(CyberwareSource.Cyberware, "Res Port", "Standard", 2).Item!;
        var b = manager.Add(CyberwareSource.Cyberware, "Res Port", "Standard", 1).Item!;
        var root = session.Xml.Root!;
        Assert.Equal("True", root.Element("resenabled")!.Value);

        Assert.True(manager.SetRating(a, 3).Success);
        Assert.Equal(2, root.Element("improvements")!.Elements("improvement").Count(i => i.Element("unique")?.Value == "enableattribute"));

        Assert.True(manager.Remove(a.Guid).Success);
        Assert.Equal("True", root.Element("resenabled")!.Value);
        Assert.True(manager.Remove(b.Guid).Success);
        Assert.Equal("False", root.Element("resenabled")!.Value);
        Assert.DoesNotContain(root.Element("improvements")!.Elements("improvement"), i => i.Element("unique")?.Value == "enableattribute");
    }

    [Theory]
    [InlineData("Edge Port", "addattribute")]
    [InlineData("Mixed Port", "addattribute, initiative")]
    [InlineData("Unique Port", "addattribute")]
    public void UnsupportedAddAttributeVariantsFailClosedWithoutMutation(string name, string tags)
    {
        var session = NewSession();
        var before = session.Serialize();
        var result = EnableManager(session).Add(CyberwareSource.Cyberware, name, "Standard", 0);
        Assert.False(result.Success);
        Assert.Contains(tags, result.Error);
        Assert.Equal(before, session.Serialize());
    }
    private static string? FindDirectory(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private sealed class StubCatalog : IRuleCatalog
    {
        public Task<RuleCatalogDocument?> GetAsync(string catalogName, string culture, CancellationToken cancellationToken)
        {
            var content = catalogName == "bioware.xml" ? BiowareCatalogXml : CyberwareCatalogXml;
            return Task.FromResult<RuleCatalogDocument?>(new RuleCatalogDocument(catalogName, culture, "1", content));
        }
    }
}