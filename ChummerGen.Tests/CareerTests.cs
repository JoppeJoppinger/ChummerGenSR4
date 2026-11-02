using System;
using System.Linq;
using System.Xml.Linq;
using ChummerGen.Domain.Abstractions;
using ChummerGen.Domain.Character;
using ChummerGen.Domain.Character.Career;
using ChummerGen.Domain.Chum;
using ChummerGen.Domain.Models;

namespace ChummerGen.Tests;

public sealed class CareerTests
{
    private const string SampleCharacterXml = """
        <character>
          <name>Shadowrunner</name>
          <created>False</created>
          <karma>10</karma>
          <nuyen>5000</nuyen>
          <customnode test="preserve"><nested>intact</nested></customnode>
          <expenses />
        </character>
        """;

    [Fact]
    public void IsCareerMode_DetectsCreatedStateAccurately()
    {
        var sessionCreatedFalse = new CharacterSession(ChumSerializer.Parse(SampleCharacterXml));
        Assert.False(CareerManager.IsCareerMode(sessionCreatedFalse));

        var sessionCreatedTrue = new CharacterSession(ChumSerializer.Parse(
            "<character><created>True</created><karma>5</karma><nuyen>1000</nuyen></character>"));
        Assert.True(CareerManager.IsCareerMode(sessionCreatedTrue));
    }

    [Fact]
    public void SetCareerMode_SetsCreatedAndLogsStartingNuyen()
    {
        var session = new CharacterSession(ChumSerializer.Parse(SampleCharacterXml));

        CareerManager.SetCareerMode(session, true);

        Assert.True(CareerManager.IsCareerMode(session));
        var expenses = CareerManager.GetExpenses(session, ExpenseType.Nuyen);
        var startingNuyen = Assert.Single(expenses);
        Assert.Equal("Starting Nuyen", startingNuyen.Reason);
        Assert.Equal(5000, startingNuyen.Amount);
        Assert.False(startingNuyen.Refund);
        Assert.NotNull(startingNuyen.Undo);
        Assert.Equal(NuyenExpenseType.ManualAdd, startingNuyen.Undo.NuyenType);
    }

    [Fact]
    public void AddKarmaGained_IncreasesKarmaAndLogsPositiveEntry()
    {
        var session = new CharacterSession(ChumSerializer.Parse(SampleCharacterXml));

        var entry = CareerManager.AddKarmaExpense(session, 15, "Run reward", isSpent: false);

        Assert.Equal(25, CareerManager.GetKarma(session));
        Assert.Equal(25, session.Model.Karma);
        Assert.Equal(15, entry.Amount);
        Assert.Equal(ExpenseType.Karma, entry.Type);
        Assert.Equal(KarmaExpenseType.ManualAdd, entry.Undo?.KarmaType);

        var list = CareerManager.GetExpenses(session, ExpenseType.Karma);
        Assert.Single(list);
        Assert.Equal("Run reward", list[0].Reason);
    }

    [Fact]
    public void AddKarmaSpent_DecreasesKarmaAndLogsNegativeEntry()
    {
        var session = new CharacterSession(ChumSerializer.Parse(SampleCharacterXml));

        var entry = CareerManager.AddKarmaExpense(session, 4, "Skill purchase", isSpent: true);

        Assert.Equal(6, CareerManager.GetKarma(session));
        Assert.Equal(6, session.Model.Karma);
        Assert.Equal(-4, entry.Amount);
        Assert.Equal(ExpenseType.Karma, entry.Type);
        Assert.Equal(KarmaExpenseType.ManualSubtract, entry.Undo?.KarmaType);
    }

    [Fact]
    public void AddKarmaSpent_ThrowsWhenInsufficientKarma_PreservesBalance()
    {
        var session = new CharacterSession(ChumSerializer.Parse(SampleCharacterXml));

        var ex = Assert.Throws<InvalidOperationException>(
            () => CareerManager.AddKarmaExpense(session, 11, "Too expensive", isSpent: true));

        Assert.Contains("Karma", ex.Message);
        Assert.Equal(10, CareerManager.GetKarma(session));
        Assert.Empty(CareerManager.GetExpenses(session));
    }

