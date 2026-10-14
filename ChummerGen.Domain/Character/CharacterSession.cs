using System.Globalization;
using System.Xml.Linq;
using ChummerGen.Domain.Chum;

namespace ChummerGen.Domain.Character;

public sealed class CharacterSession
{
    public CharacterSession(ChumDocument document, CharacterSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        Document = document;
        Model = new CharacterModel(document);
        Settings = settings ?? new CharacterSettings();
    }

    public ChumDocument Document { get; }
    public XDocument Xml => Document.Xml;
    public CharacterModel Model { get; }
    public CharacterSettings Settings { get; }

    public string Serialize() => ChumSerializer.Serialize(Document);
}

public sealed class CharacterModel
{
    private readonly ChumDocument _document;

    internal CharacterModel(ChumDocument document) => _document = document;

    public string? Version { get => _document.Version; set => _document.Version = value; }
    public string? Name { get => _document.Name; set => _document.Name = value; }
    public string? Alias { get => _document.Alias; set => _document.Alias = value; }
    public string? Metatype { get => _document.Metatype; set => _document.Metatype = value; }
    public int? Karma { get => _document.Karma; set => _document.Karma = value; }
    public int? Nuyen { get => _document.Nuyen; set => _document.Nuyen = value; }
    public int? BuildPoints { get => _document.BuildPoints; set => _document.BuildPoints = value; }
    public int? TotalKarma { get => GetInteger("totalkarma"); set => SetInteger("totalkarma", value); }
    public int? BuildKarma { get => GetInteger("buildkarma"); set => SetInteger("buildkarma", value); }
    public int? StreetCred { get => GetInteger("streetcred"); set => SetInteger("streetcred", value); }
    public int? Notoriety { get => GetInteger("notoriety"); set => SetInteger("notoriety", value); }
    public int? PublicAwareness { get => GetInteger("publicawareness"); set => SetInteger("publicawareness", value); }
    public int? InitiativeBase { get => GetNestedInteger("init", "base"); set => SetNestedInteger("init", "base", value); }
    public int? InitiativeTotal { get => GetNestedInteger("init", "total"); set => SetNestedInteger("init", "total", value); }
    public int? InitiativePasses { get => GetNestedInteger("ip", "total"); set => SetNestedInteger("ip", "total", value); }
    public int? Composure { get => GetInteger("composure"); set => SetInteger("composure", value); }
    public int? JudgeIntentions { get => GetInteger("judgeintentions"); set => SetInteger("judgeintentions", value); }
    public int? LiftAndCarry { get => GetInteger("liftandcarry"); set => SetInteger("liftandcarry", value); }
    public int? Memory { get => GetInteger("memory"); set => SetInteger("memory", value); }
    public string? Notes { get => GetValue("notes"); set => SetValue("notes", value); }

    public string? GetValue(string elementName) => Root.Element(XName.Get(elementName))?.Value;

    public void SetValue(string elementName, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementName);
        var element = Root.Element(XName.Get(elementName));
        if (value is null)
        {
            element?.Remove();
            return;
        }

        if (element is null)
            Root.Add(new XElement(elementName, value));
        else
            element.Value = value;
    }

    public string? GetAttributeValue(string attributeName, string valueElement = "base") =>
        FindAttribute(attributeName)?.Element(XName.Get(valueElement))?.Value;

    public void SetAttributeValue(string attributeName, string? value, string valueElement = "base")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueElement);
        var attributes = Root.Element("attributes");
        var attribute = FindAttribute(attributeName);
        if (value is null)
        {
            attribute?.Element(XName.Get(valueElement))?.Remove();
            return;
        }

        if (attributes is null)
        {
            attributes = new XElement("attributes");
            Root.Add(attributes);
        }
        if (attribute is null)
        {
            attribute = new XElement("attribute", new XElement("name", attributeName));
            attributes.Add(attribute);
        }

        var valueNode = attribute.Element(XName.Get(valueElement));
        if (valueNode is null)
            attribute.Add(new XElement(valueElement, value));
        else
            valueNode.Value = value;
    }

    public IReadOnlyList<XElement> GetItems(string sectionName, string itemName) =>
        Root.Element(XName.Get(sectionName))?.Elements(XName.Get(itemName)).ToArray() ?? [];

    public void AddItem(string sectionName, XElement item)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);
        ArgumentNullException.ThrowIfNull(item);
        var section = Root.Element(XName.Get(sectionName));
        if (section is null)
        {
            section = new XElement(sectionName);
            Root.Add(section);
        }
        section.Add(new XElement(item));
    }

    public bool RemoveItem(string sectionName, XElement item)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);
        ArgumentNullException.ThrowIfNull(item);
        if (item.Parent?.Name != XName.Get(sectionName))
            return false;
        item.Remove();
        return true;
    }

    private XElement Root => _document.Xml.Root!;

    private XElement? FindAttribute(string attributeName) =>
        Root.Element("attributes")?.Elements("attribute")
            .FirstOrDefault(element => string.Equals((string?)element.Element("name"), attributeName, StringComparison.Ordinal));

    private int? GetInteger(string elementName)
    {
        var value = GetValue(elementName);
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;
        throw new ChumFormatException($"Das Feld '{elementName}' enthält keine gültige Ganzzahl.");
    }

    private int? GetNestedInteger(string sectionName, string elementName)
    {
        var value = Root.Element(XName.Get(sectionName))?.Element(XName.Get(elementName))?.Value;
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;
        throw new ChumFormatException($"Das Feld '{sectionName}/{elementName}' enthält keine gültige Ganzzahl.");
    }

    private void SetNestedInteger(string sectionName, string elementName, int? value)
    {
        var section = Root.Element(XName.Get(sectionName));
        if (value is null)
        {
            section?.Element(XName.Get(elementName))?.Remove();
            return;
        }
        if (section is null)
        {
            section = new XElement(sectionName);
            Root.Add(section);
        }
        var node = section.Element(XName.Get(elementName));
        if (node is null)
            section.Add(new XElement(elementName, value.Value.ToString(CultureInfo.InvariantCulture)));
        else
            node.Value = value.Value.ToString(CultureInfo.InvariantCulture);
    }

    private void SetInteger(string elementName, int? value) =>
        SetValue(elementName, value?.ToString(CultureInfo.InvariantCulture));
}