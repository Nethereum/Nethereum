using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Documentation;
using Nethereum.JsonRpc.Client;
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Client;
using Nethereum.X402.Facilitator;
using Nethereum.X402.IntegrationTests.AspNetCore;
using Nethereum.X402.Models;
using Nethereum.X402.Processors;
using Nethereum.X402.Server;
using Xunit;
using Authorization = Nethereum.X402.Models.Authorization;

namespace Nethereum.X402.IntegrationTests.Integration;

public class X402PaymentFlowAndSponsoringDocExamples
{
    private const string FacilitatorKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    private const string PayerKey = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";
    private const string Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66";
    private const string ReceiverAddress = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C";
    private const string UsdcBaseSepolia = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "Upfront payment flow: settle before the handler runs", Order = 13)]
    public async Task UpfrontRoute_SettlesBeforeTheHandlerRunsAndTheResponseCarriesThePaymentResponse()
    {
        var usdcAddress = UsdcBaseSepolia;
        var receiverAddress = ReceiverAddress;

        var facilitator = new MockFacilitatorClient
        {
            SettleResponse = new SettlementResponse
            {
                Success = true,
                Transaction = "0x1234",
                Network = "eip155:84532",
                Payer = Payer
            }
        };

        var builder = new WebHostBuilder()
            .ConfigureServices(services => services.AddSingleton<IFacilitatorClient>(facilitator))
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "https://x402.org/facilitator";
                    options.Routes.Add(new RoutePaymentConfig("/api/generate", new PaymentRequirements
                    {
                        Scheme = "exact",
                        Network = "eip155:84532",
                        Amount = "10000",
                        Asset = usdcAddress,
                        PayTo = receiverAddress,
                        MaxTimeoutSeconds = 60,
                        Extra = new ExactSchemeExtra
                        {
                            AssetTransferMethod = X402AssetTransferMethods.Eip3009,
                            PaymentFlow = X402PaymentFlows.Upfront,
                            Name = "USDC",
                            Version = "2"
                        }
                    }));
                });

                app.Run(context => context.Response.WriteAsync("generated"));
            });

        using var server = new TestServer(builder);
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Add(X402Headers.PaymentSignature, EncodePaymentHeader());

        var response = await client.GetAsync("/api/generate");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(facilitator.SettleCalled);
        Assert.True(facilitator.VerifyCalled);
        Assert.True(response.Headers.Contains(X402Headers.PaymentResponse));
        Assert.True(response.Headers.CacheControl!.Private);
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "eip2612GasSponsoring: declare the extension on a route", Order = 50)]
    public async Task Route_DeclaresEip2612GasSponsoringInThePaymentRequired()
    {
        var usdcAddress = UsdcBaseSepolia;
        var receiverAddress = ReceiverAddress;

        var builder = new WebHostBuilder()
            .ConfigureServices(services => services.AddSingleton<IFacilitatorClient>(new MockFacilitatorClient()))
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "https://x402.org/facilitator";
                    options.Routes.Add(new RoutePaymentConfig("/api/premium-permit2", new PaymentRequirements
                    {
                        Scheme = "exact",
                        Network = "eip155:84532",
                        Amount = "10000",
                        Asset = usdcAddress,
                        PayTo = receiverAddress,
                        MaxTimeoutSeconds = 60,
                        Extra = new ExactSchemeExtra
                        {
                            AssetTransferMethod = X402AssetTransferMethods.Permit2,
                            Name = "USDC",
                            Version = "2"
                        }
                    })
                    {
                        Extensions = new Dictionary<string, object>
                        {
                            [X402ExtensionKeys.Eip2612GasSponsoring] = X402GasSponsoringExtensions.DeclareEip2612()
                        }
                    });
                });

                app.Run(context => context.Response.WriteAsync("premium"));
            });

        using var server = new TestServer(builder);

        var response = await server.CreateClient().GetAsync("/api/premium-permit2");

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.NotNull(body["extensions"]![X402ExtensionKeys.Eip2612GasSponsoring]);
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "Gas sponsoring: client with an RPC endpoint per chain", Order = 51)]
    public void Client_IsGivenAnRpcEndpointPerChainForGasSponsoring()
    {
        var payerPrivateKey = PayerKey;

        var httpClient = new HttpClient();
        var options = new X402HttpClientOptions
        {
            MaxAmount = "100000",
            PreferredNetwork = "eip155:84532",
            PreferredScheme = "exact",
            RpcClientsByChainId = new Dictionary<int, IClient>
            {
                [84532] = new RpcClient(new Uri("https://sepolia.base.org"))
            }
        };

        var x402Client = new X402HttpClient(httpClient, payerPrivateKey, options);

        Assert.NotNull(x402Client.Address);
        Assert.Single(options.RpcClientsByChainId);
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "eip2612GasSponsoring: the facilitator advertises the extension", Order = 52)]
    public async Task Facilitator_AdvertisesEip2612GasSponsoringInSupported()
    {
        var facilitatorPrivateKey = FacilitatorKey;

        var clients = new Dictionary<int, IClient>
        {
            [84532] = new RpcClient(new Uri("https://sepolia.base.org"))
        };
        var permit2Service = new X402ExactPermit2Service(facilitatorPrivateKey, clients);

        var supported = await permit2Service.GetSupportedAsync();

        Assert.Contains(X402ExtensionKeys.Eip2612GasSponsoring, supported.Extensions);
        Assert.DoesNotContain(X402ExtensionKeys.Erc20ApprovalGasSponsoring, supported.Extensions);
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "erc20ApprovalGasSponsoring: declare the extension on a route", Order = 60)]
    public async Task Route_DeclaresErc20ApprovalGasSponsoringInThePaymentRequired()
    {
        var usdcAddress = UsdcBaseSepolia;
        var receiverAddress = ReceiverAddress;

        var builder = new WebHostBuilder()
            .ConfigureServices(services => services.AddSingleton<IFacilitatorClient>(new MockFacilitatorClient()))
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "https://x402.org/facilitator";
                    options.Routes.Add(new RoutePaymentConfig("/api/premium-permit2", new PaymentRequirements
                    {
                        Scheme = "exact",
                        Network = "eip155:84532",
                        Amount = "10000",
                        Asset = usdcAddress,
                        PayTo = receiverAddress,
                        MaxTimeoutSeconds = 60,
                        Extra = new ExactSchemeExtra { AssetTransferMethod = X402AssetTransferMethods.Permit2 }
                    })
                    {
                        Extensions = new Dictionary<string, object>
                        {
                            [X402ExtensionKeys.Erc20ApprovalGasSponsoring] = X402GasSponsoringExtensions.DeclareErc20Approval()
                        }
                    });
                });

                app.Run(context => context.Response.WriteAsync("premium"));
            });

        using var server = new TestServer(builder);

        var response = await server.CreateClient().GetAsync("/api/premium-permit2");

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.NotNull(body["extensions"]![X402ExtensionKeys.Erc20ApprovalGasSponsoring]);
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "erc20ApprovalGasSponsoring: the facilitator opts in", Order = 61)]
    public async Task Facilitator_OptsInToErc20ApprovalGasSponsoring()
    {
        var facilitatorPrivateKey = FacilitatorKey;
        var builder = WebApplication.CreateBuilder();

        var clients = new Dictionary<int, IClient>
        {
            [84532] = new RpcClient(new Uri("https://sepolia.base.org"))
        };

        var sponsoring = new Erc20ApprovalGasSponsoringOptions
        {
            Enabled = true,
            MaxSponsoredGasCost = Nethereum.Util.UnitConversion.Convert.ToWei(0.001m)
        };

        var processor = new X402ExactSchemeProcessor(
            new X402TransferWithAuthorisation3009Service(facilitatorPrivateKey, clients),
            new X402ExactPermit2Service(facilitatorPrivateKey, clients, sponsoring));

        builder.Services.AddSingleton<IX402PaymentProcessor>(processor);

        var supported = await processor.GetSupportedAsync();
        using var provider = builder.Services.BuildServiceProvider();
        Assert.Same(processor, provider.GetRequiredService<IX402PaymentProcessor>());

        Assert.Contains(X402ExtensionKeys.Eip2612GasSponsoring, supported.Extensions);
        Assert.Contains(X402ExtensionKeys.Erc20ApprovalGasSponsoring, supported.Extensions);
    }

    private static string EncodePaymentHeader()
    {
        var payment = new PaymentPayload
        {
            Accepted = new PaymentRequirements
            {
                Scheme = "exact",
                Network = "eip155:84532",
                Amount = "10000",
                Asset = UsdcBaseSepolia,
                PayTo = ReceiverAddress,
                MaxTimeoutSeconds = 60,
                Extra = new ExactSchemeExtra
                {
                    AssetTransferMethod = X402AssetTransferMethods.Eip3009,
                    PaymentFlow = X402PaymentFlows.Upfront,
                    Name = "USDC",
                    Version = "2"
                }
            },
            Payload = new ExactSchemePayload
            {
                Signature = "0xsignature",
                Authorization = new Authorization
                {
                    From = Payer,
                    To = ReceiverAddress,
                    Value = "10000",
                    ValidAfter = "0",
                    ValidBefore = "1740672154",
                    Nonce = "0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480"
                }
            }
        };
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payment)));
    }
}
