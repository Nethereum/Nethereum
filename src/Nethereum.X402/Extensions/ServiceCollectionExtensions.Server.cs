using Nethereum.Documentation;
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

[NethereumDocExample(DocSection.DeFi, "x402-payments", "ServiceCollectionExtensionsServer - facilitator proxy and processor registrations")]
public static class ServiceCollectionExtensionsServer
{
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

        services.AddHttpClient(HttpFacilitatorClient.HttpClientName, client => client.Timeout = System.Threading.Timeout.InfiniteTimeSpan);

        services.AddSingleton<IFacilitatorClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(HttpFacilitatorClient.HttpClientName);
            return new HttpFacilitatorClient(httpClient, facilitatorUrl);
        });

        services.AddSingleton(sp =>
        {
            var facilitator = sp.GetRequiredService<IFacilitatorClient>();
            return new X402FacilitatorProxyProcessor(facilitator, routes);
        });

        return services;
    }

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

        var account = new Account(facilitatorPrivateKey);
        return services.AddX402TransferProcessor(sp => account, rpcEndpointsByChainId);
    }

    public static IServiceCollection AddX402TransferProcessor(
        this IServiceCollection services,
        IAccount facilitatorAccount,
        Dictionary<int, string> rpcEndpointsByChainId)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(facilitatorAccount, nameof(facilitatorAccount));
        ArgumentNullException.ThrowIfNull(rpcEndpointsByChainId, nameof(rpcEndpointsByChainId));

        return services.AddX402TransferProcessor(sp => facilitatorAccount, rpcEndpointsByChainId);
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
                ClientsFromUrls(rpcEndpointsByChainId),
                sp.GetService<X402FacilitatorOptions>());
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
                new X402TransferWithAuthorisation3009Service(account, clients, sp.GetService<X402FacilitatorOptions>()),
                new X402ExactPermit2Service(account, clients, (Erc20ApprovalGasSponsoringOptions?)null, sp.GetService<X402FacilitatorOptions>()));
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

        return services.AddX402ExactProcessor(sp => facilitatorAccount, rpcEndpointsByChainId);
    }

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

        var account = new Account(receiverPrivateKey);
        return services.AddX402ReceiveProcessor(sp => account, rpcEndpointsByChainId);
    }

    public static IServiceCollection AddX402ReceiveProcessor(
        this IServiceCollection services,
        IAccount receiverAccount,
        Dictionary<int, string> rpcEndpointsByChainId)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(receiverAccount, nameof(receiverAccount));
        ArgumentNullException.ThrowIfNull(rpcEndpointsByChainId, nameof(rpcEndpointsByChainId));

        return services.AddX402ReceiveProcessor(sp => receiverAccount, rpcEndpointsByChainId);
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
                ClientsFromUrls(rpcEndpointsByChainId),
                sp.GetService<X402FacilitatorOptions>());
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
