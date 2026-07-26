using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace BlazorTerm;

public static class BuildInformation
{
    public static string GitSha
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("BLAZORTERM_GIT_SHA");
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;
            var informational = typeof(BuildInformation).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return informational?.Split('+').ElementAtOrDefault(1) ?? "development";
        }
    }

    public static string BuildTimestamp => Environment.GetEnvironmentVariable("BLAZORTERM_BUILD_TIMESTAMP") ?? "development";
    public static string Framework => RuntimeInformation.FrameworkDescription;
}

public static class CareerGit
{
    public static IReadOnlyList<OutputLine> Execute(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
            return [Error("git: missing command")];

        return arguments[0].ToLowerInvariant() switch
        {
            "log" => Log(arguments.Skip(1).ToArray()),
            "show" => Show(arguments.Skip(1).ToArray()),
            "blame" => Blame(arguments.Skip(1).ToArray()),
            _ => [Error($"git: '{arguments[0]}' is not a supported git command")]
        };
    }

    private static IReadOnlyList<OutputLine> Log(IReadOnlyList<string> arguments)
    {
        if (arguments.Any(argument => argument != "--oneline"))
            return [Error("git log: only --oneline is supported")];

        var oneline = arguments.Contains("--oneline");
        List<OutputLine> lines = [];
        foreach (var entry in TerminalContent.Resume)
        {
            var sha = Sha(entry);
            if (oneline)
            {
                lines.Add(new LinkLine($"{sha} {entry.Role} at {entry.Company}", $"git show {sha}", LinkKind.Command) { Style = "accent" });
            }
            else
            {
                lines.Add(new TextLine($"commit {sha}") { Style = "heading" });
                lines.Add(new TextLine($"Date:   {entry.From:yyyy-MM-dd}"));
                lines.Add(new TextLine($"    Joined {entry.Company} as {entry.Role}"));
                lines.Add(new TextLine(string.Empty));
            }
        }
        return lines;
    }

    private static IReadOnlyList<OutputLine> Show(IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 1)
            return [Error("usage: git show <sha>")];
        var entry = TerminalContent.Resume.FirstOrDefault(item => Sha(item).StartsWith(arguments[0], StringComparison.OrdinalIgnoreCase));
        if (entry is null)
            return [Error($"fatal: bad object {arguments[0]}")];

        List<OutputLine> lines =
        [
            new TextLine($"commit {Sha(entry)}") { Style = "heading" },
            new TextLine($"Date:   {entry.From:yyyy-MM-dd}"),
            new TextLine(string.Empty),
            new TextLine($"    {entry.Role} at {entry.Company}")
        ];
        lines.AddRange(entry.Highlights.Select(highlight => new TextLine("    + " + highlight)));
        return lines;
    }

    private static IReadOnlyList<OutputLine> Blame(IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 1)
            return [Error("usage: git blame stack/<file>.txt")];
        var category = Path.GetFileNameWithoutExtension(arguments[0]);
        var group = TerminalContent.Stack.FirstOrDefault(item => Slug(item.Category).Equals(category, StringComparison.OrdinalIgnoreCase));
        if (group is null)
            return [Error($"fatal: no such path '{arguments[0]}'")];

        var firstYear = TerminalContent.Resume.Min(entry => entry.From.Year);
        return group.Technologies.Select((technology, index) =>
        {
            var year = firstYear + Math.Min(index, DateTimeOffset.Now.Year - firstYear);
            var sha = ShortSha($"{group.Category}:{technology}");
            return (OutputLine)new TextLine($"{sha} ({TerminalContent.DisplayName} {year}-01-01) {technology}");
        }).ToArray();
    }

    private static string Sha(ResumeEntry entry) => ShortSha($"{entry.Company}:{entry.Role}:{entry.From:O}");
    private static string ShortSha(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..7].ToLowerInvariant();
    private static string Slug(string value) => value.ToLowerInvariant().Replace(' ', '-');
    private static OutputLine Error(string text) => new TextLine(text) { Style = "error" };
}