    [Fact]
    public void AddNuyenGained_IncreasesNuyenAndLogsPositiveEntry()
    {
        var session = new CharacterSession(ChumSerializer.Parse(SampleCharacterXml));

        var entry = CareerManager.AddNuyenExpense(session, 2500, "Loot", isSpent: false);

        Assert.Equal(7500, CareerManager.GetNuyen(session));
        Assert.Equal(7500, session.Model.Nuyen);
        Assert.Equal(2500, entry.Amount);
        Assert.Equal(ExpenseType.Nuyen, entry.Type);
        Assert.Equal(NuyenExpenseType.ManualAdd, entry.Undo?.NuyenType);
    }

    [Fact]
    public void AddNuyenSpent_DecreasesNuyenAndLogsNegativeEntry()
    {
        var session = new CharacterSession(ChumSerializer.Parse(SampleCharacterXml));

        var entry = CareerManager.AddNuyenExpense(session, 1500, "Ammo purchase", isSpent: true);

        Assert.Equal(3500, CareerManager.GetNuyen(session));
        Assert.Equal(3500, session.Model.Nuyen);
        Assert.Equal(-1500, entry.Amount);
        Assert.Equal(ExpenseType.Nuyen, entry.Type);
        Assert.Equal(NuyenExpenseType.ManualSubtract, entry.Undo?.NuyenType);
    }

    [Fact]
    public void AddNuyenSpent_ThrowsWhenInsufficientNuyen_PreservesBalance()
    {
        var session = new CharacterSession(ChumSerializer.Parse(SampleCharacterXml));

        var ex = Assert.Throws<InvalidOperationException>(
            () => CareerManager.AddNuyenExpense(session, 5001, "Too expensive", isSpent: true));

        Assert.Contains("Nuyen", ex.Message);
        Assert.Equal(5000, CareerManager.GetNuyen(session));
        Assert.Empty(CareerManager.GetExpenses(session));
    }

    [Fact]
    public void UndoExpense_RevertsBalanceAndRemovesXmlNode()
    {
        var session = new CharacterSession(ChumSerializer.Parse(SampleCharacterXml));
        var entry = CareerManager.AddKarmaExpense(session, 6, "Training", isSpent: true);
        Assert.Equal(4, CareerManager.GetKarma(session));

        var undone = CareerManager.UndoExpense(session, entry.Guid);

        Assert.True(undone);
        Assert.Equal(10, CareerManager.GetKarma(session));
        Assert.Empty(CareerManager.GetExpenses(session));
    }

    private static void AddRaw(CharacterSession session, ExpenseLogEntry e)
    {
        var el = new XElement("expense", new XElement("guid", e.Guid.ToString()), new XElement("date", e.Date.ToString("s")),
            new XElement("amount", e.Amount), new XElement("reason", e.Reason), new XElement("type", e.Type.ToString()), new XElement("refund", e.Refund.ToString()));
        if (e.Undo is not null)
            el.Add(new XElement("undo", new XElement("karmatype", e.Undo.KarmaType.ToString()), new XElement("nuyentype", e.Undo.NuyenType.ToString()), new XElement("objectid", e.Undo.ObjectId), new XElement("qty", 0), new XElement("extra")));
        session.Xml.Root!.Element("expenses")!.Add(el);
    }

    [Fact]
    public void UndoExpense_RejectsEntriesWithoutManualUndo()
    {
        var session = new CharacterSession(ChumSerializer.Parse(SampleCharacterXml));
        var noUndo = new ExpenseLogEntry(Guid.NewGuid(), DateTime.Now, -3, "Legacy", ExpenseType.Karma);
        var skill = new ExpenseLogEntry(Guid.NewGuid(), DateTime.Now, -2, "Attr", ExpenseType.Karma, false,
            new ExpenseUndo(KarmaExpenseType.AddQuality, NuyenExpenseType.AddCyberware, "Quality"));
        AddRaw(session, noUndo);
        AddRaw(session, skill);

        Assert.False(CareerManager.UndoExpense(session, noUndo.Guid));
        Assert.False(CareerManager.UndoExpense(session, skill.Guid));
        Assert.Equal(10, CareerManager.GetKarma(session));
        Assert.Equal(2, CareerManager.GetExpenses(session).Count);
    }

