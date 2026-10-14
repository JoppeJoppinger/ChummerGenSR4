using Microsoft.Extensions.DependencyInjection;

namespace ChummerGen.Web.Localization;

public static class LocalizationServiceCollectionExtensions
{
    public static IServiceCollection AddChummerWebLocalization(this IServiceCollection services)
    {
        services.AddScoped<IChummerLocalizationAccessor, ChummerLocalizationAccessor>();
        return services;
    }
}
