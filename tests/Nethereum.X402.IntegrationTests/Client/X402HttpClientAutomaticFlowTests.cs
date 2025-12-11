using Nethereum.X402.Client;
using Nethereum.X402.Models;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Nethereum.X402.IntegrationTests.Client;

/// <summary>
/// BDD tests for X402HttpClient automatic payment flow
///
/// Traceability:
/// - Spec: Section 4, Client Flow (Automatic Payment Handling)
/// - Use Cases: UC-C1 through UC-C4
/// - Requirements: Automatic 402 handling, payment selection, amount validation
/// - Implementation: src/Nethereum.X402/Client/X402HttpClient.cs
/// </summary>
public class X402HttpClientAutomaticFlowTests
{
    private const string TEST_PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    private const string TOKEN_NAME = "USD Coin";
    private const string TOKEN_VERSION = "2";
    private const int CHAIN_ID = 31337;
    private const string TOKEN_ADDRESS = "0x5FbDB2315678afecb367f032d93F642f64180aa3";
    private const string NETWORK = "localhost";

    #region Constructor Tests

    /// <summary>
    /// Spec: Section 4 - Client initialization with options
    /// Use Case: UC-C1 Scenario 1
    /// </summary>
    [Fact]
    public void Given_ValidOptions_When_CreatingClient_Then_ClientIsCreated()
    {
        // Arrange
        var httpClient = new HttpClient();
        var options = CreateValidOptions();

        // Act
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options);

