using System.Net.Http.Json;

namespace Nethereum.Aspire.Explorer.Services;

public class LoadTestMetricsService : ILoadTestMetricsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LoadTestMetricsService> _logger;

    public LoadTestMetricsService(HttpClient httpClient, ILogger<LoadTestMetricsService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<LoadTestStats?> GetStatsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<LoadTestStats>("/metrics/stats");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch load test metrics");
            return null;
        }
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/metrics/stats");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
