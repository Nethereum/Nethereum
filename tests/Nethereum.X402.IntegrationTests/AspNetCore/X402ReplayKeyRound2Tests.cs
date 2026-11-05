using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Server;

namespace Nethereum.X402.IntegrationTests.AspNetCore;

public class X402ReplayKeyRound2Tests
{
    private const string Network = "eip155:84532";
    private const string Token = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";
    private const string Payee = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C";
    private const string Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66";
    private const string Decoy = "0x1111111111111111111111111111111111111111";
    private const string Nonce = "0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480";

    [Fact]
    [Trait("Rule", "V3")]
    public void Given_ADecoyAuthorizationPropertyBeforeTheRealOne_When_TheKeyIsDerived_Then_ItIsTheKeyOfTheRealAuthorization()
    {
        var real = Wire(Eip3009Json(Payer, Nonce));
        var withDecoy = Wire($"{{\"signature\":\"0x00\",\"Authorization\":{Authorization(Decoy, "0x" + new string('2', 64))},\"authorization\":{Authorization(Payer, Nonce)}}}");

        var decoyKey = PaymentReplayIdentity.Extract(Wire(Eip3009Json(Decoy, "0x" + new string('2', 64))), Requirements());

        Assert.NotNull(PaymentReplayIdentity.Extract(real, Requirements()));
        Assert.Equal(PaymentReplayIdentity.Extract(real, Requirements()), PaymentReplayIdentity.Extract(withDecoy, Requirements()));
        Assert.NotEqual(decoyKey, PaymentReplayIdentity.Extract(withDecoy, Requirements()));
    }

    [Fact]
    [Trait("Rule", "V3")]
    public void Given_ADuplicatedAuthorizationProperty_When_TheKeyIsDerived_Then_TheLastOneWinsLikeTheFacilitatorDeserialiser()
    {
        var first = Authorization(Decoy, "0x" + new string('2', 64));
        var last = Authorization(Payer, Nonce);
        var duplicated = Wire($"{{\"signature\":\"0x00\",\"authorization\":{first},\"authorization\":{last}}}");

        var typed = JsonSerializer.Deserialize<ExactSchemePayload>(duplicated.Payload.ToString()!)!;

        Assert.Equal(Payer, typed.Authorization.From);
        Assert.Equal(
            PaymentReplayIdentity.Extract(Wire(Eip3009Json(Payer, Nonce)), Requirements()),
            PaymentReplayIdentity.Extract(duplicated, Requirements()));
    }

    [Fact]
    [Trait("Rule", "V3")]
    public void Given_ANonceWithAndWithoutThe0xPrefix_When_TheKeyIsDerived_Then_BothSpellingsAreTheSameKey()
    {
        var prefixed = PaymentReplayIdentity.Extract(Wire(Eip3009Json(Payer, Nonce)), Requirements());
        var bare = PaymentReplayIdentity.Extract(Wire(Eip3009Json(Payer, Nonce.Substring(2))), Requirements());
        var upper = PaymentReplayIdentity.Extract(Wire(Eip3009Json(Payer, "0X" + Nonce.Substring(2).ToUpperInvariant())), Requirements());

        Assert.NotNull(prefixed);
        Assert.Equal(prefixed, bare);
        Assert.Equal(prefixed, upper);
    }

    [Fact]
    [Trait("Rule", "V3")]
    public void Given_APayerWithAndWithoutThe0xPrefix_When_TheKeyIsDerived_Then_BothSpellingsAreTheSameKey()
    {
        var prefixed = PaymentReplayIdentity.Extract(Wire(Eip3009Json(Payer, Nonce)), Requirements());
        var bare = PaymentReplayIdentity.Extract(Wire(Eip3009Json(Payer.Substring(2), Nonce)), Requirements());
        var lower = PaymentReplayIdentity.Extract(Wire(Eip3009Json(Payer.ToLowerInvariant(), Nonce)), Requirements());

        Assert.NotNull(prefixed);
        Assert.Equal(prefixed, bare);
        Assert.Equal(prefixed, lower);
    }

