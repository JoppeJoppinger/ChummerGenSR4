using System.Globalization;
using System.Xml.Linq;

namespace ChummerGen.Domain.Character.Qualities;

public sealed class ImprovementModel
{
    private readonly XElement _element;

    public ImprovementModel(XElement element)
    {
        _element = element ?? throw new ArgumentNullException(nameof(element));
    }

    public XElement UnderlyingElement => _element;

    public string? UniqueName
    {
        get => _element.Element("unique")?.Value;
        set => SetElementValue("unique", value);
    }

    public string? ImprovedName
    {
        get => _element.Element("improvedname")?.Value;
        set => SetElementValue("improvedname", value);
    }

    public string SourceName
    {
        get => _element.Element("sourcename")?.Value ?? string.Empty;
        set => SetElementValue("sourcename", value);
    }

    public int Min
    {
        get => int.TryParse(_element.Element("min")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        set => SetElementValue("min", value.ToString(CultureInfo.InvariantCulture));
    }

    public int Max
    {
        get => int.TryParse(_element.Element("max")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        set => SetElementValue("max", value.ToString(CultureInfo.InvariantCulture));
    }

    public int Aug
    {
        get => int.TryParse(_element.Element("aug")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        set => SetElementValue("aug", value.ToString(CultureInfo.InvariantCulture));
    }

    public int AugMax
    {
        get => int.TryParse(_element.Element("augmax")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        set => SetElementValue("augmax", value.ToString(CultureInfo.InvariantCulture));
    }

    public int Val
    {
        get => int.TryParse(_element.Element("val")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        set => SetElementValue("val", value.ToString(CultureInfo.InvariantCulture));
    }

    public int Rating
    {
        get => int.TryParse(_element.Element("rating")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        set => SetElementValue("rating", value.ToString(CultureInfo.InvariantCulture));
    }

    public string? Exclude
    {
        get => _element.Element("exclude")?.Value;
        set => SetElementValue("exclude", value);
    }

    public string ImprovementType
    {
        get => _element.Element("improvementttype")?.Value ?? string.Empty;
        set => SetElementValue("improvementttype", value);
    }

    public string ImprovementSource
    {
        get => _element.Element("improvementsource")?.Value ?? string.Empty;
        set => SetElementValue("improvementsource", value);
    }

    public bool Custom
    {
        get => string.Equals(_element.Element("custom")?.Value, "True", StringComparison.OrdinalIgnoreCase);
        set => SetElementValue("custom", value ? "True" : "False");
    }

    public string? CustomName
    {
        get => _element.Element("customname")?.Value;
        set => SetElementValue("customname", value);
    }

    public string? CustomId
    {
        get => _element.Element("customid")?.Value;
        set => SetElementValue("customid", value);
    }

    public string? CustomGroup
    {
        get => _element.Element("customgroup")?.Value;
        set => SetElementValue("customgroup", value);
    }

    public bool AddToRating
    {
        get => string.Equals(_element.Element("addtorating")?.Value, "True", StringComparison.OrdinalIgnoreCase);
        set => SetElementValue("addtorating", value ? "True" : "False");
    }

    public bool Enabled
    {
        get => !string.Equals(_element.Element("enabled")?.Value, "False", StringComparison.OrdinalIgnoreCase);
        set => SetElementValue("enabled", value ? "True" : "False");
    }

    public int Order
    {
        get => int.TryParse(_element.Element("order")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        set => SetElementValue("order", value.ToString(CultureInfo.InvariantCulture));
    }

    public string? Notes
    {
        get => _element.Element("notes")?.Value;
        set => SetElementValue("notes", value);
    }

    public bool IsQualitySource => string.Equals(ImprovementSource, "Quality", StringComparison.OrdinalIgnoreCase);

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
