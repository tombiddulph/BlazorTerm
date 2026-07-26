using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace BlazorTerm;

public sealed class TerminalTelemetry : CircuitHandler, IDisposable
{
    public const string MeterName = "BlazorTerm.Terminal";

    private readonly long _startedAt = Stopwatch.GetTimestamp();
    private readonly Meter _meter = new(MeterName);
    private readonly Histogram<double> _requestDuration;
    private long _activeCircuits;
    private long _lastRequestMilliseconds;

    public TerminalTelemetry()
    {
        _requestDuration = _meter.CreateHistogram<double>(
            "blazorterm.request.duration",
            "ms",
            "Observed request duration displayed by the terminal telemetry command.");
    }

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _activeCircuits);
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        Interlocked.Decrement(ref _activeCircuits);
        return Task.CompletedTask;
    }

    public void RecordRequest(TimeSpan elapsed)
    {
        var milliseconds = elapsed.TotalMilliseconds;
        Interlocked.Exchange(ref _lastRequestMilliseconds, BitConverter.DoubleToInt64Bits(milliseconds));
        _requestDuration.Record(milliseconds);
    }

    public TerminalTelemetrySnapshot Snapshot()
    {
        return new TerminalTelemetrySnapshot(
            Interlocked.Read(ref _activeCircuits),
            Stopwatch.GetElapsedTime(_startedAt),
            BitConverter.Int64BitsToDouble(Interlocked.Read(ref _lastRequestMilliseconds)));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}

public sealed record TerminalTelemetrySnapshot(long ActiveCircuits, TimeSpan Uptime, double LastRequestMilliseconds);