    [Fact]
    public void ImproveAttribute_ChargesKarmaAndUndoRestores()
    {
        var session = new CharacterSession(ChumSerializer.Parse("""
            <character><created>True</created><karma>30</karma><nuyen>0</nuyen><expenses />
              <attributes><attribute><name>BOD</name><metatypemin>1</metatypemin><metatypemax>6</metatypemax><metatypeaugmax>9</metatypeaugmax><value>3</value><base>3</base><augmodifier>0</augmodifier><totalvalue>3</totalvalue></attribute></attributes>
            </character>
            """));

        var entry = CareerManager.ImproveAttribute(session, "BOD");

        Assert.Equal(-20, entry.Amount);
        Assert.Equal(10, CareerManager.GetKarma(session));
        Assert.Equal(4, new ChummerGen.Domain.Character.AttributesSkills.AttributesSkillsManager(session).GetAttribute("BOD")!.BaseValue);
        Assert.Throws<InvalidOperationException>(() => CareerManager.ImproveAttribute(session, "BOD"));
        Assert.Throws<InvalidOperationException>(() => CareerManager.ImproveAttribute(session, "MAG"));

        Assert.True(CareerManager.UndoExpense(session, entry.Guid));
        Assert.Equal(30, CareerManager.GetKarma(session));
        Assert.Equal(3, new ChummerGen.Domain.Character.AttributesSkills.AttributesSkillsManager(session).GetAttribute("BOD")!.BaseValue);
    }

    [Fact]
    public void ImproveSkillAndGroup_ChargeKarmaAndUndoRestores()
    {
        var session = new CharacterSession(ChumSerializer.Parse("""
            <character><created>True</created><karma>50</karma><nuyen>0</nuyen><uncouth>True</uncouth><expenses />
              <skills>
                <skill><name>Con</name><skillgroup>Influence</skillgroup><skillcategory>Social Active</skillcategory><grouped>False</grouped><rating>0</rating><ratingmax>6</ratingmax><attribute>CHA</attribute></skill>
                <skill><name>Pistols</name><skillgroup>Firearms</skillgroup><skillcategory>Combat Active</skillcategory><grouped>True</grouped><rating>0</rating><ratingmax>6</ratingmax><attribute>AGI</attribute></skill>
                <skill><name>Language</name><knowledge>True</knowledge><rating>0</rating></skill>
              </skills>
              <skillgroups><skillgroup><name>Influence</name><rating>0</rating><ratingmax>6</ratingmax></skillgroup><skillgroup><name>Firearms</name><rating>0</rating><ratingmax>6</ratingmax></skillgroup></skillgroups>
            </character>
            """));

        var skill = CareerManager.ImproveSkill(session, "Con");
        Assert.Equal(-8, skill.Amount);
        Assert.Equal(42, CareerManager.GetKarma(session));
        Assert.Equal("True", session.Xml.Root!.Element("skillgroups")!.Elements("skillgroup").First().Element("broken")!.Value);
        Assert.Throws<InvalidOperationException>(() => CareerManager.ImproveSkill(session, "Pistols"));
        Assert.Throws<InvalidOperationException>(() => CareerManager.ImproveSkill(session, "Language"));

        var group = CareerManager.ImproveSkillGroup(session, "Firearms");
        Assert.Equal(-10, group.Amount);
        Assert.Equal(1, new ChummerGen.Domain.Character.AttributesSkills.AttributesSkillsManager(session).GetSkills().Single(s => s.Name == "Pistols").Rating);
        Assert.Throws<InvalidOperationException>(() => CareerManager.ImproveSkillGroup(session, "Influence"));

        Assert.True(CareerManager.UndoExpense(session, group.Guid));
        Assert.True(CareerManager.UndoExpense(session, skill.Guid));
        Assert.Equal(50, CareerManager.GetKarma(session));
        Assert.All(new ChummerGen.Domain.Character.AttributesSkills.AttributesSkillsManager(session).GetSkills(), s => Assert.Equal(0, s.Rating));
    }

