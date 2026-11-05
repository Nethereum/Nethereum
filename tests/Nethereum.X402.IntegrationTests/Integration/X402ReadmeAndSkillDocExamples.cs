using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.DevChain;
using Nethereum.Documentation;
using Nethereum.JsonRpc.Client;
using Nethereum.Web3.Accounts;
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Client;
using Nethereum.X402.Extensions;
using Nethereum.X402.Facilitator;
using Nethereum.X402.IntegrationTests.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Processors;
using Nethereum.X402.Server;
using Nethereum.X402.Signers;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Integration;

[Collection("X402 DevChain E2E")]
public class X402ReadmeAndSkillDocExamples : IClassFixture<X402DevChainFixture>
{
    private const string FacilitatorUrl = "https://x402.org/facilitator";
    private const string BaseUsdc = "0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913";

    private readonly X402DevChainFixture _chain;

    public X402ReadmeAndSkillDocExamples(X402DevChainFixture chain)
    {
        _chain = chain;
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "Manual payment flow: pay with explicit requirements", Order = 70)]
    public async Task ManualFlow_PaysTheExplicitRequirementsPassedToGetAsync()
    {
        using var facilitator = FacilitatorHost();
        using var resource = ResourceHost(facilitator);
        var httpClient = resource.CreateClient();
        var payerPrivateKey = X402DevChainFixture.PayerKey;
        var usdcAddress = _chain.TokenAddress;
        var before = await BalanceOfAsync(X402DevChainFixture.PayerAddress);

        var x402Client = new X402HttpClient(httpClient, payerPrivateKey);

        var requirements = new PaymentRequirements
        {
            Scheme = "exact",
            Network = "eip155:31337",
            Amount = "10000",
            Asset = usdcAddress,
            PayTo = X402DevChainFixture.RecipientAddress,
            MaxTimeoutSeconds = 60,
            Extra = new ExactSchemeExtra { Name = "USDC", Version = "2" }
        };

        var response = await x402Client.GetAsync("/premium", requirements);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.IsPaymentSuccessful());
        Assert.Equal("premium content", await response.Content.ReadAsStringAsync());
        var after = await BalanceOfAsync(X402DevChainFixture.PayerAddress);
        Assert.Equal(10000, (long)(before - after));
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "Client DI registration: AddX402Client registers a manual-mode client", Order = 71)]
    public async Task AddX402Client_RegistersAManualModeClient()
    {
        using var facilitator = FacilitatorHost();
        using var resource = ResourceHost(facilitator);
        var payerPrivateKey = X402DevChainFixture.PayerKey;
        var services = new ServiceCollection();

        services.AddX402Client(privateKey: payerPrivateKey);

        services.AddHttpClient(nameof(X402HttpClient)).ConfigurePrimaryHttpMessageHandler(() => resource.CreateHandler());
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<X402HttpClient>();

        Assert.Equal(X402DevChainFixture.PayerAddress, client.Address, ignoreCase: true);
        var automatic = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync("http://localhost/premium"));
        Assert.Contains("requires automatic payment mode", automatic.Message);
        var paid = await client.GetAsync("http://localhost/premium", Requirements());
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        Assert.True(paid.IsPaymentSuccessful());
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "Server: AddX402Services and UseX402 protect an EIP-3009 route", Order = 72)]
    public async Task Server_AddX402ServicesAndUseX402_ProtectAnEip3009RouteAndSettleThroughTheFacilitator()
    {
        using var facilitator = FacilitatorHost();
        var usdcAddress = _chain.TokenAddress;
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddX402Services("https://x402.org/facilitator");
                services.AddHttpClient(HttpFacilitatorClient.HttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => facilitator.CreateHandler());
            })
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "https://x402.org/facilitator";
                    options.Routes.Add(new RoutePaymentConfig("/api/premium/*", new PaymentRequirements
                    {
                        Scheme = "exact",
                        Network = "eip155:31337",
                        Amount = "10000",
                        Asset = usdcAddress,
                        PayTo = X402DevChainFixture.RecipientAddress,
                        MaxTimeoutSeconds = 60,
                        Extra = new ExactSchemeExtra { AssetTransferMethod = "eip3009", Name = "USDC", Version = "2" }
                    }));
                });

                app.Run(context => context.Response.WriteAsync("Premium content"));
            });
        using var resource = new TestServer(builder);

        var unpaid = await resource.CreateClient().GetAsync("/api/premium/content");
        var offer = JsonNode.Parse(await unpaid.Content.ReadAsStringAsync())!["accepts"]![0]!;
        var client = new X402HttpClient(resource.CreateClient(), X402DevChainFixture.PayerKey, new X402HttpClientOptions
        {
            MaxAmount = "100000",
            PreferredNetwork = "eip155:31337",
            PreferredScheme = "exact"
        });
        var paid = await client.GetAsync("/api/premium/content");

        Assert.Equal(HttpStatusCode.PaymentRequired, unpaid.StatusCode);
        Assert.Equal("10000", (string?)offer["amount"]);
        Assert.Equal("eip3009", (string?)offer["extra"]!["assetTransferMethod"]);
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        Assert.True(paid.IsPaymentSuccessful());
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "Permit2 route: extra.assetTransferMethod permit2 in the 402", Order = 73)]
    public async Task Server_RouteWithPermit2AssetTransferMethod_OffersPermit2In402()
    {
        var usdcAddress = _chain.TokenAddress;
        var receiverAddress = X402DevChainFixture.RecipientAddress;
        var builder = new WebHostBuilder()
            .ConfigureServices(services => services.AddSingleton<IFacilitatorClient>(new IntegrationMockFacilitator()))
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = FacilitatorUrl;
                    options.Routes.Add(new RoutePaymentConfig("/api/premium-permit2", new PaymentRequirements
                    {
                        Scheme = "exact",
                        Network = "eip155:84532",
                        Amount = "10000",
                        Asset = usdcAddress,
                        PayTo = receiverAddress,
                        MaxTimeoutSeconds = 60,
                        Extra = new ExactSchemeExtra { AssetTransferMethod = "permit2" }
                    }));
                });

                app.Run(context => context.Response.WriteAsync("premium"));
            });
        using var resource = new TestServer(builder);

        var response = await resource.CreateClient().GetAsync("/api/premium-permit2");

        var offer = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["accepts"]![0]!;
        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.Equal("permit2", (string?)offer["extra"]!["assetTransferMethod"]);
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "Facilitator-side processor registrations", Order = 74)]
    public async Task ProcessorRegistrations_ResolveTheTransferExactAndReceiveProcessors()
    {
        var facilitatorPrivateKey = X402DevChainFixture.FacilitatorKey;
        var receiverPrivateKey = X402DevChainFixture.PayerKey;
        var rpcByChainId = new Dictionary<int, string>
        {
            [8453] = "https://mainnet.base.org",
            [84532] = "https://sepolia.base.org"
        };
        var transfer = new ServiceCollection();
        var exact = new ServiceCollection();
        var receive = new ServiceCollection();

        transfer.AddX402TransferProcessor(facilitatorPrivateKey, rpcByChainId);

        exact.AddX402ExactProcessor(facilitatorPrivateKey, rpcByChainId);

        receive.AddX402ReceiveProcessor(receiverPrivateKey, rpcByChainId);

        using var transferProvider = transfer.BuildServiceProvider();
        using var exactProvider = exact.BuildServiceProvider();
        using var receiveProvider = receive.BuildServiceProvider();
        Assert.IsType<X402TransferWithAuthorisation3009Service>(transferProvider.GetRequiredService<IX402PaymentProcessor>());
        Assert.IsType<X402ExactSchemeProcessor>(exactProvider.GetRequiredService<IX402PaymentProcessor>());
        var receiveProcessor = receiveProvider.GetRequiredService<IX402PaymentProcessor>();
        Assert.IsType<X402ReceiveWithAuthorisation3009Service>(receiveProcessor);
        var supported = await receiveProcessor.GetSupportedAsync();
        Assert.Contains(supported.Kinds, kind => kind.Network == "eip155:8453" && kind.Scheme == "exact");
        Assert.Contains(supported.Kinds, kind => kind.Network == "eip155:84532" && kind.Scheme == "exact");
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "HttpFacilitatorClient: verify, settle and supported", Order = 75)]
    public async Task HttpFacilitatorClient_VerifiesSettlesAndListsSupportedKinds()
    {
        using var facilitator = FacilitatorHost();
        var httpClient = facilitator.CreateClient();
        var requirements = Requirements();
        var paymentPayload = await X402DevChainPayments.SignedTransfer(_chain, requirements);

        var facilitatorClient = new HttpFacilitatorClient(httpClient, "http://localhost/facilitator");

        var verification = await facilitatorClient.VerifyAsync(paymentPayload, requirements);
        var settlement = await facilitatorClient.SettleAsync(paymentPayload, requirements);
        var supported = await facilitatorClient.GetSupportedAsync();

        Assert.True(verification.IsValid, verification.InvalidReason);
        Assert.True(settlement.Success, settlement.ErrorReason);
        Assert.False(string.IsNullOrEmpty(settlement.Transaction));
        Assert.Contains(supported.Kinds, kind => kind.Network == "eip155:31337");
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "Host a facilitator: AddX402ExactProcessor and AddX402FacilitatorControllers", Order = 76)]
    public async Task HostAFacilitator_ServesVerifySettleAndSupportedEndpoints()
    {
        var facilitatorAccount = new Account(X402DevChainFixture.FacilitatorKey);
        var rpcByChainId = new Dictionary<int, string>
        {
            [8453] = "https://mainnet.base.org",
            [84532] = "https://sepolia.base.org"
        };
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddX402ExactProcessor(facilitatorAccount, rpcByChainId);

                services.AddControllers().AddX402FacilitatorControllers();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });
        using var server = new TestServer(builder);
        var client = server.CreateClient();

        var supported = await client.GetAsync("/facilitator/supported");
        var verify = await client.PostAsync("/facilitator/verify", new StringContent("{}", Encoding.UTF8, "application/json"));
        var settle = await client.PostAsync("/facilitator/settle", new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, supported.StatusCode);
        var kinds = JsonNode.Parse(await supported.Content.ReadAsStringAsync())!["kinds"]!.AsArray();
        Assert.Contains(kinds, kind => (string?)kind!["network"] == "eip155:8453");
        Assert.Contains(kinds, kind => (string?)kind!["network"] == "eip155:84532");
        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, settle.StatusCode);
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "Facilitator options: gas ceiling, allowed assets and authorization policy", Order = 77)]
    public async Task FacilitatorOptions_AreRegisteredAndTheAuthorizationPolicyGuardsTheEndpoints()
    {
        var facilitatorAccount = new Account(X402DevChainFixture.FacilitatorKey);
        var rpcByChainId = new Dictionary<int, string> { [8453] = "https://mainnet.base.org" };
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddX402FacilitatorOptions(options =>
                {
                    options.MaxSettlementGas = X402FacilitatorOptions.DefaultMaxSettlementGas;
                    options.AllowedAssets["eip155:8453"] = new List<string> { "0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913" };
                    options.AuthorizationPolicy = "x402-facilitator";
                    options.RoutePrefix = X402FacilitatorOptions.DefaultRoutePrefix;
                    options.RequiredConfirmations = 1;
                    options.MaxReceiptWaitSeconds = X402FacilitatorOptions.DefaultMaxReceiptWaitSeconds;
                });

                services.AddAuthorization(authorization => authorization.AddPolicy("x402-facilitator", policy => policy.RequireAuthenticatedUser()));

                services.AddAuthentication("header").AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("header", _ => { });
                services.AddX402ExactProcessor(facilitatorAccount, rpcByChainId);
                services.AddControllers().AddX402FacilitatorControllers();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });
        using var server = new TestServer(builder);
        var options = server.Services.GetRequiredService<X402FacilitatorOptions>();
        var anonymous = await server.CreateClient().GetAsync("/facilitator/supported");
        var authenticated = new HttpRequestMessage(HttpMethod.Get, "/facilitator/supported");
        authenticated.Headers.Add("X-User", "settler");
        var allowed = await server.CreateClient().SendAsync(authenticated);

        Assert.Equal("x402-facilitator", options.AuthorizationPolicy);
        Assert.Equal(500000, options.MaxSettlementGas);
        Assert.False(options.IsAssetAllowed("eip155:8453", "0x036CbD53842c5426634e7929541eC2318f3dCF7e"));
        Assert.True(options.IsAssetAllowed("eip155:8453", BaseUsdc));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        await Task.CompletedTask;
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "Replay protection: register a shared IPaymentReplayStore", Order = 78)]
    public async Task ReplayStoreRegistration_IsTheStoreTheMiddlewareReserves()
    {
        using var facilitator = FacilitatorHost();
        var store = new MyRedisPaymentReplayStore();
        using var resource = ResourceHost(facilitator, services =>
        {
            services.AddSingleton<IPaymentReplayStore>(store);
        });
        var requirements = Requirements();
        var payment = await X402DevChainPayments.SignedTransfer(_chain, requirements);
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payment)));

        var first = await Pay(resource, header);
        var replay = await Pay(resource, header);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Single(store.Reserved);
        Assert.Equal(HttpStatusCode.PaymentRequired, replay.StatusCode);
        var body = JsonNode.Parse(await replay.Content.ReadAsStringAsync())!;
        Assert.Equal(X402ErrorCodes.NonceAlreadyUsed, (string?)body["error"]);
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "Replay protection: register the store by type", Order = 79)]
    public void ReplayStoreRegistration_ResolvesTheRegisteredStoreType()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IPaymentReplayStore, MyRedisPaymentReplayStore>();

        using var provider = services.BuildServiceProvider();
        Assert.IsType<MyRedisPaymentReplayStore>(provider.GetRequiredService<IPaymentReplayStore>());
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "Network configuration: RPC endpoint, USDC address and chain id by name", Order = 80)]
    public void NetworkConfiguration_ReturnsTheRpcEndpointUsdcAddressAndChainId()
    {
        var config = NetworkConfiguration.Default;
        var rpcUrl = config.GetRpcEndpoint("base");
        var usdcAddress = config.GetUSDCAddress("base");
        var chainId = config.GetChainId("base");

        Assert.Equal("https://mainnet.base.org", rpcUrl);
        Assert.Equal(BaseUsdc, usdcAddress);
        Assert.Equal(8453, chainId);
        Assert.Equal("eip155:8453", Caip2.FormatEip155(chainId));
        Assert.True(Caip2.TryParseEip155ChainId("eip155:8453", out var parsed));
        Assert.Equal(8453, parsed);
    }

    private async Task<System.Numerics.BigInteger> BalanceOfAsync(string holder) =>
        await _chain.Node.CreateWeb3().Eth.ERC20.GetContractService(_chain.TokenAddress).BalanceOfQueryAsync(holder);

    private PaymentRequirements Requirements()
    {
        var requirements = X402DevChainPayments.Requirements(_chain, "10000", X402DevChainFixture.RecipientAddress);
        requirements.MaxTimeoutSeconds = 60;
        return requirements;
    }

    private static async Task<HttpResponseMessage> Pay(TestServer resource, string header)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/premium");
        request.Headers.Add(X402Headers.PaymentSignature, header);
        return await resource.CreateClient().SendAsync(request);
    }

    private TestServer FacilitatorHost()
    {
        var processor = new X402TransferWithAuthorisation3009Service(
            X402DevChainFixture.FacilitatorKey,
            new Dictionary<int, IClient> { [X402DevChainFixture.ChainId] = _chain.Client });

        return new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IX402PaymentProcessor>(processor);
                services.AddControllers().AddX402FacilitatorControllers();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            }));
    }

    private TestServer ResourceHost(TestServer facilitator, Action<IServiceCollection>? configureServices = null) =>
        new(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddX402Services(FacilitatorUrl);
                services.AddHttpClient(HttpFacilitatorClient.HttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => facilitator.CreateHandler());
                configureServices?.Invoke(services);
            })
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = FacilitatorUrl;
                    options.Routes.Add(new RoutePaymentConfig("/premium", Requirements()));
                });

                app.Run(context => context.Response.WriteAsync("premium content"));
            }));

    private sealed class IntegrationMockFacilitator : IFacilitatorClient
    {
        public Task<VerificationResponse> VerifyAsync(PaymentPayload paymentPayload, PaymentRequirements requirements, CancellationToken cancellationToken = default) =>
            Task.FromResult(new VerificationResponse { IsValid = true, Payer = X402DevChainFixture.PayerAddress });

        public Task<SettlementResponse> SettleAsync(PaymentPayload paymentPayload, PaymentRequirements requirements, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SettlementResponse { Success = true, Transaction = "0x01", Network = requirements.Network });

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SupportedPaymentKindsResponse { Kinds = new List<PaymentKind>() });
    }

    private sealed class MyRedisPaymentReplayStore : IPaymentReplayStore
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> _reservations = new();

        public List<string> Reserved { get; } = new();

        public Task<bool> TryReserveAsync(string paymentKey, TimeSpan retention, CancellationToken cancellationToken = default)
        {
            var reserved = _reservations.TryAdd(paymentKey, DateTimeOffset.UtcNow.Add(retention));
            if (reserved)
                Reserved.Add(paymentKey);
            return Task.FromResult(reserved);
        }
    }

    private sealed class HeaderAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public HeaderAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-User", out var user))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, user.ToString()) }, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
