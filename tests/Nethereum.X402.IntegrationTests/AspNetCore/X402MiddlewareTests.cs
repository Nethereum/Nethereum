using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Server;
using System.Net;
using System.Text;
using System.Text.Json;
using Authorization = Nethereum.X402.Models.Authorization;

namespace Nethereum.X402.IntegrationTests.AspNetCore;

/// <summary>
/// BDD tests for X402 ASP.NET Core middleware
///
/// Traceability:
/// - Spec: Section 8 - Server Implementation
/// - Use Cases: UC-ASPNET-1 through UC-ASPNET-5
/// - Requirements: ASP.NET Core middleware for x402 payment processing
/// - Implementation: src/Nethereum.X402/AspNetCore/X402Middleware.cs
/// </summary>
public class X402MiddlewareTests
{
    /// <summary>
    /// Spec: Section 5.1 - 402 Payment Required response
    /// Use Case: UC-ASPNET-2 Scenario 1
    /// </summary>
    [Fact]
    public async Task Given_RequestToProtectedRouteWithoutPayment_When_ProcessingRequest_Then_402IsReturned()
    {
        // Arrange
        var mockFacilitator = new MockFacilitatorClient();
        using var server = CreateTestServer(mockFacilitator);

        // Act
        var response = await server.CreateClient().GetAsync("/api/premium");

        // Assert
        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var paymentResponse = JsonSerializer.Deserialize<PaymentRequirementsResponse>(content);

        Assert.NotNull(paymentResponse);
        Assert.Equal(1, paymentResponse.X402Version);
        Assert.NotNull(paymentResponse.Error);
        Assert.NotNull(paymentResponse.Accepts);
        Assert.Single(paymentResponse.Accepts);
    }

    /// <summary>
    /// Spec: Section 5.1 - Payment requirements in 402 response
    /// Use Case: UC-ASPNET-2 Scenario 2
    /// </summary>
    [Fact]
    public async Task Given_ProtectedRoute_When_402ResponseGenerated_Then_IncludesCorrectPaymentRequirements()
    {
        // Arrange
        var mockFacilitator = new MockFacilitatorClient();
        using var server = CreateTestServer(mockFacilitator);

        // Act
        var response = await server.CreateClient().GetAsync("/api/premium");
        var content = await response.Content.ReadAsStringAsync();
        var paymentResponse = JsonSerializer.Deserialize<PaymentRequirementsResponse>(content);

        // Assert
        Assert.NotNull(paymentResponse);
        var requirements = paymentResponse.Accepts.First();
        Assert.Equal("exact", requirements.Scheme);
        Assert.Equal("base-sepolia", requirements.Network);
        Assert.Equal("10000", requirements.MaxAmountRequired);
        Assert.Equal("0x209693Bc6afc0C5328bA36FaF03C514EF312287C", requirements.PayTo);
        Assert.Equal("0x1c7D4B196Cb0C7B01d743Fbc6116a902379C7238", requirements.Asset);
        Assert.Equal(60, requirements.MaxTimeoutSeconds);
    }

    /// <summary>
    /// Spec: Section 8 - Unmatched route passthrough
    /// Use Case: UC-ASPNET-4 Scenario 1
    /// </summary>
    [Fact]
    public async Task Given_RequestToUnprotectedRoute_When_ProcessingRequest_Then_PassesThrough()
    {
        // Arrange
        var mockFacilitator = new MockFacilitatorClient();
        using var server = CreateTestServer(mockFacilitator);

        // Act
        var response = await server.CreateClient().GetAsync("/api/public");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("public data", content);
    }

