namespace ChummerGen.Domain.Character.AttributesSkills;

public sealed record SkillModel(
    string Name,
    string SkillGroup,
    string SkillCategory,
    bool Grouped,
    bool Default,
    int Rating,
    int RatingMax,
    bool Knowledge,
    bool Exotic,
    string? Specialization,
    bool AllowDelete,
    string Attribute,
    int TotalValue
)
{
    public bool IsActive => !Knowledge;
}

public sealed record SkillGroupModel(
    string Name,
    int Rating,
    int RatingMax,
    bool Broken
);

public sealed record SkillsSummary(
    int ActiveSkillsBp,
    int SkillGroupsBp,
    int SpecializationsBp,
    int TotalBp,
    int TotalActiveSkillsCount,
    int TotalSkillGroupsCount
);
