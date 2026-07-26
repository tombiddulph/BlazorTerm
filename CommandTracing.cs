using System.Diagnostics;

namespace BlazorTerm;

public static class TerminalActivities
{
    public const string SourceName = "BlazorTerm.Commands";
    public static readonly ActivitySource Source = new(SourceName);

    public static RootActivityScope StartRoot(string name) => new(name);
}

public sealed class RootActivityScope : IDisposable
{
    private readonly Activity? _previous;
    private bool _stopped;

    internal RootActivityScope(string name)
    {
        _previous = Activity.Current;
        Activity.Current = null;
        Activity = TerminalActivities.Source.StartActivity(name, ActivityKind.Internal);
        if (Activity is null)
            Activity.Current = _previous;
    }

    public Activity? Activity { get; }

    public void Stop()
    {
        if (_stopped)
            return;
        _stopped = true;
        Activity?.Stop();
        Activity.Current = _previous;
    }

    public void Dispose()
    {
        Stop();
        Activity?.Dispose();
    }
}

public sealed record ActivitySnapshot(
    ActivityTraceId TraceId,
    ActivitySpanId SpanId,
    ActivitySpanId ParentSpanId,
    string Name,
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    ActivityStatusCode Status,
    string StatusDescription,
    IReadOnlyDictionary<string, string> Attributes);

public sealed class CommandTraceCollector : IDisposable
{
    public const int DefaultCapacity = 200;

    private readonly Lock _lock = new();
    private readonly ActivityListener _listener;
    private readonly List<ActivitySnapshot> _snapshots = [];
    private readonly int _capacity;
    private ActivityTraceId _traceId;
    private ActivitySpanId _rootSpanId;
    private bool _hasTraceId;
    private int _discarded;

