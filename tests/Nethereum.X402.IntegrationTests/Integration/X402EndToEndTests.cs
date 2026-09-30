using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Client;
using Nethereum.X402.Extensions;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Processors;
using System.Net;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Nethereum.X402.IntegrationTests.Integration;

[Collection("X402 DevChain E2E")]
public class X402EndToEndTests : IAsyncLifetime
{
    private const int CHAIN_ID = 31337;
    private const string NETWORK_NAME = "eip155:31337";

    private const string PAYER_PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    private const string PAYER_ADDRESS = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";

    private const string PAYEE_PRIVATE_KEY = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";
    private const string PAYEE_ADDRESS = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";

    private const string TOKEN_NAME = "USD Coin";
    private const string TOKEN_VERSION = "2";
    private const int TOKEN_DECIMALS = 6;

    private DevChainNode? _node;
    private Nethereum.Web3.Web3? _web3;
    private USDCDeploymentHelper? _usdcHelper;
    private string? _usdcAddress;
    private TestServer? _facilitatorServer;
    private TestServer? _resourceServer;
    private HttpClient? _facilitatorClient;

    private IClient DevChainClient => _node!.CreateWeb3().Client;

    public async Task InitializeAsync()
    {
        _node = DevChainNode.CreateInMemory(new DevChainConfig
        {
            ChainId = CHAIN_ID,
            BaseFee = 1_000_000_000,
            BlockGasLimit = 30_000_000,
            AutoMine = true
        });
        await _node.StartAsync(
            new[] { PAYER_ADDRESS, PAYEE_ADDRESS },
            Nethereum.Web3.Web3.Convert.ToWei(10000));

        var deployerAccount = new Account(PAYER_PRIVATE_KEY, CHAIN_ID);
        _web3 = (Nethereum.Web3.Web3)_node.CreateWeb3(deployerAccount);

        _usdcHelper = new USDCDeploymentHelper(_web3, deployerAccount);

        try
        {
            _usdcAddress = await _usdcHelper.DeployAsync(TOKEN_NAME, "USDC", TOKEN_DECIMALS, TOKEN_VERSION);
            Console.WriteLine($"USDC deployed at: {_usdcAddress}");

            var mintAmount = new BigInteger(1000) * BigInteger.Pow(10, TOKEN_DECIMALS);
            var mintReceipt = await _usdcHelper.MintAsync(PAYER_ADDRESS, mintAmount);
            Console.WriteLine($"Minted 1000 USDC to {PAYER_ADDRESS}, tx: {mintReceipt.TransactionHash}");

            var balance = await _usdcHelper.GetBalanceAsync(PAYER_ADDRESS);
            Console.WriteLine($"Payer balance: {balance} atomic units");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("bytecode not available"))
        {
            Console.WriteLine("WARNING: USDC contract bytecode not available. E2E tests will be skipped.");
            Console.WriteLine("To enable E2E tests, add contract bytecode to USDCDeploymentHelper.CONTRACT_BYTECODE");
            _usdcAddress = "0x" + new string('0', 40);
            return;
        }

        _facilitatorServer = await CreateFacilitatorServerAsync();
        _facilitatorClient = _facilitatorServer.CreateClient();

        _resourceServer = await CreateResourceServerAsync(_facilitatorClient);
    }

    public Task DisposeAsync()
    {
        _facilitatorServer?.Dispose();
        _resourceServer?.Dispose();
        _node?.Dispose();
        return Task.CompletedTask;
    }

    #region Test Cases

