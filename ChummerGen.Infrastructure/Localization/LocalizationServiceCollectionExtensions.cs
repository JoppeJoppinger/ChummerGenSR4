using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChummerGen.Infrastructure.Localization;

public static class LocalizationServiceCollectionExtensions
{
    public static IServiceCollection AddChummerLocalization(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var languageDirectory = ResolveDirectory(configuration["Localization:LanguageDirectory"], environment.ContentRootPath)
            ?? ResolveDirectory(configuration["Rules:LanguageDirectory"], environment.ContentRootPath)
            ?? FindDefaultLanguageDirectory(environment.ContentRootPath);

        services.AddMemoryCache();
        services.AddSingleton<IChummerLocalizationService>(provider => new ChummerLocalizationService(
            languageDirectory,
            provider.GetRequiredService<IMemoryCache>()));

        return services;
    }

    private static string? ResolveDirectory(string? configuredPath, string contentRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            return null;

        return Path.GetFullPath(Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(contentRoot, configuredPath));
    }

    private static string FindDefaultLanguageDirectory(string contentRoot)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(contentRoot));
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "bin", "lang");
            if (Directory.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        return Path.Combine(contentRoot, "bin", "lang");
    }
}
