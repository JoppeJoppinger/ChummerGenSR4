using System.Xml.Linq;
using ChummerGen.Domain.Character;
using ChummerGen.Domain.Character.Export;
using ChummerGen.Domain.Chum;

namespace ChummerGen.Tests;

public sealed class CharacterExportTests
{
    [Fact]
    public void ChumExport_RoundtripsExactXmlIncludingUnknownContent()
    {
        const string xml = "<character><name>Renée &amp; Rune</name><future flag=\"keep\"><nested><![CDATA[a < b]]></nested></future></character>";
        var session = new CharacterSession(ChumSerializer.Parse(xml));

        var exported = CharacterExportService.SerializeChum(session);
        var restored = ChumSerializer.Parse(exported);

        Assert.Equal(xml, exported);
        Assert.True(XNode.DeepEquals(session.Xml, restored.Xml));
        Assert.Equal("a < b", restored.Xml.Root?.Element("future")?.Element("nested")?.Value);
    }

    [Fact]
    public void PrintSummary_EncodesCharacterValuesAsHtml()
    {
        var session = new CharacterSession(ChumSerializer.Parse(
            "<character><name>&lt;script&gt;alert(&quot;x&quot;)&lt;/script&gt;</name><alias>&lt;img src=x onerror=alert(1)&gt;&amp;</alias><metatype>Elf</metatype><karma>12</karma></character>"));

        var html = CharacterExportService.BuildPrintSummaryHtml(session);

        Assert.Contains("&lt;script&gt;alert(&quot;x&quot;)&lt;/script&gt;", html);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;&amp;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img", html);
        Assert.Contains("<dt>Karma</dt><dd>12</dd>", html);
    }
}
