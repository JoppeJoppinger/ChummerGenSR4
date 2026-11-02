using System.Xml.Linq;

namespace ChummerGen.Domain.Character.Qualities;

public sealed record QualitiesSummary(
    int PositiveBpTotal,
    int NegativeBpTotal,
    int NetBpTotal,
    int PositiveKarmaTotal,
    int NegativeKarmaTotal,
    int NetKarmaTotal,
    int CountPositive,
    int CountNegative,
    int CountMetatype);

public sealed class QualitiesManager
{
    private readonly CharacterSession _session;

    public QualitiesManager(CharacterSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public IReadOnlyList<QualityModel> GetAllQualities()
    {
        var root = _session.Xml.Root;
        if (root is null)
            return [];

        var qualitiesElem = root.Element("qualities");
        if (qualitiesElem is null)
            return [];

        return qualitiesElem.Elements("quality")
            .Select(e => new QualityModel(e))
            .ToList();
    }

    public IReadOnlyList<QualityModel> GetPositiveQualities() =>
        GetAllQualities().Where(q => q.Category == QualityCategory.Positive).ToList();

    public IReadOnlyList<QualityModel> GetNegativeQualities() =>
        GetAllQualities().Where(q => q.Category == QualityCategory.Negative).ToList();

    public IReadOnlyList<ImprovementModel> GetAllImprovements()
    {
        var root = _session.Xml.Root;
        if (root is null)
            return [];

        var impsElem = root.Element("improvements");
        if (impsElem is null)
            return [];

        return impsElem.Elements("improvement")
            .Select(e => new ImprovementModel(e))
            .ToList();
    }

    public IReadOnlyList<ImprovementModel> GetQualityImprovements()
    {
        return GetAllImprovements().Where(i => i.IsQualitySource).ToList();
    }

    public IReadOnlyList<ImprovementModel> GetImprovementsForQuality(string qualityGuid)
    {
        if (string.IsNullOrWhiteSpace(qualityGuid))
            return [];
        return GetAllImprovements()
            .Where(i => string.Equals(i.SourceName, qualityGuid, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public QualitiesSummary CalculateSummary()
    {
        var all = GetAllQualities();
        var karmaMult = _session.Settings.QualityKarmaCost > 0 ? _session.Settings.QualityKarmaCost : 2;

        int posBp = 0;
        int negBp = 0;
        int countPos = 0;
        int countNeg = 0;
        int countMeta = 0;

        foreach (var q in all)
        {
            if (q.IsFromMetatype)
                countMeta++;

            if (q.Category == QualityCategory.Positive)
            {
                countPos++;
                if (q.ContributesToBp)
                    posBp += q.Bp;
            }
            else
            {
                countNeg++;
                if (q.ContributesToBp)
                    negBp += q.Bp;
            }
        }

        int netBp = posBp + negBp;
        int posKarma = posBp * karmaMult;
        int negKarma = negBp * karmaMult;
        int netKarma = posKarma + negKarma;

        return new QualitiesSummary(
            PositiveBpTotal: posBp,
            NegativeBpTotal: negBp,
            NetBpTotal: netBp,
            PositiveKarmaTotal: posKarma,
            NegativeKarmaTotal: negKarma,
            NetKarmaTotal: netKarma,
            CountPositive: countPos,
            CountNegative: countNeg,
            CountMetatype: countMeta);
    }

    public bool UpdateNotes(string guid, string? newNotes)
    {
        if (string.IsNullOrWhiteSpace(guid))
            return false;

        var qual = GetAllQualities().FirstOrDefault(q => string.Equals(q.Guid, guid, StringComparison.OrdinalIgnoreCase));
        if (qual is null)
            return false;

        qual.Notes = newNotes;
        return true;
    }

    public bool UpdatePrint(string guid, bool print)
    {
        if (string.IsNullOrWhiteSpace(guid))
            return false;

        var qual = GetAllQualities().FirstOrDefault(q => string.Equals(q.Guid, guid, StringComparison.OrdinalIgnoreCase));
        if (qual is null)
            return false;

        qual.Print = print;
        return true;
    }

    public bool DeleteQuality(string guid)
    {
        if (string.IsNullOrWhiteSpace(guid))
            return false;

        var root = _session.Xml.Root;
        if (root is null)
            return false;

        var qualitiesElem = root.Element("qualities");
        var qualElem = qualitiesElem?.Elements("quality")
            .FirstOrDefault(e => string.Equals(e.Element("guid")?.Value, guid, StringComparison.OrdinalIgnoreCase));

        if (qualElem is null)
            return false;

        var model = new QualityModel(qualElem);
        if (model.IsFromMetatype)
            return false;

        // Remove linked improvements
        var impsElem = root.Element("improvements");
        if (impsElem is not null)
        {
            var matchingImps = impsElem.Elements("improvement")
                .Where(e => string.Equals(e.Element("sourcename")?.Value, guid, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var imp in matchingImps)
                imp.Remove();
        }

        qualElem.Remove();
        Cyberware.CyberwareManager.RefreshExternalFlags(_session);
        return true;
    }
}
