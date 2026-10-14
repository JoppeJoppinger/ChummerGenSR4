using System.Xml.Linq;
using ChummerGen.Domain.Chum;

namespace ChummerGen.Tests;

public sealed class ChumRoundtripTests
{
    internal const string ReferenceFilePathForTest = @"C:\Users\Joppe\Downloads\FipsDonnerfaust_Verbessert.chum";

    [ReferenceChumFact]
    public void ReferenceFile_RoundtripsWithoutLosingXml()
    {
        var original = ChumSerializer.LoadFile(ReferenceFilePathForTest);
        var serialized = ChumSerializer.Serialize(original);
        var restored = ChumSerializer.Parse(serialized);

        Assert.Equal("467", original.Version);
        Assert.False(string.IsNullOrWhiteSpace(original.Name));
        Assert.False(string.IsNullOrWhiteSpace(original.Metatype));
        Assert.NotNull(original.Karma);
        Assert.NotNull(original.Nuyen);
        Assert.NotNull(original.BuildPoints);
        Assert.True(XNode.DeepEquals(original.Xml.Root, restored.Xml.Root));
    }

    [Fact]
    public void MinimalCharacter_ExposesTypedFieldsAndPreservesUnknownElements()
    {
        const string xml = "<character><appversion>467</appversion><name>Test</name><alias>Fips</alias><metatype>Dwarf</metatype><karma>12</karma><nuyen>3400</nuyen><bp>25</bp><unknown flag=\"keep\"><nested>value</nested></unknown></character>";
        var character = ChumSerializer.Parse(xml);

        Assert.Equal("467", character.Version);
        Assert.Equal("Test", character.Name);
        Assert.Equal("Fips", character.Alias);
        Assert.Equal("Dwarf", character.Metatype);
        Assert.Equal(12, character.Karma);
        Assert.Equal(3400, character.Nuyen);
        Assert.Equal(25, character.BuildPoints);
        character.Karma = 13;

        var restored = ChumSerializer.Parse(ChumSerializer.Serialize(character));
        Assert.Equal(13, restored.Karma);
        Assert.True(XNode.DeepEquals(character.Xml.Root, restored.Xml.Root));
        Assert.Equal("value", (string?)restored.Xml.Root?.Element("unknown")?.Element("nested"));
    }

    [Fact]
    public void Parse_RejectsMalformedXmlWithGermanError()
    {
        var exception = Assert.Throws<ChumFormatException>(() => ChumSerializer.Parse("<character><name>"));

        Assert.Contains("ungültiges XML", exception.Message);
    }

    [Fact]
    public void Parse_RejectsDtd()
    {
        const string xml = "<!DOCTYPE character [<!ENTITY xxe SYSTEM 'file:///C:/Windows/win.ini'>]><character><name>&xxe;</name></character>";

        var exception = Assert.Throws<ChumFormatException>(() => ChumSerializer.Parse(xml));

        Assert.Contains("verbotene DTD", exception.Message);
    }

    [Fact]
    public void Parse_EnforcesCharacterLimit()
    {
        var exception = Assert.Throws<ChumFormatException>(() => ChumSerializer.Parse("<character />", maxCharacters: 5));

        Assert.Contains("Größenlimit", exception.Message);
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class ReferenceChumFactAttribute : FactAttribute
{
    public ReferenceChumFactAttribute()
    {
        if (!File.Exists(ChumRoundtripTests.ReferenceFilePathForTest))
            Skip = "Die Referenzdatei ist nicht vorhanden.";
    }
}
