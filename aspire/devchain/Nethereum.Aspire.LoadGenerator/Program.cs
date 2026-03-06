using Nethereum.Aspire.LoadGenerator.Configuration;
using Nethereum.Aspire.LoadGenerator.Endpoints;
using Nethereum.Aspire.LoadGenerator.Metrics;
using Nethereum.Aspire.LoadGenerator.Services;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.Configure<LoadGeneratorOptions>(
    builder.Configuration.GetSection(LoadGeneratorOptions.SectionName));

builder.Services.AddSingleton<LoadGeneratorMetrics>();
builder.Services.AddHostedService<LoadGeneratorService>();

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddMeter(LoadGeneratorMetrics.MeterName));

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapMetricsEndpoints();
app.MapMudEndpoints();

app.Run();
