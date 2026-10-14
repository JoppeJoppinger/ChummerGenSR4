using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace ChummerGen.Domain.Character.Career;

public static class CareerManager
{
    public static bool IsCareerMode(CharacterSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var created = session.Xml.Root?.Element("created")?.Value;
        return string.Equals(created, "True", StringComparison.OrdinalIgnoreCase);
    }

    public static void SetCareerMode(CharacterSession session, bool isCareer)
    {
        ArgumentNullException.ThrowIfNull(session);
        var root = session.Xml.Root
            ?? throw new InvalidOperationException("Charakter-XML hat kein Root-Element.");

        var createdElement = root.Element("created");
        if (createdElement is null)
        {
            createdElement = new XElement("created", isCareer ? "True" : "False");
            root.Add(createdElement);
        }
        else
        {
            createdElement.Value = isCareer ? "True" : "False";
        }

        if (isCareer)
        {
            var expenses = GetExpenses(session, ExpenseType.Nuyen);
            var hasStartingNuyen = expenses.Any(e => string.Equals(e.Reason, "Starting Nuyen", StringComparison.OrdinalIgnoreCase));
            if (!hasStartingNuyen)
            {
                var startingNuyen = session.Model.Nuyen ?? 0;
                AddRawExpense(session, new ExpenseLogEntry(
                    Guid.NewGuid(),
                    DateTime.Now,
                    startingNuyen,
                    "Starting Nuyen",
                    ExpenseType.Nuyen,
                    false,
                    new ExpenseUndo(KarmaExpenseType.ImproveAttribute, NuyenExpenseType.ManualAdd)));
            }
        }
    }

    public static int GetKarma(CharacterSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Model.Karma ?? 0;
    }

    public static int GetNuyen(CharacterSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Model.Nuyen ?? 0;
    }

    public static int GetCareerKarma(CharacterSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return GetExpenses(session, ExpenseType.Karma)
            .Where(e => e.Amount > 0 && !e.Refund)
            .Sum(e => e.Amount);
    }

    public static int GetCareerNuyen(CharacterSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return GetExpenses(session, ExpenseType.Nuyen)
            .Where(e => e.Amount > 0 && !e.Refund)
            .Sum(e => e.Amount);
    }

    public static IReadOnlyList<ExpenseLogEntry> GetExpenses(CharacterSession session, ExpenseType? filter = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        var expensesRoot = session.Xml.Root?.Element("expenses");
        if (expensesRoot is null)
            return Array.Empty<ExpenseLogEntry>();

        var result = new List<ExpenseLogEntry>();
        foreach (var el in expensesRoot.Elements("expense"))
        {
            var entry = ParseExpenseElement(el);
            if (entry is not null)
            {
                if (filter is null || entry.Type == filter.Value)
                    result.Add(entry);
            }
        }

        return result;
    }

    public static ExpenseLogEntry AddKarmaExpense(
        CharacterSession session,
        int amount,
        string reason,
        DateTime? date = null,
        bool isSpent = false,
        bool refund = false)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        var current = GetKarma(session);
        int storedAmount;
        ExpenseUndo undo;

        if (isSpent)
        {
            if (current - amount < 0)
                throw new InvalidOperationException("Nicht genügend Karma.");

            session.Model.Karma = current - amount;
            storedAmount = -amount;
            undo = new ExpenseUndo(KarmaExpenseType.ManualSubtract, NuyenExpenseType.AddCyberware);
        }
        else
        {
            session.Model.Karma = current + amount;
            storedAmount = amount;
            undo = new ExpenseUndo(KarmaExpenseType.ManualAdd, NuyenExpenseType.AddCyberware);
        }

        var entry = new ExpenseLogEntry(
            Guid.NewGuid(),
            date ?? DateTime.Now,
            storedAmount,
            reason ?? string.Empty,
            ExpenseType.Karma,
            refund,
            undo);

