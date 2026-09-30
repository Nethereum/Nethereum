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

public class X402MiddlewareTests
{
    [Fact]
    public async Task Given_RequestToProtectedRouteWithoutPayment_When_ProcessingRequest_Then_402IsReturned()
    {
        var mockFacilitator = new MockFacilitatorClient();
        using var server = CreateTestServer(mockFacilitator);

        var response = await server.CreateClient().GetAsync("/api/premium");

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var paymentResponse = JsonSerializer.Deserialize<PaymentRequired>(content);

        Assert.NotNull(paymentResponse);
        Assert.Equal(2, paymentResponse.X402Version);
        Assert.NotNull(paymentResponse.Error);
        Assert.NotNull(paymentResponse.Resource);
        Assert.NotNull(paymentResponse.Accepts);
        Assert.Single(paymentResponse.Accepts);
    }

    [Fact]
    public async Task Given_402_When_ProcessingRequest_Then_PaymentRequiredHeaderIsSet()
    {
        var mockFacilitator = new MockFacilitatorClient();
        using var server = CreateTestServer(mockFacilitator);

        var response = await server.CreateClient().GetAsync("/api/premium");

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("PAYMENT-REQUIRED", out var values));
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(values!.First()));
        var decoded = JsonSerializer.Deserialize<PaymentRequired>(json);
        Assert.NotNull(decoded);
        Assert.Equal(2, decoded!.X402Version);
        Assert.NotNull(decoded.Resource);
        Assert.Single(decoded.Accepts);
    }

    [Fact]
    public async Task Given_ProtectedRoute_When_402ResponseGenerated_Then_IncludesCorrectPaymentRequirements()
    {
        var mockFacilitator = new MockFacilitatorClient();
        using var server = CreateTestServer(mockFacilitator);

        var response = await server.CreateClient().GetAsync("/api/premium");
        var content = await response.Content.ReadAsStringAsync();
        var paymentResponse = JsonSerializer.Deserialize<PaymentRequired>(content);

        Assert.NotNull(paymentResponse);
        var requirements = paymentResponse.Accepts.First();
        Assert.Equal("exact", requirements.Scheme);
        Assert.Equal("base-sepolia", requirements.Network);
        Assert.Equal("10000", requirements.Amount);
        Assert.Equal("0x209693Bc6afc0C5328bA36FaF03C514EF312287C", requirements.PayTo);
        Assert.Equal("0x1c7D4B196Cb0C7B01d743Fbc6116a902379C7238", requirements.Asset);
        Assert.Equal(60, requirements.MaxTimeoutSeconds);
    }

    [Fact]
    public async Task Given_RequestToUnprotectedRoute_When_ProcessingRequest_Then_PassesThrough()
    {
        var mockFacilitator = new MockFacilitatorClient();
        using var server = CreateTestServer(mockFacilitator);

        var response = await server.CreateClient().GetAsync("/api/public");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("public data", content);
    }

    [Fact]
    public async Task Given_ValidPayment_When_ProcessingRequest_Then_VerificationIsCalledAndRequestProceeds()
    {
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
        client.DefaultRequestHeaders.Add("PAYMENT-SIGNATURE", paymentHeader);

        var response = await client.GetAsync("/api/premium");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(mockFacilitator.VerifyCalled);
        Assert.True(mockFacilitator.SettleCalled);

        Assert.True(response.Headers.Contains("PAYMENT-RESPONSE"));
    }

    [Fact]
    public async Task Given_InvalidPayment_When_ProcessingRequest_Then_402IsReturnedWithReason()
    {
        var mockFacilitator = new MockFacilitatorClient();
        using var server = CreateTestServer(mockFacilitator);
        mockFacilitator.VerifyResponse = new VerificationResponse
        {
            IsValid = false,
            InvalidReason = "invalid_exact_evm_insufficient_balance",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        var client = server.CreateClient();
        var payment = CreateTestPaymentPayload();
        var paymentHeader = EncodePaymentHeader(payment);
        client.DefaultRequestHeaders.Add("PAYMENT-SIGNATURE", paymentHeader);

        var response = await client.GetAsync("/api/premium");

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.True(mockFacilitator.VerifyCalled);
        Assert.False(mockFacilitator.SettleCalled);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid_exact_evm_insufficient_balance", content);
    }

    [Fact]
    public async Task Given_ValidPaymentButEndpointReturnsError_When_ProcessingRequest_Then_SettlementIsSkipped()
    {
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
        client.DefaultRequestHeaders.Add("PAYMENT-SIGNATURE", paymentHeader);

        var response = await client.GetAsync("/api/premium");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(mockFacilitator.VerifyCalled);
        Assert.False(mockFacilitator.SettleCalled);
    }

    [Fact]
    public async Task Given_MalformedPaymentHeader_When_ProcessingRequest_Then_402IsReturnedWithError()
    {
        var mockFacilitator = new MockFacilitatorClient();
        using var server = CreateTestServer(mockFacilitator);
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("PAYMENT-SIGNATURE", "invalid-base64!@#");

        var response = await client.GetAsync("/api/premium");

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.False(mockFacilitator.VerifyCalled);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(X402ErrorCodes.InvalidPayload, content);
    }

    [Fact]
    public async Task Given_SuccessfulPayment_When_Settled_Then_XPaymentResponseHeaderIsAdded()
    {
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
        client.DefaultRequestHeaders.Add("PAYMENT-SIGNATURE", paymentHeader);

        var response = await client.GetAsync("/api/premium");

        Assert.True(response.Headers.Contains("PAYMENT-RESPONSE"));
        var settlementHeader = response.Headers.GetValues("PAYMENT-RESPONSE").First();
        Assert.NotNull(settlementHeader);
        Assert.NotEmpty(settlementHeader);

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(settlementHeader));
        var settlement = JsonSerializer.Deserialize<SettlementResponse>(json);
        Assert.NotNull(settlement);
        Assert.True(settlement.Success);
        Assert.Equal("0x1234567890abcdef", settlement.Transaction);
    }

    [Fact]
    public async Task Given_MethodSpecificRoute_When_DifferentMethodUsed_Then_PassesThrough()
    {
        var mockFacilitator = new MockFacilitatorClient();
        using var server = CreateTestServerWithMethodFiltering(mockFacilitator);

        var response = await server.CreateClient().PostAsync("/api/data", new StringContent("test"));

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
                        Amount = "10000",
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
                        Amount = "10000",
                        PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
                        MaxTimeoutSeconds = 60,
                        Asset = "0x1c7D4B196Cb0C7B01d743Fbc6116a902379C7238"
                    }, "GET"));
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
            Accepted = new PaymentRequirements
            {
                Scheme = "exact",
                Network = "eip155:84532",
                Amount = "10000",
                Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
                PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
                MaxTimeoutSeconds = 60
            },
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
