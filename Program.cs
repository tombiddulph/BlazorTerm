using BlazorTerm;
using BlazorTerm.Components;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.PersistedCircuitInMemoryMaxRetained = 1_000;
        options.PersistedCircuitInMemoryRetentionPeriod = TimeSpan.FromHours(2);
    });

var telemetry = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: builder.Configuration["OTEL_SERVICE_NAME"] ?? "blazorterm",
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString(),
        serviceInstanceId: Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName));

var exportTelemetry = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

telemetry.WithTracing(traces =>
{
    traces
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
        .AddRuntimeInstrumentation();

    if (exportTelemetry)
        metering.AddOtlpExporter();
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapGet("/healthz", () => Results.NoContent());
app.MapGet("/sitemap.xml", () => Results.Text(
    $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n{string.Join('\n', TerminalContent.PublicRoutes.Select(route => $"  <url><loc>{TerminalContent.SiteUrl}{route}</loc></url>"))}\n</urlset>",
    "application/xml"));
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
