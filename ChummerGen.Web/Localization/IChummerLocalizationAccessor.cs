namespace ChummerGen.Web.Localization;

public interface IChummerLocalizationAccessor
{
    IReadOnlyList<string> GetSupportedCultures();

    Task<string> GetCultureAsync(CancellationToken cancellationToken = default);

    Task<string> GetRequiredStringAsync(string key, CancellationToken cancellationToken = default);

    Task SetCultureAsync(string culture, CancellationToken cancellationToken = default);
}
