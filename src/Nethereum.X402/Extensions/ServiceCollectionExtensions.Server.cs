using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.RPC.Accounts;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Processors;
using Nethereum.X402.Server;

namespace Nethereum.X402.Extensions;

public static class ServiceCollectionExtensionsServer
{
    /// <summary>
    /// Registers X402FacilitatorProxyProcessor for proxying payments to a remote facilitator service.
    /// This is the simplest option - no private keys or blockchain interaction needed on the server.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="facilitatorUrl">URL of the remote facilitator service</param>
    /// <param name="routes">Route configurations for payment requirements</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddX402FacilitatorProxy(
        this IServiceCollection services,
        string facilitatorUrl,
        IEnumerable<RoutePaymentConfig> routes)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        if (string.IsNullOrWhiteSpace(facilitatorUrl))
        {
            throw new ArgumentException("Facilitator URL is required", nameof(facilitatorUrl));
        }

        ArgumentNullException.ThrowIfNull(routes, nameof(routes));

        // Register HttpClient for facilitator
        services.AddHttpClient();

        // Register facilitator client
        services.AddSingleton<IFacilitatorClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            return new HttpFacilitatorClient(httpClient, facilitatorUrl);
        });

        // Register the proxy processor
        services.AddSingleton(sp =>
        {
            var facilitator = sp.GetRequiredService<IFacilitatorClient>();
            return new X402FacilitatorProxyProcessor(facilitator, routes);
        });

        return services;
    }

    /// <summary>
    /// Registers X402TransferWithAuthorisation3009Service for direct blockchain settlement.
    /// Uses TransferWithAuthorization pattern where the facilitator (this server) submits transactions.
    /// Requires a private key with ETH for gas fees.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="facilitatorPrivateKey">Private key for submitting transactions (hex format with or without 0x prefix)</param>
    /// <param name="rpcEndpoints">Dictionary mapping network names to RPC endpoints (e.g., "base-sepolia" -> "https://...")</param>
    /// <param name="tokenAddresses">Dictionary mapping network names to token contract addresses</param>
    /// <param name="chainIds">Dictionary mapping network names to chain IDs</param>
    /// <param name="tokenNames">Dictionary mapping network names to token names (e.g., "USD Coin")</param>
    /// <param name="tokenVersions">Dictionary mapping network names to token versions (e.g., "2")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddX402TransferProcessor(
        this IServiceCollection services,
        string facilitatorPrivateKey,
        Dictionary<string, string> rpcEndpoints,
        Dictionary<string, string> tokenAddresses,
        Dictionary<string, int> chainIds,
        Dictionary<string, string> tokenNames,
        Dictionary<string, string> tokenVersions)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        if (string.IsNullOrWhiteSpace(facilitatorPrivateKey))
        {
            throw new ArgumentException("Facilitator private key is required", nameof(facilitatorPrivateKey));
        }

        ArgumentNullException.ThrowIfNull(rpcEndpoints, nameof(rpcEndpoints));
        ArgumentNullException.ThrowIfNull(tokenAddresses, nameof(tokenAddresses));
        ArgumentNullException.ThrowIfNull(chainIds, nameof(chainIds));
        ArgumentNullException.ThrowIfNull(tokenNames, nameof(tokenNames));
        ArgumentNullException.ThrowIfNull(tokenVersions, nameof(tokenVersions));

        // Register as IX402PaymentProcessor
        services.AddSingleton<IX402PaymentProcessor>(sp =>
        {
            return new X402TransferWithAuthorisation3009Service(
                facilitatorPrivateKey,
                rpcEndpoints,
                tokenAddresses,
                chainIds,
                tokenNames,
                tokenVersions);
        });

        return services;
    }

    public static IServiceCollection AddX402TransferProcessor(
        this IServiceCollection services,
        IAccount facilitatorAccount,
        Dictionary<string, string> rpcEndpoints,
        Dictionary<string, string> tokenAddresses,
        Dictionary<string, int> chainIds,
        Dictionary<string, string> tokenNames,
        Dictionary<string, string> tokenVersions)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(facilitatorAccount, nameof(facilitatorAccount));
        ArgumentNullException.ThrowIfNull(rpcEndpoints, nameof(rpcEndpoints));
        ArgumentNullException.ThrowIfNull(tokenAddresses, nameof(tokenAddresses));
        ArgumentNullException.ThrowIfNull(chainIds, nameof(chainIds));
        ArgumentNullException.ThrowIfNull(tokenNames, nameof(tokenNames));
        ArgumentNullException.ThrowIfNull(tokenVersions, nameof(tokenVersions));

        services.AddSingleton<IX402PaymentProcessor>(sp =>
        {
            return new X402TransferWithAuthorisation3009Service(
                facilitatorAccount,
                rpcEndpoints,
                tokenAddresses,
                chainIds,
                tokenNames,
                tokenVersions);
        });

        return services;
    }

    public static IServiceCollection AddX402TransferProcessor(
        this IServiceCollection services,
        Func<IServiceProvider, IAccount> accountFactory,
        Dictionary<string, string> rpcEndpoints,
        Dictionary<string, string> tokenAddresses,
        Dictionary<string, int> chainIds,
        Dictionary<string, string> tokenNames,
        Dictionary<string, string> tokenVersions)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(accountFactory, nameof(accountFactory));
        ArgumentNullException.ThrowIfNull(rpcEndpoints, nameof(rpcEndpoints));
        ArgumentNullException.ThrowIfNull(tokenAddresses, nameof(tokenAddresses));
        ArgumentNullException.ThrowIfNull(chainIds, nameof(chainIds));
        ArgumentNullException.ThrowIfNull(tokenNames, nameof(tokenNames));
        ArgumentNullException.ThrowIfNull(tokenVersions, nameof(tokenVersions));

        services.AddSingleton<IX402PaymentProcessor>(sp =>
        {
            var account = accountFactory(sp);
            return new X402TransferWithAuthorisation3009Service(
                account,
                rpcEndpoints,
                tokenAddresses,
                chainIds,
                tokenNames,
                tokenVersions);
        });

        return services;
    }

    /// <summary>
    /// Registers X402ReceiveWithAuthorisation3009Service for direct blockchain settlement.
    /// Uses ReceiveWithAuthorization pattern where the receiver (this server) submits transactions.
    /// Requires a private key with ETH for gas fees.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="receiverPrivateKey">Private key for submitting transactions (hex format with or without 0x prefix)</param>
    /// <param name="rpcEndpoints">Dictionary mapping network names to RPC endpoints (e.g., "base-sepolia" -> "https://...")</param>
    /// <param name="tokenAddresses">Dictionary mapping network names to token contract addresses</param>
    /// <param name="chainIds">Dictionary mapping network names to chain IDs</param>
    /// <param name="tokenNames">Dictionary mapping network names to token names (e.g., "USD Coin")</param>
    /// <param name="tokenVersions">Dictionary mapping network names to token versions (e.g., "2")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddX402ReceiveProcessor(
        this IServiceCollection services,
        string receiverPrivateKey,
        Dictionary<string, string> rpcEndpoints,
        Dictionary<string, string> tokenAddresses,
        Dictionary<string, int> chainIds,
        Dictionary<string, string> tokenNames,
        Dictionary<string, string> tokenVersions)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        if (string.IsNullOrWhiteSpace(receiverPrivateKey))
        {
            throw new ArgumentException("Receiver private key is required", nameof(receiverPrivateKey));
        }

        ArgumentNullException.ThrowIfNull(rpcEndpoints, nameof(rpcEndpoints));
        ArgumentNullException.ThrowIfNull(tokenAddresses, nameof(tokenAddresses));
        ArgumentNullException.ThrowIfNull(chainIds, nameof(chainIds));
        ArgumentNullException.ThrowIfNull(tokenNames, nameof(tokenNames));
        ArgumentNullException.ThrowIfNull(tokenVersions, nameof(tokenVersions));

        // Register as IX402PaymentProcessor
        services.AddSingleton<IX402PaymentProcessor>(sp =>
        {
            return new X402ReceiveWithAuthorisation3009Service(
                receiverPrivateKey,
                rpcEndpoints,
                tokenAddresses,
                chainIds,
                tokenNames,
                tokenVersions);
        });

        return services;
    }

    public static IServiceCollection AddX402ReceiveProcessor(
        this IServiceCollection services,
        IAccount receiverAccount,
        Dictionary<string, string> rpcEndpoints,
        Dictionary<string, string> tokenAddresses,
        Dictionary<string, int> chainIds,
        Dictionary<string, string> tokenNames,
        Dictionary<string, string> tokenVersions)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(receiverAccount, nameof(receiverAccount));
        ArgumentNullException.ThrowIfNull(rpcEndpoints, nameof(rpcEndpoints));
        ArgumentNullException.ThrowIfNull(tokenAddresses, nameof(tokenAddresses));
        ArgumentNullException.ThrowIfNull(chainIds, nameof(chainIds));
        ArgumentNullException.ThrowIfNull(tokenNames, nameof(tokenNames));
        ArgumentNullException.ThrowIfNull(tokenVersions, nameof(tokenVersions));

        services.AddSingleton<IX402PaymentProcessor>(sp =>
        {
            return new X402ReceiveWithAuthorisation3009Service(
                receiverAccount,
                rpcEndpoints,
                tokenAddresses,
                chainIds,
                tokenNames,
                tokenVersions);
        });

        return services;
    }

    public static IServiceCollection AddX402ReceiveProcessor(
        this IServiceCollection services,
        Func<IServiceProvider, IAccount> accountFactory,
        Dictionary<string, string> rpcEndpoints,
        Dictionary<string, string> tokenAddresses,
        Dictionary<string, int> chainIds,
        Dictionary<string, string> tokenNames,
        Dictionary<string, string> tokenVersions)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(accountFactory, nameof(accountFactory));
        ArgumentNullException.ThrowIfNull(rpcEndpoints, nameof(rpcEndpoints));
        ArgumentNullException.ThrowIfNull(tokenAddresses, nameof(tokenAddresses));
        ArgumentNullException.ThrowIfNull(chainIds, nameof(chainIds));
        ArgumentNullException.ThrowIfNull(tokenNames, nameof(tokenNames));
        ArgumentNullException.ThrowIfNull(tokenVersions, nameof(tokenVersions));

        services.AddSingleton<IX402PaymentProcessor>(sp =>
        {
            var account = accountFactory(sp);
            return new X402ReceiveWithAuthorisation3009Service(
                account,
                rpcEndpoints,
                tokenAddresses,
                chainIds,
                tokenNames,
                tokenVersions);
        });

        return services;
    }
}