    /// <summary>
    /// Spec: Section 6.1.2 - Payment verification
    /// Use Case: UC-ASPNET-3 Scenario 1
    /// </summary>
    [Fact]
    public async Task Given_ValidPayment_When_ProcessingRequest_Then_VerificationIsCalledAndRequestProceeds()
    {
        // Arrange
        var mockFacilitator = new MockFacilitatorClient();
        using var server = CreateTestServer(mockFacilitator);
        mockFacilitator.VerifyResponse = new VerificationResponse
        {
            IsValid = true,
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };
        mockFacilitator.SettleResponse = new SettlementResponse
        {
            Success = true,
            Transaction = "0xtxhash",
            Network = "base-sepolia",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        var client = server.CreateClient();
        var payment = CreateTestPaymentPayload();
        var paymentHeader = EncodePaymentHeader(payment);
        client.DefaultRequestHeaders.Add("X-PAYMENT", paymentHeader);

        // Act
        var response = await client.GetAsync("/api/premium");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(mockFacilitator.VerifyCalled);
        Assert.True(mockFacilitator.SettleCalled);

        // Check for X-PAYMENT-RESPONSE header
        Assert.True(response.Headers.Contains("X-PAYMENT-RESPONSE"));
    }

    /// <summary>
    /// Spec: Section 6.1.2 - Invalid payment rejection
    /// Use Case: UC-ASPNET-3 Scenario 2
    /// </summary>
    [Fact]
    public async Task Given_InvalidPayment_When_ProcessingRequest_Then_402IsReturnedWithReason()
    {
        // Arrange
        var mockFacilitator = new MockFacilitatorClient();
        using var server = CreateTestServer(mockFacilitator);
        mockFacilitator.VerifyResponse = new VerificationResponse
        {
            IsValid = false,
            InvalidReason = "insufficient_funds",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        var client = server.CreateClient();
        var payment = CreateTestPaymentPayload();
        var paymentHeader = EncodePaymentHeader(payment);
        client.DefaultRequestHeaders.Add("X-PAYMENT", paymentHeader);

        // Act
        var response = await client.GetAsync("/api/premium");

        // Assert
        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.True(mockFacilitator.VerifyCalled);
        Assert.False(mockFacilitator.SettleCalled); // Settlement should not be called

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("insufficient_funds", content);
    }

    /// <summary>
    /// Spec: Section 6.1.3 - Settlement after successful response
    /// Use Case: UC-ASPNET-3 Scenario 3
    /// </summary>
    [Fact]
    public async Task Given_ValidPaymentButEndpointReturnsError_When_ProcessingRequest_Then_SettlementIsSkipped()
    {
        // Arrange
        var mockFacilitator = new MockFacilitatorClient();
        using var server = CreateTestServer(mockFacilitator, returnError: true);
        mockFacilitator.VerifyResponse = new VerificationResponse
        {
            IsValid = true,
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        var client = server.CreateClient();
        var payment = CreateTestPaymentPayload();
        var paymentHeader = EncodePaymentHeader(payment);
        client.DefaultRequestHeaders.Add("X-PAYMENT", paymentHeader);

        // Act
        var response = await client.GetAsync("/api/premium");

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(mockFacilitator.VerifyCalled);
        Assert.False(mockFacilitator.SettleCalled); // Settlement should be skipped
    }

    /// <summary>
    /// Spec: Section 5.2 - Invalid payment payload handling
    /// Use Case: UC-ASPNET-5 Scenario 1
    /// </summary>
    [Fact]
    public async Task Given_MalformedPaymentHeader_When_ProcessingRequest_Then_402IsReturnedWithError()
    {
        // Arrange
        var mockFacilitator = new MockFacilitatorClient();
        using var server = CreateTestServer(mockFacilitator);
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-PAYMENT", "invalid-base64!@#");

        // Act
        var response = await client.GetAsync("/api/premium");

        // Assert
        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.False(mockFacilitator.VerifyCalled); // Verification should not be called for invalid payload

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid payment payload", content);
    }

    /// <summary>
    /// Spec: Section 7.2 - Settlement response header
    /// Use Case: UC-ASPNET-3 Scenario 1
    /// </summary>
    [Fact]
    public async Task Given_SuccessfulPayment_When_Settled_Then_XPaymentResponseHeaderIsAdded()
    {
        // Arrange
        var mockFacilitator = new MockFacilitatorClient();
        using var server = CreateTestServer(mockFacilitator);
        mockFacilitator.VerifyResponse = new VerificationResponse
        {
            IsValid = true,
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };
        mockFacilitator.SettleResponse = new SettlementResponse
        {
            Success = true,
            Transaction = "0x1234567890abcdef",
            Network = "base-sepolia",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        var client = server.CreateClient();
        var payment = CreateTestPaymentPayload();
        var paymentHeader = EncodePaymentHeader(payment);
        client.DefaultRequestHeaders.Add("X-PAYMENT", paymentHeader);

        // Act
        var response = await client.GetAsync("/api/premium");

        // Assert
        Assert.True(response.Headers.Contains("X-PAYMENT-RESPONSE"));
        var settlementHeader = response.Headers.GetValues("X-PAYMENT-RESPONSE").First();
        Assert.NotNull(settlementHeader);
        Assert.NotEmpty(settlementHeader);

        // Decode and verify
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(settlementHeader));
        var settlement = JsonSerializer.Deserialize<SettlementResponse>(json);
        Assert.NotNull(settlement);
        Assert.True(settlement.Success);
        Assert.Equal("0x1234567890abcdef", settlement.Transaction);
    }

    /// <summary>
    /// Spec: Section 8 - HTTP method filtering
    /// Use Case: UC-CORE-2 Scenario 3 (applied at middleware level)
    /// </summary>
    [Fact]
    public async Task Given_MethodSpecificRoute_When_DifferentMethodUsed_Then_PassesThrough()
    {
        // Arrange
        var mockFacilitator = new MockFacilitatorClient();
        using var server = CreateTestServerWithMethodFiltering(mockFacilitator);

        // Act - POST should pass through since only GET requires payment
        var response = await server.CreateClient().PostAsync("/api/data", new StringContent("test"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("posted", content);
    }

    private TestServer CreateTestServer(MockFacilitatorClient mockFacilitator, bool returnError = false)
    {

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IFacilitatorClient>(mockFacilitator);
            })
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "https://facilitator.test";
                    options.Routes.Add(new RoutePaymentConfig("/api/premium", new PaymentRequirements
                    {
                        Scheme = "exact",
                        Network = "base-sepolia",
                        MaxAmountRequired = "10000",
                        Resource = "/api/premium",
                        Description = "Premium API access",
                        MimeType = "application/json",
                        PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
                        MaxTimeoutSeconds = 60,
                        Asset = "0x1c7D4B196Cb0C7B01d743Fbc6116a902379C7238"
                    }));
                });

                app.Run(async context =>
                {
                    if (context.Request.Path == "/api/premium")
                    {
                        if (returnError)
                        {
                            context.Response.StatusCode = 500;
                            await context.Response.WriteAsync("endpoint error");
                        }
                        else
                        {
                            await context.Response.WriteAsync("premium data");
                        }
                    }
                    else if (context.Request.Path == "/api/public")
                    {
                        await context.Response.WriteAsync("public data");
                    }
                });
            });

