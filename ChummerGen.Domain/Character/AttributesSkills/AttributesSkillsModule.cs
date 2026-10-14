using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChummerGen.Domain.Abstractions;
using ChummerGen.Domain.Character;

namespace ChummerGen.Domain.Character.AttributesSkills;

public sealed class AttributesSkillsModule : ICharacterModule
{
    public const string ModuleName = "AttributesSkills";
    public string Name => ModuleName;

    public Task RecalculateAsync(
        CharacterSession session,
        IRuleCatalog ruleCatalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var manager = new AttributesSkillsManager(session);
        manager.UpdateAllSkillTotals();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CharacterValidationIssue>> ValidateAsync(
        CharacterSession session,
        IRuleCatalog ruleCatalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var issues = new List<CharacterValidationIssue>();
        var manager = new AttributesSkillsManager(session);

        // Attribute validation
        var attrSummary = manager.CalculateAttributesSummary();
        if (attrSummary.ExceedsPrimaryBpLimit)
        {
            issues.Add(new CharacterValidationIssue(
                Module: Name,
                Code: "ATTR_PRIMARY_BP_EXCEEDED",
                Message: $"GP-Kosten für primäre Attribute ({attrSummary.PrimaryAttributeBp} GP) übersteigen das erlaubte Maximum ({attrSummary.MaxPrimaryBpAllowed} GP).",
                Severity: CharacterIssueSeverity.Error
            ));
        }

        if (attrSummary.NaturalMaxCount > 1)
        {
            issues.Add(new CharacterValidationIssue(
                Module: Name,
                Code: "ATTR_NATURAL_MAX_EXCEEDED",
                Message: $"Bei der Charaktererschaffung darf nur ein primäres Attribut auf dem natürlichen Maximum liegen (aktuell: {attrSummary.NaturalMaxCount}).",
                Severity: CharacterIssueSeverity.Warning
            ));
        }

        foreach (var attr in manager.GetAttributes())
        {
            if (attr.BaseValue < attr.MetatypeMin)
            {
                issues.Add(new CharacterValidationIssue(
                    Module: Name,
                    Code: "ATTR_BELOW_MIN",
                    Message: $"Attribut {attr.Name} liegt unter dem Metatyp-Minimum ({attr.BaseValue} < {attr.MetatypeMin}).",
                    Severity: CharacterIssueSeverity.Error
                ));
            }
            if (attr.MetatypeMax > 0 && attr.BaseValue > attr.MetatypeMax)
            {
                issues.Add(new CharacterValidationIssue(
                    Module: Name,
                    Code: "ATTR_ABOVE_MAX",
                    Message: $"Attribut {attr.Name} liegt über dem Metatyp-Maximum ({attr.BaseValue} > {attr.MetatypeMax}).",
                    Severity: CharacterIssueSeverity.Error
                ));
            }
        }

        // Skills validation
        int ratingSixCount = 0;
        foreach (var skill in manager.GetSkills(activeOnly: true))
        {
            if (skill.Rating > skill.RatingMax)
            {
                issues.Add(new CharacterValidationIssue(
                    Module: Name,
                    Code: "SKILL_RATING_EXCEEDED",
                    Message: $"Fertigkeit '{skill.Name}' überschreitet das Maximum ({skill.Rating} > {skill.RatingMax}).",
                    Severity: CharacterIssueSeverity.Error
                ));
            }

            if (skill.Rating == 6)
            {
                ratingSixCount++;
            }
        }

        if (ratingSixCount > 1)
        {
            issues.Add(new CharacterValidationIssue(
                Module: Name,
                Code: "SKILL_MULTIPLE_MAX",
                Message: $"Bei der Charaktererschaffung darf höchstens eine Fertigkeit auf Stufe 6 sein (oder zwei auf Stufe 5). Aktuell: {ratingSixCount} auf Stufe 6.",
                Severity: CharacterIssueSeverity.Warning
            ));
        }

        return Task.FromResult<IReadOnlyList<CharacterValidationIssue>>(issues);
    }
}
