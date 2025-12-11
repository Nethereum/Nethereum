using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
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

/// <summary>
/// End-to-End integration tests for complete x402 payment flow.
///
/// Test Flow:
/// 1. Deploy USDC contract to local Anvil
/// 2. Mint USDC to payer account
/// 3. Start resource server (returns 402)
/// 4. Start facilitator server (verifies and settles)
/// 5. Client makes request → receives 402 → pays → receives content
/// 6. Verify payment settled on-chain
///
/// Traceability:
/// - Spec: Complete x402 flow (Sections 4-7)
/// - Use Cases: UC-E2E-1 Complete Payment Flow
/// - Requirements: Full end-to-end payment verification
/// </summary>
public class X402EndToEndTests : IAsyncLifetime
{
    // Anvil local network
    private const string RPC_URL = "http://localhost:8545";
    private const int CHAIN_ID = 84532; // base-sepolia (Anvil default)
    private const string NETWORK_NAME = "localhost";

    // Test accounts (Anvil defaults)
    private const string PAYER_PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    private const string PAYER_ADDRESS = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";

    private const string PAYEE_PRIVATE_KEY = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";
    private const string PAYEE_ADDRESS = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";

    // USDC EIP-3009 details
    private const string TOKEN_NAME = "USD Coin";
    private const string TOKEN_VERSION = "2";
    private const int TOKEN_DECIMALS = 6;

    private Nethereum.Web3.Web3? _web3;
    private USDCDeploymentHelper? _usdcHelper;
    private string? _usdcAddress;
    private TestServer? _facilitatorServer;
    private TestServer? _resourceServer;
    private HttpClient? _facilitatorClient;

    /// <summary>
    /// Initialize test environment - deploy USDC, mint tokens, start servers
    /// </summary>
    public async Task InitializeAsync()
    {
        // Setup Web3 with deployer account (Account 0 - has ETH for gas)
        var deployerAccount = new Account(PAYER_PRIVATE_KEY, CHAIN_ID);
        _web3 = new Nethereum.Web3.Web3(deployerAccount, RPC_URL);

        // Deploy USDC contract
        _usdcHelper = new USDCDeploymentHelper(_web3, deployerAccount);

        try
        {
            _usdcAddress = await _usdcHelper.DeployAsync(TOKEN_NAME, "USDC", TOKEN_DECIMALS, TOKEN_VERSION);
            Console.WriteLine($"USDC deployed at: {_usdcAddress}");

            // Mint tokens to payer account (1000 USDC)
            var mintAmount = new BigInteger(1000) * BigInteger.Pow(10, TOKEN_DECIMALS);
            var mintReceipt = await _usdcHelper.MintAsync(PAYER_ADDRESS, mintAmount);
            Console.WriteLine($"Minted 1000 USDC to {PAYER_ADDRESS}, tx: {mintReceipt.TransactionHash}");

            // Verify minting
            var balance = await _usdcHelper.GetBalanceAsync(PAYER_ADDRESS);
            Console.WriteLine($"Payer balance: {balance} atomic units");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("bytecode not available"))
        {
            // Contract bytecode not available - tests will be skipped
            Console.WriteLine("WARNING: USDC contract bytecode not available. E2E tests will be skipped.");
            Console.WriteLine("To enable E2E tests, add contract bytecode to USDCDeploymentHelper.CONTRACT_BYTECODE");
            _usdcAddress = "0x" + new string('0', 40); // Placeholder
            return; // Skip server setup if contract can't be deployed
        }

        // Start facilitator server
        _facilitatorServer = await CreateFacilitatorServerAsync();
        _facilitatorClient = _facilitatorServer.CreateClient();

        // Start resource server (needs facilitator client for inter-server communication)
        _resourceServer = await CreateResourceServerAsync(_facilitatorClient);
    }

    /// <summary>
    /// Cleanup test environment
    /// </summary>
    public Task DisposeAsync()
    {
        _facilitatorServer?.Dispose();
        _resourceServer?.Dispose();
        return Task.CompletedTask;
    }

    #region Test Cases

