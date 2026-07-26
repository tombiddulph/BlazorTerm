using BlazorTerm;
using BlazorTerm.Components;
using Microsoft.AspNetCore.Components.Server.Circuits;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.PersistedCircuitInMemoryMaxRetained = 1_000;
        options.PersistedCircuitInMemoryRetentionPeriod = TimeSpan.FromHours(2);
    });
builder.Services.AddSingleton<TerminalTelemetry>();
builder.Services.AddSingleton<CircuitHandler>(services => services.GetRequiredService<TerminalTelemetry>());

var telemetry = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: builder.Configuration["OTEL_SERVICE_NAME"] ?? "blazorterm",
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString(),
        serviceInstanceId: Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName));

var exportTelemetry = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

telemetry.WithTracing(traces =>
{
    traces
        .AddSource(TerminalActivities.SourceName)
        .AddAspNetCoreInstrumentation(options =>
            options.Filter = context => !context.Request.Path.StartsWithSegments("/healthz"))
        .AddHttpClientInstrumentation();

    if (exportTelemetry)
        traces.AddOtlpExporter();
});

telemetry.WithMetrics(metering =>
{
    metering
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(TerminalTelemetry.MeterName);

    if (exportTelemetry)
        metering.AddOtlpExporter();
});

var app = builder.Build();
var terminalTelemetry = app.Services.GetRequiredService<TerminalTelemetry>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    if (IsApexResumeRequest(context.Request))
    {
        context.Response.Headers.CacheControl = "public, max-age=300, stale-if-error=604800";
        context.Response.Headers["CDN-Cache-Control"] = "public, max-age=86400, stale-if-error=604800";
        context.Response.Redirect("/resume", permanent: true, preserveMethod: true);
        return;
    }

    await next(context);
});

app.Use(async (context, next) =>
{
    var startedAt = Stopwatch.GetTimestamp();
    try
    {
        await next(context);
    }
    finally
    {
        if (!context.WebSockets.IsWebSocketRequest && !context.Request.Path.StartsWithSegments("/healthz"))
            terminalTelemetry.RecordRequest(Stopwatch.GetElapsedTime(startedAt));
    }
});

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/" && WantsPlainText(context.Request))
    {
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.Headers.Vary = "Accept, User-Agent";
        await context.Response.WriteAsync(PlainTextPortfolioFormatter.AnsiResume());
        return;
    }

    await next(context);
});

app.Use(async (context, next) =>
{
    if (IsCdnCacheablePath(context.Request.Path))
    {
        context.Response.OnStarting(() =>
        {
            if (context.Response.StatusCode == StatusCodes.Status200OK)
            {
                context.Response.Headers.CacheControl = "public, max-age=300, stale-if-error=604800";
                context.Response.Headers["CDN-Cache-Control"] = "public, max-age=86400, stale-if-error=604800";
            }

            return Task.CompletedTask;
        });
    }

    await next(context);
});

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapGet("/healthz", () => Results.NoContent());
app.MapGet("/llms.txt", () => Results.Text(PlainTextPortfolioFormatter.LlmsText(), "text/plain", Encoding.UTF8));
app.MapGet("/sitemap.xml", () => Results.Text(
    $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n{string.Join('\n', TerminalContent.PublicRoutes.Select(route => $"  <url><loc>{TerminalContent.SiteUrl}{route}</loc></url>"))}\n</urlset>",
    "application/xml"));
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static bool WantsPlainText(HttpRequest request)
{
    var acceptsPlainText = request.GetTypedHeaders().Accept?.Any(mediaType =>
        mediaType.MediaType.Value?.Equals("text/plain", StringComparison.OrdinalIgnoreCase) == true) == true;
    var userAgent = request.Headers.UserAgent.ToString();
    var isTerminalClient = userAgent.StartsWith("curl/", StringComparison.OrdinalIgnoreCase)
        || userAgent.StartsWith("Wget/", StringComparison.OrdinalIgnoreCase)
        || userAgent.StartsWith("HTTPie/", StringComparison.OrdinalIgnoreCase);

    return acceptsPlainText || isTerminalClient;
}

static bool IsCdnCacheablePath(PathString path)
{
    return path == "/resume"
        || path == "/timeline"
        || path == "/contact"
        || path == "/llms.txt"
        || path == "/sitemap.xml"
        || path.StartsWithSegments("/projects");
}

static bool IsApexResumeRequest(HttpRequest request)
{
    return request.Path == "/"
        && (request.Host.Host.Equals("tommyb.dev", StringComparison.OrdinalIgnoreCase)
            || request.Host.Host.Equals("www.tommyb.dev", StringComparison.OrdinalIgnoreCase));
}

public partial class Program;
