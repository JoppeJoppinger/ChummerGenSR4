using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChummerGen.Domain.Character;

public static class CharacterServiceCollectionExtensions
{
    public static IServiceCollection AddChummerCharacterEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<CharacterEngine>();
        return services;
    }
}