namespace ChummerGen.Domain.Character;

public sealed class CharacterSettings
{
    public string Name { get; set; } = "Default Settings";
    public bool ConfirmDelete { get; set; } = true;
    public bool ConfirmKarmaExpense { get; set; } = true;
    public bool PrintSkillsWithZeroRating { get; set; } = true;
    public bool MoreLethalGameplay { get; set; }
    public bool SkillDefaultingIncludesModifiers { get; set; }
    public bool EnforceSkillMaximumModifiedRating { get; set; } = true;
    public bool CapSkillRating { get; set; }
    public bool FreeKarmaKnowledge { get; set; }
    public bool NoSingleArmorEncumbrance { get; set; }
    public bool IgnoreArmorEncumbrance { get; set; }
    public bool AlternateArmorEncumbrance { get; set; }
    public bool EssenceLossReducesMaximumOnly { get; set; }
    public bool MetatypeCostsKarma { get; set; }
    public bool AllowCyberwareEssenceDiscounts { get; set; }
    public bool StrengthAffectsRecoil { get; set; }
    public bool ExceedPositiveQualities { get; set; }
    public bool ExceedNegativeQualities { get; set; }
    public bool EnforceCapacity { get; set; } = true;
    public bool RestrictRecoil { get; set; } = true;
    public bool AllowExceedAttributeBuildPoints { get; set; }
    public bool UnrestrictedNuyen { get; set; }
    public bool CalculateCommlinkResponse { get; set; } = true;
    public bool AlternateMetatypeAttributeKarma { get; set; }
    public bool CreateBackupOnCareer { get; set; }
    public string BookXPath { get; set; } = string.Empty;
    public int NuyenPerBuildPoint { get; set; } = 5000;
    public int FreeContactsMultiplier { get; set; } = 2;
    public int MetatypeCostMultiplier { get; set; } = 1;
    public int RestrictedCostMultiplier { get; set; } = 1;
    public int ForbiddenCostMultiplier { get; set; } = 1;
    public int AttributeBuildPointCost { get; set; } = 10;
    public int AttributeBuildPointMaximum { get; set; } = 15;
    public int ActiveSkillBuildPointCost { get; set; } = 4;
    public int ActiveSkillSpecializationBuildPointCost { get; set; } = 2;
    public int SkillGroupBuildPointCost { get; set; } = 10;
    public int AttributeKarmaCost { get; set; } = 5;
    public int QualityKarmaCost { get; set; } = 2;
    public int NewActiveSkillKarmaCost { get; set; } = 4;
    public int ImproveActiveSkillKarmaCost { get; set; } = 2;
    public int NewSkillGroupKarmaCost { get; set; } = 10;
    public int KarmaNuyenPer { get; set; } = 2500;
    public string BuildMethod { get; set; } = "BP";
    public int BuildPoints { get; set; } = 400;
    public int Availability { get; set; } = 12;
    public IList<string> Books { get; set; } = new List<string>();
}