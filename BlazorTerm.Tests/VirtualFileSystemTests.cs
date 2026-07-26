namespace BlazorTerm.Tests;

public sealed class VirtualFileSystemTests
{
    [Theory]
    [InlineData("~", "projects", "~/projects")]
    [InlineData("~/projects", "..", "~")]
    [InlineData("~/projects", "./service-bus-explorer", "~/projects/service-bus-explorer")]
    [InlineData("~", "/home/tom/projects/property-resolvers", "~/projects/property-resolvers")]
    [InlineData("~/stack", "~/projects", "~/projects")]
    public void Resolve_HandlesRelativeAndAbsolutePaths(string current, string requested, string expected)
    {
        var resolved = VirtualFileSystem.TryResolve(current, requested, out var resolution);

        Assert.True(resolved);
        Assert.Equal(expected, resolution.Path);
    }

    [Fact]
    public void Resolve_RejectsUnknownUsersAndMissingNodes()
    {
        Assert.False(VirtualFileSystem.TryResolve("~", "/home/other/projects", out _));
        Assert.False(VirtualFileSystem.TryResolve("~", "missing/file.txt", out _));
    }

    [Fact]
    public void ProjectReadmesUseTheSharedContentModel()
    {
        foreach (var project in TerminalContent.Projects)
        {
            Assert.True(VirtualFileSystem.TryResolve("~", $"projects/{project.Slug}/README.md", out var resolution));
            var file = Assert.IsType<FileNode>(resolution.Node);
            Assert.Contains(file.Read().Lines, line => line.ToPlainText().Contains(project.Title, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void CompletionsTraversePathSegments()
    {
        var paths = VirtualFileSystem.Completions("~");
        var directories = VirtualFileSystem.Completions("~", directoriesOnly: true);

        Assert.Contains("projects/service-bus-explorer/README.md", paths);
        Assert.Contains("projects/service-bus-explorer/", directories);
        Assert.DoesNotContain("projects/service-bus-explorer/README.md", directories);
    }

    [Fact]
    public void AbsolutePathMapsToTheVirtualHome()
    {
        Assert.Equal("/home/tom", VirtualFileSystem.ToAbsolutePath("~"));
        Assert.Equal("/home/tom/projects", VirtualFileSystem.ToAbsolutePath("~/projects"));
    }
}