    public CommandTraceCollector(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
        _listener = new()
        {
            ShouldListenTo = source => source.Name == TerminalActivities.SourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = OnActivityStopped
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public int DiscardedCount
    {
        get
        {
            lock (_lock)
                return _discarded;
        }
    }

    public void Capture(ActivityTraceId traceId, ActivitySpanId rootSpanId)
    {
        lock (_lock)
        {
            _traceId = traceId;
            _rootSpanId = rootSpanId;
            _hasTraceId = true;
        }
    }

    public IReadOnlyList<ActivitySnapshot> Snapshot()
    {
        lock (_lock)
            return _snapshots.ToArray();
    }

    public void Dispose() => _listener.Dispose();

    private void OnActivityStopped(Activity activity)
    {
        lock (_lock)
        {
            if (!_hasTraceId || activity.TraceId != _traceId)
                return;
            if (_snapshots.Count >= _capacity)
            {
                if (activity.SpanId == _rootSpanId)
                {
                    _snapshots.RemoveAt(_snapshots.Count - 1);
                    _discarded++;
                }
                else
                {
                    _discarded++;
                    return;
                }
            }

            _snapshots.Add(new(
                activity.TraceId,
                activity.SpanId,
                activity.ParentSpanId,
                activity.DisplayName,
                activity.StartTimeUtc,
                activity.Duration,
                activity.Status,
                activity.StatusDescription ?? string.Empty,
                activity.TagObjects.GroupBy(tag => tag.Key, StringComparer.Ordinal).ToDictionary(
                    group => group.Key,
                    group => Convert.ToString(group.Last().Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                    StringComparer.Ordinal)));
        }
    }
}

public static class TraceOutputFormatter
{
    private const int BarWidth = 24;

    public static CommandResult Format(
        IReadOnlyList<ActivitySnapshot> snapshots,
        bool verbose,
        int discardedCount,
        bool exporterEnabled)
    {
        var root = snapshots.FirstOrDefault(snapshot => snapshot.ParentSpanId == default)
            ?? snapshots.OrderBy(snapshot => snapshot.StartedAt).FirstOrDefault();
        if (root is null)
            return new([new TextLine("trace: no activities were recorded") { Style = "error" }], 1);

        var children = snapshots
            .Where(snapshot => snapshot.SpanId != root.SpanId)
            .GroupBy(snapshot => snapshot.ParentSpanId)
            .ToDictionary(group => group.Key, group => group.OrderBy(snapshot => snapshot.StartedAt).ToArray());
        var ordered = Flatten(root, children).ToArray();
        var totalDuration = root.Duration <= TimeSpan.Zero ? TimeSpan.FromTicks(1) : root.Duration;

        List<OutputLine> lines =
        [
            new TextLine($"TRACE  {root.TraceId.ToHexString()[..16]}  total {FormatDuration(root.Duration)}") { Style = "heading" },
            new TextLine(string.Empty)
        ];

        foreach (var (snapshot, depth) in ordered)
        {
            var offsetRatio = Math.Clamp((snapshot.StartedAt - root.StartedAt).TotalMilliseconds / totalDuration.TotalMilliseconds, 0, 1);
            var durationRatio = Math.Clamp(snapshot.Duration.TotalMilliseconds / totalDuration.TotalMilliseconds, 0, 1);
            var offset = Math.Min(BarWidth - 1, (int)Math.Floor(offsetRatio * BarWidth));
            var width = Math.Max(1, (int)Math.Ceiling(durationRatio * BarWidth));
            width = Math.Min(width, BarWidth - offset);
            var bar = new string(' ', offset) + new string('#', width) + new string(' ', BarWidth - offset - width);
            var label = depth == 0 ? snapshot.Name : new string(' ', (depth - 1) * 2) + "|- " + snapshot.Name;
            var status = snapshot.Status == ActivityStatusCode.Unset ? string.Empty : $" [{snapshot.Status.ToString().ToUpperInvariant()}]";
            lines.Add(new TraceLine(label, $"|{bar}|", FormatDuration(snapshot.Duration) + status)
            {
                Style = snapshot.Status == ActivityStatusCode.Error ? "trace-line error" : "trace-line"
            });

            if (verbose)
            {
                foreach (var attribute in snapshot.Attributes.OrderBy(attribute => attribute.Key, StringComparer.Ordinal))
                    lines.Add(new TextLine($"{new string(' ', depth * 2 + 3)}{attribute.Key} = {attribute.Value}") { Style = "trace-attribute" });
                if (!string.IsNullOrEmpty(snapshot.StatusDescription))
                    lines.Add(new TextLine($"{new string(' ', depth * 2 + 3)}status.description = {snapshot.StatusDescription}") { Style = "trace-attribute error" });
            }
        }

        var errors = snapshots.Count(snapshot => snapshot.Status == ActivityStatusCode.Error);
        lines.Add(new TextLine(string.Empty));
        lines.Add(new TextLine($"{snapshots.Count} spans / {errors} errors / exporter: {(exporterEnabled ? "OTLP -> collector" : "disabled")}") { Style = "trace-summary" });
        if (discardedCount > 0)
            lines.Add(new TextLine($"trace: {discardedCount} spans discarded after the {CommandTraceCollector.DefaultCapacity}-span limit") { Style = "error" });

        return new(lines);
    }

    private static IEnumerable<(ActivitySnapshot Snapshot, int Depth)> Flatten(
        ActivitySnapshot current,
        IReadOnlyDictionary<ActivitySpanId, ActivitySnapshot[]> children,
        int depth = 0)
    {
        yield return (current, depth);
        if (!children.TryGetValue(current.SpanId, out var descendants))
            yield break;
        foreach (var child in descendants)
        {
            foreach (var nested in Flatten(child, children, depth + 1))
                yield return nested;
        }
    }

    private static string FormatDuration(TimeSpan duration) => duration.TotalMilliseconds >= 0.1
        ? $"{duration.TotalMilliseconds:F1}ms"
        : $"{duration.TotalMicroseconds:F0}us";
}
