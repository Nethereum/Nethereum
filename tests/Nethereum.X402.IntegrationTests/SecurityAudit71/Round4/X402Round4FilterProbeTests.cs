using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.X402.Extensions;
using Nethereum.X402.Models;
using Nethereum.X402.Processors;

namespace Nethereum.X402.IntegrationTests.SecurityAudit71.Round4;

public class X402Round4FilterProbeTests
{
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

    private static TestServer Server(RecordingProcessor processor) => new(new WebHostBuilder()
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

    private static string Body(bool duplicated, int padding = 0)
    {
        var payload = AuditServer.PayloadJson(7).ToJsonString();
        var real = "\"nonce\":\"" + AuditServer.Nonce(7) + "\"";
        if (duplicated)
            payload = payload.Replace(real, "\"nonce\":\"" + AuditServer.Nonce(8) + "\"," + real);
        var requirements = System.Text.Json.JsonSerializer.Serialize(AuditServer.Requirements());
        var pad = padding > 0 ? ",\"pad\":\"" + new string('a', padding) + "\"" : string.Empty;
        return "{\"x402Version\":2,\"paymentPayload\":" + payload + ",\"paymentRequirements\":" + requirements + pad + "}";
    }

    [Theory(Timeout = 60000)]
    [InlineData("utf-16", "application/json; charset=utf-16;")]
    [InlineData("utf-16", "application/json;charset=utf-16")]
    [InlineData("utf-16", "application/json ; charset = \"utf-16\"")]
    [InlineData("utf-16", "application/json; charset=utf-16; charset=utf-8")]
    [InlineData("utf-16", "application/json; charset=utf-8; charset=utf-16")]
    [InlineData("utf-16", "application/json; CHARSET=UTF-16")]
    [InlineData("utf-16", "application/problem+json; charset=utf-16")]
    [InlineData("utf-16", "application/json; boundary=x; charset=utf-16")]
    [InlineData("utf-16", "application/json; charset=utf-16, text/plain")]
    [InlineData("utf-16be", "application/json; charset=utf-16be")]
    [InlineData("utf-32", "application/json; charset=utf-32")]
    [InlineData("utf-8", "application/json; charset=utf8")]
    [InlineData("utf-8", "application/json; charset=\"utf-8\"")]
    [InlineData("utf-8", "application/json; charset=utf-8; foo=bar")]
    [InlineData("utf-8", "application/json")]
    [InlineData("utf-8-bom", "application/json")]
    [InlineData("utf-8-bom", "application/json; charset=utf-8")]
    public async Task Given_ADuplicateNonceBodyInAnyEncodingAndContentType_When_Posted_Then_TheProcessorIsNeverReached(string encoding, string contentType)
    {
        var processor = new RecordingProcessor();
        using var server = Server(processor);
        var text = Body(true);
        var bytes = encoding switch
        {
            "utf-16" => new UnicodeEncoding(false, true).GetBytes(text),
            "utf-16be" => new UnicodeEncoding(true, true).GetBytes(text),
            "utf-32" => new UTF32Encoding(false, true).GetBytes(text),
            "utf-8-bom" => new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray(),
            _ => Encoding.UTF8.GetBytes(text)
        };
        var content = new ByteArrayContent(bytes);
        content.Headers.TryAddWithoutValidation("Content-Type", contentType);

        var response = await server.CreateClient().PostAsync("/facilitator/verify", content);

        Assert.Equal(0, processor.Calls);
        Assert.True((int)response.StatusCode < 500, response.StatusCode.ToString());
    }

    [Theory(Timeout = 60000)]
    [InlineData("application/x-www-form-urlencoded")]
    [InlineData("multipart/form-data; boundary=x")]
    [InlineData("text/plain")]
    [InlineData("application/xml")]
    public async Task Given_ANonJsonContentType_When_Posted_Then_TheProcessorIsNeverReached(string contentType)
    {
        var processor = new RecordingProcessor();
        using var server = Server(processor);
        var content = new StringContent(Body(true), Encoding.UTF8);
        content.Headers.TryAddWithoutValidation("Content-Type", contentType);

        var response = await server.CreateClient().PostAsync("/facilitator/verify", content);

        Assert.Equal(0, processor.Calls);
        Assert.True((int)response.StatusCode < 500, response.StatusCode.ToString());
    }

    private sealed class ChunkedContent : HttpContent
    {
        private readonly byte[] _bytes;

        public ChunkedContent(byte[] bytes) => _bytes = bytes;

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            for (var offset = 0; offset < _bytes.Length; offset += 7)
            {
                await stream.WriteAsync(_bytes.AsMemory(offset, Math.Min(7, _bytes.Length - offset)));
                await stream.FlushAsync();
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    [Theory(Timeout = 60000)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_AChunkedBody_When_Posted_Then_DuplicatesAreRejectedAndCleanBodiesAreServed(bool duplicated)
    {
        var processor = new RecordingProcessor();
        using var server = Server(processor);
        var content = new ChunkedContent(Encoding.UTF8.GetBytes(Body(duplicated)));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var response = await server.CreateClient().PostAsync("/facilitator/verify", content);

        if (duplicated)
        {
            Assert.Equal(0, processor.Calls);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        else
        {
            Assert.Equal(1, processor.Calls);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Theory(Timeout = 120000)]
    [InlineData("application/json; charset=utf-8")]
    [InlineData("application/json; charset=\"UTF-8\"")]
    [InlineData("application/json")]
    public async Task Given_ACleanBodyWithALargePaddingField_When_Posted_Then_ItIsServed(string contentType)
    {
        var processor = new RecordingProcessor();
        using var server = Server(processor);
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(Body(false, 4 * 1024 * 1024)));
        content.Headers.TryAddWithoutValidation("Content-Type", contentType);

        var response = await server.CreateClient().PostAsync("/facilitator/verify", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, processor.Calls);
    }
}
