using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Nethereum.Aspire.LoadGenerator.Metrics;

public class LoadGeneratorMetrics
{
    public static readonly string MeterName = "Nethereum.LoadGenerator";

    private readonly Meter _meter;
    private readonly Counter<long> _txsSent;
    private readonly Counter<long> _txsConfirmed;
    private readonly Counter<long> _txsFailed;
    private readonly Histogram<double> _latencyMs;
    private readonly UpDownCounter<int> _activeWorkers;

    private long _totalSuccess;
    private long _totalFailed;
    private readonly ConcurrentBag<long> _latencies = new();
    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private double _peakTps;

    public LoadGeneratorMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        _txsSent = _meter.CreateCounter<long>("loadgen.txs_sent", "txs", "Total transactions sent");
        _txsConfirmed = _meter.CreateCounter<long>("loadgen.txs_confirmed", "txs", "Total transactions confirmed");
        _txsFailed = _meter.CreateCounter<long>("loadgen.txs_failed", "txs", "Total transactions failed");
        _latencyMs = _meter.CreateHistogram<double>("loadgen.latency_ms", "ms", "Transaction latency");
        _activeWorkers = _meter.CreateUpDownCounter<int>("loadgen.active_workers", "workers");
        _meter.CreateObservableGauge("loadgen.current_tps", () => GetCurrentTps());
        _meter.CreateObservableGauge("loadgen.peak_tps", () => _peakTps);
    }

    public void RecordSent() => _txsSent.Add(1);

    public void RecordSuccess(long latencyMs)
    {
        Interlocked.Increment(ref _totalSuccess);
        _txsConfirmed.Add(1);
        _latencyMs.Record(latencyMs);
        _latencies.Add(latencyMs);
        UpdateTps();
    }

    public void RecordFailure()
    {
        Interlocked.Increment(ref _totalFailed);
        _txsFailed.Add(1);
    }

    public void WorkerStarted() => _activeWorkers.Add(1);
    public void WorkerStopped() => _activeWorkers.Add(-1);

    public long TotalSuccess => Interlocked.Read(ref _totalSuccess);
    public long TotalFailed => Interlocked.Read(ref _totalFailed);
    public double PeakTps => _peakTps;

    public double GetCurrentTps()
    {
        var elapsed = _uptime.Elapsed.TotalSeconds;
        return elapsed > 0 ? TotalSuccess / elapsed : 0;
    }

    public LoadGeneratorStats GetStats()
    {
        var latencyArray = _latencies.ToArray();
        Array.Sort(latencyArray);

        return new LoadGeneratorStats
        {
            TotalSuccess = TotalSuccess,
            TotalFailed = TotalFailed,
            CurrentTps = Math.Round(GetCurrentTps(), 2),
            PeakTps = Math.Round(_peakTps, 2),
            UptimeSeconds = (long)_uptime.Elapsed.TotalSeconds,
            P50LatencyMs = GetPercentile(latencyArray, 0.50),
            P95LatencyMs = GetPercentile(latencyArray, 0.95),
            P99LatencyMs = GetPercentile(latencyArray, 0.99),
            AvgLatencyMs = latencyArray.Length > 0 ? Math.Round(latencyArray.Average(), 2) : 0
        };
    }

    private void UpdateTps()
    {
        var currentTps = GetCurrentTps();
        double peakTps;
        do
        {
            peakTps = _peakTps;
            if (currentTps <= peakTps) return;
        }
        while (Interlocked.CompareExchange(ref _peakTps, currentTps, peakTps) != peakTps);
    }

    private static double GetPercentile(long[] sorted, double percentile)
    {
        if (sorted.Length == 0) return 0;
        var index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Length - 1))];
    }
}

public class LoadGeneratorStats
{
    public long TotalSuccess { get; set; }
    public long TotalFailed { get; set; }
    public double CurrentTps { get; set; }
    public double PeakTps { get; set; }
    public long UptimeSeconds { get; set; }
    public double P50LatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
    public double P99LatencyMs { get; set; }
    public double AvgLatencyMs { get; set; }
}
