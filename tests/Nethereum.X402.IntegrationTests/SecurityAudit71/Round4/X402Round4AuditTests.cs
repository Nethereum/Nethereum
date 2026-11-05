using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.X402.Extensions;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Processors;
using Nethereum.X402.Server;

namespace Nethereum.X402.IntegrationTests.SecurityAudit71.Round4;

public class X402Round4AuditTests
{
    private sealed class StatusFacilitator : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly int _status;
        private readonly string _body;
        private readonly CancellationTokenSource _stop = new();
        public int Settles;

        public StatusFacilitator(int status, string body)
        {
            _status = status;
            _body = body;
            _listener.Start();
            _ = Task.Run(AcceptLoop);
        }

        public string Url => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/facilitator";

        private async Task AcceptLoop()
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    _ = Task.Run(() => Serve(client));
                }
            }
            catch (Exception) { }
        }

        private async Task Serve(TcpClient client)
        {
            try
            {
                var stream = client.GetStream();
                var head = new StringBuilder();
                var one = new byte[1];
                while (!head.ToString().EndsWith("\r\n\r\n"))
                {
                    if (await stream.ReadAsync(one) == 0) return;
                    head.Append((char)one[0]);
                }

                var text = head.ToString();
                var length = 0;
                foreach (var line in text.Split("\r\n"))
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        length = int.Parse(line.Substring(15).Trim());
                var body = new byte[length];
                var got = 0;
                while (got < length)
                {
                    var n = await stream.ReadAsync(body.AsMemory(got));
                    if (n == 0) return;
                    got += n;
                }

                if (text.Contains("/verify"))
                {
                    await Respond(stream, 200, "{\"isValid\":true,\"payer\":\"" + AuditServer.Payer + "\"}");
                    return;
                }

                Interlocked.Increment(ref Settles);
                await Respond(stream, _status, _body);
            }
            catch (Exception) { }
            finally { client.Dispose(); }
        }

        private static async Task Respond(NetworkStream stream, int status, string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            var head = $"HTTP/1.1 {status} X\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
            await stream.WriteAsync(bytes);
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
        }
    }

    private const string FailureBody = "{\"success\":false,\"errorReason\":\"unexpected_settle_error\"}";

    [Theory(Timeout = 60000)]
    [Trait("Rule", "R4-1")]
    [InlineData(302, X402PaymentFlows.Upfront)]
    [InlineData(302, null)]
    [InlineData(408, X402PaymentFlows.Upfront)]
    [InlineData(408, null)]
    [InlineData(499, X402PaymentFlows.Upfront)]
    [InlineData(499, null)]
    public async Task Given_AFacilitatorAnsweringASettleWithAStatusThatDoesNotProveNothingWasBroadcast_When_TheSamePaymentIsReplayed_Then_ItIsNotSettledTwice(int status, string? flow)
    {
        using var facilitator = new StatusFacilitator(status, FailureBody);
        var requirements = AuditServer.Requirements(flow);
        var client = new HttpFacilitatorClient(new HttpClient(), facilitator.Url);
        using var server = AuditServer.Server(client, new RoutePaymentConfig("/api/*", requirements), AuditServer.Ok);
        var caller = server.CreateClient();
        var header = AuditServer.Encode(AuditServer.PayloadJson(5, requirements));

        await caller.SendAsync(AuditServer.Request("/api/real", header));
        await caller.SendAsync(AuditServer.Request("/api/real", header));

        Assert.Equal(1, facilitator.Settles);
    }

    private sealed class PayerlessFacilitator : IFacilitatorClient
    {
        public int Settles;

        public Task<VerificationResponse> VerifyAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default) =>
            Task.FromResult(new VerificationResponse { IsValid = true });

        public Task<SettlementResponse> SettleAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default)
        {
            Interlocked.Increment(ref Settles);
            return Task.FromResult(AuditFacilitator.Succeeded());
        }

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken c = default) =>
            Task.FromResult(new SupportedPaymentKindsResponse());
    }

    [Theory(Timeout = 60000)]
    [Trait("Rule", "R4-2")]
    [InlineData(X402PaymentFlows.Upfront)]
    [InlineData(null)]
    public async Task Given_AFacilitatorThatVerifiesAPaymentWithoutNamingThePayer_When_ItIsPresented_Then_TheAnswerIsNotAnEmptySuccess(string? flow)
    {
        var facilitator = new PayerlessFacilitator();
        var requirements = AuditServer.Requirements(flow);
        using var server = AuditServer.Server(facilitator, new RoutePaymentConfig("/api/*", requirements), AuditServer.Ok);
        var header = AuditServer.Encode(AuditServer.PayloadJson(5, requirements));

        var response = await server.CreateClient().SendAsync(AuditServer.Request("/api/real", header));
        var body = await response.Content.ReadAsStringAsync();

        Assert.False(response.StatusCode == HttpStatusCode.OK && body.Length == 0, "200 with an empty body, no content, no settlement");
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "R4-3")]
    public async Task Given_APaymentHeaderWhoseAuthorizationIsRepeatedUnderADifferentCase_When_Presented_Then_ItIsRejectedBeforeTheFacilitator()
    {
        var facilitator = new AuditFacilitator();
        var requirements = AuditServer.Requirements();
        using var server = AuditServer.Server(facilitator, new RoutePaymentConfig("/api/*", requirements), AuditServer.Ok);
        var json = AuditServer.PayloadJson(7, requirements);
        var shadow = JsonNode.Parse(json["payload"]!["authorization"]!.ToJsonString())!.AsObject();
        shadow["nonce"] = AuditServer.Nonce(8);
        json["payload"]!.AsObject()["Authorization"] = shadow;

        var response = await server.CreateClient().SendAsync(AuditServer.Request("/api/real", AuditServer.Encode(json)));

        Assert.Equal(0, facilitator.Verifies);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class RecordingProcessor : IX402PaymentProcessor
    {
        public int Calls;

        public Task<VerificationResponse> VerifyPaymentAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(new VerificationResponse { IsValid = true, Payer = AuditServer.Payer });
        }

        public Task<SettlementResponse> SettlePaymentAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default) =>
            Task.FromResult(AuditFacilitator.Succeeded());

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken c = default) =>
            Task.FromResult(new SupportedPaymentKindsResponse());
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "R4-3")]
    public async Task Given_AFacilitatorBodyWithPaymentRequirementsRepeatedUnderADifferentCase_When_Posted_Then_ItIsRejected()
    {
        var processor = new RecordingProcessor();
        using var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IX402PaymentProcessor>(processor);
                services.AddControllers().AddX402FacilitatorControllers();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            }));
        var requirements = System.Text.Json.JsonSerializer.Serialize(AuditServer.Requirements());
        var cheap = System.Text.Json.JsonSerializer.Serialize(new { scheme = "exact", network = AuditServer.Network, amount = "1", asset = AuditServer.Token, payTo = AuditServer.Payee, maxTimeoutSeconds = 300 });
        var body = "{\"x402Version\":2,\"paymentPayload\":" + AuditServer.PayloadJson(7).ToJsonString()
            + ",\"paymentRequirements\":" + requirements + ",\"PaymentRequirements\":" + cheap + "}";

        var response = await server.CreateClient().PostAsync(
            "/facilitator/verify", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(0, processor.Calls);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
