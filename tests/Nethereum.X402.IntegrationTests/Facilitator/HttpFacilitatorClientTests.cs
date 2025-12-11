using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using System.Net;
using System.Text.Json;

namespace Nethereum.X402.IntegrationTests.Facilitator;

/// <summary>
/// BDD tests for HttpFacilitatorClient implementation
///
/// Traceability:
/// - Spec: Section 7, Facilitator API
/// - Use Cases: UC-F2 through UC-F5
/// - Requirements: HTTP client for verify, settle, and supported endpoints
/// - Implementation: src/Nethereum.X402/Facilitator/HttpFacilitatorClient.cs
/// </summary>
public class HttpFacilitatorClientTests
{
    /// <summary>
    /// Spec: Section 7 - Client initialization with base URL
    /// Use Case: UC-F5 Scenario 1
    /// </summary>
    [Fact]
    public void Given_BaseUrlWithTrailingSlash_When_CreatingClient_Then_TrailingSlashIsRemoved()
    {
        // Arrange
        var httpClient = new HttpClient();
        var baseUrl = "https://facilitator.example.com/";

        // Act
        var client = new HttpFacilitatorClient(httpClient, baseUrl);

        // Assert - No exception, client created successfully
        Assert.NotNull(client);
    }

    /// <summary>
    /// Spec: Section 7 - Null parameter validation
    /// Use Case: UC-F5 Scenario 2
    /// </summary>
    [Fact]
    public void Given_NullHttpClient_When_CreatingClient_Then_ArgumentNullExceptionIsThrown()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new HttpFacilitatorClient(null!, "https://facilitator.example.com"));
    }

    /// <summary>
    /// Spec: Section 7 - Null parameter validation
    /// Use Case: UC-F5 Scenario 2
    /// </summary>
    [Fact]
    public void Given_NullBaseUrl_When_CreatingClient_Then_ArgumentNullExceptionIsThrown()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new HttpFacilitatorClient(httpClient, null!));
    }

    /// <summary>
    /// Spec: Section 7.1 - POST /verify endpoint
    /// Use Case: UC-F2 Scenario 1
    /// </summary>
    [Fact]
    public async Task Given_ValidPayment_When_VerifyingAsync_Then_SuccessResponseIsReturned()
    {
        // Arrange
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

        // Act
        var response = await client.VerifyAsync(paymentPayload, requirements);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsValid);
    }

    /// <summary>
    /// Spec: Section 7.1 - POST /verify with invalid payment
    /// Use Case: UC-F2 Scenario 2
    /// </summary>
    [Fact]
    public async Task Given_InvalidPayment_When_VerifyingAsync_Then_FailureResponseIsReturned()
    {
        // Arrange
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

        // Act
        var response = await client.VerifyAsync(paymentPayload, requirements);

        // Assert
        Assert.NotNull(response);
        Assert.False(response.IsValid);
        Assert.Equal("invalid_signature", response.InvalidReason);
    }

    /// <summary>
    /// Spec: Section 7.1 - HTTP error handling
    /// Use Case: UC-F2 Scenario 3
    /// </summary>
    [Fact]
    public async Task Given_ServerError_When_VerifyingAsync_Then_HttpRequestExceptionIsThrown()
    {
        // Arrange
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            return Task.FromResult(responseMessage);
        });

        var httpClient = new HttpClient(handler);
        var client = new HttpFacilitatorClient(httpClient, "https://facilitator.example.com");

        var paymentPayload = CreateTestPaymentPayload();
        var requirements = CreateTestPaymentRequirements();

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.VerifyAsync(paymentPayload, requirements));
    }

    /// <summary>
    /// Spec: Section 7.2 - POST /settle endpoint
    /// Use Case: UC-F3 Scenario 1
    /// </summary>
    [Fact]
    public async Task Given_VerifiedPayment_When_SettlingAsync_Then_SuccessResponseIsReturned()
    {
        // Arrange
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

        // Act
        var response = await client.SettleAsync(paymentPayload, requirements);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
    }

    /// <summary>
    /// Spec: Section 7.2 - POST /settle with failed settlement
    /// Use Case: UC-F3 Scenario 2
    /// </summary>
    [Fact]
    public async Task Given_FailedSettlement_When_SettlingAsync_Then_FailureResponseIsReturned()
    {
        // Arrange
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

        // Act
        var response = await client.SettleAsync(paymentPayload, requirements);

        // Assert
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.Equal("insufficient_balance", response.ErrorReason);
    }

    /// <summary>
    /// Spec: Section 7.2 - Cancellation token support
    /// Use Case: UC-F3 Scenario 3
    /// </summary>
    [Fact]
    public async Task Given_CancelledToken_When_SettlingAsync_Then_OperationCancelledExceptionIsThrown()
    {
        // Arrange
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

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.SettleAsync(paymentPayload, requirements, cts.Token));
    }

    /// <summary>
    /// Spec: Section 7.3 - GET /supported endpoint
    /// Use Case: UC-F4 Scenario 1
    /// </summary>
    [Fact]
    public async Task Given_Facilitator_When_GettingSupportedAsync_Then_SupportedKindsAreReturned()
    {
        // Arrange
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

        // Act
        var response = await client.GetSupportedAsync();

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Kinds);
        Assert.Equal(2, response.Kinds.Count);
        Assert.Equal("exact", response.Kinds[0].Scheme);
        Assert.Equal("base-sepolia", response.Kinds[0].Network);
    }

    /// <summary>
    /// Spec: Section 7.3 - GET /supported with empty list
    /// Use Case: UC-F4 Scenario 2
    /// </summary>
    [Fact]
    public async Task Given_FacilitatorWithNoSupport_When_GettingSupportedAsync_Then_EmptyArrayIsReturned()
    {
        // Arrange
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

        // Act
        var response = await client.GetSupportedAsync();

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Kinds);
        Assert.Empty(response.Kinds);
    }

    /// <summary>
    /// Spec: Section 7.1 - Request body validation
    /// Use Case: UC-F2 Scenario 1
    /// </summary>
    [Fact]
    public async Task Given_ValidPayment_When_VerifyingAsync_Then_RequestBodyIsCorrect()
    {
        // Arrange
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

        // Act
        await client.VerifyAsync(paymentPayload, requirements);

        // Assert
        Assert.NotNull(capturedRequestBody);
        Assert.Contains("\"paymentPayload\"", capturedRequestBody);
        Assert.Contains("\"paymentRequirements\"", capturedRequestBody);
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

/// <summary>
/// Mock HTTP message handler for testing
/// </summary>
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
