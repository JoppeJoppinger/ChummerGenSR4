using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ChummerGen.Web.Components;
using ChummerGen.Web.Components.Account;
using ChummerGen.Domain.Abstractions;
using ChummerGen.Infrastructure.Persistence;
using ChummerGen.Infrastructure.Import;
using ChummerGen.Infrastructure.Rules;
using ChummerGen.Infrastructure.Localization;
using ChummerGen.Web.Identity;
using ChummerGen.Web.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHealthChecks();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddAuthorization();
builder.Services.AddChummerPersistence(builder.Configuration, builder.Environment);
builder.Services.AddChummerRuleCatalog(builder.Configuration, builder.Environment);
builder.Services.AddChummerLocalization(builder.Configuration, builder.Environment);
builder.Services.AddChummerWebLocalization();
builder.Services.AddScoped<ChumImportService>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 1;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "ChummerGen.Authentication";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
    await IdentityBootstrapper.SeedRolesAndInitialAdministratorAsync(
        scope.ServiceProvider,
        app.Configuration,
        app.Logger);
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapGet("/characters/{id:guid}/download", async (Guid id, ICharacterStore store, CancellationToken cancellationToken) =>
{
    var character = await store.GetAsync(id, cancellationToken);
    if (character is null)
        return Results.NotFound();

    var safeName = string.Concat(character.Name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
    if (string.IsNullOrWhiteSpace(safeName))
        safeName = "character";
    return Results.File(ChumImportService.EncodeForDownload(character.ChumXml), "application/xml", $"{safeName}.chum");
}).RequireAuthorization();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAdditionalIdentityEndpoints();

app.Run();

public partial class Program { }


