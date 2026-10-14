namespace ChummerGen.Infrastructure.Localization;

public interface IChummerLocalizationService
{
    IReadOnlyList<string> GetSupportedCultures();

    bool IsCultureSupported(string culture);

    string GetVersion(string culture);

    bool TryGetString(string key, string culture, out string? value);

    string GetRequiredString(string key, string culture);

    bool TryTranslateData(string originalText, string culture, out string? translatedText);

    string TranslateData(string originalText, string culture);
}
