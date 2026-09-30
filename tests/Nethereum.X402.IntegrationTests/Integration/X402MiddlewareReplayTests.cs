using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Server;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Integration;

public class X402MiddlewareReplayTests
{
    private const string Network = "eip155:84532";
    private const string Payee = "0x1111111111111111111111111111111111111111";
    private const string Token = "0x2222222222222222222222222222222222222222";

    private sealed class AlwaysValidFacilitator : IFacilitatorClient
    {
        public Task<VerificationResponse> VerifyAsync(PaymentPayload p, PaymentRequirements r, CancellationToken ct = default)
            => Task.FromResult(new VerificationResponse { IsValid = true, Payer = "0xpayer" });

        public Task<SettlementResponse> SettleAsync(PaymentPayload p, PaymentRequirements r, CancellationToken ct = default)
            => Task.FromResult(new SettlementResponse { Success = true, Transaction = "0xdeadbeef", Network = r.Network, Payer = "0xpayer" });

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken ct = default)
            => Task.FromResult(new SupportedPaymentKindsResponse { Kinds = new List<PaymentKind>() });
    }

    [Fact]
    public async Task ReplayedPayment_IsRejected_WithoutRunningTheEndpointAgain()
    {
        var callCount = new int[1];
        using var server = await CreateServerAsync(callCount, replayProtection: true);
        var client = server.CreateClient();

        var header = BuildHeader("0x" + new string('a', 64));

        var first = await Send(client, header);
        var second = await Send(client, header);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, second.StatusCode);
        Assert.Contains(X402ErrorCodes.NonceAlreadyUsed, await second.Content.ReadAsStringAsync());
        Assert.Equal(1, callCount[0]);
    }

    [Fact]
    public async Task DistinctPayments_BothRunTheEndpoint()
    {
        var callCount = new int[1];
        using var server = await CreateServerAsync(callCount, replayProtection: true);
        var client = server.CreateClient();

        var first = await Send(client, BuildHeader("0x" + new string('a', 64)));
        var second = await Send(client, BuildHeader("0x" + new string('b', 64)));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(2, callCount[0]);
    }

    [Fact]
    public async Task ReplayProtectionDisabled_RunsTheEndpointOnEachReplay()
    {
        var callCount = new int[1];
        using var server = await CreateServerAsync(callCount, replayProtection: false);
        var client = server.CreateClient();

        var header = BuildHeader("0x" + new string('a', 64));
        var first = await Send(client, header);
        var second = await Send(client, header);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(2, callCount[0]);
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, string paymentHeader)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/premium");
        request.Headers.Add("PAYMENT-SIGNATURE", paymentHeader);
        return client.SendAsync(request);
    }

    private static string BuildHeader(string nonce)
    {
        var payload = new PaymentPayload
        {
            X402Version = 2,
            Accepted = new PaymentRequirements { Scheme = "exact", Network = Network },
            Payload = new ExactSchemePayload
            {
                Signature = "0x" + new string('0', 130),
                Authorization = new Nethereum.X402.Models.Authorization
                {
                    From = "0x3333333333333333333333333333333333333333",
                    To = Payee,
                    Value = "100000",
                    ValidAfter = "0",
                    ValidBefore = "99999999999",
                    Nonce = nonce
                }
            }
        };
        var json = JsonSerializer.Serialize(payload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private static async Task<TestServer> CreateServerAsync(int[] callCount, bool replayProtection)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<IFacilitatorClient>(new AlwaysValidFacilitator());
                });
                webHost.Configure(app =>
                {
                    app.UseX402(options =>
                    {
                        options.FacilitatorUrl = "http://facilitator.local";
                        options.EnablePaymentReplayProtection = replayProtection;
                        options.Routes.Add(new RoutePaymentConfig
                        {
                            PathPattern = "/premium",
                            Requirements = new PaymentRequirements
                            {
                                Scheme = "exact",
                                Network = Network,
                                Amount = "100000",
                                PayTo = Payee,
                                Asset = Token,
                                MaxTimeoutSeconds = 300,
                                Extra = new ExactSchemeExtra { Name = "USDC", Version = "2" }
                            }
                        });
                    });

                    app.Run(async context =>
                    {
                        Interlocked.Increment(ref callCount[0]);
                        context.Response.StatusCode = 200;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync("{\"message\":\"premium\"}");
                    });
                });
            })
            .StartAsync();

        return host.GetTestServer();
    }
}
