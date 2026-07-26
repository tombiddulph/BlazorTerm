namespace BlazorTerm;

public static class TerminalContent
{
    public const string SiteUrl = "https://terminal.tommyb.dev";
    public const string DisplayName = "Tom Biddulph";
    public const string Owner = "tom";
    public const string Host = "portfolio";
    public const string City = "Leeds";
    public const string CountryCode = "GB";
    public const string Location = "Leeds, United Kingdom";
    public const string LinkedInUrl = "https://www.linkedin.com/in/tabiddulph/";
    public const string GitHubUrl = "https://github.com/tombiddulph";
    public const string PageTitle = "Tom Biddulph - Senior Software Engineer, C#/.NET | Leeds";
    public const string MetaDescription = "Tom Biddulph is a Senior Software Engineer in Leeds specialising in C#, .NET, backend systems, fintech, Azure, and OpenTelemetry.";

    public static readonly PersonProfile Profile = new(
        DisplayName,
        "Senior Software Engineer",
        Location,
        "Backend-focused C# engineer building reliable, accessible web and fintech systems.",
        "At NewDay, I build and maintain accessible financial products used by millions of customers. My background spans several leading fintech teams.");

    public static readonly ResumeEntry[] Resume =
    [
        new("NewDay", "Senior Software Engineer", new DateOnly(2023, 1, 1), null,
            ["Builds and maintains accessible financial products used by millions of customers."]),
        new("Codat", "Senior Software Engineer", new DateOnly(2022, 1, 1), new DateOnly(2022, 12, 31), []),
        new("Checkout.com", "Software Engineer II", new DateOnly(2021, 1, 1), new DateOnly(2022, 12, 31), []),
        new("NewDay", "Software Engineer", new DateOnly(2019, 1, 1), new DateOnly(2020, 12, 31), []),
        new("AND Digital", "Associate", new DateOnly(2018, 1, 1), new DateOnly(2019, 12, 31), [])
    ];

    public static readonly EducationEntry[] Education =
    [
        new("University of Hull", "Bachelor's Degree, Computer Science", "First-class honours (1:1)")
    ];

    public static readonly StackGroup[] Stack =
    [
        new("Languages", ["C#", "TypeScript", "SQL"]),
        new("Platform", [".NET", "ASP.NET Core", "Azure Functions"]),
        new("Cloud", ["Azure", "Service Bus", "Application Insights"]),
        new("Infrastructure", ["Proxmox", "Kubernetes", "self-hosting"]),
        new("Observability", ["OpenTelemetry", "distributed tracing", "OTLP"]),
        new("Delivery", ["Docker", "GitHub Actions", "CI/CD"]),
        new("Testing", ["TUnit", "integration testing", "test hosts"])
    ];

    public static readonly TimelineItem[] Timeline = Resume
        .Reverse()
        .Select(entry => new TimelineItem(entry.From, entry.Company, entry.Role))
        .ToArray();