    [Fact]
    public void EditExpense_ChangesAmountOnlyForManualEntries()
    {
        var session = new CharacterSession(ChumSerializer.Parse(SampleCharacterXml));
        var manual = CareerManager.AddKarmaExpense(session, 5, "Reward");
        var other = new ExpenseLogEntry(Guid.NewGuid(), DateTime.Now, -2, "Attr", ExpenseType.Karma, false,
            new ExpenseUndo(KarmaExpenseType.ImproveAttribute, NuyenExpenseType.AddCyberware, "BOD"));
        AddRaw(session, other);
        var noUndo = new ExpenseLogEntry(Guid.NewGuid(), DateTime.Now, 1, "Legacy", ExpenseType.Karma);
        AddRaw(session, noUndo);

        Assert.True(CareerManager.EditExpense(session, manual.Guid, 8, "Neu", new DateTime(2020, 1, 2)));
        Assert.Equal(18, CareerManager.GetKarma(session));

        Assert.True(CareerManager.EditExpense(session, other.Guid, 99, "Umbenannt", new DateTime(2020, 1, 2)));
        Assert.Equal(18, CareerManager.GetKarma(session));
        var edited = CareerManager.GetExpenses(session).Single(e => e.Guid == other.Guid);
        Assert.Equal(-2, edited.Amount);
        Assert.Equal("Umbenannt", edited.Reason);

        Assert.False(CareerManager.EditExpense(session, noUndo.Guid, 5, "x", DateTime.Now));
    }

    [Fact]
    public void CareerTotals_AccuratelySumEarnedAmountsExcludingRefunds()
    {
        var session = new CharacterSession(ChumSerializer.Parse(SampleCharacterXml));

        CareerManager.AddKarmaExpense(session, 10, "Run 1", isSpent: false, refund: false);
        CareerManager.AddKarmaExpense(session, 5, "Refunded Karma", isSpent: false, refund: true);
        CareerManager.AddKarmaExpense(session, 4, "Spent Karma", isSpent: true, refund: false);

        CareerManager.AddNuyenExpense(session, 10000, "Payoff 1", isSpent: false, refund: false);
        CareerManager.AddNuyenExpense(session, 2000, "Refunded Nuyen", isSpent: false, refund: true);
        CareerManager.AddNuyenExpense(session, 3000, "Spent Nuyen", isSpent: true, refund: false);

        Assert.Equal(10, CareerManager.GetCareerKarma(session));
        Assert.Equal(10000, CareerManager.GetCareerNuyen(session));
    }

    [Fact]
    public void Roundtrip_PreservesAllUnmodeledXmlIntact()
    {
        var session = new CharacterSession(ChumSerializer.Parse(SampleCharacterXml));

        CareerManager.SetCareerMode(session, true);
        CareerManager.AddKarmaExpense(session, 12, "Reward", isSpent: false);
        CareerManager.AddNuyenExpense(session, 800, "Gear sale", isSpent: false);

        var serialized = session.Serialize();
        var reloaded = new CharacterSession(ChumSerializer.Parse(serialized));

        Assert.True(CareerManager.IsCareerMode(reloaded));
        Assert.Equal(22, CareerManager.GetKarma(reloaded));
        Assert.Equal(5800, CareerManager.GetNuyen(reloaded));

        Assert.Equal("intact", reloaded.Xml.Root?.Element("customnode")?.Element("nested")?.Value);
        Assert.Equal("preserve", (string?)reloaded.Xml.Root?.Element("customnode")?.Attribute("test"));
    }

