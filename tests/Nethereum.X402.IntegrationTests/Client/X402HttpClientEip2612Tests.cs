using System.Net;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.ABI.EIP712.EIP2612;
using Nethereum.JsonRpc.Client;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.X402.Client;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Nethereum.X402.Server;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Client;

public class X402HttpClientEip2612Tests
{
    private const string PrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    private const int ChainId = 84532;
    private const string Network = "eip155:84532";
    private const string Token = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";
    private const string PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C";
    private const string Amount = "100000";
    private const string Key = X402ExtensionKeys.Eip2612GasSponsoring;

    private static readonly BigInteger TokenNonce = 7;

    [Fact]
    public async Task Given_EveryConditionHolds_When_ClientPays_Then_Eip2612InfoIsAttachedWithValueEqualToTheAmount()
    {
        var rpc = new ScriptedTokenRpcClient { Allowance = 0, Nonce = TokenNonce };

        var paid = await PayAsync(Declaration(), Permit2Requirements(), RpcFor(rpc));

        var info = Info(paid);
        var payer = new EthECKey(PrivateKey).GetPublicAddress();
        var permit2 = Permit2Of(paid);
        Assert.Equal(payer, info["from"]!.GetValue<string>());
        Assert.Equal(Token, info["asset"]!.GetValue<string>());
        Assert.Equal(X402Permit2Addresses.Permit2, info["spender"]!.GetValue<string>());
        Assert.Equal(Amount, info["amount"]!.GetValue<string>());
        Assert.Equal(TokenNonce.ToString(), info["nonce"]!.GetValue<string>());
        Assert.Equal(permit2.Deadline, info["deadline"]!.GetValue<string>());
        Assert.Equal("1", info["version"]!.GetValue<string>());

        var recovered = new Eip2612PermitSigner().RecoverSigner(
            new Permit
            {
                Owner = payer,
                Spender = X402Permit2Addresses.Permit2,
                Value = BigInteger.Parse(Amount),
                Nonce = TokenNonce,
                Deadline = BigInteger.Parse(permit2.Deadline)
            },
            "USDC", "2", ChainId, Token, info["signature"]!.GetValue<string>());
        Assert.True(recovered.IsTheSameAddress(payer));
    }

    [Fact]
    public async Task Given_ServerDeclaredTheExtension_When_ClientPays_Then_PayloadExtensionIsTheDeclarationMergedWithTheClientInfo()
    {
        var rpc = new ScriptedTokenRpcClient { Allowance = 0, Nonce = TokenNonce };

        var paid = await PayAsync(Declaration(), Permit2Requirements(), RpcFor(rpc));

        var extension = ExtensionOf(paid);
        var info = extension["info"]!.AsObject();
        Assert.Equal(
            "The facilitator accepts EIP-2612 gasless Permit to `Permit2` canonical contract.",
            info["description"]!.GetValue<string>());
        Assert.NotNull(info["signature"]);
        Assert.NotNull(info["from"]);
        Assert.Equal("object", extension["schema"]!["type"]!.GetValue<string>());
        Assert.Equal(8, extension["schema"]!["required"]!.AsArray().Count);
    }

    [Fact]
    public async Task Given_ServerDidNotDeclareTheExtension_When_ClientPays_Then_NoEip2612InfoIsAttached()
    {
        var rpc = new ScriptedTokenRpcClient { Allowance = 0, Nonce = TokenNonce };

        var paid = await PayAsync(null, Permit2Requirements(), RpcFor(rpc));

        Assert.Null(paid.Extensions);
        Assert.Empty(rpc.Calls);
    }

    [Fact]
    public async Task Given_NoRpcConfigured_When_ClientPays_Then_NoEip2612InfoIsAttached()
    {
        var paid = await PayAsync(Declaration(), Permit2Requirements(), null);

        Assert.Null(Info(paid)["signature"]);
    }

    [Fact]
    public async Task Given_RpcConfiguredForAnotherChain_When_ClientPays_Then_NoEip2612InfoIsAttached()
    {
        var rpc = new ScriptedTokenRpcClient { Allowance = 0, Nonce = TokenNonce };

        var paid = await PayAsync(Declaration(), Permit2Requirements(), new Dictionary<int, IClient> { [1] = rpc });

        Assert.Null(Info(paid)["signature"]);
        Assert.Empty(rpc.Calls);
    }

