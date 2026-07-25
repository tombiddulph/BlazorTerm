namespace BlazorTerm;

public static class TerminalContent
{
    public const string DisplayName = "Tom Biddulph";
    public const string Owner = "tom";
    public const string Host = "portfolio";
    public const string LinkedInUrl = "https://www.linkedin.com/in/tabiddulph/";
    public const string GitHubUrl = "https://github.com/tombiddulph";

    public static readonly PortfolioProject[] Projects =
    [
        new(
            "service-bus-explorer",
            "Service Bus Emulator Explorer",
            "React 19 + .NET 10 / 13 stars / 2 forks",
            "A web UI for exploring and managing the Azure Service Bus Emulator.",
            "https://github.com/tombiddulph/Service-Bus-Emulator-Explorer",
            ["React + Vite UI", "ASP.NET Core API", "Azure Service Bus Emulator + SQL Server"],
            ["Queue, topic, subscription and DLQ management", "Docker delivery and OpenTelemetry observability", "Monaco message editor and real-time statistics"]),
        new(
            "property-resolvers",
            "PropertyResolvers",
            "C# / Roslyn source generator",
            "Generates type-safe property resolvers at compile time, avoiding runtime reflection.",
            "https://github.com/tombiddulph/PropertyResolvers",
            ["Annotated C# models", "Roslyn source generator", "Generated switch-based resolvers"],
            ["Compile-time generation instead of runtime reflection", "Nullable property support", "Type-safe resolver registry"]),
        new(
            "functions-test-host",
            "Azure Functions .NET Worker Test Host",
            "C# / MIT licensed",
            "A focused test host for isolated-worker Azure Functions applications.",
            "https://github.com/tombiddulph/azure-functions-dotnet-worker-test-host",
            ["Test project", "Isolated worker test host", "Azure Function under test"],
            ["Designed for isolated-worker Functions", "Small reusable testing utility", "Open source under the MIT license"]),
        new(
            "otel-tracing-demo",
            "OpenTelemetry Tracing Demo",
            "C# / OpenTelemetry",
            "A practical tracing and collector integration demonstration.",
            "https://github.com/tombiddulph/OtelTracingDemo",
            ["HTTP API", "Azure Service Bus Emulator", "Four message consumers + Aspire dashboard"],
            ["Distributed traces across asynchronous messaging", "Activity links versus parent context", "Docker Compose development environment"])
    ];

    public static readonly OpenSourceContribution[] Contributions =
    [
        new(
            "OPEN",
            "OpenTelemetry Collector Contrib #49399",
            "Adds configurable Azure Monitor HTTP success mapping aligned with OTel semantics.",
            "https://github.com/open-telemetry/opentelemetry-collector-contrib/pull/49399"),
        new(
            "MERGED",
            "OpenTelemetry .NET #4882",
            "Allowed '/' characters in metric instrument names, including tests and changelog.",
            "https://github.com/open-telemetry/opentelemetry-dotnet/pull/4882"),
        new(
            "MERGED",
            "OpenTelemetry .NET #4881",
            "Modernised the dotnet format CI job to use the .NET SDK command.",
            "https://github.com/open-telemetry/opentelemetry-dotnet/pull/4881"),
        new(
            "MERGED",
            "SemanticBlazor #19",
            "Added a Blazor header component based on Semantic UI header patterns.",
            "https://github.com/strakamichal/SemanticBlazor/pull/19"),
        new(
            "MERGED",
            "Canola #37",
            "Corrected an off-by-one issue in the seed-value controls.",
            "https://github.com/cleavera/canola/pull/37")
    ];

    public static readonly IReadOnlyDictionary<string, string[]> Files =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["readme.txt"] = new[]
            {
                "Welcome to my small corner of the web, presented as a shell.",
                "",
                "Start with 'about', 'experience', 'education', or 'skills'."
            },
            ["about.txt"] = new[]
            {
                "ABOUT",
                "Hi, I'm Tom Biddulph, a Senior Software Engineer based in Leeds, UK.",
                "I specialise in C#, backend engineering, and web applications.",
                "",
                "At NewDay, I build and maintain accessible financial products used by",
                "millions of customers. My background spans several leading fintech teams."
            },
            ["projects.txt"] = new[]
            {
                "PROJECTS",
                "Service-Bus-Emulator-Explorer  / Azure Service Bus emulator UI",
                "PropertyResolvers              / C# source generator",
                "azure-functions test host      / Isolated-worker testing utility",
                "OtelTracingDemo                 / OpenTelemetry tracing demo"
            },
            ["experience.txt"] = new[]
            {
                "EXPERIENCE",
                "2023 - now   Senior Software Engineer  / NewDay",
                "2022         Senior Software Engineer  / Codat",
                "2021 - 2022  Software Engineer II      / Checkout.com",
                "2019 - 2020  Software Engineer         / NewDay",
                "2018 - 2019  Associate                 / AND Digital"
            },
            ["education.txt"] = new[]
            {
                "EDUCATION",
                "University of Hull",
                "Bachelor's Degree, Computer Science / First-class honours (1:1)"
            },
            ["skills.txt"] = new[]
            {
                "SKILLS",
                "C#             Backend engineering",
                ".NET           Web application development",
                "Azure          Cloud and distributed messaging",
                "Infrastructure Proxmox, Kubernetes, and self-hosting"
            },
            ["hosting.txt"] = new[]
            {
                "HOSTING",
                "This site is self-hosted on a Talos Linux Kubernetes cluster running",
                "across my Proxmox homelab.",
                "",
                "GitHub Actions",
                "      |  build + test",
                "      v",
                "GitHub Container Registry",
                "      |  container image",
                "      v",
                "Talos Kubernetes on Proxmox",
                "      |  outbound tunnel",
                "      v",
                "Cloudflare Tunnel + DNS + TLS",
                "      |",
                "      v",
                "terminal.tommyb.dev",
                "",
                "The application runs as a non-root .NET 10 container with Kubernetes",
                "health probes, resource limits, and no inbound homelab ports exposed."
            },
            ["contact.txt"] = new[]
            {
                "CONTACT",
                "github    github.com/tombiddulph",
                "linkedin  linkedin.com/in/tabiddulph"
            }
        };
}

public sealed record PortfolioProject(
    string Slug,
    string Name,
    string Stack,
    string Description,
    string Url,
    string[] Architecture,
    string[] Highlights);

public sealed record OpenSourceContribution(string Status, string Name, string Description, string Url);
