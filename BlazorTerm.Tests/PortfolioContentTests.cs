using System.Reflection;
using System.Text.Json;
using BlazorTerm.Components.Pages;
using Microsoft.AspNetCore.Components;

namespace BlazorTerm.Tests;

public sealed class PortfolioContentTests
{
    [Fact]
    public void Projects_HaveUniqueSlugsAndValidLinks()
    {
        var projects = TerminalContent.Projects;

        Assert.NotEmpty(projects);
        Assert.Equal(projects.Length, projects.Select(project => project.Slug).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(projects, project =>
        {
            Assert.NotEmpty(project.Title);
            Assert.NotEmpty(project.Summary);
            Assert.True(Uri.TryCreate(project.Url, UriKind.Absolute, out var url));
            Assert.Equal(Uri.UriSchemeHttps, url!.Scheme);
            Assert.NotEmpty(project.Stack);
            Assert.NotEmpty(project.CaseStudy);
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
        var content = TerminalFormatter.FileNames
            .SelectMany(TerminalFormatter.ReadFile)
            .Select(line => line.Text);

        Assert.DoesNotContain(content, line => line.Contains("you@example.com", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(content, line => line.Contains("your-handle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HostingContent_DescribesThePublicDeploymentStack()
    {
        var hosting = TerminalFormatter.ReadFile("hosting.txt").Select(line => line.Text);

        Assert.Contains(hosting, line => line.Contains("Proxmox"));
        Assert.Contains(hosting, line => line.Contains("Talos Kubernetes"));
        Assert.Contains(hosting, line => line.Contains("Cloudflare Tunnel"));
        Assert.DoesNotContain(hosting, line => line.Contains("token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ResumeAndTimeline_AreStructuredAndComplete()
    {
        Assert.Equal(5, TerminalContent.Resume.Length);
        Assert.Equal(TerminalContent.Resume.Length, TerminalContent.Timeline.Length);
        Assert.Equal(
            TerminalContent.Resume.Reverse().Select(entry => (entry.From, entry.Company, entry.Role)),
            TerminalContent.Timeline.Select(item => (item.When, item.Title, item.Detail)));
        Assert.All(TerminalContent.Resume, entry =>
        {
            Assert.NotEmpty(entry.Company);
            Assert.NotEmpty(entry.Role);
            Assert.True(entry.To is null || entry.To >= entry.From);
        });
    }

    [Fact]
    public void Stack_HasUniquePopulatedCategories()
    {
        Assert.Equal(
            TerminalContent.Stack.Length,
            TerminalContent.Stack.Select(group => group.Category).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(TerminalContent.Stack, group => Assert.NotEmpty(group.Technologies));
    }

    [Fact]
    public void TerminalSession_IsPersistentAndSerializable()
    {
        var persistedSession = typeof(Home).GetProperty(nameof(Home.PersistedSession));
        Assert.NotNull(persistedSession?.GetCustomAttribute<PersistentStateAttribute>());

        var session = new Home.TerminalSession
        {
            Input = "projects",
            Theme = "theme-amber",
            Entries =
            [
                new Home.HistoryEntry(
                    "resume",
                    "~",
                    [TerminalFormatter.Line("TOM BIDDULPH", "heading")])
            ],
            CommandHistory = ["help", "resume"]
        };

        var restored = JsonSerializer.Deserialize<Home.TerminalSession>(JsonSerializer.Serialize(session));

        Assert.NotNull(restored);
        Assert.Equal(session.Input, restored.Input);
        Assert.Equal(session.Theme, restored.Theme);
        Assert.Equal(session.CommandHistory, restored.CommandHistory);
        Assert.Single(restored.Entries);
        Assert.Equal(session.Entries[0].Command, restored.Entries[0].Command);
        Assert.Equal(session.Entries[0].Path, restored.Entries[0].Path);
        Assert.Equal(session.Entries[0].Lines, restored.Entries[0].Lines);
    }

    [Fact]
    public void TextFormatters_UseStructuredContent()
    {
        var ansi = PlainTextPortfolioFormatter.AnsiResume();
        var llms = PlainTextPortfolioFormatter.LlmsText();

        Assert.All(TerminalContent.Resume, entry =>
        {
            Assert.Contains(entry.Company, ansi);
            Assert.Contains(entry.Company, llms);
        });
        Assert.All(TerminalContent.Projects, project =>
        {
            Assert.Contains(project.Title, ansi);
            Assert.Contains(project.Title, llms);
        });
    }
}
