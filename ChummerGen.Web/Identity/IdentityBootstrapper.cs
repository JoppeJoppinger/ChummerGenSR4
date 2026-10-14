using ChummerGen.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ChummerGen.Web.Identity;

public static class IdentityBootstrapper
{
    public static async Task SeedRolesAndInitialAdministratorAsync(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var roleName in new[] { RoleNames.Administrator, RoleNames.User })
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var roleResult = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create the '{roleName}' role: {string.Join("; ", roleResult.Errors.Select(error => error.Description))}");
            }
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var userCount = userManager.Users.Count();
        var administrators = await userManager.GetUsersInRoleAsync(RoleNames.Administrator);
        if (administrators.Count > 0)
        {
            return;
        }

        if (userCount > 0)
        {
            throw new InvalidOperationException(
                "The database has users but no administrator. Restore an administrator through the documented recovery procedure before starting the application.");
        }

        var email = configuration["InitialAdmin:Email"];
        var password = configuration["InitialAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "A fresh database requires InitialAdmin:Email and InitialAdmin:Password configuration to create the first administrator.");
        }

        var administrator = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        var createResult = await userManager.CreateAsync(administrator, password);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not create the initial administrator: {string.Join("; ", createResult.Errors.Select(error => error.Description))}");
        }

        var roleAssignment = await userManager.AddToRoleAsync(administrator, RoleNames.Administrator);
        if (!roleAssignment.Succeeded)
        {
            var rollback = await userManager.DeleteAsync(administrator);
            if (!rollback.Succeeded)
            {
                throw new InvalidOperationException(
                    "Initial administrator role assignment failed and the partially created account could not be removed.");
            }

            throw new InvalidOperationException(
                $"Could not assign the administrator role: {string.Join("; ", roleAssignment.Errors.Select(error => error.Description))}");
        }

        logger.LogInformation("Created the initial administrator account.");
    }
}
