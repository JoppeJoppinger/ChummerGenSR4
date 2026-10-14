using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using ChummerGen.Domain.Character;

namespace ChummerGen.Domain.Character.AttributesSkills;

public sealed class AttributesSkillsManager
{
    private static readonly string[] PrimaryAttributeNames = ["BOD", "AGI", "REA", "STR", "CHA", "INT", "LOG", "WIL"];
    private static readonly string[] SpecialAttributeNames = ["EDG", "MAG", "RES"];

    private readonly CharacterSession _session;

    public AttributesSkillsManager(CharacterSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    private XElement Root => _session.Xml.Root ?? throw new InvalidOperationException("Root element missing");

    #region Attributes

    public IReadOnlyList<AttributeModel> GetAttributes()
    {
        var attributesElem = Root.Element("attributes");
        if (attributesElem is null)
            return Array.Empty<AttributeModel>();

        var result = new List<AttributeModel>();
        foreach (var attr in attributesElem.Elements("attribute"))
        {
            var name = attr.Element("name")?.Value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(name))
                continue;

            int min = (int?)attr.Element("metatypemin") ?? 1;
            int max = (int?)attr.Element("metatypemax") ?? 6;
            int augMax = (int?)attr.Element("metatypeaugmax") ?? (int)Math.Floor(max * 1.5);

            int val = (int?)attr.Element("value") ?? (int?)attr.Element("base") ?? min;
            int augMod = (int?)attr.Element("augmodifier") ?? 0;
            int total = (int?)attr.Element("totalvalue") ?? (val + augMod);

            result.Add(new AttributeModel(name, min, max, augMax, val, augMod, total));
        }

        return result;
    }

