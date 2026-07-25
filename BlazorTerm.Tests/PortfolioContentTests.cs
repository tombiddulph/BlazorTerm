namespace BlazorTerm.Tests;

public sealed class PortfolioContentTests
{
    [Fact]
    public void Projects_HaveUniqueSlugsAndValidLinks()
    {
        var projects = TerminalContent.Projects;

        Assert.NotEmpty(projects);
        Assert.Equal(projects.Length, projects.Select(project => project.Slug).Distinct().Count());
        Assert.All(projects, project =>
        {
            Assert.NotEmpty(project.Name);
            Assert.NotEmpty(project.Description);
            Assert.True(Uri.TryCreate(project.Url, UriKind.Absolute, out var url));
            Assert.Equal(Uri.UriSchemeHttps, url!.Scheme);
            Assert.NotEmpty(project.Architecture);
            Assert.NotEmpty(project.Highlights);
        });
    }

    [Fact]
    public void Contributions_AreLinkedAndHaveKnownStatuses()
    {
        Assert.NotEmpty(TerminalContent.Contributions);
        Assert.All(TerminalContent.Contributions, contribution =>
        {
            Assert.Contains(contribution.Status, new[] { "OPEN", "MERGED" });
            Assert.True(Uri.TryCreate(contribution.Url, UriKind.Absolute, out _));
        });
    }

    [Fact]
    public void PublicContent_DoesNotContainOriginalPlaceholders()
    {
        var content = TerminalContent.Files.Values.SelectMany(lines => lines);

        Assert.DoesNotContain(content, line => line.Contains("you@example.com", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(content, line => line.Contains("your-handle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HostingContent_DescribesThePublicDeploymentStack()
    {
        var hosting = TerminalContent.Files["hosting.txt"];

        Assert.Contains(hosting, line => line.Contains("Proxmox"));
        Assert.Contains(hosting, line => line.Contains("Talos Kubernetes"));
        Assert.Contains(hosting, line => line.Contains("Cloudflare Tunnel"));
        Assert.DoesNotContain(hosting, line => line.Contains("token", StringComparison.OrdinalIgnoreCase));
    }
}
