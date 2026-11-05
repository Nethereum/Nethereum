using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.JsonRpc.Client;
using Nethereum.Signer;
using Nethereum.X402.AspNetCore;
using Nethereum.X402.IntegrationTests.AspNetCore;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Facilitator;
using Nethereum.X402.IntegrationTests.Blockchain;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Nethereum.X402.Processors;
using Nethereum.X402.Server;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Permit2;

[Collection("X402 DevChain E2E")]
public class X402Eip2612GasSponsoringTests : IClassFixture<X402DevChainFixture>
{
    private const string UnapprovedPayerKey = "0x47e179ec197488593b187f80a00eb0da91f1b9d0b13f8733639f19c30a34926a";
    private const string Amount = "1000000";
    private const string Key = X402ExtensionKeys.Eip2612GasSponsoring;
    private const string OtherAddress = "0x000000000000000000000000000000000000dEaD";

    private const string ReferenceDeclaration = """
        {
          "eip2612GasSponsoring": {
            "info": {
              "description": "The facilitator accepts EIP-2612 gasless Permit to `Permit2` canonical contract.",
              "version": "1"
            },
            "schema": {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "properties": {
                "from": { "type": "string", "pattern": "^0x[a-fA-F0-9]{40}$", "description": "The address of the sender." },
                "asset": { "type": "string", "pattern": "^0x[a-fA-F0-9]{40}$", "description": "The address of the ERC-20 token contract." },
                "spender": { "type": "string", "pattern": "^0x[a-fA-F0-9]{40}$", "description": "The address of the spender (Canonical Permit2)." },
                "amount": { "type": "string", "pattern": "^[0-9]+$", "description": "The amount to approve (uint256). Typically MaxUint." },
                "nonce": { "type": "string", "pattern": "^[0-9]+$", "description": "The current nonce of the sender." },
                "deadline": { "type": "string", "pattern": "^[0-9]+$", "description": "The timestamp at which the signature expires." },
                "signature": { "type": "string", "pattern": "^0x[a-fA-F0-9]+$", "description": "The 65-byte concatenated signature (r, s, v) as a hex string." },
                "version": { "type": "string", "pattern": "^[0-9]+(\\.[0-9]+)*$", "description": "Schema version identifier." }
              },
              "required": ["from", "asset", "spender", "amount", "nonce", "deadline", "signature", "version"]
            }
          }
        }
        """;

    private readonly X402DevChainFixture _chain;
    private readonly X402ExactPermit2Service _service;
    private readonly string _payer = new EthECKey(UnapprovedPayerKey.Substring(2)).GetPublicAddress();

    public X402Eip2612GasSponsoringTests(X402DevChainFixture chain)
    {
        _chain = chain;
        _service = new X402ExactPermit2Service(
            X402DevChainFixture.FacilitatorKey,
            new Dictionary<int, IClient> { [X402DevChainFixture.ChainId] = chain.Client });
    }

