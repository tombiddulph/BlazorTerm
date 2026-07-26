using System.Net;
using System.Net.Http.Headers;
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
        Assert.Contains(">telemetry</button>", html);
        Assert.Contains("class=\"neofetch-output\"", html);
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
    public async Task HelpCommand_GroupsPrimaryNavigation()
    {
        var html = await _client.GetStringAsync("/?cmd=help");

        Assert.Contains(">PROFILE</div>", html);
        Assert.Contains(">NAVIGATE</div>", html);
        Assert.Contains(">SYSTEM</div>", html);
        Assert.Contains(">project &lt;name&gt;</button>", html);
        Assert.Contains(">Open a project case study</span>", html);
        Assert.Contains(">theme &lt;name&gt;</span>", html);
    }

    [Fact]
    public async Task PlainTextRequest_ReturnsAnsiResume()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("\u001b[1;32mTom Biddulph", content);
        Assert.Contains("EXPERIENCE", content);
        Assert.Contains("SELECTED PROJECTS", content);
    }

    [Fact]
    public async Task CurlUserAgent_ReturnsAnsiResume()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.UserAgent.ParseAdd("curl/8.0.0");

        var response = await _client.SendAsync(request);

        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task LlmsText_ContainsStructuredPortfolioContent()
    {
        var response = await _client.GetAsync("/llms.txt");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("# Tom Biddulph", content);
        Assert.Contains("## Experience", content);
        Assert.All(TerminalContent.Projects, project => Assert.Contains(project.Title, content));
    }

    [Fact]
    public async Task TelemetryCommand_IsPrerendered()
    {
        var html = await _client.GetStringAsync("/?cmd=telemetry");

        Assert.Contains("LIVE TELEMETRY", html);
        Assert.Contains("circuits", html);
        Assert.Contains("last request", html);
    }

    [Theory]
    [InlineData("/resume")]
    [InlineData("/projects")]
    [InlineData("/projects/property-resolvers")]
    [InlineData("/timeline")]
    [InlineData("/contact")]
    [InlineData("/llms.txt")]
    [InlineData("/sitemap.xml")]
    public async Task StaticContent_IsMarkedForCdnCaching(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.Public);
        Assert.Equal(TimeSpan.FromMinutes(5), response.Headers.CacheControl?.MaxAge);
        Assert.Equal("public, max-age=86400, stale-if-error=604800", response.Headers.GetValues("CDN-Cache-Control").Single());
    }

    [Fact]
    public async Task InteractiveHome_IsNotMarkedForCdnCaching()
    {
        var response = await _client.GetAsync("/");

        Assert.NotEqual(true, response.Headers.CacheControl?.Public);
        Assert.False(response.Headers.Contains("CDN-Cache-Control"));
    }

    [Fact]
    public async Task MissingProject_IsNotMarkedForCdnCaching()
    {
        var response = await _client.GetAsync("/projects/not-a-project");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(response.Headers.Contains("CDN-Cache-Control"));
    }

    [Theory]
    [InlineData("tommyb.dev")]
    [InlineData("www.tommyb.dev")]
    public async Task ApexDomainRoot_RedirectsToStaticResume(string host)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Host = host;

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PermanentRedirect, response.StatusCode);
        Assert.Equal("/resume", response.Headers.Location?.OriginalString);
        Assert.True(response.Headers.CacheControl?.Public);
        Assert.True(response.Headers.Contains("CDN-Cache-Control"));

        using var resumeRequest = new HttpRequestMessage(HttpMethod.Get, "/resume");
        resumeRequest.Headers.Host = host;
        var resumeResponse = await _client.SendAsync(resumeRequest);
        var html = await resumeResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, resumeResponse.StatusCode);
        Assert.Contains("<main class=\"static-page\"", html);
        Assert.Contains("Experience", html);
        Assert.Contains($"href=\"{TerminalContent.SiteUrl}\"", html);
        Assert.DoesNotContain("\"type\":\"server\"", html);
        Assert.True(resumeResponse.Headers.CacheControl?.Public);
        Assert.True(resumeResponse.Headers.Contains("CDN-Cache-Control"));
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
