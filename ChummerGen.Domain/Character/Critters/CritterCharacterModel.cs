using System.Globalization;
using System.Xml.Linq;
using ChummerGen.Domain.Chum;

namespace ChummerGen.Domain.Character.Critters;

public sealed class CritterCharacterModel
{
    private readonly XDocument _xml;

    public CritterCharacterModel(CharacterSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _xml = session.Xml;
    }

    public CritterCharacterModel(ChumDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _xml = document.Xml;
    }

    public CritterCharacterModel(XDocument xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        _xml = xml;
    }

    public XElement Root => _xml.Root ?? throw new InvalidOperationException("Dokument hat kein Root-Element.");

    public bool IsCritter
    {
        get
        {
            var text = Root.Element("iscritter")?.Value;
            return bool.TryParse(text, out var result) && result;
        }
        set
        {
            var el = Root.Element("iscritter");
            if (value)
            {
                if (el is null)
                    Root.Add(new XElement("iscritter", "True"));
                else
                    el.Value = "True";
            }
            else
            {
                el?.Remove();
            }
        }
    }

    public bool CritterEnabled
    {
        get
        {
            var text = Root.Element("critter")?.Value;
            return bool.TryParse(text, out var result) && result;
        }
        set
        {
            var el = Root.Element("critter");
            if (el is null)
                Root.Add(new XElement("critter", value ? "True" : "False"));
            else
                el.Value = value ? "True" : "False";
        }
    }

    public int MutantCritterBaseSkills
    {
        get
        {
            var text = Root.Element("mutantcritterbaseskills")?.Value;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
        }
        set
        {
            var el = Root.Element("mutantcritterbaseskills");
            if (el is null)
                Root.Add(new XElement("mutantcritterbaseskills", value.ToString(CultureInfo.InvariantCulture)));
            else
                el.Value = value.ToString(CultureInfo.InvariantCulture);
        }
    }

    public IReadOnlyList<CritterPower> Powers =>
        Root.Element("critterpowers")?.Elements("critterpower").Select(el => new CritterPower(el)).ToList()
        ?? (IReadOnlyList<CritterPower>)[];

    public IReadOnlyList<CritterPower> ActivePowers =>
        Powers.Where(p => !p.IsWeakness).ToList();

    public IReadOnlyList<CritterPower> Weaknesses =>
        Powers.Where(p => p.IsWeakness).ToList();

    public int CountTowardsLimitTotal =>
        Powers.Count(p => p.CountTowardsLimit);

    public double TotalPowerPoints =>
        Powers.Sum(p => p.Points ?? 0.0);

    public bool HasCritterData =>
        IsCritter || CritterEnabled || Powers.Count > 0;

    public CritterPower? FindPower(string? guid)
    {
        if (string.IsNullOrWhiteSpace(guid))
            return null;

        var el = Root.Element("critterpowers")?.Elements("critterpower")
            .FirstOrDefault(p => string.Equals((string?)p.Element("guid"), guid, StringComparison.OrdinalIgnoreCase));

        return el is not null ? new CritterPower(el) : null;
    }

    public void AddPower(CritterPower power)
    {
        ArgumentNullException.ThrowIfNull(power);

        var powersContainer = Root.Element("critterpowers");
        if (powersContainer is null)
        {
            powersContainer = new XElement("critterpowers");
            Root.Add(powersContainer);
        }

        powersContainer.Add(power.Element);

        if (!CritterEnabled)
            CritterEnabled = true;
    }

    public bool RemovePower(string? guid)
    {
        if (string.IsNullOrWhiteSpace(guid))
            return false;

        var el = Root.Element("critterpowers")?.Elements("critterpower")
            .FirstOrDefault(p => string.Equals((string?)p.Element("guid"), guid, StringComparison.OrdinalIgnoreCase));

        if (el is null)
            return false;

        el.Remove();
        return true;
    }

    public void EnsureCritterContainer()
    {
        if (Root.Element("critterpowers") is null)
            Root.Add(new XElement("critterpowers"));
    }
}
