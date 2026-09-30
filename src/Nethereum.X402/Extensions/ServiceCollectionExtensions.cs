using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.X402.Client;

namespace Nethereum.X402.Extensions;

/// <summary>
/// Extension methods for registering X402 client services in dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers an X402HttpClient for the manual payment flow. The signing domain (token, chain,
    /// EIP-712 name/version) is derived from the PaymentRequirements supplied per request, so only
    /// the payer's private key is needed here.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="privateKey">The private key for signing payments (hex format with or without 0x prefix)</param>
    /// <param name="configureHttpClient">Optional action to configure the HttpClient</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddX402Client(
        this IServiceCollection services,
        string privateKey,
        Action<HttpClient>? configureHttpClient = null)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        if (string.IsNullOrWhiteSpace(privateKey))
        {
            throw new ArgumentException("Private key is required", nameof(privateKey));
        }

        // Validate private key format (basic check)
        var key = privateKey.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? privateKey[2..]
            : privateKey;

        if (key.Length != 64)
        {
            throw new ArgumentException(
                "Private key must be 64 hex characters (with or without 0x prefix)",
                nameof(privateKey));
        }

        // Register HttpClient for X402HttpClient
        services.AddHttpClient<X402HttpClient>((sp, client) =>
        {
            configureHttpClient?.Invoke(client);
        });

        // Register X402HttpClient as transient (follows HttpClient pattern)
        services.AddTransient<X402HttpClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(X402HttpClient));
            return new X402HttpClient(httpClient, privateKey);
        });

        return services;
    }

    /// <summary>
    /// Registers X402 client services from configuration. Expected configuration key: PrivateKey.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration section with X402 client settings</param>
    /// <param name="configureHttpClient">Optional action to configure the HttpClient</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddX402Client(
        this IServiceCollection services,
        IConfigurationSection configuration,
        Action<HttpClient>? configureHttpClient = null)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

        var privateKey = configuration["PrivateKey"]
            ?? throw new InvalidOperationException("X402 PrivateKey not found in configuration");

        return services.AddX402Client(privateKey, configureHttpClient);
    }
}