        return new TestServer(builder);
    }

    private TestServer CreateTestServerWithMethodFiltering(MockFacilitatorClient mockFacilitator)
    {

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IFacilitatorClient>(mockFacilitator);
            })
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "https://facilitator.test";
                    options.Routes.Add(new RoutePaymentConfig("/api/data", new PaymentRequirements
                    {
                        Scheme = "exact",
                        Network = "base-sepolia",
                        MaxAmountRequired = "10000",
                        Resource = "/api/data",
                        Description = "Data API access",
                        MimeType = "application/json",
                        PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
                        MaxTimeoutSeconds = 60,
                        Asset = "0x1c7D4B196Cb0C7B01d743Fbc6116a902379C7238"
                    }, "GET")); // Only GET requires payment
                });

                app.Run(async context =>
                {
                    if (context.Request.Method == "POST")
                    {
                        await context.Response.WriteAsync("posted");
                    }
                    else
                    {
                        await context.Response.WriteAsync("data");
                    }
                });
            });

        return new TestServer(builder);
    }

    private PaymentPayload CreateTestPaymentPayload()
    {
        return new PaymentPayload
        {
            Scheme = "exact",
            Payload = new ExactSchemePayload
            {
                Signature = "0xsignature",
                Authorization = new Authorization
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

    private string EncodePaymentHeader(PaymentPayload payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }
}

/// <summary>
/// Mock facilitator client for middleware testing
/// </summary>
public class MockFacilitatorClient : IFacilitatorClient
{
    public VerificationResponse? VerifyResponse { get; set; }
    public SettlementResponse? SettleResponse { get; set; }
    public bool VerifyCalled { get; private set; }
    public bool SettleCalled { get; private set; }

    public Task<VerificationResponse> VerifyAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        VerifyCalled = true;
        return Task.FromResult(VerifyResponse ?? new VerificationResponse
        {
            IsValid = true,
            Payer = "0xtest"
        });
    }

    public Task<SettlementResponse> SettleAsync(
        PaymentPayload paymentPayload,
        PaymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        SettleCalled = true;
        return Task.FromResult(SettleResponse ?? new SettlementResponse
        {
            Success = true,
            Transaction = "0xtest",
            Network = "base-sepolia",
            Payer = "0xtest"
        });
    }

    public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SupportedPaymentKindsResponse
        {
            Kinds = new List<PaymentKind>()
        });
    }
}
