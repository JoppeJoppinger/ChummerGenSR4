using System.Globalization;
using System.Xml.Linq;

namespace ChummerGen.Domain.Character.Critters;

public sealed class CritterPower
{
    public CritterPower(XElement element)
    {
        Element = element ?? throw new ArgumentNullException(nameof(element));
    }

    public XElement Element { get; }

    public string Guid
    {
        get => GetText("guid") ?? string.Empty;
        set => SetText("guid", value);
    }

    public string Name
    {
        get => GetText("name") ?? string.Empty;
        set => SetText("name", value);
    }

    public string Extra
    {
        get => GetText("extra") ?? string.Empty;
        set => SetText("extra", value);
    }

    public string Category
    {
        get => GetText("category") ?? string.Empty;
        set => SetText("category", value);
    }

    public string Type
    {
        get => GetText("type") ?? string.Empty;
        set => SetText("type", value);
    }

    public string Action
    {
        get => GetText("action") ?? string.Empty;
        set => SetText("action", value);
    }

    public string Range
    {
        get => GetText("range") ?? string.Empty;
        set => SetText("range", value);
    }

    public string Duration
    {
        get => GetText("duration") ?? string.Empty;
        set => SetText("duration", value);
    }

    public string Source
    {
        get => GetText("source") ?? string.Empty;
        set => SetText("source", value);
    }

    public string Page
    {
        get => GetText("page") ?? string.Empty;
        set => SetText("page", value);
    }

    public double? Points
    {
        get
        {
            var text = GetText("points");
            if (string.IsNullOrWhiteSpace(text))
                return null;
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var val)
                ? val
                : null;
        }
        set
        {
            if (value is null)
                SetText("points", null);
            else
                SetText("points", value.Value.ToString(CultureInfo.InvariantCulture));
        }
    }

    public bool CountTowardsLimit
    {
        get
        {
            var text = GetText("counttowardslimit");
            if (string.IsNullOrWhiteSpace(text))
                return true;
            return !bool.TryParse(text, out var result) || result;
        }
        set => SetText("counttowardslimit", value ? "True" : "False");
    }

    public string Notes
    {
        get => GetText("notes") ?? string.Empty;
        set => SetText("notes", value);
    }

    public bool IsWeakness => string.Equals(Category, "Weakness", StringComparison.OrdinalIgnoreCase);

    public string DisplayName => string.IsNullOrWhiteSpace(Extra) ? Name : $"{Name} ({Extra})";

    public static CritterPower Create(
        string name,
        string category = "Mundane",
        string type = "P",
        string action = "Auto",
        string range = "Self",
        string duration = "Always",
        string source = "RW",
        string page = "",
        string extra = "",
        double points = 0,
        bool countTowardsLimit = true,
        string notes = "",
        string? guid = null)
    {
        var element = new XElement("critterpower",
            new XElement("guid", guid ?? System.Guid.NewGuid().ToString()),
            new XElement("name", name),
            new XElement("extra", extra),
            new XElement("category", category),
            new XElement("type", type),
            new XElement("action", action),
            new XElement("range", range),
            new XElement("duration", duration),
            new XElement("source", source),
            new XElement("page", page),
            new XElement("points", points.ToString(CultureInfo.InvariantCulture)),
            new XElement("counttowardslimit", countTowardsLimit ? "True" : "False"),
            new XElement("bonus"),
            new XElement("notes", notes));

        return new CritterPower(element);
    }

    private string? GetText(string elementName) => Element.Element(elementName)?.Value;

    private void SetText(string elementName, string? value)
    {
        var el = Element.Element(elementName);
        if (value is null)
        {
            el?.Remove();
            return;
        }
        if (el is null)
            Element.Add(new XElement(elementName, value));
        else
            el.Value = value;
    }
}