    public static readonly Project[] Projects =
    [
        new(
            "service-bus-explorer",
            "Service Bus Emulator Explorer",
            "A web UI for exploring and managing the Azure Service Bus Emulator.",
            ["React 19", ".NET 10", "Azure Service Bus", "SQL Server", "OpenTelemetry"],
            "The application combines a React and Vite interface with an ASP.NET Core API and the Azure Service Bus Emulator.",
            "https://github.com/tombiddulph/Service-Bus-Emulator-Explorer",
            ["React + Vite UI", "ASP.NET Core API", "Azure Service Bus Emulator + SQL Server"],
            ["Queue, topic, subscription and DLQ management", "Docker delivery and OpenTelemetry observability", "Monaco message editor and real-time statistics"]),
        new(
            "property-resolvers",
            "PropertyResolvers",
            "Generates type-safe property resolvers at compile time, avoiding runtime reflection.",
            ["C#", "Roslyn", "Source Generators"],
            "Annotated C# models drive a Roslyn source generator that emits switch-based property resolvers and a type-safe registry.",
            "https://github.com/tombiddulph/PropertyResolvers",
            ["Annotated C# models", "Roslyn source generator", "Generated switch-based resolvers"],
            ["Compile-time generation instead of runtime reflection", "Nullable property support", "Type-safe resolver registry"]),
        new(
            "functions-test-host",
            "Azure Functions .NET Worker Test Host",
            "A focused test host for isolated-worker Azure Functions applications.",
            ["C#", ".NET", "Azure Functions"],
            "The reusable test host runs isolated-worker Azure Functions from a test project without requiring a deployed Functions host.",
            "https://github.com/tombiddulph/azure-functions-dotnet-worker-test-host",
            ["Test project", "Isolated worker test host", "Azure Function under test"],
            ["Designed for isolated-worker Functions", "Small reusable testing utility", "Open source under the MIT license"]),
        new(
            "otel-tracing-demo",
            "OpenTelemetry Tracing Demo",
            "A practical tracing and collector integration demonstration.",
            ["C#", ".NET", "OpenTelemetry", "Azure Service Bus"],
            "An HTTP API publishes messages to the Azure Service Bus Emulator, where four consumers demonstrate distributed trace propagation.",
            "https://github.com/tombiddulph/OtelTracingDemo",
            ["HTTP API", "Azure Service Bus Emulator", "Four message consumers + Aspire dashboard"],
            ["Distributed traces across asynchronous messaging", "Activity links versus parent context", "Docker Compose development environment"])
    ];

    public static readonly OpenSourceContribution[] Contributions =
    [
        new("OPEN", "OpenTelemetry Collector Contrib #49399", "Adds configurable Azure Monitor HTTP success mapping aligned with OTel semantics.", "https://github.com/open-telemetry/opentelemetry-collector-contrib/pull/49399"),
        new("MERGED", "OpenTelemetry .NET #4882", "Allowed '/' characters in metric instrument names, including tests and changelog.", "https://github.com/open-telemetry/opentelemetry-dotnet/pull/4882"),
        new("MERGED", "OpenTelemetry .NET #4881", "Modernised the dotnet format CI job to use the .NET SDK command.", "https://github.com/open-telemetry/opentelemetry-dotnet/pull/4881"),
        new("MERGED", "SemanticBlazor #19", "Added a Blazor header component based on Semantic UI header patterns.", "https://github.com/strakamichal/SemanticBlazor/pull/19"),
        new("MERGED", "Canola #37", "Corrected an off-by-one issue in the seed-value controls.", "https://github.com/cleavera/canola/pull/37")
    ];

    public static readonly HostingProfile Hosting = new(
        "This site is self-hosted on a Talos Linux Kubernetes cluster running across my Proxmox homelab.",
        ["GitHub Actions", "GitHub Container Registry", "Talos Kubernetes on Proxmox", "Cloudflare Tunnel + DNS + TLS", "terminal.tommyb.dev"],
        "The application runs as a non-root .NET 10 container with Kubernetes health probes, resource limits, and no inbound homelab ports exposed.");

    public static readonly ContactLink[] ContactLinks =
    [
        new("GitHub", "github.com/tombiddulph", GitHubUrl),
        new("LinkedIn", "linkedin.com/in/tabiddulph", LinkedInUrl)
    ];

    public static readonly string[] PublicRoutes =
    [
        "/", "/resume", "/projects",
        .. Projects.Select(project => $"/projects/{project.Slug}"),
        "/timeline", "/contact"
    ];
}

public sealed record PersonProfile(string Name, string Role, string Location, string Summary, string About);
public sealed record ResumeEntry(string Company, string Role, DateOnly From, DateOnly? To, string[] Highlights);
public sealed record EducationEntry(string Institution, string Qualification, string Result);
public sealed record StackGroup(string Category, string[] Technologies);
public sealed record TimelineItem(DateOnly When, string Title, string Detail);
public sealed record Project(string Slug, string Title, string Summary, string[] Stack, string CaseStudy, string Url, string[] Architecture, string[] Highlights);
public sealed record OpenSourceContribution(string Status, string Name, string Description, string Url);
public sealed record HostingProfile(string Summary, string[] Pipeline, string Runtime);
public sealed record ContactLink(string Name, string DisplayUrl, string Url);
