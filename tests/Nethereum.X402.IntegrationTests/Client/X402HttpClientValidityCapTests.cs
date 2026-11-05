using System.Net;
using System.Text.Json;
using Nethereum.X402.Client;
using Nethereum.X402.Models;

namespace Nethereum.X402.IntegrationTests.Client;

public class X402HttpClientValidityCapTests
{
    private const string PrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    private const string Network = "eip155:31337";
    private const string Token = "0x5FbDB2315678afecb367f032d93F642f64180aa3";
    private const string PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C";
    private const int ToleranceSeconds = 30;

    [Theory]
    [Trait("Rule", "V15")]
    [InlineData(86_400, 3_600)]
    [InlineData(120, 120)]
    [InlineData(3_600, 3_600)]
    public async Task Given_ARequirementTimeout_When_AnEip3009PaymentIsSigned_Then_ValidBeforeIsCappedAtTheDefaultOneHour(int requirementTimeout, int expectedValidity)
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var paid = await PaidAsync(Requirements(requirementTimeout, "eip3009"), new X402HttpClientOptions { PreferredNetwork = Network, MaxAmount = "1000000" });

        var validBefore = long.Parse(Eip3009(paid).Authorization.ValidBefore);
        AssertNear(before + expectedValidity, validBefore);
    }

    [Fact]
    [Trait("Rule", "V15")]
    public async Task Given_AConfiguredValidityCap_When_AnEip3009PaymentIsSigned_Then_ValidBeforeUsesTheSmallerOfTheCapAndTheRequirementTimeout()
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var options = new X402HttpClientOptions { PreferredNetwork = Network, MaxAmount = "1000000", MaxAuthorizationValiditySeconds = 300 };

        var capped = await PaidAsync(Requirements(86_400, "eip3009"), options);
        var shorter = await PaidAsync(Requirements(60, "eip3009"), options);

        AssertNear(before + 300, long.Parse(Eip3009(capped).Authorization.ValidBefore));
        AssertNear(before + 60, long.Parse(Eip3009(shorter).Authorization.ValidBefore));
    }

    [Theory]
    [Trait("Rule", "V15")]
    [InlineData(86_400, 3_600)]
    [InlineData(90, 90)]
    public async Task Given_ARequirementTimeout_When_APermit2PaymentIsSigned_Then_TheDeadlineIsCappedAtTheDefaultOneHour(int requirementTimeout, int expectedValidity)
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var paid = await PaidAsync(Requirements(requirementTimeout, "permit2"), new X402HttpClientOptions { PreferredNetwork = Network, MaxAmount = "1000000" });

        AssertNear(before + expectedValidity, long.Parse(Permit2(paid).Permit2Authorization.Deadline));
    }

    [Fact]
    [Trait("Rule", "V15")]
    public async Task Given_AConfiguredValidityCap_When_APermit2PaymentIsSigned_Then_TheDeadlineUsesIt()
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var options = new X402HttpClientOptions { PreferredNetwork = Network, MaxAmount = "1000000", MaxAuthorizationValiditySeconds = 200 };

        var paid = await PaidAsync(Requirements(86_400, "permit2"), options);

        AssertNear(before + 200, long.Parse(Permit2(paid).Permit2Authorization.Deadline));
    }

    [Fact]
    [Trait("Rule", "V15")]
    public async Task Given_TheManualPaymentFlow_When_ALongRequirementTimeoutIsSigned_Then_ValidBeforeIsStillCappedAtOneHour()
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        PaymentPayload? sent = null;
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            sent = X402HttpClient.DecodePaymentHeader(request.Headers.GetValues("PAYMENT-SIGNATURE").First());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        await new X402HttpClient(new HttpClient(handler), PrivateKey)
            .GetAsync("http://localhost/premium", Requirements(86_400, "eip3009"));

        AssertNear(before + 3_600, long.Parse(Eip3009(sent!).Authorization.ValidBefore));
    }

    [Theory]
    [Trait("Rule", "V15")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Given_ANonPositiveValidityCap_When_TheOptionsAreValidated_Then_TheyAreRejected(int cap)
    {
        var options = new X402HttpClientOptions { PreferredNetwork = Network, MaxAmount = "1", MaxAuthorizationValiditySeconds = cap };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    [Trait("Rule", "V15")]
    public void Given_NewClientOptions_Then_TheValidityCapDefaultsToOneHour()
    {
        Assert.Equal(3_600, new X402HttpClientOptions().MaxAuthorizationValiditySeconds);
    }

    private static async Task<PaymentPayload> PaidAsync(PaymentRequirements requirements, X402HttpClientOptions options)
    {
        PaymentPayload? sent = null;
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            if (!request.Headers.Contains("PAYMENT-SIGNATURE"))
            {
                var required = new PaymentRequired { Accepts = new List<PaymentRequirements> { requirements } };
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PaymentRequired)
                {
                    Content = new StringContent(JsonSerializer.Serialize(required))
                });
            }

            sent = X402HttpClient.DecodePaymentHeader(request.Headers.GetValues("PAYMENT-SIGNATURE").First());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        await new X402HttpClient(new HttpClient(handler), PrivateKey, options).GetAsync("http://localhost/premium");
        return sent!;
    }

    private static ExactSchemePayload Eip3009(PaymentPayload payment) =>
        JsonSerializer.Deserialize<ExactSchemePayload>(((JsonElement)payment.Payload).GetRawText())!;

    private static Permit2SchemePayload Permit2(PaymentPayload payment) =>
        JsonSerializer.Deserialize<Permit2SchemePayload>(((JsonElement)payment.Payload).GetRawText())!;

    private static PaymentRequirements Requirements(int maxTimeoutSeconds, string method) => new()
    {
        Scheme = "exact",
        Network = Network,
        Amount = "100000",
        Asset = Token,
        PayTo = PayTo,
        MaxTimeoutSeconds = maxTimeoutSeconds,
        Extra = new ExactSchemeExtra { AssetTransferMethod = method, Name = "USD Coin", Version = "2" }
    };

    private static void AssertNear(long expected, long actual) =>
        Assert.InRange(actual, expected - ToleranceSeconds, expected + ToleranceSeconds);
}
