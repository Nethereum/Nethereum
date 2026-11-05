using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nethereum.JsonRpc.Client;
using Nethereum.Signer;
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Facilitator;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Server;
using Nethereum.X402.Signers;

namespace Nethereum.X402.IntegrationTests.Blockchain;

[Collection("X402 DevChain E2E")]
public class X402NonceAndNetworkCanonicalFormDevChainTests : IClassFixture<X402DevChainFixture>
{
    private const string ShortNonce = "0xab";
    private static readonly string FullNonce = "0xab" + new string('0', 62);

    private readonly X402DevChainFixture _chain;

    public X402NonceAndNetworkCanonicalFormDevChainTests(X402DevChainFixture chain)
    {
        _chain = chain;
    }

    [Theory]
    [Trait("Rule", "W3")]
    [InlineData("0xab")]
    [InlineData("0x")]
    [InlineData("abababababababababababababababababababababababababababababababab")]
    [InlineData("0xababababababababababababababababababababababababababababababab")]
    [InlineData("0xababababababababababababababababababababababababababababababababab")]
    [InlineData("0xzzabababababababababababababababababababababababababababababababab")]
    public async Task Given_ANonceThatIsNotExactly32Bytes_When_TheAuthorizationIsVerified_Then_ItIsInvalidPayloadBeforeAnyRpc(string nonce)
    {
        var rpc = new InterceptingRpcClient(_chain.Client);
        var service = new X402TransferWithAuthorisation3009Service(X402DevChainFixture.FacilitatorKey, Clients(rpc));
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await SignedWithNonceAsync(requirements, FullNonce, nonce);

        var verification = await service.VerifyPaymentAsync(payload, requirements);

        Assert.False(verification.IsValid);
        Assert.Equal(X402ErrorCodes.InvalidPayload, verification.InvalidReason);
        Assert.Empty(rpc.Methods);
    }

    [Fact]
    [Trait("Rule", "W3")]
    public async Task Given_AFull32ByteNonce_When_TheAuthorizationIsVerified_Then_ItIsValid()
    {
        var service = new X402TransferWithAuthorisation3009Service(X402DevChainFixture.FacilitatorKey, Clients(_chain.Client));
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var nonce = "0x" + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var payload = await SignedWithNonceAsync(requirements, nonce, nonce);

        var verification = await service.VerifyPaymentAsync(payload, requirements);

        Assert.True(verification.IsValid, verification.InvalidReason);
    }

    [Theory]
    [Trait("Rule", "W3")]
    [InlineData("0xab")]
    [InlineData("0x")]
    [InlineData("0xababababababababababababababababababababababababababababababababab")]
    public async Task Given_ANonceThatIsNotExactly32Bytes_When_TheReplayKeyIsDerived_Then_ThereIsNoKeyAndNothingIsPadded(string nonce)
    {
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await SignedWithNonceAsync(requirements, FullNonce, nonce);

        Assert.Null(PaymentReplayIdentity.Extract(payload, requirements));
    }

