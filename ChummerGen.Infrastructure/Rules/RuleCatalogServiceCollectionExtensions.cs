using ChummerGen.Domain.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChummerGen.Infrastructure.Rules;

public static class RuleCatalogServiceCollectionExtensions
{
    public static IServiceCollection AddChummerRuleCatalog(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var dataDirectory = ResolveDirectory(configuration["Rules:DataDirectory"], environment.ContentRootPath)
            ?? FindDefaultDataDirectory(environment.ContentRootPath);
        var languageDirectory = ResolveDirectory(configuration["Rules:LanguageDirectory"], environment.ContentRootPath)
            ?? Path.Combine(Directory.GetParent(dataDirectory)?.FullName ?? dataDirectory, "lang");

        services.AddMemoryCache();
        services.AddScoped<IRuleCatalog>(provider => new FileSystemRuleCatalog(
            dataDirectory,
            languageDirectory,
            provider.GetRequiredService<IMemoryCache>()));
        return services;
    }

    private static string? ResolveDirectory(string? configuredPath, string contentRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        return Path.GetFullPath(Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(contentRoot, configuredPath));
    }

    private static string FindDefaultDataDirectory(string contentRoot)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(contentRoot));
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "bin", "data");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return Path.Combine(contentRoot, "bin", "data");
    }
}
