using System.Text;

namespace BlazorTerm;

public static class PlainTextPortfolioFormatter
{
    private const string Reset = "\u001b[0m";
    private const string Heading = "\u001b[1;32m";
    private const string Accent = "\u001b[36m";

    public static string AnsiResume()
    {
        var text = new StringBuilder();
        text.AppendLine($"{Heading}{TerminalContent.Profile.Name}{Reset}");
        text.AppendLine($"{TerminalContent.Profile.Role} | C# | .NET | Backend | Fintech");
        text.AppendLine(TerminalContent.Profile.Location);
        text.AppendLine();
        text.AppendLine(TerminalContent.Profile.Summary);
        text.AppendLine();
        text.AppendLine($"{Heading}EXPERIENCE{Reset}");

        foreach (var entry in TerminalContent.Resume)
        {
            text.AppendLine($"{Period(entry),-11} {Accent}{entry.Company}{Reset} | {entry.Role}");
            foreach (var highlight in entry.Highlights)
                text.AppendLine($"            - {highlight}");
        }

        text.AppendLine();
        text.AppendLine($"{Heading}SELECTED PROJECTS{Reset}");
        foreach (var project in TerminalContent.Projects)
        {
            text.AppendLine($"{Accent}{project.Title}{Reset} | {string.Join(", ", project.Stack)}");
            text.AppendLine($"  {project.Summary}");
            text.AppendLine($"  {TerminalContent.SiteUrl}/projects/{project.Slug}");
        }

        text.AppendLine();
        text.AppendLine($"{Heading}CONTACT{Reset}");
        foreach (var link in TerminalContent.ContactLinks)
            text.AppendLine($"{link.Name,-10} {link.Url}");

        return text.ToString();
    }

    public static string LlmsText()
    {
        var text = new StringBuilder();
        text.AppendLine($"# {TerminalContent.Profile.Name}");
        text.AppendLine();
        text.AppendLine($"> {TerminalContent.Profile.Role} in {TerminalContent.Profile.Location}");
        text.AppendLine();
        text.AppendLine(TerminalContent.Profile.Summary);
        text.AppendLine();
        text.AppendLine("## Experience");
        text.AppendLine();

        foreach (var entry in TerminalContent.Resume)
        {
            text.AppendLine($"### {entry.Role}, {entry.Company} ({Period(entry)})");
            foreach (var highlight in entry.Highlights)
                text.AppendLine($"- {highlight}");
            text.AppendLine();
        }

        text.AppendLine("## Technology Stack");
        text.AppendLine();
        foreach (var group in TerminalContent.Stack)
            text.AppendLine($"- **{group.Category}:** {string.Join(", ", group.Technologies)}");

        text.AppendLine();
        text.AppendLine("## Selected Projects");
        text.AppendLine();
        foreach (var project in TerminalContent.Projects)
        {
            text.AppendLine($"### [{project.Title}]({TerminalContent.SiteUrl}/projects/{project.Slug})");
            text.AppendLine(project.Summary);
            text.AppendLine();
            text.AppendLine(project.CaseStudy);
            text.AppendLine();
            text.AppendLine($"Stack: {string.Join(", ", project.Stack)}");
            text.AppendLine();
        }

        text.AppendLine("## Contact");
        text.AppendLine();
        foreach (var link in TerminalContent.ContactLinks)
            text.AppendLine($"- [{link.Name}]({link.Url})");

        text.AppendLine();
        text.AppendLine($"Canonical site: {TerminalContent.SiteUrl}");
        return text.ToString();
    }

    private static string Period(ResumeEntry entry)
    {
        return entry.To is null
            ? $"{entry.From.Year}-present"
            : entry.From.Year == entry.To.Value.Year
                ? entry.From.Year.ToString()
                : $"{entry.From.Year}-{entry.To.Value.Year}";
    }
}
