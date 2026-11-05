using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Extensions;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Server;

namespace Nethereum.X402.IntegrationTests.Facilitator;

public class HttpFacilitatorClientBoundTests
{
    private const string Network = "eip155:84532";
    private const string Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66";

    [Fact]
    [Trait("Rule", "V11")]
    public async Task Given_ASlowFacilitatorAndAShortRequirementTimeout_When_Settling_Then_TheCallEndsAtTheSettleBoundWithSettlementPending()
    {
        using var server = SlowFacilitator(TimeSpan.FromSeconds(60));
        var client = new HttpFacilitatorClient(server.CreateClient(), "http://localhost/facilitator")
        {
            SettleTimeoutGrace = TimeSpan.FromSeconds(1)
        };
        var elapsed = Stopwatch.StartNew();

        var settlement = await client.SettleAsync(Payload(), Requirements(maxTimeoutSeconds: 1)).WaitAsync(TimeSpan.FromSeconds(40));

        Assert.False(settlement.Success);
        Assert.Equal(X402ErrorCodes.SettlementPending, settlement.ErrorReason);
        Assert.Equal(Network, settlement.Network);
        Assert.True(elapsed.Elapsed >= TimeSpan.FromSeconds(3.5), elapsed.Elapsed.ToString());
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(20), elapsed.Elapsed.ToString());
    }

    [Fact]
    [Trait("Rule", "V11")]
    public async Task Given_AFacilitatorSlowerThanTheRequirementTimeoutButWithinThreeTimesIt_When_Settling_Then_TheSettlementIsReturned()
    {
        using var server = SlowFacilitator(TimeSpan.FromSeconds(3));
        var client = new HttpFacilitatorClient(server.CreateClient(), "http://localhost/facilitator")
        {
            SettleTimeoutGrace = TimeSpan.Zero
        };

        var settlement = await client.SettleAsync(Payload(), Requirements(maxTimeoutSeconds: 2)).WaitAsync(TimeSpan.FromSeconds(40));

        Assert.True(settlement.Success, settlement.ErrorReason);
        Assert.Equal("0x01", settlement.Transaction);
    }

    [Fact]
    [Trait("Rule", "V11")]
    public async Task Given_ASlowFacilitator_When_Verifying_Then_TheCallTimesOutAtTheVerifyBound()
    {
        using var server = SlowFacilitator(TimeSpan.FromSeconds(60));
        var client = new HttpFacilitatorClient(server.CreateClient(), "http://localhost/facilitator")
        {
            VerifyTimeout = TimeSpan.FromSeconds(1)
        };
        var elapsed = Stopwatch.StartNew();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            client.VerifyAsync(Payload(), Requirements(maxTimeoutSeconds: 60)).WaitAsync(TimeSpan.FromSeconds(40)));

        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(20), elapsed.Elapsed.ToString());
    }

    [Fact]
    [Trait("Rule", "V11")]
    public async Task Given_TheCallerCancels_When_Settling_Then_TheCancellationPropagatesInsteadOfAFailureResponse()
    {
        using var server = SlowFacilitator(TimeSpan.FromSeconds(60));
        var client = new HttpFacilitatorClient(server.CreateClient(), "http://localhost/facilitator");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.SettleAsync(Payload(), Requirements(maxTimeoutSeconds: 60), cancellation.Token));
    }

    [Fact]
    [Trait("Rule", "V11")]
    public void Given_TheFacilitatorHttpClientRegisteredByDependencyInjection_When_ItIsCreated_Then_ItsOwnTimeoutIsInfiniteSoTheBoundGoverns()
    {
        var services = new ServiceCollection();
        services.AddX402Services("https://facilitator.example");
        using var provider = services.BuildServiceProvider();

        var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(HttpFacilitatorClient.HttpClientName);

        Assert.Equal(Timeout.InfiniteTimeSpan, http.Timeout);
        Assert.IsType<HttpFacilitatorClient>(provider.GetRequiredService<IFacilitatorClient>());
    }

    [Fact]
    [Trait("Rule", "V11")]
    public void Given_TheFacilitatorProxyRegistration_When_TheClientIsCreated_Then_ItsOwnTimeoutIsInfinite()
    {
        var services = new ServiceCollection();
        services.AddX402FacilitatorProxy("https://facilitator.example", new List<RoutePaymentConfig>());
        using var provider = services.BuildServiceProvider();

        var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(HttpFacilitatorClient.HttpClientName);

        Assert.Equal(Timeout.InfiniteTimeSpan, http.Timeout);
    }

    private static TestServer SlowFacilitator(TimeSpan delay) => new(new WebHostBuilder().Configure(app =>
        app.Run(async context =>
        {
            await Task.Delay(delay, context.RequestAborted);
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                success = true,
                transaction = "0x01",
                network = Network,
                payer = Payer,
                isValid = true
            }));
        })));

    private static PaymentRequirements Requirements(int maxTimeoutSeconds) => new()
    {
        Scheme = "exact",
        Network = Network,
        Amount = "100000",
        Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
        PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
        MaxTimeoutSeconds = maxTimeoutSeconds
    };

    private static PaymentPayload Payload() => new()
    {
        X402Version = 2,
        Accepted = Requirements(60),
        Payload = new ExactSchemePayload { Signature = "0x00", Authorization = new Nethereum.X402.Models.Authorization { From = Payer } }
    };
}
