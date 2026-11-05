using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;

namespace Nethereum.AppChain.Server.Metrics
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ChainInfrastructure, "appchain-server-core", "Prometheus extensions")]
    public static class AppChainPrometheusMetricsExtensions
    {
        public static TBuilder AddPrometheusMetrics<TBuilder>(this TBuilder builder, int metricsPort) where TBuilder : IHostApplicationBuilder
        {
            if (metricsPort > 0)
            {
                builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddPrometheusExporter());
            }

            return builder;
        }

        public static WebApplication MapPrometheusMetrics(this WebApplication app, string host, int rpcPort, int metricsPort)
        {
            if (metricsPort <= 0)
                return app;

            var hostManagesUrls = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS"));
            if (!hostManagesUrls)
            {
                app.Urls.Add($"http://{host}:{rpcPort}");
                if (metricsPort != rpcPort)
                    app.Urls.Add($"http://{host}:{metricsPort}");
            }

            app.MapPrometheusScrapingEndpoint().RequireHost($"*:{metricsPort}");

            return app;
        }
    }
}
