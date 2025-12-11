using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.X402.Facilitator;

namespace Nethereum.X402.AspNetCore;

/// <summary>
/// Extension methods for adding X402 middleware to ASP.NET Core pipeline.
/// </summary>
public static class X402MiddlewareExtensions
{
    /// <summary>
    /// Adds X402 payment middleware to the application pipeline.
    /// Spec Reference: Section 8 - Server Implementation
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <param name="configureOptions">Action to configure X402 options</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseX402(
        this IApplicationBuilder app,
        Action<X402Options> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(app, nameof(app));
        ArgumentNullException.ThrowIfNull(configureOptions, nameof(configureOptions));

        var options = new X402Options();
        configureOptions(options);

        // Get facilitator client from DI
        var facilitator = app.ApplicationServices.GetRequiredService<IFacilitatorClient>();

        return app.UseMiddleware<X402Middleware>(options, facilitator);
    }

    /// <summary>
    /// Adds X402 services to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="facilitatorUrl">URL of the facilitator service</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddX402Services(
        this IServiceCollection services,
        string facilitatorUrl)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        if (string.IsNullOrWhiteSpace(facilitatorUrl))
        {
            throw new ArgumentException("Facilitator URL is required", nameof(facilitatorUrl));
        }

        // Register HttpClient for facilitator
        services.AddHttpClient();

        // Register facilitator client
        services.AddSingleton<IFacilitatorClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            return new HttpFacilitatorClient(httpClient, facilitatorUrl);
        });

        return services;
    }
}
