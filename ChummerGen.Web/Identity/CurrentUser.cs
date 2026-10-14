using System.Security.Claims;
using ChummerGen.Domain.Abstractions;
using Microsoft.AspNetCore.Components.Authorization;

namespace ChummerGen.Web.Identity;

public sealed class CurrentUser(
    AuthenticationStateProvider authenticationStateProvider,
    IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public async Task<string?> GetUserIdAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var requestUser = httpContextAccessor.HttpContext?.User;
        if (requestUser?.Identity?.IsAuthenticated == true)
            return requestUser.FindFirstValue(ClaimTypes.NameIdentifier);

        var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();
        return authenticationState.User.Identity?.IsAuthenticated == true
            ? authenticationState.User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
    }
}
