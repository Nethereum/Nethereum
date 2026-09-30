using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.JsonRpc.Client;
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
    /// <param name="rpcEndpointsByChainId">Dictionary mapping chain IDs to RPC endpoints (e.g., 84532 -> "https://...")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddX402TransferProcessor(
        this IServiceCollection services,
        string facilitatorPrivateKey,
        Dictionary<int, string> rpcEndpointsByChainId)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        if (string.IsNullOrWhiteSpace(facilitatorPrivateKey))
        {
            throw new ArgumentException("Facilitator private key is required", nameof(facilitatorPrivateKey));
        }

        ArgumentNullException.ThrowIfNull(rpcEndpointsByChainId, nameof(rpcEndpointsByChainId));

        // Register as IX402PaymentProcessor
        services.AddSingleton<IX402PaymentProcessor>(sp =>
        {
            return new X402TransferWithAuthorisation3009Service(
                facilitatorPrivateKey,
                ClientsFromUrls(rpcEndpointsByChainId));
        });

        return services;
    }

    public static IServiceCollection AddX402TransferProcessor(
        this IServiceCollection services,
        IAccount facilitatorAccount,
        Dictionary<int, string> rpcEndpointsByChainId)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(facilitatorAccount, nameof(facilitatorAccount));
        ArgumentNullException.ThrowIfNull(rpcEndpointsByChainId, nameof(rpcEndpointsByChainId));

        services.AddSingleton<IX402PaymentProcessor>(sp =>
        {
            return new X402TransferWithAuthorisation3009Service(
                facilitatorAccount,
                ClientsFromUrls(rpcEndpointsByChainId));
        });

        return services;
    }

    public static IServiceCollection AddX402TransferProcessor(
        this IServiceCollection services,
        Func<IServiceProvider, IAccount> accountFactory,
        Dictionary<int, string> rpcEndpointsByChainId)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(accountFactory, nameof(accountFactory));
        ArgumentNullException.ThrowIfNull(rpcEndpointsByChainId, nameof(rpcEndpointsByChainId));

        services.AddSingleton<IX402PaymentProcessor>(sp =>
        {
            var account = accountFactory(sp);
            return new X402TransferWithAuthorisation3009Service(
                account,
                ClientsFromUrls(rpcEndpointsByChainId));
        });

        return services;
    }

    public static IServiceCollection AddX402ExactProcessor(
        this IServiceCollection services,
        string facilitatorPrivateKey,
        Dictionary<int, string> rpcEndpointsByChainId)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        if (string.IsNullOrWhiteSpace(facilitatorPrivateKey))
            throw new ArgumentException("Facilitator private key is required", nameof(facilitatorPrivateKey));
        return services.AddX402ExactProcessor(new Account(facilitatorPrivateKey), rpcEndpointsByChainId);
    }

    public static IServiceCollection AddX402ExactProcessor(
        this IServiceCollection services,
        Func<IServiceProvider, IAccount> accountFactory,
        Dictionary<int, string> rpcEndpointsByChainId)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(accountFactory, nameof(accountFactory));
        ArgumentNullException.ThrowIfNull(rpcEndpointsByChainId, nameof(rpcEndpointsByChainId));

        services.AddSingleton<IX402PaymentProcessor>(sp =>
        {
            var account = accountFactory(sp);
            var clients = ClientsFromUrls(rpcEndpointsByChainId);
            return new X402ExactSchemeProcessor(
                new X402TransferWithAuthorisation3009Service(account, clients),
                new X402ExactPermit2Service(account, clients));
        });

        return services;
    }

    public static IServiceCollection AddX402ExactProcessor(
        this IServiceCollection services,
        IAccount facilitatorAccount,
        Dictionary<int, string> rpcEndpointsByChainId)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(facilitatorAccount, nameof(facilitatorAccount));
        ArgumentNullException.ThrowIfNull(rpcEndpointsByChainId, nameof(rpcEndpointsByChainId));

        services.AddSingleton<IX402PaymentProcessor>(sp =>
        {
            var clients = ClientsFromUrls(rpcEndpointsByChainId);
            return new X402ExactSchemeProcessor(
                new X402TransferWithAuthorisation3009Service(facilitatorAccount, clients),
                new X402ExactPermit2Service(facilitatorAccount, clients));
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
    /// <param name="rpcEndpointsByChainId">Dictionary mapping chain IDs to RPC endpoints (e.g., 84532 -> "https://...")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddX402ReceiveProcessor(
        this IServiceCollection services,
        string receiverPrivateKey,
        Dictionary<int, string> rpcEndpointsByChainId)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        if (string.IsNullOrWhiteSpace(receiverPrivateKey))
        {
            throw new ArgumentException("Receiver private key is required", nameof(receiverPrivateKey));
        }

        ArgumentNullException.ThrowIfNull(rpcEndpointsByChainId, nameof(rpcEndpointsByChainId));

        // Register as IX402PaymentProcessor
        services.AddSingleton<IX402PaymentProcessor>(sp =>
        {
            return new X402ReceiveWithAuthorisation3009Service(
                receiverPrivateKey,
                ClientsFromUrls(rpcEndpointsByChainId));
        });

        return services;
    }

    public static IServiceCollection AddX402ReceiveProcessor(
        this IServiceCollection services,
        IAccount receiverAccount,
        Dictionary<int, string> rpcEndpointsByChainId)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(receiverAccount, nameof(receiverAccount));
        ArgumentNullException.ThrowIfNull(rpcEndpointsByChainId, nameof(rpcEndpointsByChainId));

        services.AddSingleton<IX402PaymentProcessor>(sp =>
        {
            return new X402ReceiveWithAuthorisation3009Service(
                receiverAccount,
                ClientsFromUrls(rpcEndpointsByChainId));
        });

        return services;
    }

    public static IServiceCollection AddX402ReceiveProcessor(
        this IServiceCollection services,
        Func<IServiceProvider, IAccount> accountFactory,
        Dictionary<int, string> rpcEndpointsByChainId)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(accountFactory, nameof(accountFactory));
        ArgumentNullException.ThrowIfNull(rpcEndpointsByChainId, nameof(rpcEndpointsByChainId));

        services.AddSingleton<IX402PaymentProcessor>(sp =>
        {
            var account = accountFactory(sp);
            return new X402ReceiveWithAuthorisation3009Service(
                account,
                ClientsFromUrls(rpcEndpointsByChainId));
        });

        return services;
    }

    private static Dictionary<int, IClient> ClientsFromUrls(Dictionary<int, string> rpcEndpointsByChainId)
    {
        ArgumentNullException.ThrowIfNull(rpcEndpointsByChainId, nameof(rpcEndpointsByChainId));
        return rpcEndpointsByChainId.ToDictionary(
            pair => pair.Key,
            pair => (IClient)new RpcClient(new Uri(pair.Value)));
    }
}
