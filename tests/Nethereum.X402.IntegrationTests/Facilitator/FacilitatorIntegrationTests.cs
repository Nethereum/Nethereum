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

public class FacilitatorIntegrationTests
{
    [Fact]
    public async Task Given_Facilitator_When_CallingSupportedEndpoint_Then_ReturnsSupportedKinds()
    {
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

        var response = await client.GetAsync("/facilitator/supported");

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

    [Fact]
    public async Task Given_InvalidRequest_When_CallingVerifyEndpoint_Then_Returns400()
    {
        var mockProcessor = new Mock<IX402PaymentProcessor>();
        using var server = CreateTestServer(mockProcessor.Object);
        var client = server.CreateClient();

        var content = new StringContent(
            "{}",
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/facilitator/verify", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Given_InvalidRequest_When_CallingSettleEndpoint_Then_Returns400()
    {
        var mockProcessor = new Mock<IX402PaymentProcessor>();
        using var server = CreateTestServer(mockProcessor.Object);
        var client = server.CreateClient();

        var content = new StringContent(
            "{}",
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/facilitator/settle", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void Given_AnAccountFactory_When_TheExactProcessorIsRegistered_Then_ItResolvesTheDispatcher()
    {
        var services = new ServiceCollection();
        var factoryCalls = 0;

        services.AddX402ExactProcessor(
            _ => { factoryCalls++; return new Account("0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80"); },
            new Dictionary<int, string> { { 84532, "http://localhost:8545" } });

        using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<IX402PaymentProcessor>();

        Assert.IsType<X402ExactSchemeProcessor>(processor);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task Given_RealProcessorConfiguration_When_StartingServer_Then_ServerStarts()
    {
        var testPrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        var testAccount = new Account(testPrivateKey);

        var rpcEndpointsByChainId = new Dictionary<int, string>
        {
            { 31337, "http://localhost:8545" }
        };

        using var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddControllers()
                    .AddX402FacilitatorControllers();

                services.AddX402TransferProcessor(
                    testAccount,
                    rpcEndpointsByChainId);
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
                        options.JsonSerializerOptions.PropertyNamingPolicy = null;
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