    [Theory]
    [Trait("Rule", "V3")]
    [InlineData("+5")]
    [InlineData(" 5")]
    [InlineData("5 ")]
    [InlineData("5\n")]
    [InlineData("-5")]
    [InlineData("0x5")]
    [InlineData("5.0")]
    [InlineData("")]
    public void Given_APermit2NonceThatIsNotAPlainDecimal_When_TheKeyIsDerived_Then_ThereIsNoKey(string nonce)
    {
        Assert.Null(PaymentReplayIdentity.Extract(Wire(Permit2Json(Payer, nonce)), Permit2Requirements()));
    }

    [Fact]
    [Trait("Rule", "V3")]
    public void Given_APermit2NonceWithLeadingZeros_When_TheKeyIsDerived_Then_ItIsTheCanonicalDecimalKey()
    {
        Assert.Equal(
            PaymentReplayIdentity.Extract(Wire(Permit2Json(Payer, "5")), Permit2Requirements()),
            PaymentReplayIdentity.Extract(Wire(Permit2Json(Payer, "0005")), Permit2Requirements()));
    }

    [Fact]
    [Trait("Rule", "V3")]
    public void Given_APermit2NonceAboveUint256_When_TheKeyIsDerived_Then_ThereIsNoKey()
    {
        var tooBig = (System.Numerics.BigInteger.Pow(2, 256)).ToString();

        Assert.Null(PaymentReplayIdentity.Extract(Wire(Permit2Json(Payer, tooBig)), Permit2Requirements()));
        Assert.NotNull(PaymentReplayIdentity.Extract(Wire(Permit2Json(Payer, (System.Numerics.BigInteger.Pow(2, 256) - 1).ToString())), Permit2Requirements()));
    }

    [Theory]
    [Trait("Rule", "V3")]
    [InlineData("0xzz")]
    [InlineData("0x")]
    [InlineData("")]
    public void Given_AnEip3009NonceThatIsNotHex_When_TheKeyIsDerived_Then_ThereIsNoKey(string nonce)
    {
        Assert.Null(PaymentReplayIdentity.Extract(Wire(Eip3009Json(Payer, nonce)), Requirements()));
    }

    [Fact]
    [Trait("Rule", "V3")]
    public void Given_AnEip3009NonceLongerThan32Bytes_When_TheKeyIsDerived_Then_ThereIsNoKey()
    {
        Assert.Null(PaymentReplayIdentity.Extract(Wire(Eip3009Json(Payer, "0x" + new string('1', 66))), Requirements()));
    }

    [Fact]
    [Trait("Rule", "V3")]
    public void Given_ThePaymentNetworkAndAsset_When_TheKeyIsDerived_Then_TheSamePayerAndNonceOnAnotherAssetOrNetworkIsAnotherKey()
    {
        var payment = Wire(Eip3009Json(Payer, Nonce));
        var baseKey = PaymentReplayIdentity.Extract(payment, Requirements());

        var otherAsset = Requirements();
        otherAsset.Asset = "0x2222222222222222222222222222222222222222";
        var otherNetwork = Requirements();
        otherNetwork.Network = "eip155:8453";

        Assert.NotEqual(baseKey, PaymentReplayIdentity.Extract(payment, otherAsset));
        Assert.NotEqual(baseKey, PaymentReplayIdentity.Extract(payment, otherNetwork));
    }

    [Fact]
    [Trait("Rule", "V3")]
    public void Given_TheSameNonceUnderEip3009AndPermit2_When_TheKeyIsDerived_Then_TheSchemesDoNotShareAReservation()
    {
        var eip3009 = PaymentReplayIdentity.Extract(Wire(Eip3009Json(Payer, "0x" + new string('0', 63) + "5")), Requirements());
        var permit2 = PaymentReplayIdentity.Extract(Wire(Permit2Json(Payer, "5")), Permit2Requirements());

        Assert.NotNull(eip3009);
        Assert.NotNull(permit2);
        Assert.NotEqual(eip3009, permit2);
    }

