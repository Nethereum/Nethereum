using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Server;

namespace Nethereum.X402.IntegrationTests.SecurityAudit71;

internal sealed class AuditFacilitator : IFacilitatorClient
{
    public Func<SettlementResponse> Settle { get; set; } = Succeeded;
    public int Settles;
    public int Verifies;

    public static SettlementResponse Succeeded() =>
        new() { Success = true, Transaction = "0xabc", Network = AuditServer.Network, Payer = AuditServer.Payer };

    public static SettlementResponse Failed(string reason, string transaction) =>
        new() { Success = false, ErrorReason = reason, Transaction = transaction, Network = AuditServer.Network, Payer = AuditServer.Payer };

    public Task<VerificationResponse> VerifyAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default)
    {
        Interlocked.Increment(ref Verifies);
        return Task.FromResult(new VerificationResponse { IsValid = true, Payer = AuditServer.Payer });
    }

    public Task<SettlementResponse> SettleAsync(PaymentPayload p, PaymentRequirements r, CancellationToken c = default)
    {
        Interlocked.Increment(ref Settles);
        return Task.FromResult(Settle());
    }

    public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken c = default) =>
        Task.FromResult(new SupportedPaymentKindsResponse());
}

internal static class AuditServer
{
    public const string Network = "eip155:84532";
    public const string Token = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";
    public const string Payee = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C";
    public const string Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66";

    public static PaymentRequirements Requirements(string? paymentFlow = null) => new()
    {
        Scheme = X402Schemes.Exact,
        Network = Network,
        Amount = "100000",
        PayTo = Payee,
        Asset = Token,
        MaxTimeoutSeconds = 300,
        Extra = new ExactSchemeExtra { Name = "USDC", Version = "2", PaymentFlow = paymentFlow }
    };

    public static string Nonce(int value) => "0x" + value.ToString("x").PadLeft(64, '0');

    public static JsonObject PayloadJson(int nonce, PaymentRequirements? accepted = null)
    {
        var payload = new PaymentPayload
        {
            X402Version = 2,
            Accepted = accepted ?? Requirements(),
            Payload = new ExactSchemePayload
            {
                Signature = "0x" + new string('1', 130),
                Authorization = new Nethereum.X402.Models.Authorization
                {
                    From = Payer,
                    To = Payee,
                    Value = "100000",
                    ValidAfter = "0",
                    ValidBefore = "4102444800",
                    Nonce = Nonce(nonce)
                }
            }
        };
        return JsonNode.Parse(JsonSerializer.Serialize(payload))!.AsObject();
    }

    public static string Encode(JsonObject payload) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(payload.ToJsonString()));

    public static HttpRequestMessage Request(string path, string header)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, path);
        message.Headers.TryAddWithoutValidation(X402Headers.PaymentSignature, header);
        return message;
    }

    public static TestServer Server(
        IFacilitatorClient facilitator, RoutePaymentConfig route, RequestDelegate endpoint, int maxEntries = 1000) =>
        new(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(facilitator);
                services.AddSingleton<IPaymentReplayStore>(new InMemoryPaymentReplayStore(maxEntries));
            })
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "http://facilitator.local";
                    options.Routes.Add(route);
                });
                app.Run(endpoint);
            }));

    public static Task Ok(HttpContext context) => context.Response.WriteAsync("ok");
}