    [Fact]
    public async Task Given_TokenNameMissingFromExtra_When_ClientPays_Then_NoEip2612InfoIsAttached()
    {
        var rpc = new ScriptedTokenRpcClient { Allowance = 0, Nonce = TokenNonce };
        var requirements = Permit2Requirements();
        requirements.Extra = new Dictionary<string, string> { ["assetTransferMethod"] = "permit2", ["version"] = "2" };

        var paid = await PayAsync(Declaration(), requirements, RpcFor(rpc));

        Assert.Null(Info(paid)["signature"]);
        Assert.Empty(rpc.Calls);
    }

    [Fact]
    public async Task Given_TokenVersionMissingFromExtra_When_ClientPays_Then_NoEip2612InfoIsAttached()
    {
        var rpc = new ScriptedTokenRpcClient { Allowance = 0, Nonce = TokenNonce };
        var requirements = Permit2Requirements();
        requirements.Extra = new Dictionary<string, string> { ["assetTransferMethod"] = "permit2", ["name"] = "USDC" };

        var paid = await PayAsync(Declaration(), requirements, RpcFor(rpc));

        Assert.Null(Info(paid)["signature"]);
        Assert.Empty(rpc.Calls);
    }

    [Fact]
    public async Task Given_Permit2AllowanceCoversTheAmount_When_ClientPays_Then_NoEip2612InfoIsAttached()
    {
        var rpc = new ScriptedTokenRpcClient { Allowance = BigInteger.Parse(Amount), Nonce = TokenNonce };

        var paid = await PayAsync(Declaration(), Permit2Requirements(), RpcFor(rpc));

        Assert.Null(Info(paid)["signature"]);
    }

    [Fact]
    public async Task Given_TheAllowanceReadFails_When_ClientPays_Then_TheClientStillSignsThePermit()
    {
        var rpc = new ScriptedTokenRpcClient { AllowanceReadFails = true, Nonce = TokenNonce };

        var paid = await PayAsync(Declaration(), Permit2Requirements(), RpcFor(rpc));

        Assert.NotNull(Info(paid)["signature"]);
    }

    [Fact]
    public async Task Given_RequirementIsEip3009_When_ClientPays_Then_NoEip2612InfoIsAttached()
    {
        var rpc = new ScriptedTokenRpcClient { Allowance = 0, Nonce = TokenNonce };
        var requirements = Permit2Requirements();
        requirements.Extra = new ExactSchemeExtra { AssetTransferMethod = "eip3009", Name = "USDC", Version = "2" };

        var paid = await PayAsync(Declaration(), requirements, RpcFor(rpc));

        Assert.Null(Info(paid)["signature"]);
        Assert.Empty(rpc.Calls);
    }

    private static Dictionary<int, IClient> RpcFor(IClient client) => new() { [ChainId] = client };

    private static Dictionary<string, object> Declaration() =>
        new() { [Key] = X402GasSponsoringExtensions.DeclareEip2612() };

    private static PaymentRequirements Permit2Requirements() => new()
    {
        Scheme = "exact",
        Network = Network,
        Amount = Amount,
        PayTo = PayTo,
        MaxTimeoutSeconds = 300,
        Asset = Token,
        Extra = new ExactSchemeExtra { AssetTransferMethod = "permit2", Name = "USDC", Version = "2" }
    };

    private static JsonObject ExtensionOf(PaymentPayload paid) =>
        JsonSerializer.SerializeToNode(paid.Extensions)![Key]!.AsObject();

    private static JsonObject Info(PaymentPayload paid) => ExtensionOf(paid)["info"]!.AsObject();

    private static Permit2Authorization Permit2Of(PaymentPayload paid) =>
        JsonSerializer.Deserialize<Permit2SchemePayload>(((JsonElement)paid.Payload).GetRawText())!.Permit2Authorization;

    private static async Task<PaymentPayload> PayAsync(
        object? declaredExtensions,
        PaymentRequirements requirements,
        Dictionary<int, IClient>? rpcClients)
    {
        PaymentPayload? paid = null;
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                var required = new PaymentRequired
                {
                    Accepts = new List<PaymentRequirements> { requirements },
                    Extensions = declaredExtensions
                };
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PaymentRequired)
                {
                    Content = new StringContent(JsonSerializer.Serialize(required))
                });
            }

            paid = X402HttpClient.DecodePaymentHeader(request.Headers.GetValues("PAYMENT-SIGNATURE").First());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        });

        var client = new X402HttpClient(new HttpClient(handler), PrivateKey, new X402HttpClientOptions
        {
            PreferredNetwork = Network,
            PreferredScheme = "exact",
            MaxAmount = "1000000",
            RpcClientsByChainId = rpcClients
        });

        var response = await client.GetAsync("http://localhost/premium");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return paid!;
    }
}
