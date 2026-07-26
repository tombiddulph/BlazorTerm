namespace BlazorTerm;

public abstract record FsNode(string Name);

public sealed record DirectoryNode(string Name, IReadOnlyList<FsNode> Children) : FsNode(Name);

public sealed record FileNode(string Name, Func<CommandResult> Read) : FsNode(Name);

public sealed record PathResolution(FsNode Node, string Path);

public static class VirtualFileSystem
{
    public static readonly DirectoryNode Root = BuildTree();

    public static bool TryResolve(string currentPath, string requestedPath, out PathResolution resolution)
    {
        var segments = Normalize(currentPath, requestedPath, out var validAbsolutePath);
        if (!validAbsolutePath)
        {
            resolution = null!;
            return false;
        }

        FsNode node = Root;
        foreach (var segment in segments)
        {
            if (node is not DirectoryNode directory)
            {
                resolution = null!;
                return false;
            }

            node = directory.Children.FirstOrDefault(child => child.Name.Equals(segment, StringComparison.OrdinalIgnoreCase))!;
            if (node is null)
            {
                resolution = null!;
                return false;
            }
        }

        resolution = new(node, ToDisplayPath(segments));
        return true;
    }

    public static IReadOnlyList<string> Completions(string currentPath, bool directoriesOnly = false)
    {
        if (!TryResolve(currentPath, ".", out var current) || current.Node is not DirectoryNode directory)
            return [];

        List<string> completions = [];
        AddCompletions(directory, string.Empty, directoriesOnly, completions);
        return completions;
    }

    public static string ToAbsolutePath(string displayPath)
    {
        var suffix = displayPath == "~" ? string.Empty : displayPath[1..];
        return $"/home/{TerminalContent.Owner}{suffix}";
    }

    private static List<string> Normalize(string currentPath, string requestedPath, out bool validAbsolutePath)
    {
        validAbsolutePath = true;
        var requested = string.IsNullOrWhiteSpace(requestedPath) ? "~" : requestedPath.Trim();
        List<string> segments;

        if (requested.StartsWith('~'))
        {
            segments = [];
            requested = requested[1..].TrimStart('/');
        }
        else if (requested.StartsWith('/'))
        {
            segments = [];
            requested = requested.TrimStart('/');
            var absolute = requested.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (absolute.Count > 0 && absolute[0].Equals("home", StringComparison.OrdinalIgnoreCase))
            {
                if (absolute.Count < 2 || !absolute[1].Equals(TerminalContent.Owner, StringComparison.OrdinalIgnoreCase))
                {
                    validAbsolutePath = false;
                    return [];
                }
                absolute.RemoveRange(0, 2);
            }
            requested = string.Join('/', absolute);
        }
        else
        {
            segments = currentPath.TrimStart('~', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        foreach (var segment in requested.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
                continue;
            if (segment == "..")
            {
                if (segments.Count > 0)
                    segments.RemoveAt(segments.Count - 1);
                continue;
            }
            segments.Add(segment);
        }

        return segments;
    }

    private static string ToDisplayPath(IReadOnlyList<string> segments) =>
        segments.Count == 0 ? "~" : "~/" + string.Join('/', segments);

    private static void AddCompletions(
        DirectoryNode directory,
        string prefix,
        bool directoriesOnly,
        ICollection<string> completions)
    {
        foreach (var child in directory.Children.OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase))
        {
            var path = prefix + child.Name;
            if (child is DirectoryNode childDirectory)
            {
                completions.Add(path + "/");
                AddCompletions(childDirectory, path + "/", directoriesOnly, completions);
            }
            else if (!directoriesOnly)
            {
                completions.Add(path);
            }
        }
    }

    private static DirectoryNode BuildTree()
    {
        var stackFiles = TerminalContent.Stack
            .Select(group => (FsNode)new FileNode(
                Slug(group.Category) + ".txt",
                () => new CommandResult(
                [
                    new TextLine(group.Category.ToUpperInvariant()) { Style = "heading" },
                    new TextLine(string.Join(", ", group.Technologies))
                ])))
            .ToArray();

        var projectDirectories = TerminalContent.Projects
            .Select(project => (FsNode)new DirectoryNode(
                project.Slug,
                [new FileNode("README.md", () => new(TerminalFormatter.ProjectDetail(project)))]))
            .ToArray();

        return new("~",
        [
            new FileNode("about.txt", () => new(TerminalFormatter.ReadFile("about.txt"))),
            new FileNode("resume.md", () => new(TerminalFormatter.Resume())),
            new FileNode("contact.txt", () => new(TerminalFormatter.Contact())),
            new DirectoryNode("stack", stackFiles),
            new DirectoryNode("projects", projectDirectories)
        ]);
    }

    private static string Slug(string value) => value.ToLowerInvariant().Replace(' ', '-');
}
