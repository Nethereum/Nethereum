using Nethereum.Documentation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.X402.Facilitator;

namespace Nethereum.X402.AspNetCore;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "X402MiddlewareExtensions - UseX402 and AddX402Services")]
public static class X402MiddlewareExtensions
{
    public static IApplicationBuilder UseX402(
        this IApplicationBuilder app,
        Action<X402Options> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(app, nameof(app));
        ArgumentNullException.ThrowIfNull(configureOptions, nameof(configureOptions));

        var options = new X402Options();
        configureOptions(options);

        var facilitator = app.ApplicationServices.GetRequiredService<IFacilitatorClient>();

        return app.UseMiddleware<X402Middleware>(options, facilitator);
    }

    public static IServiceCollection AddX402Services(
        this IServiceCollection services,
        string facilitatorUrl)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        if (string.IsNullOrWhiteSpace(facilitatorUrl))
        {
            throw new ArgumentException("Facilitator URL is required", nameof(facilitatorUrl));
        }

        services.AddHttpClient(HttpFacilitatorClient.HttpClientName, client => client.Timeout = System.Threading.Timeout.InfiniteTimeSpan);

        services.AddSingleton<IFacilitatorClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(HttpFacilitatorClient.HttpClientName);
            return new HttpFacilitatorClient(httpClient, facilitatorUrl);
        });

        return services;
    }
}
