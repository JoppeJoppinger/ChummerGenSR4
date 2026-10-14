using System.Globalization;
using System.Xml.Linq;

namespace ChummerGen.Domain.Chum;

public sealed class ChumDocument
{
    public const long DefaultMaxCharacters = 16_777_216;

    internal ChumDocument(XDocument xml)
    {
        Xml = xml;
    }

    public XDocument Xml { get; }

    public string? Version
    {
        get => GetText("appversion");
        set => SetText("appversion", value);
    }

    public string? Name
    {
        get => GetText("name");
        set => SetText("name", value);
    }

    public string? Alias
    {
        get => GetText("alias");
        set => SetText("alias", value);
    }

    public string? Metatype
    {
        get => GetText("metatype");
        set => SetText("metatype", value);
    }

    public int? Karma
    {
        get => GetInteger("karma");
        set => SetInteger("karma", value);
    }

    public int? Nuyen
    {
        get => GetInteger("nuyen");
        set => SetInteger("nuyen", value);
    }

    public int? BuildPoints
    {
        get => GetInteger("bp");
        set => SetInteger("bp", value);
    }

    private XElement Root => Xml.Root!;

    private string? GetText(string elementName) => Root.Element(elementName)?.Value;

    private void SetText(string elementName, string? value)
    {
        var element = Root.Element(elementName);
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

    private int? GetInteger(string elementName)
    {
        var value = GetText(elementName);
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;

        throw new ChumFormatException($"Das Feld '{elementName}' enthält keine gültige Ganzzahl.");
    }

    private void SetInteger(string elementName, int? value) =>
        SetText(elementName, value?.ToString(CultureInfo.InvariantCulture));
}
