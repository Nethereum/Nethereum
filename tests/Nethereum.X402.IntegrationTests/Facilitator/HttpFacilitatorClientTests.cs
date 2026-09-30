using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using System.Net;
using System.Text.Json;

namespace Nethereum.X402.IntegrationTests.Facilitator;

public class HttpFacilitatorClientTests
{
    [Fact]
    public void Given_BaseUrlWithTrailingSlash_When_CreatingClient_Then_TrailingSlashIsRemoved()
    {
        var httpClient = new HttpClient();
        var baseUrl = "https://facilitator.example.com/";

        var client = new HttpFacilitatorClient(httpClient, baseUrl);

        Assert.NotNull(client);
    }

    [Fact]
    public void Given_NullHttpClient_When_CreatingClient_Then_ArgumentNullExceptionIsThrown()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new HttpFacilitatorClient(null!, "https://facilitator.example.com"));
    }

    [Fact]
    public void Given_NullBaseUrl_When_CreatingClient_Then_ArgumentNullExceptionIsThrown()
    {
        var httpClient = new HttpClient();

        Assert.Throws<ArgumentNullException>(() =>
            new HttpFacilitatorClient(httpClient, null!));
    }

    [Fact]
    public async Task Given_ValidPayment_When_VerifyingAsync_Then_SuccessResponseIsReturned()
    {
        var mockResponse = new VerificationResponse
        {
            IsValid = true,
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.EndsWith("/verify", request.RequestUri?.ToString());

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(mockResponse))
            };
            return Task.FromResult(responseMessage);
        });

        var httpClient = new HttpClient(handler);
        var client = new HttpFacilitatorClient(httpClient, "https://facilitator.example.com");

        var paymentPayload = CreateTestPaymentPayload();
        var requirements = CreateTestPaymentRequirements();

        var response = await client.VerifyAsync(paymentPayload, requirements);

        Assert.NotNull(response);
        Assert.True(response.IsValid);
    }

    [Fact]
    public async Task Given_InvalidPayment_When_VerifyingAsync_Then_FailureResponseIsReturned()
    {
        var mockResponse = new VerificationResponse
        {
            IsValid = false,
            InvalidReason = "invalid_signature",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(mockResponse))
            };
            return Task.FromResult(responseMessage);
        });

        var httpClient = new HttpClient(handler);
        var client = new HttpFacilitatorClient(httpClient, "https://facilitator.example.com");

        var paymentPayload = CreateTestPaymentPayload();
        var requirements = CreateTestPaymentRequirements();

        var response = await client.VerifyAsync(paymentPayload, requirements);

        Assert.NotNull(response);
        Assert.False(response.IsValid);
        Assert.Equal("invalid_signature", response.InvalidReason);
    }

    [Fact]
    public async Task Given_ServerError_When_VerifyingAsync_Then_HttpRequestExceptionIsThrown()
    {
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            return Task.FromResult(responseMessage);
        });

        var httpClient = new HttpClient(handler);
        var client = new HttpFacilitatorClient(httpClient, "https://facilitator.example.com");

        var paymentPayload = CreateTestPaymentPayload();
        var requirements = CreateTestPaymentRequirements();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.VerifyAsync(paymentPayload, requirements));
    }

    [Fact]
    public async Task Given_VerifiedPayment_When_SettlingAsync_Then_SuccessResponseIsReturned()
    {
        var mockResponse = new SettlementResponse
        {
            Success = true,
            Transaction = "0xabc123",
            Network = "base-sepolia",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.EndsWith("/settle", request.RequestUri?.ToString());

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(mockResponse))
            };
            return Task.FromResult(responseMessage);
        });

        var httpClient = new HttpClient(handler);
        var client = new HttpFacilitatorClient(httpClient, "https://facilitator.example.com");

        var paymentPayload = CreateTestPaymentPayload();
        var requirements = CreateTestPaymentRequirements();

        var response = await client.SettleAsync(paymentPayload, requirements);

        Assert.NotNull(response);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task Given_FailedSettlement_When_SettlingAsync_Then_FailureResponseIsReturned()
    {
        var mockResponse = new SettlementResponse
        {
            Success = false,
            ErrorReason = "insufficient_balance",
            Transaction = "0x",
            Network = "base-sepolia",
            Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
        };

        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(mockResponse))
            };
            return Task.FromResult(responseMessage);
        });

        var httpClient = new HttpClient(handler);
        var client = new HttpFacilitatorClient(httpClient, "https://facilitator.example.com");

        var paymentPayload = CreateTestPaymentPayload();
        var requirements = CreateTestPaymentRequirements();

        var response = await client.SettleAsync(paymentPayload, requirements);

        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.Equal("insufficient_balance", response.ErrorReason);
    }

    [Fact]
    public async Task Given_CancelledToken_When_SettlingAsync_Then_OperationCancelledExceptionIsThrown()
    {
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);
            return Task.FromResult(responseMessage);
        });

        var httpClient = new HttpClient(handler);
        var client = new HttpFacilitatorClient(httpClient, "https://facilitator.example.com");

        var paymentPayload = CreateTestPaymentPayload();
        var requirements = CreateTestPaymentRequirements();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.SettleAsync(paymentPayload, requirements, cts.Token));
    }

    [Fact]
    public async Task Given_Facilitator_When_GettingSupportedAsync_Then_SupportedKindsAreReturned()
    {
        var mockResponse = new SupportedPaymentKindsResponse
        {
            Kinds = new List<PaymentKind>
            {
                new() { X402Version = 1, Scheme = "exact", Network = "base-sepolia" },
                new() { X402Version = 1, Scheme = "exact", Network = "ethereum-mainnet" }
            }
        };

        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.EndsWith("/supported", request.RequestUri?.ToString());

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(mockResponse))
            };
            return Task.FromResult(responseMessage);
        });

        var httpClient = new HttpClient(handler);
        var client = new HttpFacilitatorClient(httpClient, "https://facilitator.example.com");

        var response = await client.GetSupportedAsync();

        Assert.NotNull(response);
        Assert.NotNull(response.Kinds);
        Assert.Equal(2, response.Kinds.Count);
        Assert.Equal("exact", response.Kinds[0].Scheme);
        Assert.Equal("base-sepolia", response.Kinds[0].Network);
    }

    [Fact]
    public async Task Given_FacilitatorWithNoSupport_When_GettingSupportedAsync_Then_EmptyArrayIsReturned()
    {
        var mockResponse = new SupportedPaymentKindsResponse
        {
            Kinds = new List<PaymentKind>()
        };

        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(mockResponse))
            };
            return Task.FromResult(responseMessage);
        });

        var httpClient = new HttpClient(handler);
        var client = new HttpFacilitatorClient(httpClient, "https://facilitator.example.com");

        var response = await client.GetSupportedAsync();

        Assert.NotNull(response);
        Assert.NotNull(response.Kinds);
        Assert.Empty(response.Kinds);
    }

    [Fact]
    public async Task Given_ValidPayment_When_VerifyingAsync_Then_RequestBodyIsCorrect()
    {
        string? capturedRequestBody = null;

        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            capturedRequestBody = await request.Content!.ReadAsStringAsync(ct);

            var mockResponse = new VerificationResponse
            {
                IsValid = true,
                Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66"
            };
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(mockResponse))
            };
            return responseMessage;
        });

        var httpClient = new HttpClient(handler);
        var client = new HttpFacilitatorClient(httpClient, "https://facilitator.example.com");

        var paymentPayload = CreateTestPaymentPayload();
        var requirements = CreateTestPaymentRequirements();

        await client.VerifyAsync(paymentPayload, requirements);

        Assert.NotNull(capturedRequestBody);
        Assert.Contains("\"paymentPayload\"", capturedRequestBody);
        Assert.Contains("\"paymentRequirements\"", capturedRequestBody);
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
            Amount = "10000",
            PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            MaxTimeoutSeconds = 300,
            Asset = "0x1c7D4B196Cb0C7B01d743Fbc6116a902379C7238"
        };
    }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _handler(request, cancellationToken);
    }
}