    /// <summary>
    /// Spec: Section 4 - Complete automatic payment flow
    /// Use Case: UC-E2E-1 - Client makes request, receives 402, pays, receives content
    /// </summary>
    [Fact]
    public async Task Given_ResourceRequiresPayment_When_ClientMakesRequest_Then_PaymentIsAutomaticallySettledAndContentReturned()
    {
        // Arrange
        var httpClient = _resourceServer!.CreateClient();
        var options = new X402HttpClientOptions
        {
            PreferredNetwork = NETWORK_NAME,
            PreferredScheme = "exact",
            MaxPaymentAmount = 1.0m,
            TokenName = TOKEN_NAME,
            TokenVersion = TOKEN_VERSION,
            ChainId = CHAIN_ID,
            TokenAddress = _usdcAddress!
        };

        var client = new X402HttpClient(httpClient, PAYER_PRIVATE_KEY, options);

        // Get initial balance
        var initialBalance = await GetUSDCBalanceAsync(PAYER_ADDRESS);
        var initialBalanceUsdc = (decimal)initialBalance / (decimal)Math.Pow(10, TOKEN_DECIMALS);
        Console.WriteLine($"Payer initial balance: {initialBalanceUsdc} USDC");

        // Act
        var response = await client.GetAsync("/premium");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("premium content", content, StringComparison.OrdinalIgnoreCase);

        // Verify payment was made
        Assert.True(response.HasPaymentResponse());
        Assert.True(response.IsPaymentSuccessful());

        var txHash = response.GetTransactionHash();
        Assert.NotNull(txHash);
        Assert.NotEqual("0x", txHash);

        // Verify on-chain balance changed
        var finalBalance = await GetUSDCBalanceAsync(PAYER_ADDRESS);
        Assert.True(finalBalance < initialBalance, "Balance should decrease after payment");

        var finalBalanceUsdc = (decimal)finalBalance / (decimal)Math.Pow(10, TOKEN_DECIMALS);
        Console.WriteLine($"Payer final balance: {finalBalanceUsdc} USDC");
        Console.WriteLine($"Transaction hash: {txHash}");
    }

    /// <summary>
    /// Spec: Section 4.2 - Free content pass-through
    /// Use Case: UC-E2E-3 - Client accesses free content without payment
    /// </summary>
    [Fact]
    public async Task Given_FreeContent_When_ClientMakesRequest_Then_NoPaymentIsRequired()
    {
        // Arrange
        var httpClient = _resourceServer!.CreateClient();
        var options = new X402HttpClientOptions
        {
            PreferredNetwork = NETWORK_NAME,
            PreferredScheme = "exact",
            MaxPaymentAmount = 1.0m,
            TokenName = TOKEN_NAME,
            TokenVersion = TOKEN_VERSION,
            ChainId = CHAIN_ID,
            TokenAddress = _usdcAddress!
        };

        var client = new X402HttpClient(httpClient, PAYER_PRIVATE_KEY, options);

        // Act
        var response = await client.GetAsync("/free");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("free content", content, StringComparison.OrdinalIgnoreCase);

        // Verify no payment was made
        Assert.False(response.HasPaymentResponse());
    }

    #endregion

    #region Test Cases - Architectural Patterns

    /// <summary>
    /// Spec: Section 4 - Self-facilitated architecture pattern
    /// Use Case: UC-E2E-ARCH-1 - Resource server handles payments directly
    /// Architecture: Resource Server IS Facilitator
    /// </summary>
    [Fact]
    public async Task Given_SelfFacilitatedServer_When_ClientMakesRequest_Then_PaymentIsSettledDirectly()
    {
        // Arrange - Create self-facilitated resource server
        var resourceServer = await CreateResourceServer_SelfFacilitated();
        var httpClient = resourceServer.CreateClient();

        var options = new X402HttpClientOptions
        {
            PreferredNetwork = NETWORK_NAME,
            PreferredScheme = "exact",
            MaxPaymentAmount = 1.0m,
            TokenName = TOKEN_NAME,
            TokenVersion = TOKEN_VERSION,
            ChainId = CHAIN_ID,
            TokenAddress = _usdcAddress!
        };

        var client = new X402HttpClient(httpClient, PAYER_PRIVATE_KEY, options);

        // Get initial balance
        var initialBalance = await GetUSDCBalanceAsync(PAYER_ADDRESS);
        var initialBalanceUsdc = (decimal)initialBalance / (decimal)Math.Pow(10, TOKEN_DECIMALS);
        Console.WriteLine($"[Self-Facilitated] Payer initial balance: {initialBalanceUsdc} USDC");

        // Act
        var response = await client.GetAsync("/premium");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("premium content", content, StringComparison.OrdinalIgnoreCase);

        // Verify payment was made
        Assert.True(response.HasPaymentResponse());
        Assert.True(response.IsPaymentSuccessful());

        var txHash = response.GetTransactionHash();
        Assert.NotNull(txHash);
        Assert.NotEqual("0x", txHash);

        // Verify on-chain balance changed
        var finalBalance = await GetUSDCBalanceAsync(PAYER_ADDRESS);
        Assert.True(finalBalance < initialBalance, "Balance should decrease after payment");

        var finalBalanceUsdc = (decimal)finalBalance / (decimal)Math.Pow(10, TOKEN_DECIMALS);
        Console.WriteLine($"[Self-Facilitated] Payer final balance: {finalBalanceUsdc} USDC");
        Console.WriteLine($"[Self-Facilitated] Transaction hash: {txHash}");
        Console.WriteLine($"[Self-Facilitated] ✓ Payment settled directly by resource server");

        // Clean up
        resourceServer.Dispose();
    }

