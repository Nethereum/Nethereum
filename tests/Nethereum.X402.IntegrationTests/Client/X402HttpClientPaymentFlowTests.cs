using System.Net;
using System.Text.Json;
using Nethereum.X402.Client;
using Nethereum.X402.Models;
using Nethereum.X402.Signers;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Client;

public class X402HttpClientPaymentFlowTests
{
    private const string PrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    private const string Network = "eip155:31337";
    private const string Token = "0x5FbDB2315678afecb367f032d93F642f64180aa3";
    private const string PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C";

    [Fact]
    public async Task Given_402WithExtensionsAndResource_When_Paying_Then_PaymentPayloadEchoesThem()
    {
        PaymentPayload? paid = null;
        var handler = Handler(new PaymentRequired
        {
            Resource = new ResourceInfo { Url = "https://api.example.com/premium", Description = "d", ServiceName = "svc" },
            Accepts = new List<PaymentRequirements> { Eip3009Requirements() },
            Extensions = new Dictionary<string, object> { ["bazaar"] = new { info = "kept" } }
        }, request => paid = X402HttpClient.DecodePaymentHeader(request.Headers.GetValues("PAYMENT-SIGNATURE").First()));

        var response = await AutomaticClient(handler).GetAsync("http://localhost/premium");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paid);
        Assert.Equal("https://api.example.com/premium", paid!.Resource!.Url);
        Assert.Equal("svc", paid.Resource.ServiceName);
        var extensions = (JsonElement)paid.Extensions!;
        Assert.Equal("kept", extensions.GetProperty("bazaar").GetProperty("info").GetString());
    }

    [Theory]
    [InlineData("erc7710", null)]
    [InlineData(null, "escrow")]
    public async Task Given_UnsupportedRequirementOnly_When_PayingAutomatically_Then_NothingIsSigned(string? assetTransferMethod, string? paymentFlow)
    {
        var signed = false;
        var handler = Handler(
            new PaymentRequired { Accepts = new List<PaymentRequirements> { Eip3009Requirements(assetTransferMethod, paymentFlow) } },
            _ => signed = true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => AutomaticClient(handler).GetAsync("http://localhost/premium"));

        Assert.False(signed);
    }

    [Theory]
    [InlineData("erc7710", null)]
    [InlineData(null, "escrow")]
    public async Task Given_SelectorThatReturnsUnsupportedRequirement_When_PayingAutomatically_Then_ClientStillRefusesToSign(string? assetTransferMethod, string? paymentFlow)
    {
        var signed = false;
        var handler = Handler(
            new PaymentRequired { Accepts = new List<PaymentRequirements> { Eip3009Requirements(assetTransferMethod, paymentFlow) } },
            _ => signed = true);
        var options = Options();
        options.Selector = new TestPaymentRequirementsSelector(all => all[0]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new X402HttpClient(new HttpClient(handler), PrivateKey, options).GetAsync("http://localhost/premium"));

        Assert.False(signed);
    }

    [Theory]
    [InlineData("erc7710", null)]
    [InlineData(null, "escrow")]
    public async Task Given_UnsupportedRequirement_When_PayingManually_Then_ClientRefusesToSign(string? assetTransferMethod, string? paymentFlow)
    {
        var requested = false;
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            requested = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var client = new X402HttpClient(new HttpClient(handler), PrivateKey);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetAsync("http://localhost/premium", Eip3009Requirements(assetTransferMethod, paymentFlow)));

        Assert.False(requested);
    }

    [Fact]
    public void Given_UnsupportedAndSupportedEntries_When_Selecting_Then_TheSupportedEntryIsChosen()
    {
        var requirements = new List<PaymentRequirements>
        {
            Eip3009Requirements("erc7710", null),
            Eip3009Requirements(null, "escrow"),
            Eip3009Requirements("permit2", null)
        };

        var selected = new DefaultPaymentRequirementsSelector().SelectRequirements(requirements, Network, "exact");

        Assert.Same(requirements[2], selected);
    }

    [Fact]
    public void Given_BothFlowsOffered_When_ClientSelects_Then_AuthorizationIsChosen()
    {
        var requirements = new List<PaymentRequirements>
        {
            Eip3009Requirements(null, X402PaymentFlows.Upfront),
            Eip3009Requirements(null, null)
        };

        var selected = new DefaultPaymentRequirementsSelector().SelectRequirements(requirements, Network, "exact");

        Assert.Same(requirements[1], selected);
    }

    [Fact]
    public void Given_ExplicitAuthorizationAndUpfrontOffered_When_ClientSelects_Then_AuthorizationIsChosen()
    {
        var requirements = new List<PaymentRequirements>
        {
            Eip3009Requirements(null, X402PaymentFlows.Upfront),
            Eip3009Requirements(null, X402PaymentFlows.Authorization)
        };

        var selected = new DefaultPaymentRequirementsSelector().SelectRequirements(requirements, Network, "exact");

        Assert.Same(requirements[1], selected);
    }

    [Fact]
    public async Task Given_OnlyUpfrontOffered_When_ClientPays_Then_ItPays()
    {
        PaymentPayload? paid = null;
        var handler = Handler(
            new PaymentRequired { Accepts = new List<PaymentRequirements> { Eip3009Requirements(null, X402PaymentFlows.Upfront) } },
            request => paid = X402HttpClient.DecodePaymentHeader(request.Headers.GetValues("PAYMENT-SIGNATURE").First()));

        var response = await AutomaticClient(handler).GetAsync("http://localhost/premium");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paid);
        Assert.True(X402RequirementSupport.IsUpfrontFlow(paid!.Accepted));
        var payload = JsonSerializer.Deserialize<ExactSchemePayload>(((JsonElement)paid.Payload).GetRawText())!;
        Assert.Equal(PayTo, payload.Authorization.To);
    }

    [Fact]
    public void Given_AnUnknownFlow_When_ClientSelects_Then_ItIsSkipped()
    {
        var requirements = new List<PaymentRequirements>
        {
            Eip3009Requirements(null, "escrow"),
            Eip3009Requirements(null, X402PaymentFlows.Upfront)
        };

        var selected = new DefaultPaymentRequirementsSelector().SelectRequirements(requirements, Network, "exact");

        Assert.Same(requirements[1], selected);
    }

    [Fact]
    public void Given_OnlyUnsupportedEntries_When_Selecting_Then_TheMessageListsWhatWasOffered()
    {
        var requirements = new List<PaymentRequirements> { Eip3009Requirements("erc7710", null) };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DefaultPaymentRequirementsSelector().SelectRequirements(requirements, Network, "exact"));

        Assert.Contains("erc7710", exception.Message);
    }

    [Fact]
    public async Task Given_RequirementWithMaxTimeoutSeconds_When_Paying_Then_ValidBeforeFollowsIt()
    {
        PaymentPayload? paid = null;
        var requirements = Eip3009Requirements();
        requirements.MaxTimeoutSeconds = 90;
        var handler = Handler(
            new PaymentRequired { Accepts = new List<PaymentRequirements> { requirements } },
            request => paid = X402HttpClient.DecodePaymentHeader(request.Headers.GetValues("PAYMENT-SIGNATURE").First()));
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await AutomaticClient(handler).GetAsync("http://localhost/premium");

        var authorization = JsonSerializer.Deserialize<ExactSchemePayload>(((JsonElement)paid!.Payload).GetRawText())!.Authorization;
        var validBefore = long.Parse(authorization.ValidBefore);
        Assert.InRange(validBefore, before + 90, DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 90);
    }

    [Fact]
    public void Given_MaxTimeoutSeconds_When_BuildingAuthorization_Then_ValidBeforeIsNowPlusIt()
    {
        var requirements = Eip3009Requirements();
        requirements.MaxTimeoutSeconds = 45;
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var authorization = new TransferWithAuthorisationBuilder().BuildFromPaymentRequirements(requirements, PayTo);

        Assert.InRange(long.Parse(authorization.ValidBefore), before + 45, DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 45);
    }

    [Fact]
    public void Given_NoMaxTimeoutSeconds_When_BuildingAuthorization_Then_ValidBeforeKeepsTheOneHourDefault()
    {
        var requirements = Eip3009Requirements();
        requirements.MaxTimeoutSeconds = 0;
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var authorization = new TransferWithAuthorisationBuilder().BuildFromPaymentRequirements(requirements, PayTo);

        Assert.InRange(long.Parse(authorization.ValidBefore), before + 3600, DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600);
    }

    [Fact]
    public void Given_ExplicitValidBefore_When_BuildingAuthorization_Then_MaxTimeoutSecondsDoesNotOverrideIt()
    {
        var requirements = Eip3009Requirements();

        var authorization = new TransferWithAuthorisationBuilder().BuildFromPaymentRequirements(
            requirements, PayTo, validBeforeTimestamp: 1234567890);

        Assert.Equal("1234567890", authorization.ValidBefore);
    }

    private static MockHttpMessageHandler Handler(PaymentRequired paymentRequired, Action<HttpRequestMessage> onPaid)
    {
        var requestCount = 0;
        return new MockHttpMessageHandler((request, ct) =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PaymentRequired)
                {
                    Content = new StringContent(JsonSerializer.Serialize(paymentRequired))
                });
            }

            onPaid(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        });
    }

    private static X402HttpClient AutomaticClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler), PrivateKey, Options());

    private static X402HttpClientOptions Options() => new()
    {
        PreferredNetwork = Network,
        PreferredScheme = "exact",
        MaxAmount = "1000000"
    };

    private static PaymentRequirements Eip3009Requirements(string? assetTransferMethod = null, string? paymentFlow = null) => new()
    {
        Scheme = "exact",
        Network = Network,
        Amount = "100000",
        PayTo = PayTo,
        MaxTimeoutSeconds = 300,
        Asset = Token,
        Extra = new ExactSchemeExtra
        {
            AssetTransferMethod = assetTransferMethod,
            PaymentFlow = paymentFlow,
            Name = "USD Coin",
            Version = "2"
        }
    };
}
