using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Nethereum.RPC.Accounts;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Extensions;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Processors;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Nethereum.X402.IntegrationTests.Facilitator;

/// <summary>
/// Integration tests for Facilitator Server
///
/// Traceability:
/// - Spec: Section 7, Facilitator API
/// - Use Cases: UC-F1 through UC-F4 (End-to-End)
/// - Requirements: Full integration testing of facilitator endpoints
/// - Implementation: src/Nethereum.X402/Facilitator/FacilitatorController.cs
/// </summary>
public class FacilitatorIntegrationTests
{
    /// <summary>
    /// Spec: Section 7.3 - GET /facilitator/supported endpoint (E2E)
    /// Use Case: UC-F4 Scenario 1 (E2E)
    /// </summary>
    [Fact]
    public async Task Given_Facilitator_When_CallingSupportedEndpoint_Then_ReturnsSupportedKinds()
    {
        // Arrange
        var mockProcessor = new Mock<IX402PaymentProcessor>();
        mockProcessor
            .Setup(p => p.GetSupportedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SupportedPaymentKindsResponse
            {
                Kinds = new List<PaymentKind>
                {
                    new() { X402Version = 1, Scheme = "exact", Network = "base-sepolia" },
                    new() { X402Version = 1, Scheme = "exact", Network = "sepolia" }
                }
            });

        using var server = CreateTestServer(mockProcessor.Object);
        var client = server.CreateClient();

        // Act
        var response = await client.GetAsync("/facilitator/supported");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        var supportedResponse = JsonSerializer.Deserialize<SupportedPaymentKindsResponse>(
            responseBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(supportedResponse);
        Assert.NotNull(supportedResponse.Kinds);
        Assert.Equal(2, supportedResponse.Kinds.Count);
        Assert.Equal("exact", supportedResponse.Kinds[0].Scheme);
        Assert.Equal("base-sepolia", supportedResponse.Kinds[0].Network);
    }

    /// <summary>
    /// Spec: Section 7.1 - Request validation (E2E)
    /// Use Case: UC-F2 Scenario 3 (E2E)
    /// </summary>
    [Fact]
    public async Task Given_InvalidRequest_When_CallingVerifyEndpoint_Then_Returns400()
    {
        // Arrange
        var mockProcessor = new Mock<IX402PaymentProcessor>();
        using var server = CreateTestServer(mockProcessor.Object);
        var client = server.CreateClient();

        var content = new StringContent(
            "{}",
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/facilitator/verify", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Spec: Section 7.2 - Request validation (E2E)
    /// Use Case: UC-F3 Scenario 3 (E2E)
    /// </summary>
    [Fact]
    public async Task Given_InvalidRequest_When_CallingSettleEndpoint_Then_Returns400()
    {
        // Arrange
        var mockProcessor = new Mock<IX402PaymentProcessor>();
        using var server = CreateTestServer(mockProcessor.Object);
        var client = server.CreateClient();

        var content = new StringContent(
            "{}",
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/facilitator/settle", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Spec: Section 7 - Integration with actual processor (E2E)
    /// Use Case: UC-F1 Scenario 3 (E2E)
    /// </summary>
    [Fact]
    public async Task Given_RealProcessorConfiguration_When_StartingServer_Then_ServerStarts()
    {
        // Arrange - Use a test private key (Hardhat account #0)
        var testPrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        var testAccount = new Account(testPrivateKey);

        var rpcEndpoints = new Dictionary<string, string>
        {
            { "test-network", "http://localhost:8545" }
        };

        var tokenAddresses = new Dictionary<string, string>
        {
            { "test-network", "0x5FbDB2315678afecb367f032d93F642f64180aa3" }
        };

        var chainIds = new Dictionary<string, int>
        {
            { "test-network", 31337 }
        };

        var tokenNames = new Dictionary<string, string>
        {
            { "test-network", "USD Coin" }
        };

        var tokenVersions = new Dictionary<string, string>
        {
            { "test-network", "2" }
        };

        // Act - Create server with real processor
        using var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddControllers()
                    .AddX402FacilitatorControllers();

                services.AddX402TransferProcessor(
                    testAccount,
                    rpcEndpoints,
                    tokenAddresses,
                    chainIds,
                    tokenNames,
                    tokenVersions);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });
            }));

        var client = server.CreateClient();

        // Assert - Server can handle requests
        var response = await client.GetAsync("/facilitator/supported");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private TestServer CreateTestServer(IX402PaymentProcessor processor)
    {
        return new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(processor);
                services.AddControllers()
                    .AddJsonOptions(options =>
                    {
                        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                        options.JsonSerializerOptions.PropertyNamingPolicy = null; // Use PascalCase
                    })
                    .AddX402FacilitatorControllers();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });
            }));
    }

    private PaymentPayload CreateTestPaymentPayload()
    {
        return new PaymentPayload
        {
            Scheme = "exact",
            Payload = new ExactSchemePayload
            {
                Signature = "0xsignature",
                Authorization = new Nethereum.X402.Models.Authorization
                {
                    From = "0x857b06519E91e3A54538791bDbb0E22373e36b66",
                    To = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
                    Value = "10000",
                    ValidAfter = "0",
                    ValidBefore = "1740672154",
                    Nonce = "0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480"
                }
            }
        };
    }

    private PaymentRequirements CreateTestPaymentRequirements()
    {
        return new PaymentRequirements
        {
            Scheme = "exact",
            Network = "base-sepolia",
            MaxAmountRequired = "10000",
            Resource = "/api/data",
            Description = "Test resource",
            MimeType = "application/json",
            PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            MaxTimeoutSeconds = 300,
            Asset = "0x1c7D4B196Cb0C7B01d743Fbc6116a902379C7238"
        };
    }
}