        AddRawExpense(session, entry);
        return entry;
    }

    public static ExpenseLogEntry AddNuyenExpense(
        CharacterSession session,
        int amount,
        string reason,
        DateTime? date = null,
        bool isSpent = false,
        bool refund = false)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        var current = GetNuyen(session);
        int storedAmount;
        ExpenseUndo undo;

        if (isSpent)
        {
            if (current - amount < 0)
                throw new InvalidOperationException("Nicht genügend Nuyen.");

            session.Model.Nuyen = current - amount;
            storedAmount = -amount;
            undo = new ExpenseUndo(KarmaExpenseType.ImproveAttribute, NuyenExpenseType.ManualSubtract);
        }
        else
        {
            session.Model.Nuyen = current + amount;
            storedAmount = amount;
            undo = new ExpenseUndo(KarmaExpenseType.ImproveAttribute, NuyenExpenseType.ManualAdd);
        }

        var entry = new ExpenseLogEntry(
            Guid.NewGuid(),
            date ?? DateTime.Now,
            storedAmount,
            reason ?? string.Empty,
            ExpenseType.Nuyen,
            refund,
            undo);

        AddRawExpense(session, entry);
        return entry;
    }

    public static bool UndoExpense(CharacterSession session, Guid expenseGuid)
    {
        ArgumentNullException.ThrowIfNull(session);
        var expensesRoot = session.Xml.Root?.Element("expenses");
        if (expensesRoot is null)
            return false;

        var target = expensesRoot.Elements("expense").FirstOrDefault(el =>
            Guid.TryParse(el.Element("guid")?.Value, out var g) && g == expenseGuid);

        if (target is null)
            return false;

        var entry = ParseExpenseElement(target);
        if (entry is null)
            return false;

        if (entry.Type == ExpenseType.Karma)
        {
            var newKarma = GetKarma(session) - entry.Amount;
            session.Model.Karma = newKarma;
        }
        else if (entry.Type == ExpenseType.Nuyen)
        {
            var newNuyen = GetNuyen(session) - entry.Amount;
            session.Model.Nuyen = newNuyen;
        }

        target.Remove();
        return true;
    }

    public static bool EditExpense(
        CharacterSession session,
        Guid expenseGuid,
        int newAmount,
        string newReason,
        DateTime newDate)
    {
        ArgumentNullException.ThrowIfNull(session);
        var expensesRoot = session.Xml.Root?.Element("expenses");
        if (expensesRoot is null)
            return false;

        var target = expensesRoot.Elements("expense").FirstOrDefault(el =>
            Guid.TryParse(el.Element("guid")?.Value, out var g) && g == expenseGuid);

        if (target is null)
            return false;

        var entry = ParseExpenseElement(target);
        if (entry is null)
            return false;

        var delta = newAmount - entry.Amount;
        if (entry.Type == ExpenseType.Karma)
        {
            if (GetKarma(session) + delta < 0)
                throw new InvalidOperationException("Nicht genügend Karma.");

            session.Model.Karma = GetKarma(session) + delta;
        }
        else if (entry.Type == ExpenseType.Nuyen)
        {
            if (GetNuyen(session) + delta < 0)
                throw new InvalidOperationException("Nicht genügend Nuyen.");

            session.Model.Nuyen = GetNuyen(session) + delta;
        }

        var amountEl = target.Element("amount");
        if (amountEl is not null)
            amountEl.Value = newAmount.ToString(CultureInfo.InvariantCulture);

        var reasonEl = target.Element("reason");
        if (reasonEl is not null)
            reasonEl.Value = newReason ?? string.Empty;

        var dateEl = target.Element("date");
        if (dateEl is not null)
            dateEl.Value = newDate.ToString("s", CultureInfo.InvariantCulture);

        return true;
    }

    private static void AddRawExpense(CharacterSession session, ExpenseLogEntry entry)
    {
        var root = session.Xml.Root
            ?? throw new InvalidOperationException("Charakter-XML hat kein Root-Element.");

        var expenses = root.Element("expenses");
        if (expenses is null)
        {
            expenses = new XElement("expenses");
            root.Add(expenses);
        }

        var el = new XElement("expense",
            new XElement("guid", entry.Guid.ToString()),
            new XElement("date", entry.Date.ToString("s", CultureInfo.InvariantCulture)),
            new XElement("amount", entry.Amount.ToString(CultureInfo.InvariantCulture)),
            new XElement("reason", entry.Reason),
            new XElement("type", entry.Type.ToString()),
            new XElement("refund", entry.Refund ? "True" : "False")
        );

        if (entry.Undo is not null)
        {
            el.Add(new XElement("undo",
                new XElement("karmatype", entry.Undo.KarmaType.ToString()),
                new XElement("nuyentype", entry.Undo.NuyenType.ToString()),
                string.IsNullOrEmpty(entry.Undo.ObjectId) ? new XElement("objectid") : new XElement("objectid", entry.Undo.ObjectId),
                new XElement("qty", entry.Undo.Qty.ToString(CultureInfo.InvariantCulture)),
                string.IsNullOrEmpty(entry.Undo.Extra) ? new XElement("extra") : new XElement("extra", entry.Undo.Extra)
            ));
        }

        expenses.Add(el);
    }

    private static ExpenseLogEntry? ParseExpenseElement(XElement el)
    {
        if (!Guid.TryParse(el.Element("guid")?.Value, out var guid))
            return null;

        var dateStr = el.Element("date")?.Value;
        if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            if (!DateTime.TryParse(dateStr, CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
                date = DateTime.MinValue;
        }

        if (!int.TryParse(el.Element("amount")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
            amount = 0;

        var reason = el.Element("reason")?.Value ?? string.Empty;
        var typeStr = el.Element("type")?.Value;
        var type = string.Equals(typeStr, "Nuyen", StringComparison.OrdinalIgnoreCase)
            ? ExpenseType.Nuyen
            : ExpenseType.Karma;

        var refundStr = el.Element("refund")?.Value;
        var refund = string.Equals(refundStr, "True", StringComparison.OrdinalIgnoreCase);

        ExpenseUndo? undo = null;
        var undoEl = el.Element("undo");
        if (undoEl is not null)
        {
            var karmaTypeStr = undoEl.Element("karmatype")?.Value;
            var nuyenTypeStr = undoEl.Element("nuyentype")?.Value;

            Enum.TryParse<KarmaExpenseType>(karmaTypeStr, out var karmaExpenseType);
            Enum.TryParse<NuyenExpenseType>(nuyenTypeStr, out var nuyenExpenseType);

            var objectId = undoEl.Element("objectid")?.Value ?? string.Empty;
            int.TryParse(undoEl.Element("qty")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var qty);
            var extra = undoEl.Element("extra")?.Value ?? string.Empty;

            undo = new ExpenseUndo(karmaExpenseType, nuyenExpenseType, objectId, qty, extra);
        }

        return new ExpenseLogEntry(guid, date, amount, reason, type, refund, undo);
    }
}