    /// <summary>
    /// Spec: Section 4 - Proxy facilitator architecture pattern
    /// Use Case: UC-E2E-ARCH-2 - Resource server delegates to external facilitator
    /// Architecture: Resource Server CONSUMES Facilitator
    /// </summary>
    [Fact]
    public async Task Given_ProxyFacilitatorServer_When_ClientMakesRequest_Then_PaymentIsProxiedAndSettled()
    {
        // Arrange - Create facilitator and resource server (proxy pattern)
        // Note: _facilitatorServer is already created in InitializeAsync
        var facilitatorClient = _facilitatorServer!.CreateClient();
        var resourceServer = await CreateResourceServer_ProxyFacilitator(facilitatorClient);
        var httpClient = resourceServer.CreateClient();

        var options = new X402HttpClientOptions
        {
            PreferredNetwork = NETWORK_NAME,
            PreferredScheme = "exact",
            MaxPaymentAmount = 1.0m,
            TokenName = TOKEN_NAME,
            TokenVersion = TOKEN_VERSION,
            ChainId = CHAIN_ID,
            TokenAddress = _usdcAddress!
        };

        var client = new X402HttpClient(httpClient, PAYER_PRIVATE_KEY, options);

        // Get initial balance
        var initialBalance = await GetUSDCBalanceAsync(PAYER_ADDRESS);
        var initialBalanceUsdc = (decimal)initialBalance / (decimal)Math.Pow(10, TOKEN_DECIMALS);
        Console.WriteLine($"[Proxy Pattern] Payer initial balance: {initialBalanceUsdc} USDC");

        // Act
        var response = await client.GetAsync("/premium");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("premium content", content, StringComparison.OrdinalIgnoreCase);

        // Verify payment was made
        Assert.True(response.HasPaymentResponse());
        Assert.True(response.IsPaymentSuccessful());

        var txHash = response.GetTransactionHash();
        Assert.NotNull(txHash);
        Assert.NotEqual("0x", txHash);

        // Verify on-chain balance changed
        var finalBalance = await GetUSDCBalanceAsync(PAYER_ADDRESS);
        Assert.True(finalBalance < initialBalance, "Balance should decrease after payment");

        var finalBalanceUsdc = (decimal)finalBalance / (decimal)Math.Pow(10, TOKEN_DECIMALS);
        Console.WriteLine($"[Proxy Pattern] Payer final balance: {finalBalanceUsdc} USDC");
        Console.WriteLine($"[Proxy Pattern] Transaction hash: {txHash}");
        Console.WriteLine($"[Proxy Pattern] ✓ Payment proxied to facilitator and settled");

        // Clean up
        resourceServer.Dispose();
    }

    #endregion

    #region Test Cases - Error Scenarios

