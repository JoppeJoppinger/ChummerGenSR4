using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;

namespace ChummerGen.Domain.Character.Export;

public static class CharacterExportService
{
    public static string SerializeChum(CharacterSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Serialize();
    }

    public static string BuildPrintSummaryHtml(CharacterSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var model = session.Model;
        var name = Display(model.Name);
        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"de\"><head><meta charset=\"utf-8\"><title>")
            .Append(Encode(name))
            .Append("</title><style>body{font:16px Arial,sans-serif;margin:2rem;color:#222}h1{border-bottom:2px solid #333;padding-bottom:.4rem}dl{display:grid;grid-template-columns:12rem 1fr;gap:.5rem 1rem}dt{font-weight:bold}@media print{body{margin:1cm}}</style></head><body><main class=\"character-export-sheet\"><h1>")
            .Append(Encode(name))
            .Append("</h1><dl>");

        AddField(html, "Alias", model.Alias);
        AddField(html, "Metatyp", model.Metatype);
        AddField(html, "Karma", model.Karma?.ToString(CultureInfo.InvariantCulture));
        AddField(html, "Nuyen", model.Nuyen?.ToString(CultureInfo.InvariantCulture));
        AddField(html, "Erschaffungspunkte", model.BuildPoints?.ToString(CultureInfo.InvariantCulture));
        html.Append("</dl></main></body></html>");
        return html.ToString();
    }

    private static void AddField(StringBuilder html, string label, string? value) =>
        html.Append("<dt>").Append(Encode(label)).Append("</dt><dd>").Append(Encode(Display(value))).Append("</dd>");

    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);
}
