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
