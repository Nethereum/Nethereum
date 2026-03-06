namespace Nethereum.Aspire.Explorer.Services;

public class LoadTestStats
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

public interface ILoadTestMetricsService
{
    Task<LoadTestStats?> GetStatsAsync();
    Task<bool> IsAvailableAsync();
}