    [Fact]
    public async Task Given_ResourceRequiresPayment_When_ClientMakesRequest_Then_PaymentIsAutomaticallySettledAndContentReturned()
    {
        var httpClient = _resourceServer!.CreateClient();
        var options = new X402HttpClientOptions
        {
            PreferredNetwork = NETWORK_NAME,
            PreferredScheme = "exact",
            MaxAmount = "1000000",
        };

        var client = new X402HttpClient(httpClient, PAYER_PRIVATE_KEY, options);

        var initialBalance = await GetUSDCBalanceAsync(PAYER_ADDRESS);
        var initialBalanceUsdc = (decimal)initialBalance / (decimal)Math.Pow(10, TOKEN_DECIMALS);
        Console.WriteLine($"Payer initial balance: {initialBalanceUsdc} USDC");

        var response = await client.GetAsync("/premium");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("premium content", content, StringComparison.OrdinalIgnoreCase);

        Assert.True(response.HasPaymentResponse());
        Assert.True(response.IsPaymentSuccessful());

        var txHash = response.GetTransactionHash();
        Assert.NotNull(txHash);
        Assert.NotEqual("0x", txHash);

        var finalBalance = await GetUSDCBalanceAsync(PAYER_ADDRESS);
        Assert.True(finalBalance < initialBalance, "Balance should decrease after payment");

        var finalBalanceUsdc = (decimal)finalBalance / (decimal)Math.Pow(10, TOKEN_DECIMALS);
        Console.WriteLine($"Payer final balance: {finalBalanceUsdc} USDC");
        Console.WriteLine($"Transaction hash: {txHash}");
    }

    [Fact]
    public async Task Given_FreeContent_When_ClientMakesRequest_Then_NoPaymentIsRequired()
    {
        var httpClient = _resourceServer!.CreateClient();
        var options = new X402HttpClientOptions
        {
            PreferredNetwork = NETWORK_NAME,
            PreferredScheme = "exact",
            MaxAmount = "1000000",
        };

        var client = new X402HttpClient(httpClient, PAYER_PRIVATE_KEY, options);

        var response = await client.GetAsync("/free");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("free content", content, StringComparison.OrdinalIgnoreCase);

        Assert.False(response.HasPaymentResponse());
    }

    #endregion

    #region Test Cases - Architectural Patterns

    [Fact]
    public async Task Given_SelfFacilitatedServer_When_ClientMakesRequest_Then_PaymentIsSettledDirectly()
    {
        var resourceServer = await CreateResourceServer_SelfFacilitated();
        var httpClient = resourceServer.CreateClient();

        var options = new X402HttpClientOptions
        {
            PreferredNetwork = NETWORK_NAME,
            PreferredScheme = "exact",
            MaxAmount = "1000000",
        };

        var client = new X402HttpClient(httpClient, PAYER_PRIVATE_KEY, options);

        var initialBalance = await GetUSDCBalanceAsync(PAYER_ADDRESS);
        var initialBalanceUsdc = (decimal)initialBalance / (decimal)Math.Pow(10, TOKEN_DECIMALS);
        Console.WriteLine($"[Self-Facilitated] Payer initial balance: {initialBalanceUsdc} USDC");

        var response = await client.GetAsync("/premium");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("premium content", content, StringComparison.OrdinalIgnoreCase);

        Assert.True(response.HasPaymentResponse());
        Assert.True(response.IsPaymentSuccessful());

        var txHash = response.GetTransactionHash();
        Assert.NotNull(txHash);
        Assert.NotEqual("0x", txHash);

        var finalBalance = await GetUSDCBalanceAsync(PAYER_ADDRESS);
        Assert.True(finalBalance < initialBalance, "Balance should decrease after payment");

        var finalBalanceUsdc = (decimal)finalBalance / (decimal)Math.Pow(10, TOKEN_DECIMALS);
        Console.WriteLine($"[Self-Facilitated] Payer final balance: {finalBalanceUsdc} USDC");
        Console.WriteLine($"[Self-Facilitated] Transaction hash: {txHash}");
        Console.WriteLine($"[Self-Facilitated] ✓ Payment settled directly by resource server");

        resourceServer.Dispose();
    }