    [Fact]
    [Trait("Rule", "V3")]
    public async Task Given_APaymentWhoseIdentityCannotBeDerived_When_TheMiddlewareProcessesIt_Then_ItIsRejectedWith400AndTheEndpointDoesNotRun()
    {
        var runs = new int[1];
        using var server = CreateServer(runs);

        var response = await SendAsync(server, Header(Permit2Json(Payer, "+5")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(X402ErrorCodes.InvalidPayload, await response.Content.ReadAsStringAsync());
        Assert.Equal(0, runs[0]);
    }

    [Fact]
    [Trait("Rule", "V3")]
    public async Task Given_ASettledPaymentReplayedWithABare0xLessSpelling_When_TheMiddlewareProcessesIt_Then_ItIsRejectedAsReplayed()
    {
        var runs = new int[1];
        using var server = CreateServer(runs);

        var first = await SendAsync(server, Header(Eip3009Json(Payer, Nonce)));
        var replay = await SendAsync(server, Header(Eip3009Json(Payer.Substring(2), Nonce.Substring(2))));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, replay.StatusCode);
        Assert.Contains(X402ErrorCodes.NonceAlreadyUsed, await replay.Content.ReadAsStringAsync());
        Assert.Equal(1, runs[0]);
    }

    private static PaymentRequirements Permit2Requirements()
    {
        var requirements = Requirements();
        requirements.Extra = new ExactSchemeExtra { AssetTransferMethod = X402AssetTransferMethods.Permit2, Name = "USDC", Version = "2" };
        return requirements;
    }

    private static PaymentRequirements Requirements() => new()
    {
        Scheme = "exact",
        Network = Network,
        Amount = "100000",
        Asset = Token,
        PayTo = Payee,
        MaxTimeoutSeconds = 300,
        Extra = new ExactSchemeExtra { Name = "USDC", Version = "2" }
    };

    private static string Authorization(string from, string nonce) =>
        $"{{\"from\":\"{from}\",\"to\":\"{Payee}\",\"value\":\"100000\",\"validAfter\":\"0\",\"validBefore\":\"99999999999\",\"nonce\":{Quote(nonce)}}}";

    private static string Quote(string value) => JsonSerializer.Serialize(value);

    private static string Eip3009Json(string from, string nonce) =>
        $"{{\"signature\":\"0x00\",\"authorization\":{Authorization(from, nonce)}}}";

    private static string Permit2Json(string from, string nonce) =>
        "{\"signature\":\"0x00\",\"permit2Authorization\":{" +
        $"\"from\":\"{from}\",\"permitted\":{{\"token\":\"{Token}\",\"amount\":\"100000\"}},\"spender\":\"{Payee}\"," +
        $"\"nonce\":{Quote(nonce)},\"deadline\":\"99999999999\",\"witness\":{{\"to\":\"{Payee}\",\"validAfter\":\"0\"}}}}}}";

    private static PaymentPayload Wire(string payloadJson) => new()
    {
        X402Version = 2,
        Accepted = Requirements(),
        Payload = JsonDocument.Parse(payloadJson).RootElement.Clone()
    };

    private static string Header(string payloadJson) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Wire(payloadJson))));

    private static Task<HttpResponseMessage> SendAsync(TestServer server, string header)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/premium");
        request.Headers.Add(X402Headers.PaymentSignature, header);
        return server.CreateClient().SendAsync(request);
    }

    private static TestServer CreateServer(int[] runs) => new(new WebHostBuilder()
        .ConfigureServices(services => services.AddSingleton<IFacilitatorClient>(new MockFacilitatorClient
        {
            VerifyResponse = new VerificationResponse { IsValid = true, Payer = Payer },
            SettleResponse = new SettlementResponse { Success = true, Transaction = "0x01", Network = Network, Payer = Payer }
        }))
        .Configure(app =>
        {
            app.UseX402(options =>
            {
                options.FacilitatorUrl = "https://facilitator.test";
                options.Routes.Add(new RoutePaymentConfig("/premium", Requirements()));
            });
            app.Run(context =>
            {
                Interlocked.Increment(ref runs[0]);
                return context.Response.WriteAsync("premium");
            });
        }));
}