    [Fact]
    [Trait("Rule", "W3")]
    public async Task Given_OneSignaturePresentedWithTheShortAndTheFullNonceToTwoResources_When_BothRequestsRace_Then_OnlyOneIsServedAndTheShortOneIsBadRequest()
    {
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await SignedWithNonceAsync(requirements, FullNonce, FullNonce);
        var signature = ((ExactSchemePayload)payload.Payload).Signature;

        var fullVerified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var heldNonceReads = 0;
        var shortClient = new InterceptingRpcClient(_chain.Client, async method =>
        {
            if (method == "eth_getTransactionCount" && Interlocked.Increment(ref heldNonceReads) == 1)
            {
                await Task.WhenAny(fullVerified.Task, Task.Delay(8000));
                await Task.Delay(300);
            }
        });
        var estimates = 0;
        var fullClient = new InterceptingRpcClient(_chain.Client, method =>
        {
            if (method == "eth_estimateGas" && Interlocked.Increment(ref estimates) == 1)
                fullVerified.TrySetResult();
            return Task.CompletedTask;
        });
        var facilitator = new NonceRoutingFacilitator(
            new X402TransferWithAuthorisation3009Service(X402DevChainFixture.FacilitatorKey, Clients(shortClient)),
            new X402TransferWithAuthorisation3009Service(X402DevChainFixture.FacilitatorKey, Clients(fullClient)));
        var handlerCalls = 0;
        var host = await new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddSingleton<IFacilitatorClient>(facilitator);
            });
            web.Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "http://facilitator.local";
                    options.Routes.Add(new RoutePaymentConfig("/premium", requirements));
                });
                app.Run(async context =>
                {
                    Interlocked.Increment(ref handlerCalls);
                    await context.Response.WriteAsync("SECRET");
                });
            });
        }).StartAsync();
        try
        {
            var client = host.GetTestServer().CreateClient();
            var shortRequest = client.SendAsync(Request(Header(payload, requirements, ShortNonce, signature)));
            await Task.Delay(50);
            var fullRequest = client.SendAsync(Request(Header(payload, requirements, FullNonce, signature)));

            var responses = await Task.WhenAll(shortRequest, fullRequest);

            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, handlerCalls);
            Assert.Equal(HttpStatusCode.BadRequest, responses[0].StatusCode);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Theory]
    [Trait("Rule", "W5")]
    [InlineData("eip155:0031337")]
    [InlineData("eip155:031337")]
    [InlineData("eip155:0")]
    [InlineData("eip155:00")]
    [InlineData("eip155:+1")]
    [InlineData("eip155: 1")]
    [InlineData("eip155:1 ")]
    [InlineData("eip155:1_0")]
    [InlineData("eip155:1.0")]
    [InlineData("eip155:0x1")]
    [InlineData("eip155:99999999999999999999999999999999999999999999999999999999999999999999999999999999")]
    public void Given_ANonCanonicalEip155Reference_When_ItIsParsed_Then_ItIsRejected(string network)
    {
        Assert.False(Caip2.TryParseEip155ChainId(network, out var chainId));
        Assert.Equal(0, chainId);
    }

    [Fact]
    [Trait("Rule", "W5")]
    public async Task Given_AnAssetAllowlistForTheCanonicalNetwork_When_ALeadingZeroSpellingOfTheNetworkIsVerified_Then_ItIsRejectedAsInvalidNetworkBeforeAnyRpc()
    {
        var options = new X402FacilitatorOptions();
        options.AllowedAssets["eip155:31337"] = new List<string> { "0x1111111111111111111111111111111111111111" };
        var rpc = new InterceptingRpcClient(_chain.Client);
        var service = new X402TransferWithAuthorisation3009Service(X402DevChainFixture.FacilitatorKey, Clients(rpc), options);
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        requirements.Network = "eip155:0031337";
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);

        var verification = await service.VerifyPaymentAsync(payload, requirements);

        Assert.False(verification.IsValid);
        Assert.Equal(X402ErrorCodes.InvalidNetwork, verification.InvalidReason);
        Assert.Empty(rpc.Methods);
    }

    [Fact]
    [Trait("Rule", "W5")]
    public async Task Given_AnAssetAllowlistForTheCanonicalNetwork_When_TheCanonicalNetworkCarriesAnotherAsset_Then_TheAssetIsStillRefused()
    {
        var options = new X402FacilitatorOptions();
        options.AllowedAssets["eip155:31337"] = new List<string> { "0x1111111111111111111111111111111111111111" };
        var service = new X402TransferWithAuthorisation3009Service(X402DevChainFixture.FacilitatorKey, Clients(_chain.Client), options);
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);

        var verification = await service.VerifyPaymentAsync(payload, requirements);

        Assert.False(verification.IsValid);
        Assert.Equal(X402ErrorCodes.AssetNotAllowed, verification.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "W5")]
    public void Given_AnAllowlistKeyedByAnUpperCaseNamespace_When_TheCanonicalNetworkIsChecked_Then_TheAllowlistStillApplies()
    {
        var options = new X402FacilitatorOptions();
        options.AllowedAssets["EIP155:31337"] = new List<string> { "0x1111111111111111111111111111111111111111" };

        Assert.False(options.IsAssetAllowed("eip155:31337", "0x2222222222222222222222222222222222222222"));
        Assert.True(options.IsAssetAllowed("eip155:31337", "0x1111111111111111111111111111111111111111"));
        Assert.True(options.IsAssetAllowed("eip155:1", "0x2222222222222222222222222222222222222222"));
    }

    private async Task<PaymentPayload> SignedWithNonceAsync(PaymentRequirements requirements, string signedNonce, string presentedNonce)
    {
        var authorization = new TransferWithAuthorisationBuilder().BuildFromPaymentRequirements(
            requirements, X402DevChainFixture.PayerAddress);
        authorization.Nonce = signedNonce;
        var signature = await new TransferWithAuthorisationSigner().SignWithPrivateKeyAsync(
            authorization, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion,
            X402DevChainFixture.ChainId, _chain.TokenAddress, X402DevChainFixture.PayerKey);
        authorization.Nonce = presentedNonce;
        return new PaymentPayload
        {
            X402Version = X402Protocol.Version,
            Accepted = requirements,
            Payload = new ExactSchemePayload
            {
                Authorization = authorization,
                Signature = EthECDSASignature.CreateStringSignature(signature)
            }
        };
    }

    private static string Header(PaymentPayload template, PaymentRequirements requirements, string nonce, string signature)
    {
        var source = ((ExactSchemePayload)template.Payload).Authorization;
        var payload = new PaymentPayload
        {
            X402Version = X402Protocol.Version,
            Accepted = requirements,
            Payload = new ExactSchemePayload
            {
                Signature = signature,
                Authorization = new Nethereum.X402.Models.Authorization
                {
                    From = source.From,
                    To = source.To,
                    Value = source.Value,
                    ValidAfter = source.ValidAfter,
                    ValidBefore = source.ValidBefore,
                    Nonce = nonce
                }
            }
        };
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
    }

    private static HttpRequestMessage Request(string header)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/premium");
        request.Headers.Add(X402Headers.PaymentSignature, header);
        return request;
    }

    private static Dictionary<int, IClient> Clients(IClient client) => new() { [X402DevChainFixture.ChainId] = client };

    private sealed class NonceRoutingFacilitator : IFacilitatorClient
    {
        private readonly X402TransferWithAuthorisation3009Service _shortNonceService;
        private readonly X402TransferWithAuthorisation3009Service _fullNonceService;

        public NonceRoutingFacilitator(
            X402TransferWithAuthorisation3009Service shortNonceService,
            X402TransferWithAuthorisation3009Service fullNonceService)
        {
            _shortNonceService = shortNonceService;
            _fullNonceService = fullNonceService;
        }

        public Task<VerificationResponse> VerifyAsync(PaymentPayload payload, PaymentRequirements requirements, CancellationToken cancellationToken = default) =>
            Pick(payload).VerifyPaymentAsync(payload, requirements, cancellationToken);

        public async Task<SettlementResponse> SettleAsync(PaymentPayload payload, PaymentRequirements requirements, CancellationToken cancellationToken = default)
        {
            if (!IsShort(payload))
                await Task.Delay(150, cancellationToken);

            return await Pick(payload).SettlePaymentAsync(payload, requirements, cancellationToken);
        }

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken cancellationToken = default) =>
            _shortNonceService.GetSupportedAsync(cancellationToken);

        private static bool IsShort(PaymentPayload payload) =>
            !System.Text.RegularExpressions.Regex.IsMatch(payload.Payload.ToString()!, "\"nonce\":\"0x[0-9a-f]{64}\"");

        private X402TransferWithAuthorisation3009Service Pick(PaymentPayload payload) =>
            IsShort(payload) ? _shortNonceService : _fullNonceService;
    }
}