    [Fact]
    public async Task Given_ProxyFacilitatorServer_When_ClientMakesRequest_Then_PaymentIsProxiedAndSettled()
    {
        var facilitatorClient = _facilitatorServer!.CreateClient();
        var resourceServer = await CreateResourceServer_ProxyFacilitator(facilitatorClient);
        var httpClient = resourceServer.CreateClient();

        var options = new X402HttpClientOptions
        {
            PreferredNetwork = NETWORK_NAME,
            PreferredScheme = "exact",
            MaxAmount = "1000000",
        };

        var client = new X402HttpClient(httpClient, PAYER_PRIVATE_KEY, options);

        var initialBalance = await GetUSDCBalanceAsync(PAYER_ADDRESS);
        var initialBalanceUsdc = (decimal)initialBalance / (decimal)Math.Pow(10, TOKEN_DECIMALS);
        Console.WriteLine($"[Proxy Pattern] Payer initial balance: {initialBalanceUsdc} USDC");

        var response = await client.GetAsync("/premium");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("premium content", content, StringComparison.OrdinalIgnoreCase);

        Assert.True(response.HasPaymentResponse());
        Assert.True(response.IsPaymentSuccessful());

        var txHash = response.GetTransactionHash();
        Assert.NotNull(txHash);
        Assert.NotEqual("0x", txHash);

        var finalBalance = await GetUSDCBalanceAsync(PAYER_ADDRESS);
        Assert.True(finalBalance < initialBalance, "Balance should decrease after payment");

        var finalBalanceUsdc = (decimal)finalBalance / (decimal)Math.Pow(10, TOKEN_DECIMALS);
        Console.WriteLine($"[Proxy Pattern] Payer final balance: {finalBalanceUsdc} USDC");
        Console.WriteLine($"[Proxy Pattern] Transaction hash: {txHash}");
        Console.WriteLine($"[Proxy Pattern] ✓ Payment proxied to facilitator and settled");

        resourceServer.Dispose();
    }

    #endregion

    #region Test Cases - Error Scenarios

    [Fact]
    public async Task Error_InvalidSignature_When_ClientSendsModifiedAuth_Then_Returns402WithError()
    {
        var (invalidPaymentHeader, requirements) = CreateInvalidSignaturePayment();

        var paymentPayload = JsonSerializer.Deserialize<PaymentPayload>(
            Encoding.UTF8.GetString(Convert.FromBase64String(invalidPaymentHeader)),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var settleRequest = new Nethereum.X402.Facilitator.FacilitatorSettleRequest
        {
            PaymentPayload = paymentPayload!,
            PaymentRequirements = requirements
        };

        var settleRequestJson = JsonSerializer.Serialize(settleRequest);
        var facilitatorClient = _facilitatorServer!.CreateClient();

        var response = await facilitatorClient.PostAsync("/facilitator/settle",
            new StringContent(settleRequestJson, Encoding.UTF8, "application/json"));

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.OK,
            $"Expected BadRequest or OK, got {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            var settlement = JsonSerializer.Deserialize<SettlementResponse>(responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.NotNull(settlement);
            Assert.False(settlement!.Success);
            Assert.False(string.IsNullOrEmpty(settlement.ErrorReason));
            Console.WriteLine($"[Error Test] ✓ Invalid signature rejected with error: {settlement.ErrorReason}");
        }
        else
        {
            Console.WriteLine($"[Error Test] ✓ Invalid signature rejected with 400 BadRequest");
        }
    }

    [Fact]
    public async Task Error_ExpiredAuthorization_When_ClientSendsExpiredPayment_Then_Returns402WithError()
    {
        var (expiredPaymentHeader, requirements) = CreateExpiredAuthorizationPayment();

        var paymentPayload = JsonSerializer.Deserialize<PaymentPayload>(
            Encoding.UTF8.GetString(Convert.FromBase64String(expiredPaymentHeader)),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var settleRequest = new Nethereum.X402.Facilitator.FacilitatorSettleRequest
        {
            PaymentPayload = paymentPayload!,
            PaymentRequirements = requirements
        };

        var settleRequestJson = JsonSerializer.Serialize(settleRequest);
        var facilitatorClient = _facilitatorServer!.CreateClient();

        var response = await facilitatorClient.PostAsync("/facilitator/settle",
            new StringContent(settleRequestJson, Encoding.UTF8, "application/json"));

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.OK,
            $"Expected BadRequest or OK, got {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            var settlement = JsonSerializer.Deserialize<SettlementResponse>(responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.NotNull(settlement);
            Assert.False(settlement!.Success);
            Assert.False(string.IsNullOrEmpty(settlement.ErrorReason));
            Console.WriteLine($"[Error Test] ✓ Expired authorization rejected with error: {settlement.ErrorReason}");
        }
        else
        {
            Console.WriteLine($"[Error Test] ✓ Expired authorization rejected with 400 BadRequest");
        }
    }