    public AttributeModel? GetAttribute(string name)
    {
        return GetAttributes().FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public void UpdateAttributeValue(string name, int newValue)
    {
        var attributesElem = Root.Element("attributes");
        if (attributesElem is null)
        {
            attributesElem = new XElement("attributes");
            Root.Add(attributesElem);
        }

        var attrElem = attributesElem.Elements("attribute")
            .FirstOrDefault(a => string.Equals(a.Element("name")?.Value?.Trim(), name, StringComparison.OrdinalIgnoreCase));

        if (attrElem is null)
        {
            attrElem = new XElement("attribute",
                new XElement("name", name),
                new XElement("metatypemin", 1),
                new XElement("metatypemax", 6),
                new XElement("metatypeaugmax", 9),
                new XElement("value", newValue),
                new XElement("base", newValue),
                new XElement("augmodifier", 0),
                new XElement("totalvalue", newValue)
            );
            attributesElem.Add(attrElem);
        }
        else
        {
            attrElem.SetElementValue("value", newValue);
            attrElem.SetElementValue("base", newValue);

            int augMod = (int?)attrElem.Element("augmodifier") ?? 0;
            attrElem.SetElementValue("totalvalue", newValue + augMod);
        }

        // Also sync through model if applicable
        _session.Model.SetAttributeValue(name, newValue.ToString(), "base");
        _session.Model.SetAttributeValue(name, newValue.ToString(), "value");

        UpdateAllSkillTotals();
    }

    public DerivedAttributes GetDerivedAttributes()
    {
        int wil = GetAttributeTotal("WIL");
        int cha = GetAttributeTotal("CHA");
        int intl = GetAttributeTotal("INT");
        int str = GetAttributeTotal("STR");
        int bod = GetAttributeTotal("BOD");
        int log = GetAttributeTotal("LOG");
        int rea = GetAttributeTotal("REA");

        int composure = wil + cha;
        int judgeIntentions = intl + cha;
        int liftAndCarry = str + bod;
        int memory = log + wil;
        int initBase = rea + intl;

        int initTotal = _session.Model.InitiativeTotal ?? initBase;
        if (initTotal <= 0)
            initTotal = initBase;

        int passes = _session.Model.InitiativePasses ?? 1;
        if (passes <= 0)
            passes = 1;

        return new DerivedAttributes(
            Composure: composure,
            JudgeIntentions: judgeIntentions,
            LiftAndCarry: liftAndCarry,
            Memory: memory,
            InitiativeBase: initBase,
            InitiativeTotal: initTotal,
            InitiativePasses: passes
        );
    }

    public AttributesSummary CalculateAttributesSummary()
    {
        var attrs = GetAttributes();
        int primaryBp = 0;
        int specialBp = 0;
        int naturalMaxCount = 0;

        foreach (var attr in attrs)
        {
            if (PrimaryAttributeNames.Contains(attr.Name, StringComparer.OrdinalIgnoreCase))
            {
                int diff = Math.Max(0, attr.BaseValue - attr.MetatypeMin);
                int cost = diff * 10;
                if (attr.IsNaturalMax && attr.MetatypeMax > 0)
                {
                    cost += 15;
                    naturalMaxCount++;
                }
                primaryBp += cost;
            }
            else if (SpecialAttributeNames.Contains(attr.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (string.Equals(attr.Name, "EDG", StringComparison.OrdinalIgnoreCase))
                {
                    int diff = Math.Max(0, attr.BaseValue - attr.MetatypeMin);
                    int cost = diff * 10;
                    if (attr.IsNaturalMax && attr.MetatypeMax > 0)
                    {
                        cost += 15;
                    }
                    specialBp += cost;
                }
                else if (string.Equals(attr.Name, "MAG", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(attr.Name, "RES", StringComparison.OrdinalIgnoreCase))
                {
                    int diff = Math.Max(0, attr.BaseValue - 1);
                    int cost = diff * 10;
                    if (attr.IsNaturalMax && attr.MetatypeMax > 0)
                    {
                        cost += 15;
                    }
                    specialBp += cost;
                }
            }
        }

        int maxBuildPoints = (int?)Root.Element("buildpoints") ?? _session.Model.BuildPoints ?? 400;
        int maxPrimaryAllowed = maxBuildPoints / 2;

        return new AttributesSummary(
            TotalAttributeBp: primaryBp + specialBp,
            PrimaryAttributeBp: primaryBp,
            SpecialAttributeBp: specialBp,
            MaxPrimaryBpAllowed: maxPrimaryAllowed,
            ExceedsPrimaryBpLimit: primaryBp > maxPrimaryAllowed,
            NaturalMaxCount: naturalMaxCount
        );
    }

    private int GetAttributeTotal(string name)
    {
        var attr = GetAttribute(name);
        if (attr is not null)
            return attr.TotalValue;
        var valStr = _session.Model.GetAttributeValue(name, "value") ?? _session.Model.GetAttributeValue(name, "base");
        if (int.TryParse(valStr, out int v))
            return v;
        return 0;
    }

    #endregion

    #region Skills

    public IReadOnlyList<SkillModel> GetSkills(bool activeOnly = true)
    {
        var skillsElem = Root.Element("skills");
        if (skillsElem is null)
            return Array.Empty<SkillModel>();

        var result = new List<SkillModel>();
        foreach (var el in skillsElem.Elements("skill"))
        {
            bool isKnowledge = (bool?)el.Element("knowledge") ?? false;
            if (activeOnly && isKnowledge)
                continue;

            var name = el.Element("name")?.Value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(name))
                continue;

            var group = el.Element("skillgroup")?.Value?.Trim() ?? string.Empty;
            var cat = el.Element("skillcategory")?.Value?.Trim() ?? string.Empty;
            bool grouped = (bool?)el.Element("grouped") ?? false;
            bool def = (bool?)el.Element("default") ?? true;
            int rating = (int?)el.Element("rating") ?? 0;
            int ratingMax = (int?)el.Element("ratingmax") ?? 6;
            bool exotic = (bool?)el.Element("exotic") ?? false;
            var spec = el.Element("spec")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(spec))
                spec = null;
            bool allowDelete = (bool?)el.Element("allowdelete") ?? false;
            var attribute = el.Element("attribute")?.Value?.Trim() ?? string.Empty;
            int total = (int?)el.Element("totalvalue") ?? CalculateSkillTotal(rating, attribute, def);

            result.Add(new SkillModel(
                Name: name,
                SkillGroup: group,
                SkillCategory: cat,
                Grouped: grouped,
                Default: def,
                Rating: rating,
                RatingMax: ratingMax,
                Knowledge: isKnowledge,
                Exotic: exotic,
                Specialization: spec,
                AllowDelete: allowDelete,
                Attribute: attribute,
                TotalValue: total
            ));
        }

        return result;
    }

    public IReadOnlyList<SkillGroupModel> GetSkillGroups()
    {
        var groupsElem = Root.Element("skillgroups");
        if (groupsElem is null)
            return Array.Empty<SkillGroupModel>();

        var result = new List<SkillGroupModel>();
        foreach (var el in groupsElem.Elements("skillgroup"))
        {
            var name = el.Element("name")?.Value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(name))
                continue;

            int rating = (int?)el.Element("rating") ?? 0;
            int ratingMax = (int?)el.Element("ratingmax") ?? 6;
            bool broken = (bool?)el.Element("broken") ?? false;

            result.Add(new SkillGroupModel(name, rating, ratingMax, broken));
        }

        return result;
    }

    public int CalculateSkillTotal(int rating, string attributeName, bool canDefault)
    {
        int attrVal = GetAttributeTotal(attributeName);
        if (rating == 0)
        {
            if (canDefault)
                return Math.Max(0, attrVal - 1);
            return 0;
        }
        return rating + attrVal;
    }