    [Fact]
    public async Task CareerModule_Validation_DetectsNegativeBalances()
    {
        var module = new CareerModule();
        var catalog = new EmptyCatalog();

        var validSession = new CharacterSession(ChumSerializer.Parse(
            "<character><karma>5</karma><nuyen>100</nuyen></character>"));
        var validIssues = await module.ValidateAsync(validSession, catalog);
        Assert.Empty(validIssues);

        var invalidSession = new CharacterSession(ChumSerializer.Parse(
            "<character><karma>-1</karma><nuyen>-50</nuyen></character>"));
        var invalidIssues = await module.ValidateAsync(invalidSession, catalog);

        Assert.Equal(2, invalidIssues.Count);
        Assert.Contains(invalidIssues, i => i.Code == "career-karma-negative");
        Assert.Contains(invalidIssues, i => i.Code == "career-nuyen-negative");
    }

    private sealed class EmptyCatalog : IRuleCatalog
    {
        public Task<RuleCatalogDocument?> GetAsync(string catalogName, string culture, CancellationToken cancellationToken) =>
            Task.FromResult<RuleCatalogDocument?>(null);
    }

    private static CharacterSession QualitySession(int karma) => new(ChumSerializer.Parse($$"""
        <character><created>True</created><karma>{{karma}}</karma><nuyen>0</nuyen><expenses />
          <qualities>
            <quality><guid>g-neg</guid><name>Bad Luck</name><bp>-10</bp><qualitytype>Negative</qualitytype><qualitysource>Selected</qualitysource></quality>
            <quality><guid>g-pos</guid><name>Lucky</name><bp>12</bp><qualitytype>Positive</qualitytype><qualitysource>Selected</qualitysource></quality>
            <quality><guid>g-meta</guid><name>Low-Light</name><bp>10</bp><qualitytype>Positive</qualitytype><qualitysource>Metatype</qualitysource></quality>
          </qualities>
        </character>
        """));

    [Fact]
    public void RemoveQuality_NegativeCostsKarmaAndBooksExpense()
    {
        var session = QualitySession(30);

        var entry = CareerManager.RemoveQuality(session, "g-neg");

        Assert.NotNull(entry);
        Assert.Equal(-20, entry!.Amount);
        Assert.Equal(10, CareerManager.GetKarma(session));
        Assert.Equal(KarmaExpenseType.RemoveQuality, entry.Undo!.KarmaType);
        Assert.DoesNotContain(new ChummerGen.Domain.Character.Qualities.QualitiesManager(session).GetAllQualities(), q => q.Guid == "g-neg");
        Assert.False(CareerManager.UndoExpense(session, entry.Guid));
    }

    [Fact]
    public void RemoveQuality_PositiveIsFree_FailsWithoutKarmaOrForMetatype()
    {
        var session = QualitySession(5);

        Assert.Null(CareerManager.RemoveQuality(session, "g-pos"));
        Assert.Equal(5, CareerManager.GetKarma(session));
        Assert.Empty(CareerManager.GetExpenses(session));
        Assert.Throws<InvalidOperationException>(() => CareerManager.RemoveQuality(session, "g-neg"));
        Assert.Throws<InvalidOperationException>(() => CareerManager.RemoveQuality(session, "g-meta"));
        Assert.Equal(2, new ChummerGen.Domain.Character.Qualities.QualitiesManager(session).GetAllQualities().Count);
    }
    private const string QualityCatalogXml = """
        <chummer><qualities>
          <quality><name>Lucky</name><bp>12</bp><category>Positive</category><bonus /><source>SR4</source><page>1</page></quality>
          <quality><name>Bad Luck</name><bp>-10</bp><category>Negative</category><bonus /><source>SR4</source><page>2</page></quality>
          <quality><name>Elf Only</name><bp>5</bp><category>Positive</category><bonus /><required><oneof><metatype>Elf</metatype></oneof></required><source>SR4</source><page>3</page></quality>
          <quality><name>Not Lucky</name><bp>5</bp><category>Positive</category><bonus /><forbidden><oneof><quality>Lucky</quality></oneof></forbidden><source>SR4</source><page>4</page></quality>
          <quality><name>Phobia</name><bp>-5</bp><category>Negative</category><bonus><selecttext /></bonus><source>SR4</source><page>6</page></quality>
          <quality><name>Skilled</name><bp>5</bp><category>Positive</category><bonus><specificskill><name>Pistols</name><bonus>1</bonus></specificskill></bonus><source>SR4</source><page>7</page></quality>
          <quality><name>Uncouth</name><bp>-14</bp><category>Negative</category><bonus><notoriety>1</notoriety><uncouth /></bonus><source>SR4</source><page>8</page></quality>
          <quality><name>Bonus</name><bp>5</bp><category>Positive</category><bonus><MAG>1</MAG></bonus><source>SR4</source><page>5</page></quality>
        </qualities></chummer>
        """;