    [Fact]
    public async Task Error_UnsupportedScheme_When_ClientSendsWrongScheme_Then_Returns402WithError()
    {
        var (unsupportedPaymentHeader, requirements) = CreateUnsupportedSchemePayment();

        var paymentPayload = JsonSerializer.Deserialize<PaymentPayload>(
            Encoding.UTF8.GetString(Convert.FromBase64String(unsupportedPaymentHeader)),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var settleRequest = new Nethereum.X402.Facilitator.FacilitatorSettleRequest
        {
            PaymentPayload = paymentPayload!,
            PaymentRequirements = requirements
        };

        var settleRequestJson = JsonSerializer.Serialize(settleRequest);
        var facilitatorClient = _facilitatorServer!.CreateClient();

        var response = await facilitatorClient.PostAsync("/facilitator/settle",
            new StringContent(settleRequestJson, Encoding.UTF8, "application/json"));

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.OK,
            $"Expected BadRequest or OK, got {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            var settlement = JsonSerializer.Deserialize<SettlementResponse>(responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.NotNull(settlement);
            Assert.False(settlement!.Success);
            Assert.Contains("scheme", settlement.ErrorReason?.ToLower() ?? "");
            Console.WriteLine($"[Error Test] ✓ Unsupported scheme rejected with error: {settlement.ErrorReason}");
        }
        else
        {
            Console.WriteLine($"[Error Test] ✓ Unsupported scheme rejected with 400 BadRequest");
        }
    }

    [Fact]
    public async Task Error_MalformedPaymentHeader_When_ClientSendsInvalidBase64_Then_Returns402()
    {
        var facilitatorClient = _facilitatorServer!.CreateClient();
        var resourceServer = await CreateResourceServer_ProxyFacilitator(facilitatorClient);
        var httpClient = resourceServer.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/premium");
        request.Headers.Add("PAYMENT-SIGNATURE", "not-valid-base64!!!");

        var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);

        Console.WriteLine($"[Error Test] ✓ Malformed payment header rejected with 402");

