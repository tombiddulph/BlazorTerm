namespace BlazorTerm.Tests;

public sealed class PortfolioSystemCommandTests
{
    [Fact]
    public void GitLog_UsesResumeEntriesAsCommits()
    {
        var lines = CareerGit.Execute(["log", "--oneline"]);

        Assert.Equal(TerminalContent.Resume.Length, lines.Count);
        Assert.All(lines, line => Assert.IsType<LinkLine>(line));
        Assert.All(TerminalContent.Resume, entry =>
            Assert.Contains(lines, line => line.ToPlainText().Contains(entry.Company)));
    }

    [Fact]
    public void GitShow_ResolvesTheGeneratedSha()
    {
        var log = CareerGit.Execute(["log", "--oneline"]);
        var first = Assert.IsType<LinkLine>(log[0]);
        var sha = first.Target.Split(' ').Last();

        var shown = CareerGit.Execute(["show", sha]);

        Assert.Contains(shown, line => line.ToPlainText().Contains(TerminalContent.Resume[0].Company));
    }

    [Fact]
    public void GitBlame_UsesStackContent()
    {
        var lines = CareerGit.Execute(["blame", "stack/languages.txt"]);

        Assert.All(TerminalContent.Stack.Single(group => group.Category == "Languages").Technologies,
            technology => Assert.Contains(lines, line => line.ToPlainText().EndsWith(technology)));
    }

    [Fact]
    public void BuildInformation_ReportsTheRuntime()
    {
        Assert.Contains(".NET", BuildInformation.Framework);
        Assert.NotEmpty(BuildInformation.GitSha);
        Assert.NotEmpty(BuildInformation.BuildTimestamp);
    }
}