    [Fact]
    public void AddQuality_PositiveCostsKarma_NegativeIsFree_UndoRemoves()
    {
        var session = new CharacterSession(ChumSerializer.Parse("<character><created>True</created><karma>30</karma><nuyen>0</nuyen><metatype>Human</metatype><expenses /></character>"));
        var catalog = ChummerGen.Domain.Character.Qualities.QualityCatalog.FromXml(QualityCatalogXml);

        var pos = CareerManager.AddQuality(session, catalog, "Lucky");
        Assert.Equal(-24, pos.Amount);
        Assert.Equal(6, CareerManager.GetKarma(session));
        var neg = CareerManager.AddQuality(session, catalog, "Bad Luck");
        Assert.Equal(0, neg.Amount);
        var manager = new ChummerGen.Domain.Character.Qualities.QualitiesManager(session);
        Assert.Equal(2, manager.GetAllQualities().Count);

        Assert.True(CareerManager.UndoExpense(session, pos.Guid));
        Assert.Equal(30, CareerManager.GetKarma(session));
        Assert.Equal("Bad Luck", Assert.Single(manager.GetAllQualities()).Name);
    }

    [Fact]
    public void AddQuality_BlocksDuplicatesRequirementsForbiddenBonusAndKarma()
    {
        var session = new CharacterSession(ChumSerializer.Parse("<character><created>True</created><karma>20</karma><nuyen>0</nuyen><metatype>Human</metatype><expenses /></character>"));
        var catalog = ChummerGen.Domain.Character.Qualities.QualityCatalog.FromXml(QualityCatalogXml);

        Assert.Throws<InvalidOperationException>(() => CareerManager.AddQuality(session, catalog, "Elf Only"));
        Assert.Throws<InvalidOperationException>(() => CareerManager.AddQuality(session, catalog, "Bonus"));
        Assert.Throws<InvalidOperationException>(() => CareerManager.AddQuality(session, catalog, "Unbekannt"));
        CareerManager.AddQuality(session, catalog, "Bad Luck");
        Assert.Throws<InvalidOperationException>(() => CareerManager.AddQuality(session, catalog, "Bad Luck"));
        Assert.Throws<InvalidOperationException>(() => CareerManager.AddQuality(session, catalog, "Lucky"));
        Assert.Equal(20, CareerManager.GetKarma(session));
        Assert.Equal("Bad Luck", Assert.Single(new ChummerGen.Domain.Character.Qualities.QualitiesManager(session).GetAllQualities()).Name);
    }

    [Fact]
    public void QualityCatalog_RealCatalog_ParsesAndPlainEntriesAreAddable()
    {
        string? data = null;
        for (var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory); dir is not null && data is null; dir = dir.Parent)
        {
            var candidate = System.IO.Path.Combine(dir.FullName, "bin", "data");
            if (System.IO.Directory.Exists(candidate))
                data = candidate;
        }
        if (data is null || !System.IO.File.Exists(System.IO.Path.Combine(data, "qualities.xml")))
            return;
        var catalog = ChummerGen.Domain.Character.Qualities.QualityCatalog.FromXml(System.IO.File.ReadAllText(System.IO.Path.Combine(data, "qualities.xml")), System.IO.File.ReadAllText(System.IO.Path.Combine(data, "weapons.xml")));
        var session = new CharacterSession(ChumSerializer.Parse("<character><created>True</created><karma>500</karma><nuyen>0</nuyen><metatype>Human</metatype><metatypecategory>Metahuman</metatypecategory><expenses /></character>"));