    [Fact]
    public async Task Given_NoAllowanceAndNoExtension_When_Verifying_Then_Permit2AllowanceRequired()
    {
        var verify = await _service.VerifyPaymentAsync(Payload(extensions: null), Requirements());

        Assert.Equal(X402ErrorCodes.Permit2AllowanceRequired, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_NoAllowanceAndAnIncompleteExtension_When_Verifying_Then_Permit2AllowanceRequired()
    {
        var info = ValidInfo();
        info.Signature = "";

        var verify = await _service.VerifyPaymentAsync(Payload(Extension(info)), Requirements());

        Assert.Equal(X402ErrorCodes.Permit2AllowanceRequired, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnEip2612InfoWithBadFormat_When_Verifying_Then_InvalidEip2612ExtensionFormat()
    {
        var info = ValidInfo();
        info.Signature = "not-hex";

        var verify = await _service.VerifyPaymentAsync(Payload(Extension(info)), Requirements());

        Assert.Equal(X402ErrorCodes.InvalidEip2612ExtensionFormat, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnEip2612InfoWithANonNumericAmount_When_Verifying_Then_InvalidEip2612ExtensionFormat()
    {
        var info = ValidInfo();
        info.Amount = "1e6";

        var verify = await _service.VerifyPaymentAsync(Payload(Extension(info)), Requirements());

        Assert.Equal(X402ErrorCodes.InvalidEip2612ExtensionFormat, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnEip2612InfoWithFromMismatch_When_Verifying_Then_Eip2612FromMismatch()
    {
        var info = ValidInfo();
        info.From = OtherAddress;

        var verify = await _service.VerifyPaymentAsync(Payload(Extension(info)), Requirements());

        Assert.Equal(X402ErrorCodes.Eip2612FromMismatch, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnEip2612InfoWithAssetMismatch_When_Verifying_Then_Eip2612AssetMismatch()
    {
        var info = ValidInfo();
        info.Asset = OtherAddress;

        var verify = await _service.VerifyPaymentAsync(Payload(Extension(info)), Requirements());

        Assert.Equal(X402ErrorCodes.Eip2612AssetMismatch, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnEip2612InfoWithSpenderMismatch_When_Verifying_Then_Eip2612SpenderNotPermit2()
    {
        var info = ValidInfo();
        info.Spender = X402Permit2Addresses.ExactPermit2Proxy;

        var verify = await _service.VerifyPaymentAsync(Payload(Extension(info)), Requirements());

        Assert.Equal(X402ErrorCodes.Eip2612SpenderNotPermit2, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnEip2612InfoWithExpiredDeadline_When_Verifying_Then_Eip2612DeadlineExpired()
    {
        var info = ValidInfo();
        info.Deadline = (Now() - 10).ToString();

        var verify = await _service.VerifyPaymentAsync(Payload(Extension(info)), Requirements());

        Assert.Equal(X402ErrorCodes.Eip2612DeadlineExpired, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnEip2612InfoWithDeadlineInsideTheSixSecondBuffer_When_Verifying_Then_Eip2612DeadlineExpired()
    {
        var info = ValidInfo();
        info.Deadline = (Now() + 3).ToString();

        var verify = await _service.VerifyPaymentAsync(Payload(Extension(info)), Requirements());

        Assert.Equal(X402ErrorCodes.Eip2612DeadlineExpired, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnEip2612InfoOverTheWire_When_Verifying_Then_TheSameRejectionIsReported()
    {
        var info = ValidInfo();
        info.From = OtherAddress;
        var wire = JsonSerializer.Deserialize<PaymentPayload>(JsonSerializer.Serialize(Payload(Extension(info))))!;

        var verify = await _service.VerifyPaymentAsync(wire, Requirements());

        Assert.Equal(X402ErrorCodes.Eip2612FromMismatch, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_Eip2612Sponsoring_When_AskingWhatIsSupported_Then_TheFacilitatorAdvertisesTheExtension()
    {
        var supported = await _service.GetSupportedAsync();

        Assert.Contains(Key, supported.Extensions);
    }

    [Fact]
    public async Task Given_TheExactSchemeProcessor_When_AskingWhatIsSupported_Then_TheExtensionIsListedOnce()
    {
        var eip3009 = new X402TransferWithAuthorisation3009Service(
            X402DevChainFixture.FacilitatorKey,
            new Dictionary<int, IClient> { [X402DevChainFixture.ChainId] = _chain.Client });
        var processor = new X402ExactSchemeProcessor(eip3009, _service);

        var supported = await processor.GetSupportedAsync();

        Assert.Single(supported.Extensions, Key);
    }

    [Fact]
    public void Given_TheDeclarationHelper_When_Serialized_Then_ItEqualsTheReferenceDeclaration()
    {
        var declaration = new Dictionary<string, object> { [Key] = X402GasSponsoringExtensions.DeclareEip2612() };

        var actual = JsonSerializer.SerializeToNode(declaration);

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(ReferenceDeclaration), actual), actual!.ToJsonString());
    }

    [Fact]
    public async Task Given_ARouteWithTheDeclaration_When_NoPaymentIsSent_Then_ThePaymentRequiredServesTheExtension()
    {
        using var server = CreateTestServer(new RoutePaymentConfig("/api/premium", Requirements())
        {
            Extensions = new Dictionary<string, object> { [Key] = X402GasSponsoringExtensions.DeclareEip2612() }
        });

        var response = await server.CreateClient().GetAsync("/api/premium");

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(ReferenceDeclaration)![Key],
            body["extensions"]![Key]));
    }

    [Fact]
    public async Task Given_ARouteWithoutExtensions_When_NoPaymentIsSent_Then_ThePaymentRequiredHasNoExtensions()
    {
        using var server = CreateTestServer(new RoutePaymentConfig("/api/premium", Requirements()));

        var response = await server.CreateClient().GetAsync("/api/premium");

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.False(body.ContainsKey("extensions"));
    }

    private static TestServer CreateTestServer(RoutePaymentConfig route)
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services => services.AddSingleton<IFacilitatorClient>(new MockFacilitatorClient()))
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "https://facilitator.test";
                    options.Routes.Add(route);
                });
                app.Run(context => context.Response.WriteAsync("premium"));
            });
        return new TestServer(builder);
    }

    private PaymentRequirements Requirements() => new()
    {
        Scheme = X402Schemes.Exact,
        Network = Caip2.FormatEip155(X402DevChainFixture.ChainId),
        Amount = Amount,
        Asset = _chain.TokenAddress,
        PayTo = X402DevChainFixture.RecipientAddress,
        MaxTimeoutSeconds = 3600,
        Extra = new ExactSchemeExtra { AssetTransferMethod = X402AssetTransferMethods.Permit2, Name = "USDC", Version = "2" }
    };

    private Eip2612GasSponsoringInfo ValidInfo() => new()
    {
        From = _payer,
        Asset = _chain.TokenAddress,
        Spender = X402Permit2Addresses.Permit2,
        Amount = Amount,
        Nonce = "0",
        Deadline = (Now() + 3600).ToString(),
        Signature = "0x" + new string('1', 130),
        Version = "1"
    };

    private static Dictionary<string, object> Extension(Eip2612GasSponsoringInfo info) =>
        new() { [Key] = new Dictionary<string, object> { ["info"] = info } };

    private PaymentPayload Payload(Dictionary<string, object>? extensions)
    {
        var authorization = new Permit2Authorization
        {
            From = _payer,
            Permitted = new Permit2TokenPermissions { Token = _chain.TokenAddress, Amount = Amount },
            Spender = X402Permit2Addresses.ExactPermit2Proxy,
            Nonce = new BigInteger(Guid.NewGuid().ToByteArray(), isUnsigned: true).ToString(),
            Deadline = (Now() + 3600).ToString(),
            Witness = new Permit2WitnessData
            {
                To = X402DevChainFixture.RecipientAddress,
                ValidAfter = (Now() - 600).ToString()
            }
        };
        var signature = new Permit2WitnessSigner().Sign(
            authorization.ToWitnessMessage(),
            X402DevChainFixture.ChainId,
            X402Permit2Addresses.Permit2,
            new EthECKey(UnapprovedPayerKey.Substring(2)));

        return new PaymentPayload
        {
            X402Version = X402Protocol.Version,
            Accepted = Requirements(),
            Payload = new Permit2SchemePayload { Signature = signature, Permit2Authorization = authorization },
            Extensions = extensions
        };
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
