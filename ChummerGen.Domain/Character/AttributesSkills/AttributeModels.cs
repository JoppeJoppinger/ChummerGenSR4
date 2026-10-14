namespace ChummerGen.Domain.Character.AttributesSkills;

public sealed record AttributeModel(
    string Name,
    int MetatypeMin,
    int MetatypeMax,
    int MetatypeAugMax,
    int BaseValue,
    int AugModifier,
    int TotalValue
)
{
    public bool IsSpecial => Name is "EDG" or "ESS" or "MAG" or "RES";
    public bool IsNaturalMax => MetatypeMax > 0 && BaseValue >= MetatypeMax;
}

public sealed record DerivedAttributes(
    int Composure,
    int JudgeIntentions,
    int LiftAndCarry,
    int Memory,
    int InitiativeBase,
    int InitiativeTotal,
    int InitiativePasses
);

public sealed record AttributesSummary(
    int TotalAttributeBp,
    int PrimaryAttributeBp,
    int SpecialAttributeBp,
    int MaxPrimaryBpAllowed,
    bool ExceedsPrimaryBpLimit,
    int NaturalMaxCount
);
