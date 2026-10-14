using System.Globalization;
using System.Xml.Linq;

namespace ChummerGen.Domain.Character.Qualities;

public enum QualityCategory
{
    Positive,
    Negative
}

public enum QualitySourceType
{
    Selected,
    Metatype,
    MetatypeRemovable
}

public sealed class QualityModel
{
    private readonly XElement _element;

    public QualityModel(XElement element)
    {
        _element = element ?? throw new ArgumentNullException(nameof(element));
    }

    public XElement UnderlyingElement => _element;

    public string Guid
    {
        get => _element.Element("guid")?.Value ?? string.Empty;
        set => SetElementValue("guid", value);
    }

    public string Name
    {
        get => _element.Element("name")?.Value ?? string.Empty;
        set => SetElementValue("name", value);
    }

    public string? Extra
    {
        get => _element.Element("extra")?.Value;
        set => SetElementValue("extra", value);
    }

    public int Bp
    {
        get => int.TryParse(_element.Element("bp")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        set => SetElementValue("bp", value.ToString(CultureInfo.InvariantCulture));
    }

    public bool ContributeToLimit
    {
        get => !string.Equals(_element.Element("contributetolimit")?.Value, "False", StringComparison.OrdinalIgnoreCase);
        set => SetElementValue("contributetolimit", value ? "True" : "False");
    }

    public bool Print
    {
        get => !string.Equals(_element.Element("print")?.Value, "False", StringComparison.OrdinalIgnoreCase);
        set => SetElementValue("print", value ? "True" : "False");
    }

    public QualityCategory Category
    {
        get => string.Equals(_element.Element("qualitytype")?.Value, "Negative", StringComparison.OrdinalIgnoreCase)
            ? QualityCategory.Negative
            : QualityCategory.Positive;
        set => SetElementValue("qualitytype", value == QualityCategory.Negative ? "Negative" : "Positive");
    }

    public QualitySourceType SourceType
    {
        get => (_element.Element("qualitysource")?.Value) switch
        {
            "Metatype" => QualitySourceType.Metatype,
            "MetatypeRemovable" => QualitySourceType.MetatypeRemovable,
            _ => QualitySourceType.Selected
        };
        set => SetElementValue("qualitysource", value switch
        {
            QualitySourceType.Metatype => "Metatype",
            QualitySourceType.MetatypeRemovable => "MetatypeRemovable",
            _ => "Selected"
        });
    }

    public string? Mutant
    {
        get => _element.Element("mutant")?.Value;
        set => SetElementValue("mutant", value);
    }

    public string Source
    {
        get => _element.Element("source")?.Value ?? string.Empty;
        set => SetElementValue("source", value);
    }

    public string Page
    {
        get => _element.Element("page")?.Value ?? string.Empty;
        set => SetElementValue("page", value);
    }

    public string? Notes
    {
        get => _element.Element("notes")?.Value;
        set => SetElementValue("notes", value);
    }

    public string? WeaponGuid
    {
        get => _element.Element("weaponguid")?.Value;
        set => SetElementValue("weaponguid", value);
    }

    public bool IsFromMetatype => SourceType == QualitySourceType.Metatype || SourceType == QualitySourceType.MetatypeRemovable;

    public bool ContributesToBp => !IsFromMetatype;

    public bool ContributesToLimitCalculated => ContributeToLimit && !IsFromMetatype;

    public string DisplayName => string.IsNullOrWhiteSpace(Extra) ? Name : $"{Name} ({Extra})";

    public string SourceDisplay => string.IsNullOrWhiteSpace(Page) ? Source : $"{Source} {Page}".Trim();

    private void SetElementValue(string localName, string? value)
    {
        var child = _element.Element(localName);
        if (child is null)
        {
            if (value is not null)
                _element.Add(new XElement(localName, value));
        }
        else
        {
            child.Value = value ?? string.Empty;
        }
    }
}
