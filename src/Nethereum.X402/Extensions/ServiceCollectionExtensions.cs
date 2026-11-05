using Nethereum.Documentation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.X402.Client;

namespace Nethereum.X402.Extensions;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "ServiceCollectionExtensions - AddX402Client")]
public static class ServiceCollectionExtensions
{
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

        var key = privateKey.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? privateKey[2..]
            : privateKey;

        if (key.Length != 64)
        {
            throw new ArgumentException(
                "Private key must be 64 hex characters (with or without 0x prefix)",
                nameof(privateKey));
        }

        services.AddHttpClient<X402HttpClient>((sp, client) =>
        {
            configureHttpClient?.Invoke(client);
        });

        services.AddTransient<X402HttpClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(X402HttpClient));
            return new X402HttpClient(httpClient, privateKey);
        });

        return services;
    }

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
