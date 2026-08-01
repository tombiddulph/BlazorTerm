namespace BlazorTerm;

public enum CommandCategory
{
    Profile,
    Portfolio,
    Files,
    System,
    Filters,
    Fun
}

public sealed record CommandDocumentation(
    string Name,
    string Synopsis,
    string Description,
    CommandCategory Category,
    IReadOnlyList<string>? Examples = null,
    IReadOnlyList<string>? SeeAlso = null,
    bool IsFilter = false,
    string? LinkTarget = null,
    LinkKind? LinkKind = LinkKind.Command);

public sealed record HelpCategoryDocumentation(CommandCategory Category, string Name, string Description);

public static class CommandCatalog
{
    public static readonly IReadOnlyList<HelpCategoryDocumentation> HelpCategories =
    [
        new(CommandCategory.Profile, "profile", "Biography, CV, career and stack"),
        new(CommandCategory.Portfolio, "portfolio", "Projects, contributions and contact"),
        new(CommandCategory.Files, "files", "Virtual filesystem navigation"),
        new(CommandCategory.System, "system", "Session, observability and live data"),
        new(CommandCategory.Filters, "filters", "Pipeline filters and composition"),
        new(CommandCategory.Fun, "fun", "Themes and terminal diversions")
    ];

    public static readonly IReadOnlyList<CommandDocumentation> Entries =
    [
        Doc("about", "about", "Show a short introduction to Tom.", CommandCategory.Profile),
        Doc("resume", "resume", "Terminal-formatted CV for Tom.", CommandCategory.Profile, ["resume | grep -i azure"]),
        Doc("experience", "experience", "Show professional experience.", CommandCategory.Profile),
        Doc("education", "education", "Show degree and university details.", CommandCategory.Profile),
        Doc("skills", "skills", "List professional skills.", CommandCategory.Profile),
        Doc("stack", "stack", "Show technologies and engineering specialisms.", CommandCategory.Profile),
        Doc("hosting", "hosting", "Explain how this portfolio is hosted.", CommandCategory.Profile),
        Doc("timeline", "timeline", "Render career history at a glance.", CommandCategory.Profile),
        Doc("status", "status", "Show Tom's current role and engineering focus.", CommandCategory.Profile),

        Doc("projects", "projects", "List selected GitHub projects.", CommandCategory.Portfolio),
        Doc("project", "project <name>", "Open a project case study.", CommandCategory.Portfolio, ["project service-bus-explorer"], LinkTarget: "project service-bus-explorer"),
        Doc("contributions", "contributions", "Show verified open-source contributions.", CommandCategory.Portfolio),
        Doc("contact", "contact", "Show ways to get in touch.", CommandCategory.Portfolio),
        Doc("github", "github", "Open Tom's GitHub profile and project shortcuts.", CommandCategory.Portfolio, LinkTarget: TerminalContent.GitHubUrl, LinkKind: BlazorTerm.LinkKind.Web),
        Doc("linkedin", "linkedin", "Open Tom's LinkedIn profile.", CommandCategory.Portfolio, LinkTarget: TerminalContent.LinkedInUrl, LinkKind: BlazorTerm.LinkKind.Web),
        Doc("open", "open [project]", "Open GitHub or a selected project URL.", CommandCategory.Portfolio, ["open property-resolvers"]),
        Doc("map", "map", "Open the all-sports activity map.", CommandCategory.Portfolio),
        Doc("gui", "gui", "Open the plain web resume.", CommandCategory.Portfolio),

        Doc("ls", "ls [-l] [path]", "List virtual files and directories.", CommandCategory.Files, ["ls -l projects"]),
        Doc("cat", "cat <file>", "Read a virtual file.", CommandCategory.Files, ["cat about.txt"], LinkTarget: "cat about.txt"),
        Doc("cd", "cd [directory]", "Change working directory in the virtual filesystem.", CommandCategory.Files, ["cd projects"], LinkTarget: "cd projects"),
        Doc("pwd", "pwd", "Print the absolute virtual working directory.", CommandCategory.Files),
        Doc("tree", "tree [path]", "Show the virtual filesystem tree.", CommandCategory.Files),

        Doc("help", "help [profile|portfolio|files|system|filters|fun]", "Show concise command groups or one group's commands.", CommandCategory.System, ["help files"]),
        Doc("man", "man <command>", "Generate a manual page from the command catalog.", CommandCategory.System, ["man grep"]),
        Doc("tour", "tour [next|previous|<number>|stop]", "Navigate the six-step guided terminal tour.", CommandCategory.System, ["tour", "tour next"]),
        Doc("neofetch", "neofetch", "Show a compact portfolio and runtime summary.", CommandCategory.System),
        Doc("whoami", "whoami", "Print the current terminal user.", CommandCategory.System),
        Doc("who", "who", "Show anonymous connection and session details.", CommandCategory.System),
        Doc("date", "date", "Print the server date and time.", CommandCategory.System),
        Doc("uptime", "uptime", "Show application uptime and start time.", CommandCategory.System),
        Doc("version", "version", "Show build provenance and runtime details.", CommandCategory.System),
        Doc("history", "history", "Show commands entered in this session.", CommandCategory.System),
        Doc("telemetry", "telemetry", "Show live application health and instrumentation.", CommandCategory.System),
        Doc("trace", "trace [-v] <command>", "Render a command's OpenTelemetry activity waterfall.", CommandCategory.System, ["trace resume", "trace -v projects"]),
        Doc("git", "git <log|show|blame>", "Browse career history through a read-only Git interface.", CommandCategory.System, ["git log --oneline"]),
        Doc("kubectl", "kubectl get <pods|nodes|namespaces>", "Sanitized pods, nodes, or namespaces from optional live Kubernetes.", CommandCategory.System, ["kubectl get pods"]),
        Doc("rides", "rides", "Show optional recent Strava rides as an accessible chart.", CommandCategory.System),
        Doc("clear", "clear", "Clear terminal output.", CommandCategory.System, LinkKind: null),

        Doc("grep", "grep [-i] [-v] <pattern>", "Filter lines by a regular expression.", CommandCategory.Filters, ["resume | grep -i azure"], IsFilter: true),
        Doc("head", "head [-n count]", "Show the first pipeline lines.", CommandCategory.Filters, ["projects | head -n 3"], IsFilter: true),
        Doc("tail", "tail [-n count]", "Show the last pipeline lines.", CommandCategory.Filters, IsFilter: true),
        Doc("wc", "wc [-l]", "Count pipeline lines, words, and characters.", CommandCategory.Filters, ["resume | wc -l"], IsFilter: true),
        Doc("sort", "sort [-r]", "Sort pipeline lines.", CommandCategory.Filters, IsFilter: true),
        Doc("uniq", "uniq", "Remove adjacent duplicate pipeline lines.", CommandCategory.Filters, IsFilter: true),

        Doc("theme", "theme [green|amber|nord|solarized|dracula]", "Show or switch the terminal colour theme.", CommandCategory.Fun, ["theme nord"]),
        Doc("coffee", "coffee", "Compile a fresh terminal coffee.", CommandCategory.Fun),
        Doc("fortune", "fortune", "Print a short engineering fortune.", CommandCategory.Fun),
        Doc("cowsay", "cowsay [message]", "Ask a cow to say something with accessible ASCII art.", CommandCategory.Fun, ["cowsay ship it"]),
        Doc("sudo", "sudo", "Attempt privileged execution.", CommandCategory.Fun),
        Doc("vim", "vim [file]", "Enter a tiny read-only Vim interaction mode.", CommandCategory.Fun, ["vim about.txt"])
    ];

