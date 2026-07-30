using System.Diagnostics;

namespace BlazorTerm.Tests;

public sealed class CommandTracingTests
{
    [Fact]
    public async Task Collector_RemainsActiveAcrossAwaitedCommandWork()
    {
        using var collector = new CommandTraceCollector();
        using var rootScope = TerminalActivities.StartRoot("command.async");
        var root = Assert.IsType<Activity>(rootScope.Activity);
        collector.Capture(root.TraceId, root.SpanId);

        await Task.Yield();
        using (TerminalActivities.Source.StartActivity("async.completed"))
            await Task.Delay(1);

        rootScope.Stop();
        Assert.Contains(collector.Snapshot(), snapshot => snapshot.Name == "async.completed");
    }

    [Fact]
    public void Collector_CapturesOnlyTheSelectedTrace()
    {
        using var collector = new CommandTraceCollector();
        using var rootScope = TerminalActivities.StartRoot("command.resume");
        var root = rootScope.Activity;
        Assert.NotNull(root);
        collector.Capture(root.TraceId, root.SpanId);

        using (var child = TerminalActivities.Source.StartActivity("content.load"))
            child?.SetTag("content.command", "resume");

        using (var unrelatedScope = TerminalActivities.StartRoot("command.unrelated"))
        {
        }

        rootScope.Stop();
        var snapshots = collector.Snapshot();

        Assert.Equal(2, snapshots.Count);
        Assert.Contains(snapshots, snapshot => snapshot.Name == "command.resume");
        var content = Assert.Single(snapshots, snapshot => snapshot.Name == "content.load");
        Assert.Equal("resume", content.Attributes["content.command"]);
    }

    [Fact]
    public void Collector_CapsSpansAndRetainsTheRoot()
    {
        using var collector = new CommandTraceCollector(capacity: 2);
        using var rootScope = TerminalActivities.StartRoot("command.resume");
        var root = rootScope.Activity;
        Assert.NotNull(root);
        collector.Capture(root.TraceId, root.SpanId);

        for (var index = 0; index < 3; index++)
        {
            using var child = TerminalActivities.Source.StartActivity($"child.{index}");
        }

        rootScope.Stop();
        var snapshots = collector.Snapshot();

        Assert.Equal(2, snapshots.Count);
        Assert.Equal(2, collector.DiscardedCount);
        Assert.Contains(snapshots, snapshot => snapshot.SpanId == root.SpanId);
    }

    [Fact]
    public void Formatter_BuildsTreeBarsAttributesAndErrorSummary()
    {
        var traceId = ActivityTraceId.CreateFromString("0123456789abcdef0123456789abcdef".AsSpan());
        var rootId = ActivitySpanId.CreateFromString("0123456789abcdef".AsSpan());
        var childId = ActivitySpanId.CreateFromString("fedcba9876543210".AsSpan());
        var started = DateTimeOffset.Parse("2026-07-26T12:00:00Z");
        ActivitySnapshot[] snapshots =
        [
            new(traceId, rootId, default, "command.resume", started, TimeSpan.FromMilliseconds(10), ActivityStatusCode.Ok, string.Empty,
                new Dictionary<string, string> { ["command.name"] = "resume" }),
            new(traceId, childId, rootId, "content.load", started.AddMilliseconds(2), TimeSpan.FromMilliseconds(4), ActivityStatusCode.Error, "failed",
                new Dictionary<string, string> { ["content.command"] = "resume" })
        ];

        var result = TraceOutputFormatter.Format(snapshots, verbose: true, discardedCount: 0, exporterEnabled: true);

        Assert.Contains(result.Lines, line => line.ToPlainText().Contains("TRACE  0123456789abcdef"));
        Assert.Contains(result.Lines, line => line is TraceLine { Label: "|- content.load" });
        Assert.Contains(result.Lines, line => line.ToPlainText().Contains("content.command = resume"));
        Assert.Contains(result.Lines, line => line.ToPlainText() == "2 spans / 1 errors / exporter: OTLP -> collector");
    }
}