        // Assert
        Assert.NotNull(client);
        Assert.NotNull(client.Address);
    }

    /// <summary>
    /// Spec: Section 4 - Null parameter validation
    /// Use Case: UC-C1 Scenario 2
    /// </summary>
    [Fact]
    public void Given_NullHttpClient_When_CreatingClient_Then_ArgumentNullExceptionIsThrown()
    {
        // Arrange
        var options = CreateValidOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new X402HttpClient(null!, TEST_PRIVATE_KEY, options));
    }

    /// <summary>
    /// Spec: Section 4 - Null parameter validation
    /// Use Case: UC-C1 Scenario 2
    /// </summary>
    [Fact]
    public void Given_NullPrivateKey_When_CreatingClient_Then_ArgumentNullExceptionIsThrown()
    {
        // Arrange
        var httpClient = new HttpClient();
        var options = CreateValidOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new X402HttpClient(httpClient, null!, options));
    }

    /// <summary>
    /// Spec: Section 4 - Null parameter validation
    /// Use Case: UC-C1 Scenario 2
    /// </summary>
    [Fact]
    public void Given_NullOptions_When_CreatingClient_Then_ArgumentNullExceptionIsThrown()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new X402HttpClient(httpClient, TEST_PRIVATE_KEY, null!));
    }

    /// <summary>
    /// Spec: Section 4 - Options validation
    /// Use Case: UC-C1 Scenario 3
    /// </summary>
    [Fact]
    public void Given_InvalidOptions_When_CreatingClient_Then_InvalidOperationExceptionIsThrown()
    {
        // Arrange
        var httpClient = new HttpClient();
        var options = new X402HttpClientOptions(); // Missing required fields

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options));
    }

    /// <summary>
    /// Spec: Section 4 - Manual mode validation
    /// Use Case: UC-C1 Scenario 4
    /// </summary>
    [Fact]
    public async Task Given_ManualModeClient_When_CallingAutomaticMethod_Then_InvalidOperationExceptionIsThrown()
    {
        // Arrange
        var httpClient = new HttpClient();
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, TOKEN_NAME, TOKEN_VERSION, CHAIN_ID, TOKEN_ADDRESS);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetAsync("http://localhost:5000/test"));
    }

    #endregion

    #region Automatic Payment Flow Tests

    /// <summary>
    /// Spec: Section 4.2 - Automatic 402 handling with payment
    /// Use Case: UC-C2 Scenario 1 - Complete automatic payment flow
    /// </summary>
    [Fact]
    public async Task Given_402Response_When_MakingRequest_Then_PaymentIsAutomaticallySentAndRetried()
    {
        // Arrange
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            requestCount++;

            if (requestCount == 1)
            {
                // First request - return 402
                var paymentRequired = new PaymentRequirementsResponse
                {
                    Accepts = new List<PaymentRequirements>
                    {
                        CreateTestPaymentRequirements()
                    }
                };
                return new HttpResponseMessage(HttpStatusCode.PaymentRequired)
                {
                    Content = new StringContent(JsonSerializer.Serialize(paymentRequired))
                };
            }
            else
            {
                // Second request - verify X-PAYMENT header and return success
                Assert.True(request.Headers.Contains("X-PAYMENT"));
                var settlement = new SettlementResponse
                {
                    Success = true,
                    Transaction = "0xabc123",
                    Network = NETWORK,
                    Payer = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266"
                };
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"message\":\"Success\"}")
                };
                response.Headers.Add("X-PAYMENT-RESPONSE", EncodeBase64(JsonSerializer.Serialize(settlement)));
                return response;
            }
        });

        var httpClient = new HttpClient(handler);
        var options = CreateValidOptions();
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options);

        // Act
        var response = await client.GetAsync("http://localhost:5000/premium");

        // Assert
        Assert.Equal(2, requestCount); // Two requests made
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.HasPaymentResponse());
        Assert.True(response.IsPaymentSuccessful());
        Assert.Equal("0xabc123", response.GetTransactionHash());
    }

    /// <summary>
    /// Spec: Section 4.2 - Pass-through for non-402 responses
    /// Use Case: UC-C2 Scenario 2 - No payment needed
    /// </summary>
    [Fact]
    public async Task Given_200Response_When_MakingRequest_Then_NoPaymentIsSent()
    {
        // Arrange
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            requestCount++;
            Assert.False(request.Headers.Contains("X-PAYMENT")); // Should not have payment header

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"message\":\"Free content\"}")
            });
        });

        var httpClient = new HttpClient(handler);
        var options = CreateValidOptions();
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options);

        // Act
        var response = await client.GetAsync("http://localhost:5000/free");

        // Assert
        Assert.Equal(1, requestCount); // Only one request made
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.HasPaymentResponse());
    }

    /// <summary>
    /// Spec: Section 4.4 - Payment amount validation
    /// Use Case: UC-C2 Scenario 3 - Amount exceeds maximum
    /// </summary>
    [Fact]
    public async Task Given_PaymentExceedsMaximum_When_MakingRequest_Then_ExceptionIsThrown()
    {
        // Arrange
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            // Return 402 with high payment amount
            var paymentRequired = new PaymentRequirementsResponse
            {
                Accepts = new List<PaymentRequirements>
                {
                    new()
                    {
                        Scheme = "exact",
                        Network = NETWORK,
                        MaxAmountRequired = "5000000", // 5 USDC (exceeds 1 USDC max)
                        PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
                        Asset = TOKEN_ADDRESS
                    }
                }
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PaymentRequired)
            {
                Content = new StringContent(JsonSerializer.Serialize(paymentRequired))
            });
        });

        var httpClient = new HttpClient(handler);
        var options = CreateValidOptions();
        options.MaxPaymentAmount = 1.0m; // Max 1 USDC
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<X402PaymentExceedsMaximumException>(() =>
            client.GetAsync("http://localhost:5000/expensive"));

        Assert.Equal(5.0m, exception.RequestedAmount);
        Assert.Equal(1.0m, exception.MaximumAllowed);
    }

    /// <summary>
    /// Spec: Section 4.3 - Payment requirements selection
    /// Use Case: UC-C2 Scenario 4 - Multiple payment options
    /// </summary>
    [Fact]
    public async Task Given_MultiplePaymentOptions_When_MakingRequest_Then_PreferredNetworkIsSelected()
    {
        // Arrange
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            requestCount++;

            if (requestCount == 1)
            {
                // Return 402 with multiple options
                var paymentRequired = new PaymentRequirementsResponse
                {
                    Accepts = new List<PaymentRequirements>
                    {
                        new()
                        {
                            Scheme = "exact",
                            Network = "ethereum",
                            MaxAmountRequired = "100000",
                            PayTo = "0x111",
                            Asset = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48"
                        },
                        new()
                        {
                            Scheme = "exact",
                            Network = NETWORK,
                            MaxAmountRequired = "100000",
                            PayTo = "0x222",
                            Asset = TOKEN_ADDRESS
                        }
                    }
                };
                return new HttpResponseMessage(HttpStatusCode.PaymentRequired)
                {
                    Content = new StringContent(JsonSerializer.Serialize(paymentRequired))
                };
            }
            else
            {
                // Verify the correct network was selected by checking payment payload
                var paymentHeader = request.Headers.GetValues("X-PAYMENT").First();
                var paymentJson = Encoding.UTF8.GetString(Convert.FromBase64String(paymentHeader));
                var payment = JsonSerializer.Deserialize<PaymentPayload>(paymentJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                Assert.Equal(NETWORK, payment?.Network);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"message\":\"Success\"}")
                };
            }
        });

        var httpClient = new HttpClient(handler);
        var options = CreateValidOptions();
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options);

        // Act
        var response = await client.GetAsync("http://localhost:5000/premium");

        // Assert
        Assert.Equal(2, requestCount);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Spec: Section 4.3 - Custom selector usage
    /// Use Case: UC-C2 Scenario 5 - Custom payment selection logic
    /// </summary>
    [Fact]
    public async Task Given_CustomSelector_When_MakingRequest_Then_CustomSelectorIsUsed()
    {
        // Arrange
        var customSelectorCalled = false;
        var customSelector = new TestPaymentRequirementsSelector((reqs) =>
        {
            customSelectorCalled = true;
            return reqs.Last(); // Pick last option instead of default logic
        });

        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            requestCount++;

            if (requestCount == 1)
            {
                var paymentRequired = new PaymentRequirementsResponse
                {
                    Accepts = new List<PaymentRequirements>
                    {
                        CreateTestPaymentRequirements(),
                        CreateTestPaymentRequirements()
                    }
                };
                return new HttpResponseMessage(HttpStatusCode.PaymentRequired)
                {
                    Content = new StringContent(JsonSerializer.Serialize(paymentRequired))
                };
            }
            else
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"message\":\"Success\"}")
                };
            }
        });

        var httpClient = new HttpClient(handler);
        var options = CreateValidOptions();
        options.Selector = customSelector;
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options);

        // Act
        await client.GetAsync("http://localhost:5000/premium");

        // Assert
        Assert.True(customSelectorCalled);
    }

    /// <summary>
    /// Spec: Section 4.5 - Prevent infinite retry loops
    /// Use Case: UC-C2 Scenario 6 - Payment rejected by server
    /// </summary>
    [Fact]
    public async Task Given_PaymentRejectedBy402_When_Retrying_Then_402IsReturned()
    {
        // Arrange
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            requestCount++;
            // Always return 402, even with payment
            var paymentRequired = new PaymentRequirementsResponse
            {
                Accepts = new List<PaymentRequirements> { CreateTestPaymentRequirements() }
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PaymentRequired)
            {
                Content = new StringContent(JsonSerializer.Serialize(paymentRequired))
            });
        });

        var httpClient = new HttpClient(handler);
        var options = CreateValidOptions();
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options);

        // Act
        var response = await client.GetAsync("http://localhost:5000/premium");

        // Assert
        Assert.Equal(2, requestCount); // Two requests (initial + retry with payment)
        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode); // Still 402 after payment attempt
    }

    /// <summary>
    /// Spec: Section 4.2 - Invalid 402 response handling
    /// Use Case: UC-C2 Scenario 7 - Malformed payment requirements
    /// </summary>
    [Fact]
    public async Task Given_402WithoutPaymentRequirements_When_MakingRequest_Then_ExceptionIsThrown()
    {
        // Arrange
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            // Return 402 with empty accepts array
            var paymentRequired = new PaymentRequirementsResponse
            {
                Accepts = new List<PaymentRequirements>()
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PaymentRequired)
            {
                Content = new StringContent(JsonSerializer.Serialize(paymentRequired))
            });
        });

        var httpClient = new HttpClient(handler);
        var options = CreateValidOptions();
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetAsync("http://localhost:5000/premium"));

        Assert.Contains("no payment requirements were provided", exception.Message);
    }

    #endregion

    #region HTTP Method Tests

    /// <summary>
    /// Spec: Section 4.2 - POST method with automatic payment
    /// Use Case: UC-C3 Scenario 1
    /// </summary>
    [Fact]
    public async Task Given_PostRequest_When_402Received_Then_PaymentIsAutomaticallySent()
    {
        // Arrange
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            requestCount++;
            Assert.Equal(HttpMethod.Post, request.Method);

            if (requestCount == 1)
            {
                var paymentRequired = new PaymentRequirementsResponse
                {
                    Accepts = new List<PaymentRequirements> { CreateTestPaymentRequirements() }
                };
                return new HttpResponseMessage(HttpStatusCode.PaymentRequired)
                {
                    Content = new StringContent(JsonSerializer.Serialize(paymentRequired))
                };
            }
            else
            {
                Assert.True(request.Headers.Contains("X-PAYMENT"));
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("{\"id\":123}")
                };
            }
        });

        var httpClient = new HttpClient(handler);
        var options = CreateValidOptions();
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options);

        // Act
        var content = new StringContent("{\"data\":\"test\"}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("http://localhost:5000/data", content);

        // Assert
        Assert.Equal(2, requestCount);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>
    /// Spec: Section 4.2 - PUT method with automatic payment
    /// Use Case: UC-C3 Scenario 2
    /// </summary>
    [Fact]
    public async Task Given_PutRequest_When_402Received_Then_PaymentIsAutomaticallySent()
    {
        // Arrange
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            requestCount++;
            Assert.Equal(HttpMethod.Put, request.Method);

            if (requestCount == 1)
            {
                var paymentRequired = new PaymentRequirementsResponse
                {
                    Accepts = new List<PaymentRequirements> { CreateTestPaymentRequirements() }
                };
                return new HttpResponseMessage(HttpStatusCode.PaymentRequired)
                {
                    Content = new StringContent(JsonSerializer.Serialize(paymentRequired))
                };
            }
            else
            {
                Assert.True(request.Headers.Contains("X-PAYMENT"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"updated\":true}")
                };
            }
        });

        var httpClient = new HttpClient(handler);
        var options = CreateValidOptions();
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options);

        // Act
        var content = new StringContent("{\"data\":\"updated\"}", Encoding.UTF8, "application/json");
        var response = await client.PutAsync("http://localhost:5000/data/123", content);

        // Assert
        Assert.Equal(2, requestCount);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Spec: Section 4.2 - DELETE method with automatic payment
    /// Use Case: UC-C3 Scenario 3
    /// </summary>
    [Fact]
    public async Task Given_DeleteRequest_When_402Received_Then_PaymentIsAutomaticallySent()
    {
        // Arrange
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            requestCount++;
            Assert.Equal(HttpMethod.Delete, request.Method);

            if (requestCount == 1)
            {
                var paymentRequired = new PaymentRequirementsResponse
                {
                    Accepts = new List<PaymentRequirements> { CreateTestPaymentRequirements() }
                };
                return new HttpResponseMessage(HttpStatusCode.PaymentRequired)
                {
                    Content = new StringContent(JsonSerializer.Serialize(paymentRequired))
                };
            }
            else
            {
                Assert.True(request.Headers.Contains("X-PAYMENT"));
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
        });

        var httpClient = new HttpClient(handler);
        var options = CreateValidOptions();
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options);

        // Act
        var response = await client.DeleteAsync("http://localhost:5000/data/123");

        // Assert
        Assert.Equal(2, requestCount);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Spec: Section 4.2 - Generic SendAsync method with automatic payment
    /// Use Case: UC-C3 Scenario 4
    /// </summary>
    [Fact]
    public async Task Given_CustomRequest_When_UsingSendAsync_Then_PaymentIsAutomaticallySent()
    {
        // Arrange
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            requestCount++;

            if (requestCount == 1)
            {
                var paymentRequired = new PaymentRequirementsResponse
                {
                    Accepts = new List<PaymentRequirements> { CreateTestPaymentRequirements() }
                };
                return new HttpResponseMessage(HttpStatusCode.PaymentRequired)
                {
                    Content = new StringContent(JsonSerializer.Serialize(paymentRequired))
                };
            }
            else
            {
                Assert.True(request.Headers.Contains("X-PAYMENT"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"success\":true}")
                };
            }
        });

        var httpClient = new HttpClient(handler);
        var options = CreateValidOptions();
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:5000/custom");
        request.Headers.Add("X-Custom-Header", "test");
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(2, requestCount);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region Extension Method Tests

    /// <summary>
    /// Spec: Section 5.3 - Settlement response parsing
    /// Use Case: UC-C4 Scenario 1
    /// </summary>
    [Fact]
    public void Given_ResponseWithPayment_When_UsingExtensions_Then_SettlementIsParsed()
    {
        // Arrange
        var settlement = new SettlementResponse
        {
            Success = true,
            Transaction = "0xabc123",
            Network = NETWORK,
            Payer = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266"
        };
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("X-PAYMENT-RESPONSE", EncodeBase64(JsonSerializer.Serialize(settlement)));

        // Act
        var parsed = response.GetSettlementResponse();

        // Assert
        Assert.NotNull(parsed);
        Assert.True(parsed.Success);
        Assert.Equal("0xabc123", parsed.Transaction);
        Assert.Equal(NETWORK, parsed.Network);
        Assert.Equal("0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266", parsed.Payer);
    }

    /// <summary>
    /// Spec: Section 5.3 - Payment success check
    /// Use Case: UC-C4 Scenario 2
    /// </summary>
    [Fact]
    public void Given_SuccessfulPayment_When_CheckingSuccess_Then_ReturnsTrue()
    {
        // Arrange
        var settlement = new SettlementResponse { Success = true };
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("X-PAYMENT-RESPONSE", EncodeBase64(JsonSerializer.Serialize(settlement)));

        // Act & Assert
        Assert.True(response.IsPaymentSuccessful());
        Assert.True(response.HasPaymentResponse());
    }

    /// <summary>
    /// Spec: Section 5.3 - Failed payment parsing
    /// Use Case: UC-C4 Scenario 3
    /// </summary>
    [Fact]
    public void Given_FailedPayment_When_GettingError_Then_ErrorReasonIsReturned()
    {
        // Arrange
        var settlement = new SettlementResponse
        {
            Success = false,
            ErrorReason = "insufficient_balance"
        };
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("X-PAYMENT-RESPONSE", EncodeBase64(JsonSerializer.Serialize(settlement)));

        // Act
        var error = response.GetPaymentError();

        // Assert
        Assert.False(response.IsPaymentSuccessful());
        Assert.Equal("insufficient_balance", error);
    }

    /// <summary>
    /// Spec: Section 5.3 - No payment response handling
    /// Use Case: UC-C4 Scenario 4
    /// </summary>
    [Fact]
    public void Given_NoPaymentResponse_When_UsingExtensions_Then_ReturnsNull()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK);

        // Act & Assert
        Assert.False(response.HasPaymentResponse());
        Assert.Null(response.GetSettlementResponse());
        Assert.Null(response.GetTransactionHash());
        Assert.Null(response.GetPayerAddress());
        Assert.False(response.IsPaymentSuccessful());
        Assert.Null(response.GetPaymentError());
    }

    /// <summary>
    /// Spec: Section 5.3 - Transaction hash extraction
    /// Use Case: UC-C4 Scenario 5
    /// </summary>
    [Fact]
    public void Given_PaymentResponse_When_GettingTransactionHash_Then_HashIsReturned()
    {
        // Arrange
        var settlement = new SettlementResponse
        {
            Success = true,
            Transaction = "0x123abc456def"
        };
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("X-PAYMENT-RESPONSE", EncodeBase64(JsonSerializer.Serialize(settlement)));

        // Act
        var hash = response.GetTransactionHash();

        // Assert
        Assert.Equal("0x123abc456def", hash);
    }

    /// <summary>
    /// Spec: Section 5.3 - Payer address extraction
    /// Use Case: UC-C4 Scenario 6
    /// </summary>
    [Fact]
    public void Given_PaymentResponse_When_GettingPayerAddress_Then_AddressIsReturned()
    {
        // Arrange
        var settlement = new SettlementResponse
        {
            Success = true,
            Payer = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266"
        };
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("X-PAYMENT-RESPONSE", EncodeBase64(JsonSerializer.Serialize(settlement)));

        // Act
        var payer = response.GetPayerAddress();

        // Assert
        Assert.Equal("0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266", payer);
    }

    #endregion

    #region Helper Methods

    private X402HttpClientOptions CreateValidOptions()
    {
        return new X402HttpClientOptions
        {
            PreferredNetwork = NETWORK,
            PreferredScheme = "exact",
            MaxPaymentAmount = 1.0m,
            TokenName = TOKEN_NAME,
            TokenVersion = TOKEN_VERSION,
            ChainId = CHAIN_ID,
            TokenAddress = TOKEN_ADDRESS
        };
    }

    private PaymentRequirements CreateTestPaymentRequirements()
    {
        return new PaymentRequirements
        {
            Scheme = "exact",
            Network = NETWORK,
            MaxAmountRequired = "100000", // 0.1 USDC
            Resource = "/premium",
            Description = "Premium content",
            MimeType = "application/json",
            PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            MaxTimeoutSeconds = 300,
            Asset = TOKEN_ADDRESS
        };
    }

    private string EncodeBase64(string json)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    #endregion
}

/// <summary>
/// Test implementation of IPaymentRequirementsSelector for custom selection logic
/// </summary>
public class TestPaymentRequirementsSelector : IPaymentRequirementsSelector
{
    private readonly Func<List<PaymentRequirements>, PaymentRequirements> _selector;

    public TestPaymentRequirementsSelector(Func<List<PaymentRequirements>, PaymentRequirements> selector)
    {
        _selector = selector;
    }

    public PaymentRequirements SelectRequirements(
        IEnumerable<PaymentRequirements> availableRequirements,
        string preferredNetwork,
        string preferredScheme)
    {
        return _selector(availableRequirements.ToList());
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