    public static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["?"] = "help",
            ["bio"] = "about",
            ["cv"] = "resume",
            ["repos"] = "projects",
            ["activities"] = "map",
            ["heatmap"] = "map",
            ["oss"] = "contributions",
            ["work"] = "experience",
            ["cls"] = "clear"
        };

    private static readonly IReadOnlyDictionary<string, CommandDocumentation> ByName =
        Entries.ToDictionary(entry => entry.Name, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<CommandDocumentation> Commands => Entries.Where(entry => !entry.IsFilter).ToArray();
    public static IReadOnlyList<CommandDocumentation> Filters => Entries.Where(entry => entry.IsFilter).ToArray();
    public static IReadOnlyList<string> CommandNames => Commands.Select(entry => entry.Name).ToArray();
    public static IReadOnlyList<string> FilterNames => Filters.Select(entry => entry.Name).ToArray();
    public static IReadOnlyList<string> CompletionNames => Entries.Select(entry => entry.Name).ToArray();
    public static IReadOnlyList<string> ManualNames => Entries.Select(entry => entry.Name).Append("tom").Concat(Aliases.Keys).Order().ToArray();

    public static CommandDocumentation? Find(string name)
    {
        var canonical = Aliases.GetValueOrDefault(name, name);
        return ByName.GetValueOrDefault(canonical);
    }

    public static HelpCategoryDocumentation? FindCategory(string name) => HelpCategories.FirstOrDefault(category =>
        category.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
        name.Equals(category.Category switch
        {
            CommandCategory.Portfolio => "navigate",
            CommandCategory.Files => "filesystem",
            CommandCategory.Filters => "pipes",
            CommandCategory.Fun => "delight",
            _ => string.Empty
        }, StringComparison.OrdinalIgnoreCase));

    public static string? Suggest(string name, bool manuals = false)
    {
        var candidates = manuals ? ManualNames : CommandNames;
        var closest = candidates.Select(candidate => (Name: candidate, Distance: EditDistance(name, candidate)))
            .OrderBy(candidate => candidate.Distance)
            .FirstOrDefault();
        return closest.Distance <= 3 ? closest.Name : null;
    }

    private static CommandDocumentation Doc(
        string name,
        string synopsis,
        string description,
        CommandCategory category,
        IReadOnlyList<string>? examples = null,
        IReadOnlyList<string>? seeAlso = null,
        bool IsFilter = false,
        string? LinkTarget = null,
        LinkKind? LinkKind = BlazorTerm.LinkKind.Command) =>
        new(name, synopsis, description, category, examples, seeAlso, IsFilter, LinkTarget, LinkKind);

    private static int EditDistance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        for (var i = 1; i <= left.Length; i++)
        {
            var current = new int[right.Length + 1];
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = char.ToLowerInvariant(left[i - 1]) == char.ToLowerInvariant(right[j - 1]) ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            previous = current;
        }
        return previous[right.Length];
    }
}
