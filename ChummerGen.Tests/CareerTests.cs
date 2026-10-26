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
            new ExpenseUndo(KarmaExpenseType.ImproveAttribute, NuyenExpenseType.AddCyberware, "BOD"));
        AddRaw(session, noUndo);
        AddRaw(session, skill);

        Assert.False(CareerManager.UndoExpense(session, noUndo.Guid));
        Assert.False(CareerManager.UndoExpense(session, skill.Guid));
        Assert.Equal(10, CareerManager.GetKarma(session));
        Assert.Equal(2, CareerManager.GetExpenses(session).Count);
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
}
