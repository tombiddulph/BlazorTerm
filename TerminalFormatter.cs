namespace BlazorTerm;

public static class TerminalFormatter
{
    public static readonly string[] FileNames =
    [
        "readme.txt", "about.txt", "projects.txt", "experience.txt", "education.txt",
        "skills.txt", "hosting.txt", "contact.txt"
    ];

    public static IReadOnlyList<OutputLine> ReadFile(string fileName)
    {
        return fileName.ToLowerInvariant() switch
        {
            "readme.txt" =>
            [
                Line("Welcome to my small corner of the web, presented as a shell."),
                Line(string.Empty),
                Line("Start with 'about', 'experience', 'education', or 'skills'.")
            ],
            "about.txt" =>
            [
                Line("ABOUT", "heading"),
                Line($"Hi, I'm {TerminalContent.Profile.Name}, a {TerminalContent.Profile.Role} based in {TerminalContent.Profile.Location}."),
                Line(TerminalContent.Profile.Summary),
                Line(string.Empty),
                Line(TerminalContent.Profile.About)
            ],
            "projects.txt" => Projects(),
            "experience.txt" => Experience(),
            "education.txt" => Education(),
            "skills.txt" => Stack(),
            "hosting.txt" => Hosting(),
            "contact.txt" => Contact(),
            _ => [Line($"cat: {fileName}: No such file", "error")]
        };
    }

    public static IReadOnlyList<OutputLine> Resume()
    {
        List<OutputLine> lines =
        [
            Line(TerminalContent.DisplayName.ToUpperInvariant(), "heading"),
            Line($"{TerminalContent.Profile.Role} / C# / Backend / Fintech"),
            Line(TerminalContent.Profile.Location),
            Line(string.Empty),
            Line("EXPERIENCE", "heading")
        ];

        lines.AddRange(TerminalContent.Resume.Select(entry =>
            Line($"{Period(entry),-13} {entry.Company,-14} {entry.Role}")));
        lines.Add(Line(string.Empty));
        lines.Add(Line("EDUCATION", "heading"));
        lines.AddRange(TerminalContent.Education.Select(entry =>
            Line($"{entry.Institution} / {entry.Qualification} / {entry.Result}")));
        lines.Add(Line(string.Empty));
        lines.Add(Line("SPECIALISMS", "heading"));
        lines.AddRange(TerminalContent.Stack.Select(group =>
            Line($"{group.Category,-15}{string.Join(", ", group.Technologies)}")));
        lines.Add(Line(string.Empty));
        lines.AddRange(TerminalContent.ContactLinks.Select(link => External(link.Name, link.Url)));
        return lines;
    }

    public static IReadOnlyList<OutputLine> Stack()
    {
        return
        [
            Line("TECHNOLOGY STACK", "heading"),
            .. TerminalContent.Stack.Select(group => Line($"{group.Category,-15}{string.Join(", ", group.Technologies)}"))
        ];
    }

    public static IReadOnlyList<OutputLine> Timeline()
    {
        List<OutputLine> lines = [Line("CAREER TIMELINE", "heading")];
        foreach (var item in TerminalContent.Timeline)
        {
            lines.Add(Line($"{item.When.Year}  [{item.Title}]  {item.Detail}"));
            if (item != TerminalContent.Timeline[^1])
                lines.Add(Line("         |"));
        }

        return lines;
    }

    public static IReadOnlyList<OutputLine> Projects()
    {
        List<OutputLine> lines = [Line("SELECTED PROJECTS", "heading")];
        foreach (var project in TerminalContent.Projects)
        {
            lines.Add(WebLink($"{project.Title}  [{string.Join(" + ", project.Stack)}]", $"/projects/{project.Slug}"));
            lines.Add(Line("  " + project.Summary));
            lines.Add(Line(string.Empty));
        }

        lines.Add(WebLink("Browse project case studies", "/projects"));
        lines.Add(External("View all public repositories", TerminalContent.GitHubUrl + "?tab=repositories"));
        return lines;
    }

    public static IReadOnlyList<OutputLine> ProjectDetail(Project project)
    {
        List<OutputLine> lines =
        [
            Line(project.Title.ToUpperInvariant(), "heading"),
            Line(string.Join(" + ", project.Stack)),
            Line(project.Summary),
            Line(string.Empty),
            Line("ARCHITECTURE", "heading")
        ];

        for (var i = 0; i < project.Architecture.Length; i++)
        {
            lines.Add(Line("  " + project.Architecture[i]));
            if (i < project.Architecture.Length - 1)
                lines.Add(Line("          |"));
        }

        lines.Add(Line(string.Empty));
        lines.Add(Line("HIGHLIGHTS", "heading"));
        lines.AddRange(project.Highlights.Select(highlight => Line("  + " + highlight)));
        lines.Add(Line(string.Empty));
        lines.Add(WebLink("Read the web case study", $"/projects/{project.Slug}"));
        if (project.ReleaseUrl is not null)
            lines.Add(External("Get it on JetBrains Marketplace", project.ReleaseUrl));
        lines.Add(External("View source on GitHub", project.Url));
        return lines;
    }

    public static IReadOnlyList<OutputLine> Contact()
    {
        return
        [
            Line("CONTACT", "heading"),
            .. TerminalContent.ContactLinks.Select(link => External($"{link.Name.ToLowerInvariant(),-10}{link.DisplayUrl}", link.Url))
        ];
    }

    private static IReadOnlyList<OutputLine> Experience()
    {
        return
        [
            Line("EXPERIENCE", "heading"),
            .. TerminalContent.Resume.Select(entry => Line($"{Period(entry),-13}{entry.Role,-28} / {entry.Company}"))
        ];
    }

    private static IReadOnlyList<OutputLine> Education()
    {
        return
        [
            Line("EDUCATION", "heading"),
            .. TerminalContent.Education.SelectMany(entry => new[]
            {
                Line(entry.Institution),
                Line($"{entry.Qualification} / {entry.Result}")
            })
        ];
    }

    private static IReadOnlyList<OutputLine> Hosting()
    {
        List<OutputLine> lines = [Line("HOSTING", "heading"), Line(TerminalContent.Hosting.Summary), Line(string.Empty)];
        for (var i = 0; i < TerminalContent.Hosting.Pipeline.Length; i++)
        {
            lines.Add(Line(TerminalContent.Hosting.Pipeline[i]));
            if (i < TerminalContent.Hosting.Pipeline.Length - 1)
                lines.Add(Line("      |"));
        }

        lines.Add(Line(string.Empty));
        lines.Add(Line(TerminalContent.Hosting.Runtime));
        return lines;
    }

    private static string Period(ResumeEntry entry)
    {
        return entry.To is null ? $"{entry.From.Year} - now" : entry.From.Year == entry.To.Value.Year ? entry.From.Year.ToString() : $"{entry.From.Year} - {entry.To.Value.Year}";
    }

    public static OutputLine Line(string text, string style = "") => new TextLine(text) { Style = style };
    public static OutputLine CommandLink(string text, string command) => new LinkLine(text, command, LinkKind.Command) { Style = "accent" };
    public static OutputLine WebLink(string text, string url) => new LinkLine(text, url, LinkKind.Web) { Style = "accent" };
    public static OutputLine External(string text, string url) => new LinkLine(text, url, LinkKind.Web, true) { Style = "accent" };
}
