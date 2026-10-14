using ChummerGen.Domain.Abstractions;
using ChummerGen.Infrastructure.Localization;
using ChummerGen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChummerGen.Web.Localization;

public sealed class ChummerLocalizationAccessor(
    IChummerLocalizationService localization,
    ApplicationDbContext dbContext,
    ICurrentUser currentUser) : IChummerLocalizationAccessor
{
    public IReadOnlyList<string> GetSupportedCultures() => localization.GetSupportedCultures();

    public async Task<string> GetCultureAsync(CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetUserIdAsync(cancellationToken);
        if (userId is null)
            return GetDefaultCulture();

        var preferredCulture = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.PreferredCulture)
            .SingleOrDefaultAsync(cancellationToken);

        if (preferredCulture is null)
            return GetDefaultCulture();

        return FindExactCulture(preferredCulture);
    }

    public async Task<string> GetRequiredStringAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var culture = await GetCultureAsync(cancellationToken);
        return localization.GetRequiredString(key, culture);
    }

    public async Task SetCultureAsync(string culture, CancellationToken cancellationToken = default)
    {
        var exactCulture = FindExactCulture(culture);
        var userId = await currentUser.GetUserIdAsync(cancellationToken);
        if (userId is null)
            throw new UnauthorizedAccessException("An authenticated account is required to change its language.");

        var user = await dbContext.Users.SingleOrDefaultAsync(
            account => account.Id == userId,
            cancellationToken);
        if (user is null)
            throw new InvalidOperationException("The authenticated account no longer exists.");

        user.PreferredCulture = exactCulture;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private string GetDefaultCulture()
    {
        var cultures = localization.GetSupportedCultures();
        if (cultures.Count == 0)
            throw new CultureNotSupportedException("(no language files available)");

        return cultures.FirstOrDefault(culture => culture.Equals("de", StringComparison.OrdinalIgnoreCase))
            ?? cultures[0];
    }

    private string FindExactCulture(string culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            throw new CultureNotSupportedException(culture ?? string.Empty);

        return localization.GetSupportedCultures()
            .FirstOrDefault(available => available.Equals(culture.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new CultureNotSupportedException(culture);
    }
}
