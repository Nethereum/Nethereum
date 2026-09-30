using Nethereum.Documentation;
using Nethereum.X402.Client;
using Nethereum.X402.Models;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Nethereum.X402.IntegrationTests.Client;

public class X402HttpClientAutomaticFlowTests
{
    private const string TEST_PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    private const string TOKEN_NAME = "USD Coin";
    private const string TOKEN_VERSION = "2";
    private const int CHAIN_ID = 31337;
    private const string TOKEN_ADDRESS = "0x5FbDB2315678afecb367f032d93F642f64180aa3";
    private const string NETWORK = "eip155:31337";

    #region Constructor Tests

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "X402HttpClient: create client with options", Order = 1)]
    public void Given_ValidOptions_When_CreatingClient_Then_ClientIsCreated()
    {
        var httpClient = new HttpClient();
        var options = CreateValidOptions();

        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options);

        Assert.NotNull(client);
        Assert.NotNull(client.Address);
    }

    [Fact]
    public void Given_NullHttpClient_When_CreatingClient_Then_ArgumentNullExceptionIsThrown()
    {
        var options = CreateValidOptions();

        Assert.Throws<ArgumentNullException>(() =>
            new X402HttpClient(null!, TEST_PRIVATE_KEY, options));
    }

    [Fact]
    public void Given_NullPrivateKey_When_CreatingClient_Then_ArgumentNullExceptionIsThrown()
    {
        var httpClient = new HttpClient();
        var options = CreateValidOptions();

        Assert.Throws<ArgumentNullException>(() =>
            new X402HttpClient(httpClient, null!, options));
    }

    [Fact]
    public void Given_NullOptions_When_CreatingClient_Then_ArgumentNullExceptionIsThrown()
    {
        var httpClient = new HttpClient();

        Assert.Throws<ArgumentNullException>(() =>
            new X402HttpClient(httpClient, TEST_PRIVATE_KEY, null!));
    }

    [Fact]
    public void Given_InvalidOptions_When_CreatingClient_Then_InvalidOperationExceptionIsThrown()
    {
        var httpClient = new HttpClient();
        var options = new X402HttpClientOptions();

        Assert.Throws<InvalidOperationException>(() =>
            new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options));
    }

    [Fact]
    public async Task Given_ManualModeClient_When_CallingAutomaticMethod_Then_InvalidOperationExceptionIsThrown()
    {
        var httpClient = new HttpClient();
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetAsync("http://localhost:5000/test"));
    }

    #endregion

    #region Automatic Payment Flow Tests

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "X402HttpClient: automatic 402 payment flow", Order = 2)]
    public async Task Given_402Response_When_MakingRequest_Then_PaymentIsAutomaticallySentAndRetried()
    {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            requestCount++;

            if (requestCount == 1)
            {
                var paymentRequired = new PaymentRequired
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
                Assert.True(request.Headers.Contains("PAYMENT-SIGNATURE"));
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
                response.Headers.Add("PAYMENT-RESPONSE", EncodeBase64(JsonSerializer.Serialize(settlement)));
                return response;
            }
        });

        var httpClient = new HttpClient(handler);
        var options = CreateValidOptions();
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options);

        var response = await client.GetAsync("http://localhost:5000/premium");

        Assert.Equal(2, requestCount);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.HasPaymentResponse());
        Assert.True(response.IsPaymentSuccessful());
        Assert.Equal("0xabc123", response.GetTransactionHash());
    }

    [Fact]
    public async Task Given_200Response_When_MakingRequest_Then_NoPaymentIsSent()
    {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            requestCount++;
            Assert.False(request.Headers.Contains("PAYMENT-SIGNATURE"));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"message\":\"Free content\"}")
            });
        });

        var httpClient = new HttpClient(handler);
        var options = CreateValidOptions();
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options);

        var response = await client.GetAsync("http://localhost:5000/free");

        Assert.Equal(1, requestCount);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.HasPaymentResponse());
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "X402HttpClient: max payment protection", Order = 3)]
    public async Task Given_PaymentExceedsMaximum_When_MakingRequest_Then_ExceptionIsThrown()
    {
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            var paymentRequired = new PaymentRequired
            {
                Accepts = new List<PaymentRequirements>
                {
                    new()
                    {
                        Scheme = "exact",
                        Network = NETWORK,
                        Amount = "5000000",
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
        options.MaxAmount = "1000000";
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options);

        var exception = await Assert.ThrowsAsync<X402PaymentExceedsMaximumException>(() =>
            client.GetAsync("http://localhost:5000/expensive"));

        Assert.Equal("5000000", exception.RequestedAmount);
        Assert.Equal("1000000", exception.MaximumAllowed);
    }

    [Fact]
    public async Task Given_MultiplePaymentOptions_When_MakingRequest_Then_PreferredNetworkIsSelected()
    {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            requestCount++;

            if (requestCount == 1)
            {
                var paymentRequired = new PaymentRequired
                {
                    Accepts = new List<PaymentRequirements>
                    {
                        new()
                        {
                            Scheme = "exact",
                            Network = "ethereum",
                            Amount = "100000",
                            PayTo = "0x111",
                            Asset = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48"
                        },
                        new()
                        {
                            Scheme = "exact",
                            Network = NETWORK,
                            Amount = "100000",
                            PayTo = "0x222",
                            Asset = TOKEN_ADDRESS,
                            Extra = new ExactSchemeExtra { Name = TOKEN_NAME, Version = TOKEN_VERSION }
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
                var paymentHeader = request.Headers.GetValues("PAYMENT-SIGNATURE").First();
                var paymentJson = Encoding.UTF8.GetString(Convert.FromBase64String(paymentHeader));
                var payment = JsonSerializer.Deserialize<PaymentPayload>(paymentJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                Assert.Equal(NETWORK, payment?.Accepted?.Network);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"message\":\"Success\"}")
                };
            }
        });

        var httpClient = new HttpClient(handler);
        var options = CreateValidOptions();
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options);

        var response = await client.GetAsync("http://localhost:5000/premium");

        Assert.Equal(2, requestCount);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Given_CustomSelector_When_MakingRequest_Then_CustomSelectorIsUsed()
    {
        var customSelectorCalled = false;
        var customSelector = new TestPaymentRequirementsSelector((reqs) =>
        {
            customSelectorCalled = true;
            return reqs.Last();
        });

        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            requestCount++;

            if (requestCount == 1)
            {
                var paymentRequired = new PaymentRequired
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

        await client.GetAsync("http://localhost:5000/premium");

        Assert.True(customSelectorCalled);
    }

    [Fact]
    public async Task Given_PaymentRejectedBy402_When_Retrying_Then_402IsReturned()
    {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            requestCount++;
            var paymentRequired = new PaymentRequired
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

        var response = await client.GetAsync("http://localhost:5000/premium");

        Assert.Equal(2, requestCount);
        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
    }

    [Fact]
    public async Task Given_402WithoutPaymentRequirements_When_MakingRequest_Then_ExceptionIsThrown()
    {
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            var paymentRequired = new PaymentRequired
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

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetAsync("http://localhost:5000/premium"));

        Assert.Contains("no payment requirements were provided", exception.Message);
    }

    #endregion

    #region HTTP Method Tests

    [Fact]
    public async Task Given_PostRequest_When_402Received_Then_PaymentIsAutomaticallySent()
    {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            requestCount++;
            Assert.Equal(HttpMethod.Post, request.Method);

            if (requestCount == 1)
            {
                var paymentRequired = new PaymentRequired
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
                Assert.True(request.Headers.Contains("PAYMENT-SIGNATURE"));
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("{\"id\":123}")
                };
            }
        });

        var httpClient = new HttpClient(handler);
        var options = CreateValidOptions();
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options);

        var content = new StringContent("{\"data\":\"test\"}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("http://localhost:5000/data", content);

        Assert.Equal(2, requestCount);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Given_PutRequest_When_402Received_Then_PaymentIsAutomaticallySent()
    {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            requestCount++;
            Assert.Equal(HttpMethod.Put, request.Method);

            if (requestCount == 1)
            {
                var paymentRequired = new PaymentRequired
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
                Assert.True(request.Headers.Contains("PAYMENT-SIGNATURE"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"updated\":true}")
                };
            }
        });

        var httpClient = new HttpClient(handler);
        var options = CreateValidOptions();
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options);

        var content = new StringContent("{\"data\":\"updated\"}", Encoding.UTF8, "application/json");
        var response = await client.PutAsync("http://localhost:5000/data/123", content);

        Assert.Equal(2, requestCount);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Given_DeleteRequest_When_402Received_Then_PaymentIsAutomaticallySent()
    {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            requestCount++;
            Assert.Equal(HttpMethod.Delete, request.Method);

            if (requestCount == 1)
            {
                var paymentRequired = new PaymentRequired
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
                Assert.True(request.Headers.Contains("PAYMENT-SIGNATURE"));
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
        });

        var httpClient = new HttpClient(handler);
        var options = CreateValidOptions();
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options);

        var response = await client.DeleteAsync("http://localhost:5000/data/123");

        Assert.Equal(2, requestCount);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Given_CustomRequest_When_UsingSendAsync_Then_PaymentIsAutomaticallySent()
    {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            requestCount++;

            if (requestCount == 1)
            {
                var paymentRequired = new PaymentRequired
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
                Assert.True(request.Headers.Contains("PAYMENT-SIGNATURE"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"success\":true}")
                };
            }
        });

        var httpClient = new HttpClient(handler);
        var options = CreateValidOptions();
        var client = new X402HttpClient(httpClient, TEST_PRIVATE_KEY, options);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:5000/custom");
        request.Headers.Add("X-Custom-Header", "test");
        var response = await client.SendAsync(request);

        Assert.Equal(2, requestCount);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region Extension Method Tests

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "X402HttpClient: parse payment response headers", Order = 4)]
    public void Given_ResponseWithPayment_When_UsingExtensions_Then_SettlementIsParsed()
    {
        var settlement = new SettlementResponse
        {
            Success = true,
            Transaction = "0xabc123",
            Network = NETWORK,
            Payer = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266"
        };
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("PAYMENT-RESPONSE", EncodeBase64(JsonSerializer.Serialize(settlement)));

        var parsed = response.GetSettlementResponse();

        Assert.NotNull(parsed);
        Assert.True(parsed.Success);
        Assert.Equal("0xabc123", parsed.Transaction);
        Assert.Equal(NETWORK, parsed.Network);
        Assert.Equal("0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266", parsed.Payer);
    }

    [Fact]
    public void Given_SuccessfulPayment_When_CheckingSuccess_Then_ReturnsTrue()
    {
        var settlement = new SettlementResponse { Success = true };
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("PAYMENT-RESPONSE", EncodeBase64(JsonSerializer.Serialize(settlement)));

        Assert.True(response.IsPaymentSuccessful());
        Assert.True(response.HasPaymentResponse());
    }

    [Fact]
    public void Given_FailedPayment_When_GettingError_Then_ErrorReasonIsReturned()
    {
        var settlement = new SettlementResponse
        {
            Success = false,
            ErrorReason = "insufficient_balance"
        };
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("PAYMENT-RESPONSE", EncodeBase64(JsonSerializer.Serialize(settlement)));

        var error = response.GetPaymentError();

        Assert.False(response.IsPaymentSuccessful());
        Assert.Equal("insufficient_balance", error);
    }

    [Fact]
    public void Given_NoPaymentResponse_When_UsingExtensions_Then_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);

        Assert.False(response.HasPaymentResponse());
        Assert.Null(response.GetSettlementResponse());
        Assert.Null(response.GetTransactionHash());
        Assert.Null(response.GetPayerAddress());
        Assert.False(response.IsPaymentSuccessful());
        Assert.Null(response.GetPaymentError());
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "X402HttpClient: extract transaction hash from response", Order = 5)]
    public void Given_PaymentResponse_When_GettingTransactionHash_Then_HashIsReturned()
    {
        var settlement = new SettlementResponse
        {
            Success = true,
            Transaction = "0x123abc456def"
        };
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("PAYMENT-RESPONSE", EncodeBase64(JsonSerializer.Serialize(settlement)));

        var hash = response.GetTransactionHash();

        Assert.Equal("0x123abc456def", hash);
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "X402HttpClient: extract payer address from response", Order = 6)]
    public void Given_PaymentResponse_When_GettingPayerAddress_Then_AddressIsReturned()
    {
        var settlement = new SettlementResponse
        {
            Success = true,
            Payer = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266"
        };
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("PAYMENT-RESPONSE", EncodeBase64(JsonSerializer.Serialize(settlement)));

        var payer = response.GetPayerAddress();

        Assert.Equal("0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266", payer);
    }

    #endregion

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "X402HttpClient: allow-list refuses a redirected payment", Order = 4)]
    public async Task Given_RecipientNotAllowed_When_MakingRequest_Then_PolicyViolationIsThrown()
    {
        var signed = false;
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            signed = request.Headers.Contains("PAYMENT-SIGNATURE");
            var paymentRequired = new PaymentRequired
            {
                Accepts = new List<PaymentRequirements>
                {
                    new()
                    {
                        Scheme = "exact",
                        Network = NETWORK,
                        Amount = "100000",
                        PayTo = "0xAtTaCkEr0000000000000000000000000000dEaD",
                        Asset = TOKEN_ADDRESS,
                        Extra = new ExactSchemeExtra { Name = TOKEN_NAME, Version = TOKEN_VERSION }
                    }
                }
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PaymentRequired)
            {
                Content = new StringContent(JsonSerializer.Serialize(paymentRequired))
            });
        });

        var options = CreateValidOptions();
        options.Policy.AllowedRecipients.Add("0x209693Bc6afc0C5328bA36FaF03C514EF312287C");
        var client = new X402HttpClient(new HttpClient(handler), TEST_PRIVATE_KEY, options);

        var exception = await Assert.ThrowsAsync<X402PaymentPolicyViolationException>(() =>
            client.GetAsync("http://localhost:5000/premium"));

        Assert.Equal(PaymentPolicyDimension.Recipient, exception.Dimension);
        Assert.False(signed);
    }

    [Fact]
    public async Task Given_AssetNotAllowed_When_MakingRequest_Then_PolicyViolationIsThrown()
    {
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            var paymentRequired = new PaymentRequired
            {
                Accepts = new List<PaymentRequirements>
                {
                    new()
                    {
                        Scheme = "exact",
                        Network = NETWORK,
                        Amount = "100000",
                        PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
                        Asset = "0xBADbADbadBADBADBadBaDBADbADBadbAdBaD0001",
                        Extra = new ExactSchemeExtra { Name = TOKEN_NAME, Version = TOKEN_VERSION }
                    }
                }
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PaymentRequired)
            {
                Content = new StringContent(JsonSerializer.Serialize(paymentRequired))
            });
        });

        var options = CreateValidOptions();
        options.Policy.AllowedAssets.Add(TOKEN_ADDRESS);
        var client = new X402HttpClient(new HttpClient(handler), TEST_PRIVATE_KEY, options);

        var exception = await Assert.ThrowsAsync<X402PaymentPolicyViolationException>(() =>
            client.GetAsync("http://localhost:5000/premium"));

        Assert.Equal(PaymentPolicyDimension.Asset, exception.Dimension);
    }

    [Fact]
    public async Task Given_RequirementSatisfiesPolicy_When_MakingRequest_Then_PaymentIsSent()
    {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                var paymentRequired = new PaymentRequired
                {
                    Accepts = new List<PaymentRequirements> { CreateTestPaymentRequirements() }
                };
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PaymentRequired)
                {
                    Content = new StringContent(JsonSerializer.Serialize(paymentRequired))
                });
            }

            Assert.True(request.Headers.Contains("PAYMENT-SIGNATURE"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"message\":\"Success\"}")
            });
        });

        var options = CreateValidOptions();
        options.Policy.AllowedNetworks.Add(NETWORK);
        options.Policy.AllowedAssets.Add(TOKEN_ADDRESS.ToLowerInvariant());
        options.Policy.AllowedRecipients.Add("0x209693Bc6afc0C5328bA36FaF03C514EF312287C");
        var client = new X402HttpClient(new HttpClient(handler), TEST_PRIVATE_KEY, options);

        var response = await client.GetAsync("http://localhost:5000/premium");

        Assert.Equal(2, requestCount);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #region Helper Methods

    private X402HttpClientOptions CreateValidOptions()
    {
        return new X402HttpClientOptions
        {
            PreferredNetwork = NETWORK,
            PreferredScheme = "exact",
            MaxAmount = "1000000",
        };
    }

    private PaymentRequirements CreateTestPaymentRequirements()
    {
        return new PaymentRequirements
        {
            Scheme = "exact",
            Network = NETWORK,
            Amount = "100000",
            PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
            MaxTimeoutSeconds = 300,
            Asset = TOKEN_ADDRESS,
            Extra = new ExactSchemeExtra { Name = TOKEN_NAME, Version = TOKEN_VERSION }
        };
    }

    private string EncodeBase64(string json)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    #endregion
}

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