        resourceServer.Dispose();
    }

    [Fact]
    public async Task Error_MissingPaymentHeader_When_ClientOmitsHeader_Then_Returns402WithRequirements()
    {
        var facilitatorClient = _facilitatorServer!.CreateClient();
        var resourceServer = await CreateResourceServer_ProxyFacilitator(facilitatorClient);
        var httpClient = resourceServer.CreateClient();

        var response = await httpClient.GetAsync("/premium");

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        var paymentRequirements = JsonSerializer.Deserialize<PaymentRequired>(responseBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(paymentRequirements);
        Assert.NotNull(paymentRequirements!.Accepts);
        Assert.NotEmpty(paymentRequirements.Accepts);

        Console.WriteLine($"[Error Test] ✓ Missing payment header returns 402 with {paymentRequirements.Accepts.Count} payment options");

        resourceServer.Dispose();
    }

    #endregion

    #region Helper Methods - Error Payload Generation

    private (string header, PaymentRequirements requirements) CreateInvalidSignaturePayment()
    {
        var value = BigInteger.Parse("100000");

        var requirements = new PaymentRequirements
        {
            Scheme = "exact",
            Network = NETWORK_NAME,
            Amount = value.ToString(),
            PayTo = PAYEE_ADDRESS,
            MaxTimeoutSeconds = 300,
            Asset = _usdcAddress,
                Extra = new ExactSchemeExtra { Name = TOKEN_NAME, Version = TOKEN_VERSION }
        };

        var payload = new PaymentPayload
        {
            Accepted = requirements,
            Payload = new ExactSchemePayload
            {
                Signature = "0x0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
                Authorization = new Nethereum.X402.Models.Authorization
                {
                    From = PAYER_ADDRESS,
                    To = PAYEE_ADDRESS,
                    Value = value.ToString(),
                    ValidAfter = "0",
                    ValidBefore = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString(),
                    Nonce = "0x" + Guid.NewGuid().ToString("N")
                }
            }
        };

        var paymentJson = JsonSerializer.Serialize(payload);
        var paymentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(paymentJson));

        return (paymentBase64, requirements);
    }

    private (string header, PaymentRequirements requirements) CreateExpiredAuthorizationPayment()
    {
        var requirements = new PaymentRequirements
        {
            Scheme = "exact",
            Network = NETWORK_NAME,
            Amount = "100000",
            PayTo = PAYEE_ADDRESS,
            MaxTimeoutSeconds = 300,
            Asset = _usdcAddress,
                Extra = new ExactSchemeExtra { Name = TOKEN_NAME, Version = TOKEN_VERSION }
        };

        var payload = new PaymentPayload
        {
            Accepted = requirements,
            Payload = new ExactSchemePayload
            {
                Signature = "0xinvalid",
                Authorization = new Nethereum.X402.Models.Authorization
                {
                    From = PAYER_ADDRESS,
                    To = PAYEE_ADDRESS,
                    Value = "100000",
                    ValidAfter = "0",
                    ValidBefore = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds().ToString(),
                    Nonce = "0x" + Guid.NewGuid().ToString("N")
                }
            }
        };

        var paymentJson = JsonSerializer.Serialize(payload);
        var paymentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(paymentJson));

        return (paymentBase64, requirements);
    }

    private (string header, PaymentRequirements requirements) CreateUnsupportedSchemePayment()
    {
        var payload = new PaymentPayload
        {
            Accepted = new PaymentRequirements { Scheme = "unsupported-scheme" },
            Payload = new { test = "data" }
        };

        var requirements = new PaymentRequirements
        {
            Scheme = "exact",
            Network = NETWORK_NAME,
            Amount = "100000",
            PayTo = PAYEE_ADDRESS,
            MaxTimeoutSeconds = 300,
            Asset = _usdcAddress,
                Extra = new ExactSchemeExtra { Name = TOKEN_NAME, Version = TOKEN_VERSION }
        };

        var paymentJson = JsonSerializer.Serialize(payload);
        var paymentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(paymentJson));

        return (paymentBase64, requirements);
    }

    #endregion

    #region Helper Methods - Contract Interaction

    private async Task<BigInteger> GetUSDCBalanceAsync(string address)
    {
        if (_usdcHelper == null)
        {
            return BigInteger.Zero;
        }

        return await _usdcHelper.GetBalanceAsync(address);
    }

    #endregion

    #region Helper Methods - Server Setup

    private async Task<TestServer> CreateFacilitatorServerAsync()
    {
        var payeeAccount = new Account(PAYEE_PRIVATE_KEY, CHAIN_ID);
        var processor = new X402TransferWithAuthorisation3009Service(
            payeeAccount, new Dictionary<int, IClient> { [CHAIN_ID] = DevChainClient });

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddSingleton<IX402PaymentProcessor>(processor);

                    services.AddControllers()
                        .AddX402FacilitatorControllers();
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                    });
                });
            });

        var host = await hostBuilder.StartAsync();
        return host.GetTestServer();
    }

    private async Task<TestServer> CreateResourceServerAsync(HttpClient facilitatorClient)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/free", async context =>
                        {
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync(
                                JsonSerializer.Serialize(new { message = "This is free content" }));
                        });

                        endpoints.MapGet("/premium", async context =>
                        {
                            var paymentRequirements = new PaymentRequirements
                            {
                                Scheme = "exact",
                                Network = NETWORK_NAME,
                                Amount = "100000",
                                PayTo = PAYEE_ADDRESS,
                                MaxTimeoutSeconds = 300,
                                Asset = _usdcAddress,
                Extra = new ExactSchemeExtra { Name = TOKEN_NAME, Version = TOKEN_VERSION }
                            };

                            if (!context.Request.Headers.ContainsKey("PAYMENT-SIGNATURE"))
                            {
                                context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
                                context.Response.ContentType = "application/json";

                                var paymentRequirementsResponse = new PaymentRequired
                                {
                                    Accepts = new List<PaymentRequirements> { paymentRequirements }
                                };

                                await context.Response.WriteAsync(
                                    JsonSerializer.Serialize(paymentRequirementsResponse));
                                return;
                            }

                            var paymentHeader = context.Request.Headers["PAYMENT-SIGNATURE"].ToString();

                            try
                            {
                                var paymentJson = Encoding.UTF8.GetString(Convert.FromBase64String(paymentHeader));
                                var paymentPayload = JsonSerializer.Deserialize<PaymentPayload>(paymentJson,
                                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                                var settleRequest = new Nethereum.X402.Facilitator.FacilitatorSettleRequest
                                {
                                    PaymentPayload = paymentPayload!,
                                    PaymentRequirements = paymentRequirements
                                };

                                var settleRequestJson = JsonSerializer.Serialize(settleRequest);

                                var facilitatorResponse = await facilitatorClient.PostAsync("/facilitator/settle",
                                    new StringContent(settleRequestJson, Encoding.UTF8, "application/json"));

                                if (facilitatorResponse.IsSuccessStatusCode)
                                {
                                    var settlementJson = await facilitatorResponse.Content.ReadAsStringAsync();
                                    var settlement = JsonSerializer.Deserialize<SettlementResponse>(settlementJson,
                                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                                    context.Response.ContentType = "application/json";

                                    var settlementBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(settlementJson));
                                    context.Response.Headers.Append("PAYMENT-RESPONSE", settlementBase64);

                                    await context.Response.WriteAsync(
                                        JsonSerializer.Serialize(new { message = "This is premium content" }));
                                }
                                else
                                {
                                    context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
                                    await context.Response.WriteAsync(
                                        JsonSerializer.Serialize(new { error = "Payment settlement failed" }));
                                }
                            }
                            catch
                            {
                                context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
                                await context.Response.WriteAsync(
                                    JsonSerializer.Serialize(new { error = "Payment verification failed" }));
                            }
                        });
                    });
                });
            });

        var host = await hostBuilder.StartAsync();
        return host.GetTestServer();
    }

    private async Task<TestServer> CreateResourceServer_SelfFacilitated()
    {
        var payeeAccount = new Account(PAYEE_PRIVATE_KEY, CHAIN_ID);
        var processor = new X402TransferWithAuthorisation3009Service(
            payeeAccount, new Dictionary<int, IClient> { [CHAIN_ID] = DevChainClient });

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddSingleton<IX402PaymentProcessor>(processor);

                    services.AddRouting();
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/free", async context =>
                        {
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync(
                                JsonSerializer.Serialize(new { message = "This is free content" }));
                        });

                        endpoints.MapGet("/premium", async context =>
                        {
                            var paymentRequirements = new PaymentRequirements
                            {
                                Scheme = "exact",
                                Network = NETWORK_NAME,
                                Amount = "100000",
                                PayTo = PAYEE_ADDRESS,
                                MaxTimeoutSeconds = 300,
                                Asset = _usdcAddress,
                Extra = new ExactSchemeExtra { Name = TOKEN_NAME, Version = TOKEN_VERSION }
                            };

                            if (!context.Request.Headers.ContainsKey("PAYMENT-SIGNATURE"))
                            {
                                context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
                                context.Response.ContentType = "application/json";

                                var paymentRequirementsResponse = new PaymentRequired
                                {
                                    Accepts = new List<PaymentRequirements> { paymentRequirements }
                                };

                                await context.Response.WriteAsync(
                                    JsonSerializer.Serialize(paymentRequirementsResponse));
                                return;
                            }

                            var paymentHeader = context.Request.Headers["PAYMENT-SIGNATURE"].ToString();

                            try
                            {
                                var paymentJson = Encoding.UTF8.GetString(Convert.FromBase64String(paymentHeader));
                                var paymentPayload = JsonSerializer.Deserialize<PaymentPayload>(paymentJson,
                                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                                var processor = context.RequestServices.GetRequiredService<IX402PaymentProcessor>();
                                var settlement = await processor.SettlePaymentAsync(
                                    paymentPayload!,
                                    paymentRequirements,
                                    context.RequestAborted);

                                if (settlement.Success)
                                {
                                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                                    context.Response.ContentType = "application/json";

                                    var settlementJson = JsonSerializer.Serialize(settlement);
                                    var settlementBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(settlementJson));
                                    context.Response.Headers.Append("PAYMENT-RESPONSE", settlementBase64);

                                    await context.Response.WriteAsync(
                                        JsonSerializer.Serialize(new { message = "This is premium content" }));
                                }
                                else
                                {
                                    context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
                                    await context.Response.WriteAsync(
                                        JsonSerializer.Serialize(new { error = "Payment settlement failed", reason = settlement.ErrorReason }));
                                }
                            }
                            catch
                            {
                                context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
                                await context.Response.WriteAsync(
                                    JsonSerializer.Serialize(new { error = "Payment verification failed" }));
                            }
                        });
                    });
                });
            });

        var host = await hostBuilder.StartAsync();
        return host.GetTestServer();
    }

    private async Task<TestServer> CreateResourceServer_ProxyFacilitator(HttpClient facilitatorClient)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/free", async context =>
                        {
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync(
                                JsonSerializer.Serialize(new { message = "This is free content" }));
                        });

                        endpoints.MapGet("/premium", async context =>
                        {
                            var paymentRequirements = new PaymentRequirements
                            {
                                Scheme = "exact",
                                Network = NETWORK_NAME,
                                Amount = "100000",
                                PayTo = PAYEE_ADDRESS,
                                MaxTimeoutSeconds = 300,
                                Asset = _usdcAddress,
                Extra = new ExactSchemeExtra { Name = TOKEN_NAME, Version = TOKEN_VERSION }
                            };

                            if (!context.Request.Headers.ContainsKey("PAYMENT-SIGNATURE"))
                            {
                                context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
                                context.Response.ContentType = "application/json";

                                var paymentRequirementsResponse = new PaymentRequired
                                {
                                    Accepts = new List<PaymentRequirements> { paymentRequirements }
                                };

                                await context.Response.WriteAsync(
                                    JsonSerializer.Serialize(paymentRequirementsResponse));
                                return;
                            }

                            var paymentHeader = context.Request.Headers["PAYMENT-SIGNATURE"].ToString();

                            try
                            {
                                var paymentJson = Encoding.UTF8.GetString(Convert.FromBase64String(paymentHeader));
                                var paymentPayload = JsonSerializer.Deserialize<PaymentPayload>(paymentJson,
                                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                                var settleRequest = new Nethereum.X402.Facilitator.FacilitatorSettleRequest
                                {
                                    PaymentPayload = paymentPayload!,
                                    PaymentRequirements = paymentRequirements
                                };

                                var settleRequestJson = JsonSerializer.Serialize(settleRequest);

                                var facilitatorResponse = await facilitatorClient.PostAsync("/facilitator/settle",
                                    new StringContent(settleRequestJson, Encoding.UTF8, "application/json"));

                                if (facilitatorResponse.IsSuccessStatusCode)
                                {
                                    var settlementJson = await facilitatorResponse.Content.ReadAsStringAsync();
                                    var settlement = JsonSerializer.Deserialize<SettlementResponse>(settlementJson,
                                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                                    context.Response.ContentType = "application/json";

                                    var settlementBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(settlementJson));
                                    context.Response.Headers.Append("PAYMENT-RESPONSE", settlementBase64);

                                    await context.Response.WriteAsync(
                                        JsonSerializer.Serialize(new { message = "This is premium content" }));
                                }
                                else
                                {
                                    context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
                                    await context.Response.WriteAsync(
                                        JsonSerializer.Serialize(new { error = "Payment settlement failed" }));
                                }
                            }
                            catch
                            {
                                context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
                                await context.Response.WriteAsync(
                                    JsonSerializer.Serialize(new { error = "Payment verification failed" }));
                            }
                        });
                    });
                });
            });

        var host = await hostBuilder.StartAsync();
        return host.GetTestServer();
    }

    #endregion
}
