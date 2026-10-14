using System;

namespace ChummerGen.Domain.Character.Career;

public sealed record ExpenseUndo(
    KarmaExpenseType KarmaType,
    NuyenExpenseType NuyenType,
    string ObjectId = "",
    int Qty = 0,
    string Extra = "");

public sealed record ExpenseLogEntry(
    Guid Guid,
    DateTime Date,
    int Amount,
    string Reason,
    ExpenseType Type,
    bool Refund = false,
    ExpenseUndo? Undo = null);
