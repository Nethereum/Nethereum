using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.X402.Extensions;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Processors;
using Nethereum.X402.Server;

namespace Nethereum.X402.IntegrationTests.SecurityAudit71.Round3;

public class X402Round3AuditTests
{
    private sealed class RawFacilitator : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly string _settleMode;
        private readonly CancellationTokenSource _stop = new();
        public int Settles;

        public RawFacilitator(string settleMode)
        {
            _settleMode = settleMode;
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
                switch (_settleMode)
                {
                    case "rst":
                        client.LingerState = new LingerOption(true, 0);
                        client.Close();
                        break;
                    case "fin":
                        client.Close();
                        break;
                    case "partial":
                        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 500\r\n\r\n{\"succ"));
                        await stream.FlushAsync();
                        client.LingerState = new LingerOption(true, 0);
                        client.Close();
                        break;
                    case "502json":
                        await Respond(stream, 502, "{\"success\":false,\"errorReason\":\"unexpected_settle_error\"}");
                        break;
                }
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

    private static async Task<int> SettlesAfterReplay(string mode, string? flow)
    {
        using var facilitator = new RawFacilitator(mode);
        var requirements = AuditServer.Requirements(flow);
        var client = new HttpFacilitatorClient(new HttpClient(), facilitator.Url);
        using var server = AuditServer.Server(client, new RoutePaymentConfig("/api/*", requirements), AuditServer.Ok);
        var caller = server.CreateClient();
        var header = AuditServer.Encode(AuditServer.PayloadJson(5, requirements));

        await caller.SendAsync(AuditServer.Request("/api/real", header));
        await caller.SendAsync(AuditServer.Request("/api/real", header));
        return facilitator.Settles;
    }

    [Theory(Timeout = 60000)]
    [Trait("Rule", "R3-1")]
    [InlineData("rst", X402PaymentFlows.Upfront)]
    [InlineData("fin", X402PaymentFlows.Upfront)]
    [InlineData("partial", X402PaymentFlows.Upfront)]
    [InlineData("502json", X402PaymentFlows.Upfront)]
    [InlineData("rst", null)]
    [InlineData("502json", null)]
    public async Task Given_AFacilitatorThatReceivedTheSettleRequestAndThenFailedTheConnection_When_TheSamePaymentIsReplayed_Then_ItIsNotSettledTwice(string mode, string? flow)
    {
        Assert.Equal(1, await SettlesAfterReplay(mode, flow));
    }

    private sealed class RecordingProcessor : IX402PaymentProcessor
    {
        public int Calls;

        public Task<VerificationResponse> VerifyPaymentAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(new VerificationResponse { IsValid = true, Payer = AuditServer.Payer });
        }

        public Task<SettlementResponse> SettlePaymentAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(AuditFacilitator.Succeeded());
        }

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken c = default) =>
            Task.FromResult(new SupportedPaymentKindsResponse());
    }

    private static TestServer FacilitatorServer(RecordingProcessor processor) => new(new WebHostBuilder()
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

    private static string VerifyBody(string nonceKeys)
    {
        var payload = AuditServer.PayloadJson(7).ToJsonString();
        var real = "\"nonce\":\"" + AuditServer.Nonce(7) + "\"";
        payload = payload.Replace(real, nonceKeys);
        var requirements = System.Text.Json.JsonSerializer.Serialize(AuditServer.Requirements());
        return "{\"x402Version\":2,\"paymentPayload\":" + payload + ",\"paymentRequirements\":" + requirements + "}";
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "R3-2")]
    public async Task Given_AUtf16EncodedFacilitatorBodyWithDuplicateNonceKeys_When_Posted_Then_ItIsRejected()
    {
        var processor = new RecordingProcessor();
        using var server = FacilitatorServer(processor);
        var client = server.CreateClient();
        var duplicated = "\"nonce\":\"" + AuditServer.Nonce(8) + "\",\"nonce\":\"" + AuditServer.Nonce(7) + "\"";
        var content = new ByteArrayContent(new UnicodeEncoding(false, false).GetBytes(VerifyBody(duplicated)));
        content.Headers.TryAddWithoutValidation("Content-Type", "application/json; charset=utf-16");

        var response = await client.PostAsync("/facilitator/verify", content);

        Assert.Equal(0, processor.Calls);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "R3-3")]
    public async Task Given_AFacilitatorBodyWithALoneSurrogatePropertyName_When_Posted_Then_TheAnswerIsNotAServerError()
    {
        var processor = new RecordingProcessor();
        using var server = FacilitatorServer(processor);
        var client = server.CreateClient();
        var body = VerifyBody("\"nonce\":\"" + AuditServer.Nonce(7) + "\",\"\\ud800\":1");
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/facilitator/verify", content);

        Assert.True((int)response.StatusCode < 500, response.StatusCode.ToString());
    }

    [Fact(Timeout = 60000)]
    [Trait("Rule", "R3-4")]
    public async Task Given_APaymentHeaderWithALoneSurrogatePropertyName_When_Presented_Then_TheAnswerIsNotAServerError()
    {
        var facilitator = new AuditFacilitator();
        using var server = AuditServer.Server(
            facilitator, new RoutePaymentConfig("/api/*", AuditServer.Requirements()), AuditServer.Ok);
        var json = AuditServer.PayloadJson(7).ToJsonString();
        var real = "\"nonce\":\"" + AuditServer.Nonce(7) + "\"";
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes(json.Replace(real, real + ",\"\\ud800\":1")));

        var response = await server.CreateClient().SendAsync(AuditServer.Request("/api/real", header));

        Assert.True((int)response.StatusCode < 500, response.StatusCode.ToString());
    }
}
