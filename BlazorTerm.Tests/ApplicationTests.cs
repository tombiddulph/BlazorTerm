using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace BlazorTerm.Tests;

public sealed class ApplicationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public ApplicationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task HomePage_RendersTerminalInterface()
    {
        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"terminal-input\"", html);
        Assert.Contains("Tom Biddulph", html);
        Assert.Contains("data-command-completions", html);
        Assert.Contains("components-reconnect-modal", html);
        Assert.Contains("id=\"portfolio-content\"", html);
        Assert.Contains("NewDay", html);
        Assert.Contains("Checkout.com", html);
        Assert.Contains("Service Bus Emulator Explorer", html);
        Assert.Contains(TerminalContent.MetaDescription, html);
        Assert.Contains("https://schema.org", html);
        Assert.Contains("og-card.png", html);
        Assert.Contains("<noscript>", html);
        Assert.Contains("static-page noscript-content", html);
        Assert.Contains("executed-command\">neofetch", html);
        Assert.Contains("executed-command\">whoami", html);
        Assert.Contains("aria-label=\"Suggested terminal commands\"", html);
        Assert.Contains("role=\"log\"", html);
        Assert.Contains("aria-live=\"polite\"", html);
        Assert.Contains("href=\"#portfolio-content\"", html);
    }

    [Theory]
    [InlineData("/resume", "Experience")]
    [InlineData("/projects", "Selected projects")]
    [InlineData("/projects/property-resolvers", "Roslyn source generator")]
    [InlineData("/timeline", "Career timeline")]
    [InlineData("/contact", "linkedin.com/in/tabiddulph")]
    public async Task StaticPortfolioRoutes_RenderSemanticContentWithoutACircuit(string path, string expectedContent)
    {
        var response = await _client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<main class=\"static-page\"", html);
        Assert.Contains(expectedContent, html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"type\":\"server\"", html);
        Assert.Contains("rel=\"canonical\"", html);
        Assert.Contains("property=\"og:title\"", html);
    }

    [Fact]
    public async Task UnknownProject_ReturnsNotFoundStatus()
    {
        var response = await _client.GetAsync("/projects/not-a-project");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("No such file or directory", html);
        Assert.Contains("/projects/not-a-project", html);
    }

    [Theory]
    [InlineData("/robots.txt", "Sitemap: https://terminal.tommyb.dev/sitemap.xml")]
    [InlineData("/sitemap.xml", "https://terminal.tommyb.dev/resume")]
    public async Task DiscoveryFiles_ArePublished(string path, string expectedContent)
    {
        var response = await _client.GetAsync(path);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedContent, content);
    }

    [Fact]
    public async Task Sitemap_ContainsEveryProjectPage()
    {
        var sitemap = await _client.GetStringAsync("/sitemap.xml");

        Assert.All(TerminalContent.Projects, project =>
            Assert.Contains($"{TerminalContent.SiteUrl}/projects/{project.Slug}", sitemap));
    }

    [Fact]
    public async Task OpenGraphImage_IsPublishedAsPng()
    {
        var response = await _client.GetAsync("/og-card.png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Content.Headers.ContentLength > 0);
    }

    [Fact]
    public async Task ReconnectInterfaces_LinkToStaticResume()
    {
        var html = await _client.GetStringAsync("/");

        Assert.Contains("view the plain resume", html, StringComparison.OrdinalIgnoreCase);
        Assert.True(Regex.Matches(html, "href=\"/resume\"").Count >= 2);
        Assert.Contains("<button type=\"button\" class=\"reload\"", html);
        Assert.Contains(">Reload</button>", html);
        Assert.DoesNotContain("href=\"\"", html);
    }

    [Fact]
    public async Task CommandDeepLink_IsPrerendered()
    {
        var html = await _client.GetStringAsync("/?cmd=projects");

        Assert.Contains("executed-command\">projects", html);
        Assert.Contains("SELECTED PROJECTS", html);
    }

    [Fact]
    public async Task HomePage_ReferencesAvailableBlazorRuntime()
    {
        var html = await _client.GetStringAsync("/");
        var scriptPath = ExtractAssetPath(html, "src", @"_framework/blazor\.web(?:\.[^\""]+)?\.js");

        var response = await _client.GetAsync(scriptPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Content.Headers.ContentLength > 0);
    }

    [Fact]
    public async Task HomePage_ReferencesAvailableTerminalScript()
    {
        var html = await _client.GetStringAsync("/");
        var scriptPath = ExtractAssetPath(html, "src", @"terminal(?:\.[^\""]+)?\.js");

        var response = await _client.GetAsync(scriptPath);
        var script = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("pointerdown", script);
        Assert.Contains("terminal-input", script);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsNoContent()
    {
        var response = await _client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UnknownRoute_ReturnsTerminalNotFoundPage()
    {
        var response = await _client.GetAsync("/definitely-not-a-route");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("No such file or directory", html);
        Assert.Contains("/definitely-not-a-route", html);
        Assert.Contains("href=\"/\"", html);
    }

    [Fact]
    public void OpenTelemetry_RegistersTraceAndMetricProviders()
    {
        Assert.NotNull(_factory.Services.GetService<TracerProvider>());
        Assert.NotNull(_factory.Services.GetService<MeterProvider>());
    }

    [Fact]
    public async Task HomePage_ReferencesAvailableFavicon()
    {
        var html = await _client.GetStringAsync("/");
        var faviconPath = ExtractAssetPath(html, "href", @"favicon(?:\.[^\""]+)?\.svg");

        var response = await _client.GetAsync(faviconPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
    }

    private static string ExtractAssetPath(string html, string attribute, string assetPattern)
    {
        var match = Regex.Match(html, $"{attribute}=\"(?<path>{assetPattern})\"");

        Assert.True(match.Success, $"Could not find asset matching '{assetPattern}'.");
        return match.Groups["path"].Value;
    }
}
