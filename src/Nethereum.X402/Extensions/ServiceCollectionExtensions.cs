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
    /// Registers X402 client services including HttpClient with EIP-3009 signing.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="privateKey">The private key for signing payments (hex format with or without 0x prefix)</param>
    /// <param name="tokenName">Token name (e.g., "USDC")</param>
    /// <param name="tokenVersion">Token version (e.g., "2")</param>
    /// <param name="chainId">Chain ID (e.g., 84532 for Base Sepolia)</param>
    /// <param name="tokenAddress">Token contract address</param>
    /// <param name="configureHttpClient">Optional action to configure the HttpClient</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddX402Client(
        this IServiceCollection services,
        string privateKey,
        string tokenName,
        string tokenVersion,
        int chainId,
        string tokenAddress,
        Action<HttpClient>? configureHttpClient = null)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        if (string.IsNullOrWhiteSpace(privateKey))
        {
            throw new ArgumentException("Private key is required", nameof(privateKey));
        }

        if (string.IsNullOrWhiteSpace(tokenName))
        {
            throw new ArgumentException("Token name is required", nameof(tokenName));
        }

        if (string.IsNullOrWhiteSpace(tokenVersion))
        {
            throw new ArgumentException("Token version is required", nameof(tokenVersion));
        }

        if (string.IsNullOrWhiteSpace(tokenAddress))
        {
            throw new ArgumentException("Token address is required", nameof(tokenAddress));
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
            return new X402HttpClient(
                httpClient,
                privateKey,
                tokenName,
                tokenVersion,
                chainId,
                tokenAddress);
        });

        return services;
    }

    /// <summary>
    /// Registers X402 client services from configuration.
    /// Expected configuration keys: PrivateKey, TokenName, TokenVersion, ChainId, TokenAddress
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

        var tokenName = configuration["TokenName"]
            ?? throw new InvalidOperationException("X402 TokenName not found in configuration");

        var tokenVersion = configuration["TokenVersion"]
            ?? throw new InvalidOperationException("X402 TokenVersion not found in configuration");

        var chainIdString = configuration["ChainId"]
            ?? throw new InvalidOperationException("X402 ChainId not found in configuration");

        if (!int.TryParse(chainIdString, out var chainId))
        {
            throw new InvalidOperationException($"X402 ChainId '{chainIdString}' is not a valid integer");
        }

        var tokenAddress = configuration["TokenAddress"]
            ?? throw new InvalidOperationException("X402 TokenAddress not found in configuration");

        return services.AddX402Client(
            privateKey,
            tokenName,
            tokenVersion,
            chainId,
            tokenAddress,
            configureHttpClient);
    }
}
