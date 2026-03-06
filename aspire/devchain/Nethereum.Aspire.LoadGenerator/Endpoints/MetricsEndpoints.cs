using Nethereum.Aspire.LoadGenerator.Metrics;

namespace Nethereum.Aspire.LoadGenerator.Endpoints;

public static class MetricsEndpoints
{
    public static WebApplication MapMetricsEndpoints(this WebApplication app)
    {
        app.MapGet("/metrics/stats", (LoadGeneratorMetrics metrics) => metrics.GetStats());
        return app;
    }
}
