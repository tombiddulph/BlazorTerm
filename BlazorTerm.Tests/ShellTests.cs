namespace BlazorTerm.Tests;

public sealed class ShellTests
{
    [Fact]
    public void Parser_SplitsPipelinesAndPreservesQuotedArguments()
    {
        var parsed = ShellParser.TryParse("resume | grep -i \"azure service bus\" | wc -l", out var segments, out var error);

        Assert.True(parsed, error);
        Assert.Collection(
            segments,
            segment =>
            {
                Assert.Equal("resume", segment.Name);
                Assert.Empty(segment.Arguments);
            },
            segment =>
            {
                Assert.Equal("grep", segment.Name);
                Assert.Equal(["-i", "azure service bus"], segment.Arguments);
            },
            segment =>
            {
                Assert.Equal("wc", segment.Name);
                Assert.Equal(["-l"], segment.Arguments);
            });
    }

    [Fact]
    public void Parser_HandlesSingleQuotesAndBackslashEscapes()
    {
        var parsed = ShellParser.TryParse("cat 'contact file' foo\\|bar \\\"quoted\\\"", out var segments, out var error);

        Assert.True(parsed, error);
        var segment = Assert.Single(segments);
        Assert.Equal("cat", segment.Name);
        Assert.Equal(["contact file", "foo|bar", "\"quoted\""], segment.Arguments);
    }

    [Theory]
    [InlineData("| resume", "unexpected token")]
    [InlineData("resume || wc", "unexpected token")]
    [InlineData("resume |", "expected command")]
    [InlineData("resume |   ", "expected command")]
    [InlineData("grep 'azure", "unterminated")]
    [InlineData("grep azure\\", "trailing escape")]
    public void Parser_RejectsInvalidInput(string input, string expectedError)
    {
        var parsed = ShellParser.TryParse(input, out var segments, out var error);

        Assert.False(parsed);
        Assert.Empty(segments);
        Assert.Contains(expectedError, error);
    }

    [Fact]
    public void Grep_PreservesRichLinesAndHighlightsMatches()
    {
        var link = new LinkLine("Azure project", "/projects/azure", LinkKind.Web) { Style = "accent" };
        var result = Execute(
            [link, new TextLine("C# and .NET"), new TextLine("azure messaging")],
            new CommandSegment("grep", ["-i", "azure"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(2, result.Lines.Count);
        var matchedLink = Assert.IsType<LinkLine>(result.Lines[0]);
        Assert.Equal(link.Target, matchedLink.Target);
        Assert.Equal(link.Style, matchedLink.Style);
        Assert.Equal(new TextRange(0, 5), Assert.Single(matchedLink.Highlights));
        Assert.Equal(new TextRange(0, 5), Assert.Single(result.Lines[1].Highlights));
    }

    [Fact]
    public void Grep_InvertsMatches()
    {
        var result = Execute(
            [new TextLine("Azure"), new TextLine(".NET")],
            new CommandSegment("grep", ["-iv", "azure"]));

        Assert.Equal(".NET", Assert.Single(result.Lines).ToPlainText());
    }

    [Fact]
    public void Grep_HighlightsAHelpDescriptionWithoutFlatteningItsCommand()
    {
        var help = new HelpLine("resume", "Terminal-formatted Azure CV", "resume", LinkKind.Command);

        var result = Execute([help], new CommandSegment("grep", ["Azure"]));

        var matched = Assert.IsType<HelpLine>(Assert.Single(result.Lines));
        Assert.Equal(LinkKind.Command, matched.Kind);
        Assert.Equal("resume", matched.Target);
        Assert.Equal(new TextRange(19, 5), Assert.Single(matched.DescriptionHighlights));
    }

    [Fact]
    public void HeadAndTail_SelectRequestedLines()
    {
        OutputLine[] input = [new TextLine("one"), new TextLine("two"), new TextLine("three")];

        var head = Execute(input, new CommandSegment("head", ["-n", "2"]));
        var tail = Execute(input, new CommandSegment("tail", ["-n1"]));

        Assert.Equal(["one", "two"], PlainText(head));
        Assert.Equal(["three"], PlainText(tail));
    }

    [Fact]
    public void SortAndUniqComposeLeftToRight()
    {
        var result = Execute(
            [new TextLine("beta"), new TextLine("alpha"), new TextLine("beta")],
            new CommandSegment("sort", []),
            new CommandSegment("uniq", []));

        Assert.Equal(["alpha", "beta"], PlainText(result));
    }

    [Fact]
    public void ThreeStagePipeline_ThreadsResults()
    {
        var result = Execute(
            [new TextLine("Azure"), new TextLine(".NET"), new TextLine("Azure Functions")],
            new CommandSegment("grep", ["-i", "azure"]),
            new CommandSegment("wc", ["-l"]));

        Assert.Equal("2", Assert.Single(result.Lines).ToPlainText());
    }

    [Fact]
    public void Pipeline_RejectsACommandInFilterPosition()
    {
        var executor = new PipelineExecutor();
        CommandSegment[] segments = [new("resume", []), new("resume", [])];

        var result = executor.Execute(
            segments,
            name => name == "resume" ? new DelegateCommand(_ => new([new TextLine("resume")])) : null);

        Assert.Equal(127, result.ExitCode);
        Assert.Equal("resume: not a filter", Assert.Single(result.Lines).ToPlainText());
    }

    [Fact]
    public void Pipeline_AbortsAfterANonZeroResult()
    {
        var executor = new PipelineExecutor();
        CommandSegment[] segments = [new("source", []), new("grep", ["missing"]), new("wc", ["-l"])];

        var result = executor.Execute(
            segments,
            name => name == "source" ? new DelegateCommand(_ => new([new TextLine("present")])) : null);

        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Formatters_ExposeStablePlainTextProjections()
    {
        var resume = TerminalFormatter.Resume().Select(line => line.ToPlainText()).ToArray();
        var contact = TerminalFormatter.Contact().Select(line => line.ToPlainText()).ToArray();

        Assert.Equal(TerminalContent.DisplayName.ToUpperInvariant(), resume[0]);
        Assert.Contains("EXPERIENCE", resume);
        Assert.Contains(contact, line => line.Contains("github", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(resume.Concat(contact), line => line.Contains('<') || line.Contains('>'));
    }

    private static CommandResult Execute(IReadOnlyList<OutputLine> input, params CommandSegment[] filters)
    {
        var executor = new PipelineExecutor();
        var segments = new[] { new CommandSegment("source", []) }.Concat(filters).ToArray();
        return executor.Execute(segments, name => name == "source" ? new DelegateCommand(_ => new(input)) : null);
    }

    private static string[] PlainText(CommandResult result) => result.Lines.Select(line => line.ToPlainText()).ToArray();
}
