using Microsoft.AspNetCore.Identity;

namespace ChummerGen.Infrastructure.Persistence;

public sealed class ApplicationUser : IdentityUser
{
    public string? PreferredCulture { get; set; }
}