        Assert.True(catalog.Entries.Count() > 400);
        var addable = catalog.Entries.Where(e => catalog.GetAddBlocker(session, e) is null).ToList();
        Assert.NotEmpty(addable);
        Assert.True(addable.Count > 200, $"addable {addable.Count}");
        Assert.Contains(addable, e => e.Element.Element("bonus")!.HasElements);
        Assert.Contains(addable, e => e.Element.Element("addqualities") is not null);
        foreach (var e in addable)
        {
            var fresh = new CharacterSession(ChumSerializer.Parse("<character><created>True</created><karma>500</karma><nuyen>0</nuyen><metatype>Human</metatype><metatypecategory>Metahuman</metatypecategory><expenses /></character>"));
            var log = CareerManager.AddQuality(fresh, catalog, e.Name, text: "Test");
            Assert.True(CareerManager.UndoExpense(fresh, log.Guid), e.Name);
            Assert.Empty(new ChummerGen.Domain.Character.Qualities.QualitiesManager(fresh).GetAllImprovements());
            Assert.Empty(fresh.Xml.Root!.Element("weapons")?.Elements("weapon") ?? []);
            Assert.Empty(fresh.Xml.Root!.Element("qualities")?.Elements("quality") ?? []);
        }
    }
    [Fact]
    public void AddQuality_RealCatalogWeaponQualityAddsLinkedWeaponAndUndoRemovesIt()
    {
        string? data = null;
        for (var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory); dir is not null && data is null; dir = dir.Parent)
        {
            var candidate = System.IO.Path.Combine(dir.FullName, "bin", "data");
            if (System.IO.Directory.Exists(candidate))
                data = candidate;
        }
        if (data is null || !System.IO.File.Exists(System.IO.Path.Combine(data, "qualities.xml")) || !System.IO.File.Exists(System.IO.Path.Combine(data, "weapons.xml")))
            return;
        var catalog = ChummerGen.Domain.Character.Qualities.QualityCatalog.FromXml(System.IO.File.ReadAllText(System.IO.Path.Combine(data, "qualities.xml")), System.IO.File.ReadAllText(System.IO.Path.Combine(data, "weapons.xml")));
        const string Xml = "<character><created>True</created><karma>500</karma><nuyen>0</nuyen><metatype>Human</metatype><metatypecategory>Mutant Critters</metatypecategory><expenses /></character>";
        var weaponQualities = catalog.Entries.Where(e => e.Element.Element("addweapon") is not null).ToList();
        Assert.Equal(6, weaponQualities.Count);
        foreach (var e in weaponQualities)
        {
            var session = new CharacterSession(ChumSerializer.Parse(Xml));
            var log = CareerManager.AddQuality(session, catalog, e.Name);
            var quality = session.Xml.Root!.Element("qualities")!.Elements("quality").Single(q => q.Element("name")!.Value == e.Name);
            var weapon = Assert.Single(session.Xml.Root!.Element("weapons")!.Elements("weapon"));
            Assert.Equal(weapon.Element("guid")!.Value, quality.Element("weaponguid")!.Value);
            Assert.True(CareerManager.UndoExpense(session, log.Guid), e.Name);
            Assert.Empty(session.Xml.Root!.Element("weapons")!.Elements("weapon"));
        }
    }

    [Fact]
    public void AddQuality_RealCatalogAddqualitiesAddsFollowUpAndUndoRemovesBoth()
    {
        string? data = null;
        for (var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory); dir is not null && data is null; dir = dir.Parent)
        {
            var candidate = System.IO.Path.Combine(dir.FullName, "bin", "data");
            if (System.IO.Directory.Exists(candidate))
                data = candidate;
        }
        if (data is null || !System.IO.File.Exists(System.IO.Path.Combine(data, "qualities.xml")))
            return;
        var catalog = ChummerGen.Domain.Character.Qualities.QualityCatalog.FromXml(System.IO.File.ReadAllText(System.IO.Path.Combine(data, "qualities.xml")));
        var session = new CharacterSession(ChumSerializer.Parse("<character><created>True</created><karma>500</karma><nuyen>0</nuyen><metatype>Human</metatype><metatypecategory>Metahuman</metatypecategory><expenses /></character>"));
        var host = catalog.Entries.First(e => e.Element.Element("addqualities") is not null && catalog.GetAddBlocker(session, e) is null);
        var log = CareerManager.AddQuality(session, catalog, host.Name);
        var names = session.Xml.Root!.Element("qualities")!.Elements("quality").Select(q => q.Element("name")!.Value).ToList();
        Assert.Contains("Distinctive Style", names);
        Assert.Equal(2, names.Count);
        Assert.True(CareerManager.UndoExpense(session, log.Guid));
        Assert.Empty(session.Xml.Root!.Element("qualities")!.Elements("quality"));
    }

    [Fact]
    public void AddQuality_AppliesBonusImprovementsWithTextAndUndoRemovesThem()
    {
        var session = new CharacterSession(ChumSerializer.Parse("<character><created>True</created><karma>30</karma><nuyen>0</nuyen><metatype>Human</metatype><expenses /></character>"));
        var catalog = ChummerGen.Domain.Character.Qualities.QualityCatalog.FromXml(QualityCatalogXml);
        var manager = new ChummerGen.Domain.Character.Qualities.QualitiesManager(session);

        Assert.Throws<InvalidOperationException>(() => CareerManager.AddQuality(session, catalog, "Phobia"));
        Assert.Empty(manager.GetAllQualities());

        var phobia = CareerManager.AddQuality(session, catalog, "Phobia", text: "Spiders");
        var skilled = CareerManager.AddQuality(session, catalog, "Skilled");

        var qualities = manager.GetAllQualities();
        Assert.Equal("Phobia (Spiders)", qualities.Single(q => q.Name == "Phobia").DisplayName);
        var improvements = manager.GetQualityImprovements();
        Assert.Contains(improvements, i => i.ImprovementType == "Text" && i.ImprovedName == "Spiders");
        Assert.Contains(improvements, i => i.ImprovementType == "SkillBonus" || i.ImprovedName == "Pistols");

        Assert.True(CareerManager.UndoExpense(session, phobia.Guid));
        Assert.True(CareerManager.UndoExpense(session, skilled.Guid));
        Assert.Empty(manager.GetAllQualities());
        Assert.Empty(manager.GetAllImprovements());
    }
    [Fact]
    public void AddQuality_ValueAndFlagTags_SetImprovementsAndFlagsAndUndoResetsFlag()
    {
        var session = new CharacterSession(ChumSerializer.Parse("<character><created>True</created><karma>0</karma><nuyen>0</nuyen><metatype>Human</metatype><uncouth>False</uncouth><expenses /></character>"));
        var catalog = ChummerGen.Domain.Character.Qualities.QualityCatalog.FromXml(QualityCatalogXml);
        var manager = new ChummerGen.Domain.Character.Qualities.QualitiesManager(session);

        var log = CareerManager.AddQuality(session, catalog, "Uncouth");

        Assert.Equal("True", session.Xml.Root!.Element("uncouth")!.Value);
        var imps = manager.GetQualityImprovements();
        Assert.Contains(imps, i => i.ImprovementType == "Notoriety" && i.Val == 1 && i.UniqueName == "");
        Assert.Contains(imps, i => i.ImprovementType == "Uncouth");

        Assert.True(CareerManager.UndoExpense(session, log.Guid));
        Assert.Equal("False", session.Xml.Root.Element("uncouth")!.Value);
        Assert.Empty(manager.GetAllImprovements());
    }}