    public void UpdateSkillRating(string skillName, int newRating)
    {
        var skillsElem = Root.Element("skills");
        if (skillsElem is null)
            return;

        var el = skillsElem.Elements("skill")
            .FirstOrDefault(s => string.Equals(s.Element("name")?.Value?.Trim(), skillName, StringComparison.OrdinalIgnoreCase));

        if (el is null)
            return;

        int max = (int?)el.Element("ratingmax") ?? 6;
        newRating = Math.Clamp(newRating, 0, max);

        el.SetElementValue("rating", newRating);

        bool def = (bool?)el.Element("default") ?? true;
        string attr = el.Element("attribute")?.Value?.Trim() ?? string.Empty;
        int total = CalculateSkillTotal(newRating, attr, def);
        el.SetElementValue("totalvalue", total);
    }

    public void UpdateSkillSpecialization(string skillName, string? specialization)
    {
        var skillsElem = Root.Element("skills");
        if (skillsElem is null)
            return;

        var el = skillsElem.Elements("skill")
            .FirstOrDefault(s => string.Equals(s.Element("name")?.Value?.Trim(), skillName, StringComparison.OrdinalIgnoreCase));

        if (el is null)
            return;

        if (string.IsNullOrWhiteSpace(specialization))
        {
            el.SetElementValue("spec", string.Empty);
        }
        else
        {
            el.SetElementValue("spec", specialization.Trim());
        }
    }

    public void UpdateSkillGroupRating(string groupName, int newRating)
    {
        var groupsElem = Root.Element("skillgroups");
        if (groupsElem is null)
            return;

        var gEl = groupsElem.Elements("skillgroup")
            .FirstOrDefault(g => string.Equals(g.Element("name")?.Value?.Trim(), groupName, StringComparison.OrdinalIgnoreCase));

        if (gEl is null)
            return;

        int max = (int?)gEl.Element("ratingmax") ?? 6;
        newRating = Math.Clamp(newRating, 0, max);
        gEl.SetElementValue("rating", newRating);

        // Update grouped skills under this skill group
        var skillsElem = Root.Element("skills");
        if (skillsElem is not null)
        {
            foreach (var sEl in skillsElem.Elements("skill"))
            {
                var sGroup = sEl.Element("skillgroup")?.Value?.Trim();
                bool grouped = (bool?)sEl.Element("grouped") ?? false;
                if (grouped && string.Equals(sGroup, groupName, StringComparison.OrdinalIgnoreCase))
                {
                    sEl.SetElementValue("rating", newRating);
                    bool def = (bool?)sEl.Element("default") ?? true;
                    string attr = sEl.Element("attribute")?.Value?.Trim() ?? string.Empty;
                    int total = CalculateSkillTotal(newRating, attr, def);
                    sEl.SetElementValue("totalvalue", total);
                }
            }
        }
    }

    public void BreakSkillGroup(string groupName)
    {
        var groupsElem = Root.Element("skillgroups");
        var gEl = groupsElem?.Elements("skillgroup")
            .FirstOrDefault(g => string.Equals(g.Element("name")?.Value?.Trim(), groupName, StringComparison.OrdinalIgnoreCase));

        if (gEl is not null)
        {
            gEl.SetElementValue("broken", true);
        }

        var skillsElem = Root.Element("skills");
        if (skillsElem is not null)
        {
            foreach (var sEl in skillsElem.Elements("skill"))
            {
                var sGroup = sEl.Element("skillgroup")?.Value?.Trim();
                if (string.Equals(sGroup, groupName, StringComparison.OrdinalIgnoreCase))
                {
                    sEl.SetElementValue("grouped", false);
                }
            }
        }
    }

    public void UpdateAllSkillTotals()
    {
        var skillsElem = Root.Element("skills");
        if (skillsElem is null)
            return;

        foreach (var sEl in skillsElem.Elements("skill"))
        {
            int rating = (int?)sEl.Element("rating") ?? 0;
            bool def = (bool?)sEl.Element("default") ?? true;
            string attr = sEl.Element("attribute")?.Value?.Trim() ?? string.Empty;
            int total = CalculateSkillTotal(rating, attr, def);
            sEl.SetElementValue("totalvalue", total);
        }
    }

    public SkillsSummary CalculateSkillsSummary()
    {
        var skills = GetSkills(activeOnly: true);
        var groups = GetSkillGroups();

        int groupBp = 0;
        foreach (var g in groups)
        {
            if (!g.Broken && g.Rating > 0)
            {
                groupBp += g.Rating * 10;
            }
        }

        int activeSkillsBp = 0;
        int specBp = 0;

        foreach (var s in skills)
        {
            if (!s.Grouped && s.Rating > 0)
            {
                activeSkillsBp += s.Rating * 4;
            }
            if (!string.IsNullOrWhiteSpace(s.Specialization))
            {
                specBp += 2;
            }
        }

        return new SkillsSummary(
            ActiveSkillsBp: activeSkillsBp,
            SkillGroupsBp: groupBp,
            SpecializationsBp: specBp,
            TotalBp: activeSkillsBp + groupBp + specBp,
            TotalActiveSkillsCount: skills.Count,
            TotalSkillGroupsCount: groups.Count
        );
    }

    #endregion
}