    /// <summary>
    /// Spec: Section 5.2 - Invalid signature error handling
    /// Use Case: UC-E2E-ERR-1 - Server rejects payment with invalid signature
    /// Error Code: invalid_exact_evm_payload_signature
    /// </summary>
    [Fact]
    public async Task Error_InvalidSignature_When_ClientSendsModifiedAuth_Then_Returns402WithError()
    {
        // Arrange
        var (invalidPaymentHeader, requirements) = CreateInvalidSignaturePayment();

        // Create facilitator settle request
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

        // Act
        var response = await facilitatorClient.PostAsync("/facilitator/settle",
            new StringContent(settleRequestJson, Encoding.UTF8, "application/json"));

        // Assert
        // Facilitator should return error - either 400 BadRequest or 200 OK with error in body
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
            Assert.Equal(X402ErrorCodes.InvalidSignature, settlement.ErrorReason);
            Console.WriteLine($"[Error Test] ✓ Invalid signature rejected with error: {settlement.ErrorReason}");
        }
        else
        {
            Console.WriteLine($"[Error Test] ✓ Invalid signature rejected with 400 BadRequest");
        }
    }

    /// <summary>
    /// Spec: Section 5.2 - Expired authorization error handling
    /// Use Case: UC-E2E-ERR-2 - Server rejects payment with expired authorization
    /// Error Code: invalid_exact_evm_payload_authorization_valid_before
    /// </summary>
    [Fact]
    public async Task Error_ExpiredAuthorization_When_ClientSendsExpiredPayment_Then_Returns402WithError()
    {
        // Arrange
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

        // Act
        var response = await facilitatorClient.PostAsync("/facilitator/settle",
            new StringContent(settleRequestJson, Encoding.UTF8, "application/json"));

        // Assert
        // Facilitator should return error - either 400 BadRequest or 200 OK with error in body
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
            Assert.Equal(X402ErrorCodes.InvalidValidBefore, settlement.ErrorReason);
            Console.WriteLine($"[Error Test] ✓ Expired authorization rejected with error: {settlement.ErrorReason}");
        }
        else
        {
            Console.WriteLine($"[Error Test] ✓ Expired authorization rejected with 400 BadRequest");
        }
    }

    /// <summary>
    /// Spec: Section 5.2 - Unsupported scheme error handling
    /// Use Case: UC-E2E-ERR-3 - Server rejects payment with unsupported scheme
    /// Error Code: unsupported_scheme
    /// </summary>
    [Fact]
    public async Task Error_UnsupportedScheme_When_ClientSendsWrongScheme_Then_Returns402WithError()
    {
        // Arrange
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

        // Act
        var response = await facilitatorClient.PostAsync("/facilitator/settle",
            new StringContent(settleRequestJson, Encoding.UTF8, "application/json"));

        // Assert
        // Facilitator should return error - either 400 BadRequest or 200 OK with error in body
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

    /// <summary>
    /// Spec: Section 5.3 - Malformed payment header error handling
    /// Use Case: UC-E2E-ERR-4 - Server rejects request with malformed X-PAYMENT header
    /// </summary>
    [Fact]
    public async Task Error_MalformedPaymentHeader_When_ClientSendsInvalidBase64_Then_Returns402()
    {
        // Arrange - Create resource server
        var facilitatorClient = _facilitatorServer!.CreateClient();
        var resourceServer = await CreateResourceServer_ProxyFacilitator(facilitatorClient);
        var httpClient = resourceServer.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/premium");
        request.Headers.Add("X-PAYMENT", "not-valid-base64!!!"); // Invalid base64

        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);

        Console.WriteLine($"[Error Test] ✓ Malformed payment header rejected with 402");

        // Clean up
        resourceServer.Dispose();
    }

    /// <summary>
    /// Spec: Section 4.1 - Missing payment header
    /// Use Case: UC-E2E-ERR-5 - Server returns 402 when payment header is missing
    /// </summary>
    [Fact]
    public async Task Error_MissingPaymentHeader_When_ClientOmitsHeader_Then_Returns402WithRequirements()
    {
        // Arrange - Create resource server
        var facilitatorClient = _facilitatorServer!.CreateClient();
        var resourceServer = await CreateResourceServer_ProxyFacilitator(facilitatorClient);
        var httpClient = resourceServer.CreateClient();

        // Act - Request without X-PAYMENT header
        var response = await httpClient.GetAsync("/premium");

        // Assert
        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        var paymentRequirements = JsonSerializer.Deserialize<PaymentRequirementsResponse>(responseBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(paymentRequirements);
        Assert.NotNull(paymentRequirements!.Accepts);
        Assert.NotEmpty(paymentRequirements.Accepts);

        Console.WriteLine($"[Error Test] ✓ Missing payment header returns 402 with {paymentRequirements.Accepts.Count} payment options");

        // Clean up
        resourceServer.Dispose();
    }

    #endregion

    #region Helper Methods - Error Payload Generation

    /// <summary>
    /// Creates a payment with an invalid signature.
    /// </summary>
    private (string header, PaymentRequirements requirements) CreateInvalidSignaturePayment()
    {
        var value = BigInteger.Parse("100000"); // 0.1 USDC

        var requirements = new PaymentRequirements
        {
            Scheme = "exact",
            Network = NETWORK_NAME,
            MaxAmountRequired = value.ToString(),
            Resource = "/premium",
            Description = "Test resource",
            MimeType = "application/json",
            PayTo = PAYEE_ADDRESS,
            MaxTimeoutSeconds = 300,
            Asset = _usdcAddress
        };

        // Create authorization with invalid signature
        var payload = new PaymentPayload
        {
            Scheme = "exact",
            Payload = new ExactSchemePayload
            {
                Signature = "0x0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", // Invalid signature
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

    /// <summary>
    /// Creates a payment with expired authorization (validBefore in the past).
    /// </summary>
    private (string header, PaymentRequirements requirements) CreateExpiredAuthorizationPayment()
    {
        var payload = new PaymentPayload
        {
            Scheme = "exact",
            Payload = new ExactSchemePayload
            {
                Signature = "0xinvalid",
                Authorization = new Nethereum.X402.Models.Authorization
                {
                    From = PAYER_ADDRESS,
                    To = PAYEE_ADDRESS,
                    Value = "100000",
                    ValidAfter = "0",
                    ValidBefore = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds().ToString(), // Expired
                    Nonce = "0x" + Guid.NewGuid().ToString("N")
                }
            }
        };

        var requirements = new PaymentRequirements
        {
            Scheme = "exact",
            Network = NETWORK_NAME,
            MaxAmountRequired = "100000",
            Resource = "/premium",
            Description = "Test resource",
            MimeType = "application/json",
            PayTo = PAYEE_ADDRESS,
            MaxTimeoutSeconds = 300,
            Asset = _usdcAddress
        };

        var paymentJson = JsonSerializer.Serialize(payload);
        var paymentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(paymentJson));

        return (paymentBase64, requirements);
    }

    /// <summary>
    /// Creates a payment with an unsupported scheme.
    /// </summary>
    private (string header, PaymentRequirements requirements) CreateUnsupportedSchemePayment()
    {
        var payload = new PaymentPayload
        {
            Scheme = "unsupported-scheme", // Not supported
            Payload = new { test = "data" }
        };

        var requirements = new PaymentRequirements
        {
            Scheme = "exact",
            Network = NETWORK_NAME,
            MaxAmountRequired = "100000",
            Resource = "/premium",
            Description = "Test resource",
            MimeType = "application/json",
            PayTo = PAYEE_ADDRESS,
            MaxTimeoutSeconds = 300,
            Asset = _usdcAddress
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
        // Create facilitator server using real x402 components
        var payeeAccount = new Account(PAYEE_PRIVATE_KEY, CHAIN_ID);

        var rpcEndpoints = new Dictionary<string, string>
        {
            { NETWORK_NAME, RPC_URL }
        };

        var tokenAddresses = new Dictionary<string, string>
        {
            { NETWORK_NAME, _usdcAddress! }
        };

        var chainIds = new Dictionary<string, int>
        {
            { NETWORK_NAME, CHAIN_ID }
        };

        var tokenNames = new Dictionary<string, string>
        {
            { NETWORK_NAME, TOKEN_NAME }
        };

        var tokenVersions = new Dictionary<string, string>
        {
            { NETWORK_NAME, TOKEN_VERSION }
        };

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    // Register x402 payment processor with real blockchain service
                    services.AddX402TransferProcessor(
                        payeeAccount,
                        rpcEndpoints,
                        tokenAddresses,
                        chainIds,
                        tokenNames,
                        tokenVersions);

                    // Register controllers and FacilitatorController
                    services.AddControllers()
                        .AddX402FacilitatorControllers();
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        // Use real FacilitatorController endpoints
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
                        // Free endpoint - no payment required
                        endpoints.MapGet("/free", async context =>
                        {
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync(
                                JsonSerializer.Serialize(new { message = "This is free content" }));
                        });

                        // Premium endpoint - requires payment
                        endpoints.MapGet("/premium", async context =>
                        {
                            // Define payment requirements for this resource
                            var paymentRequirements = new PaymentRequirements
                            {
                                Scheme = "exact",
                                Network = NETWORK_NAME,
                                MaxAmountRequired = "100000", // 0.1 USDC
                                Resource = "/premium",
                                Description = "Premium content access",
                                MimeType = "application/json",
                                PayTo = PAYEE_ADDRESS,
                                MaxTimeoutSeconds = 300,
                                Asset = _usdcAddress
                            };

                            // Check for X-PAYMENT header
                            if (!context.Request.Headers.ContainsKey("X-PAYMENT"))
                            {
                                // Return 402 with payment requirements
                                context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
                                context.Response.ContentType = "application/json";

                                var paymentRequirementsResponse = new PaymentRequirementsResponse
                                {
                                    Accepts = new List<PaymentRequirements> { paymentRequirements }
                                };

                                await context.Response.WriteAsync(
                                    JsonSerializer.Serialize(paymentRequirementsResponse));
                                return;
                            }

                            // Payment provided - verify and settle with facilitator
                            var paymentHeader = context.Request.Headers["X-PAYMENT"].ToString();

                            try
                            {
                                // Decode payment payload
                                var paymentJson = Encoding.UTF8.GetString(Convert.FromBase64String(paymentHeader));
                                var paymentPayload = JsonSerializer.Deserialize<PaymentPayload>(paymentJson,
                                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                                // Create FacilitatorSettleRequest with both payload and requirements
                                var settleRequest = new Nethereum.X402.Facilitator.FacilitatorSettleRequest
                                {
                                    PaymentPayload = paymentPayload!,
                                    PaymentRequirements = paymentRequirements
                                };

                                var settleRequestJson = JsonSerializer.Serialize(settleRequest);

                                // Call facilitator to settle using proper endpoint
                                var facilitatorResponse = await facilitatorClient.PostAsync("/facilitator/settle",
                                    new StringContent(settleRequestJson, Encoding.UTF8, "application/json"));

                                if (facilitatorResponse.IsSuccessStatusCode)
                                {
                                    var settlementJson = await facilitatorResponse.Content.ReadAsStringAsync();
                                    var settlement = JsonSerializer.Deserialize<SettlementResponse>(settlementJson,
                                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                                    // Return content with settlement proof
                                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                                    context.Response.ContentType = "application/json";

                                    var settlementBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(settlementJson));
                                    context.Response.Headers.Append("X-PAYMENT-RESPONSE", settlementBase64);

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

    /// <summary>
    /// Creates a resource server using self-facilitated pattern.
    /// The resource server handles payment verification and settlement directly.
    /// </summary>
    private async Task<TestServer> CreateResourceServer_SelfFacilitated()
    {
        // Resource server IS the facilitator - handles payments directly
        var payeeAccount = new Account(PAYEE_PRIVATE_KEY, CHAIN_ID);

        var rpcEndpoints = new Dictionary<string, string>
        {
            { NETWORK_NAME, RPC_URL }
        };

        var tokenAddresses = new Dictionary<string, string>
        {
            { NETWORK_NAME, _usdcAddress! }
        };

        var chainIds = new Dictionary<string, int>
        {
            { NETWORK_NAME, CHAIN_ID }
        };

        var tokenNames = new Dictionary<string, string>
        {
            { NETWORK_NAME, TOKEN_NAME }
        };

        var tokenVersions = new Dictionary<string, string>
        {
            { NETWORK_NAME, TOKEN_VERSION }
        };

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    // Register x402 payment processor with real blockchain service
                    services.AddX402TransferProcessor(
                        payeeAccount,
                        rpcEndpoints,
                        tokenAddresses,
                        chainIds,
                        tokenNames,
                        tokenVersions);

                    services.AddRouting();
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        // Free endpoint - no payment required
                        endpoints.MapGet("/free", async context =>
                        {
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync(
                                JsonSerializer.Serialize(new { message = "This is free content" }));
                        });

                        // Premium endpoint - requires payment (self-facilitated)
                        endpoints.MapGet("/premium", async context =>
                        {
                            var paymentRequirements = new PaymentRequirements
                            {
                                Scheme = "exact",
                                Network = NETWORK_NAME,
                                MaxAmountRequired = "100000", // 0.1 USDC
                                Resource = "/premium",
                                Description = "Premium content access",
                                MimeType = "application/json",
                                PayTo = PAYEE_ADDRESS,
                                MaxTimeoutSeconds = 300,
                                Asset = _usdcAddress
                            };

                            // Check for X-PAYMENT header
                            if (!context.Request.Headers.ContainsKey("X-PAYMENT"))
                            {
                                // Return 402 with payment requirements
                                context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
                                context.Response.ContentType = "application/json";

                                var paymentRequirementsResponse = new PaymentRequirementsResponse
                                {
                                    Accepts = new List<PaymentRequirements> { paymentRequirements }
                                };

                                await context.Response.WriteAsync(
                                    JsonSerializer.Serialize(paymentRequirementsResponse));
                                return;
                            }

                            // Payment provided - settle directly (self-facilitated)
                            var paymentHeader = context.Request.Headers["X-PAYMENT"].ToString();

                            try
                            {
                                // Decode payment payload
                                var paymentJson = Encoding.UTF8.GetString(Convert.FromBase64String(paymentHeader));
                                var paymentPayload = JsonSerializer.Deserialize<PaymentPayload>(paymentJson,
                                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                                // Get processor from DI and settle payment directly
                                var processor = context.RequestServices.GetRequiredService<IX402PaymentProcessor>();
                                var settlement = await processor.SettlePaymentAsync(
                                    paymentPayload!,
                                    paymentRequirements,
                                    context.RequestAborted);

                                if (settlement.Success)
                                {
                                    // Return content with settlement proof
                                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                                    context.Response.ContentType = "application/json";

                                    var settlementJson = JsonSerializer.Serialize(settlement);
                                    var settlementBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(settlementJson));
                                    context.Response.Headers.Append("X-PAYMENT-RESPONSE", settlementBase64);

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

    /// <summary>
    /// Creates a resource server using proxy facilitator pattern.
    /// The resource server delegates payment handling to an external facilitator.
    /// </summary>
    private async Task<TestServer> CreateResourceServer_ProxyFacilitator(HttpClient facilitatorClient)
    {
        // Resource server CONSUMES external facilitator - proxies payment handling
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
                        // Free endpoint - no payment required
                        endpoints.MapGet("/free", async context =>
                        {
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync(
                                JsonSerializer.Serialize(new { message = "This is free content" }));
                        });

                        // Premium endpoint - requires payment (proxy to facilitator)
                        endpoints.MapGet("/premium", async context =>
                        {
                            var paymentRequirements = new PaymentRequirements
                            {
                                Scheme = "exact",
                                Network = NETWORK_NAME,
                                MaxAmountRequired = "100000", // 0.1 USDC
                                Resource = "/premium",
                                Description = "Premium content access",
                                MimeType = "application/json",
                                PayTo = PAYEE_ADDRESS,
                                MaxTimeoutSeconds = 300,
                                Asset = _usdcAddress
                            };

                            // Check for X-PAYMENT header
                            if (!context.Request.Headers.ContainsKey("X-PAYMENT"))
                            {
                                // Return 402 with payment requirements
                                context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
                                context.Response.ContentType = "application/json";

                                var paymentRequirementsResponse = new PaymentRequirementsResponse
                                {
                                    Accepts = new List<PaymentRequirements> { paymentRequirements }
                                };

                                await context.Response.WriteAsync(
                                    JsonSerializer.Serialize(paymentRequirementsResponse));
                                return;
                            }

                            // Payment provided - proxy to external facilitator
                            var paymentHeader = context.Request.Headers["X-PAYMENT"].ToString();

                            try
                            {
                                // Decode payment payload
                                var paymentJson = Encoding.UTF8.GetString(Convert.FromBase64String(paymentHeader));
                                var paymentPayload = JsonSerializer.Deserialize<PaymentPayload>(paymentJson,
                                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                                // Create FacilitatorSettleRequest
                                var settleRequest = new Nethereum.X402.Facilitator.FacilitatorSettleRequest
                                {
                                    PaymentPayload = paymentPayload!,
                                    PaymentRequirements = paymentRequirements
                                };

                                var settleRequestJson = JsonSerializer.Serialize(settleRequest);

                                // Call external facilitator to settle
                                var facilitatorResponse = await facilitatorClient.PostAsync("/facilitator/settle",
                                    new StringContent(settleRequestJson, Encoding.UTF8, "application/json"));

                                if (facilitatorResponse.IsSuccessStatusCode)
                                {
                                    var settlementJson = await facilitatorResponse.Content.ReadAsStringAsync();
                                    var settlement = JsonSerializer.Deserialize<SettlementResponse>(settlementJson,
                                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                                    // Return content with settlement proof
                                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                                    context.Response.ContentType = "application/json";

                                    var settlementBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(settlementJson));
                                    context.Response.Headers.Append("X-PAYMENT-RESPONSE", settlementBase64);

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
