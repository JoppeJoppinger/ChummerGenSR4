using ChummerGen.Web.Components.Characters;
using ChummerGen.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChummerGen.Tests;

public sealed class CharacterLocalizationComponentTests
{
    [Fact]
    public async Task CharacterTabsRenderRequiredLabelsFromTheAccessor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IChummerLocalizationAccessor, TestLocalizationAccessor>();
        using var serviceProvider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(serviceProvider, serviceProvider.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<CharacterTabs>(ParameterView.Empty);
            return component.ToHtmlString();
        });

        Assert.Contains("localized:Tab_CharacterInfo", html);
        Assert.Contains("localized:Label_Attributes", html);
        Assert.Contains("localized:Tab_Cyberware", html);
        Assert.DoesNotContain(">Fertigkeiten</button>", html);
    }

    private sealed class TestLocalizationAccessor : IChummerLocalizationAccessor
    {
        public IReadOnlyList<string> GetSupportedCultures() => ["de", "en-us"];

        public Task<string> GetCultureAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult("en-us");

        public Task<string> GetRequiredStringAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult($"localized:{key}");

        public Task SetCultureAsync(string culture, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
