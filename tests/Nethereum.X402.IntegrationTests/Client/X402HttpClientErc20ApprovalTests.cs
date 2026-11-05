using System.Net;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nethereum.JsonRpc.Client;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.X402.Client;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Nethereum.X402.Server;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Client;

public class X402HttpClientErc20ApprovalTests
{
    private const string PrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    private const int ChainId = 84532;
    private const string Network = "eip155:84532";
    private const string Token = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";
    private const string PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C";
    private const string Amount = "100000";
    private const string Key = X402ExtensionKeys.Erc20ApprovalGasSponsoring;

    private static readonly BigInteger PendingCount = 7;

    [Fact]
    public async Task Given_EveryConditionHolds_When_ClientPays_Then_ASignedApproveOfMaxUint256ToPermit2IsAttached()
    {
        var rpc = new ScriptedTokenRpcClient { Allowance = 0, PendingTransactionCount = PendingCount };

        var paid = await PayAsync(Declaration(), ApprovalOnlyRequirements(), RpcFor(rpc));

        var info = Info(paid);
        var payer = new EthECKey(PrivateKey).GetPublicAddress();
        Assert.Equal(payer, info["from"]!.GetValue<string>());
        Assert.Equal(Token, info["asset"]!.GetValue<string>());
        Assert.Equal(X402Permit2Addresses.Permit2, info["spender"]!.GetValue<string>());
        Assert.Equal(SignedApprovalSpec.MaxUint256.ToString(), info["amount"]!.GetValue<string>());
        Assert.Equal("1", info["version"]!.GetValue<string>());

        Assert.Matches("^0x[a-fA-F0-9]+$", info["signedTransaction"]!.GetValue<string>());
        var transaction = TransactionFactory.CreateTransaction(info["signedTransaction"]!.GetValue<string>());
        Assert.Equal(TransactionType.EIP1559, transaction.TransactionType);
        Assert.Equal(payer, transaction.GetSenderAddress());
        Assert.True(Token.IsTheSameAddress(transaction.GetReceiverAddress()));
        Assert.Equal(Erc20ApprovalTransactions.ApproveData(X402Permit2Addresses.Permit2, SignedApprovalSpec.MaxUint256),
            "0x" + Convert.ToHexString(transaction.GetData()!).ToLowerInvariant());
        Assert.Equal(new BigInteger(ChainId), (BigInteger)transaction.GetChainId());
        Assert.Equal(new BigInteger(70_000), (BigInteger)transaction.GetGasLimit());
        Assert.Equal(PendingCount, (BigInteger)transaction.GetNonce());
        Assert.True(transaction.GetValue().IsZero);
    }

    [Fact]
    public async Task Given_TheNodeCannotEstimateFees_When_ClientPays_Then_TheReferenceFallbackFeesAreSigned()
    {
        var rpc = new ScriptedTokenRpcClient { Allowance = 0, PendingTransactionCount = PendingCount };

        var paid = await PayAsync(Declaration(), ApprovalOnlyRequirements(), RpcFor(rpc));

        var transaction = TransactionFactory.CreateTransaction(Info(paid)["signedTransaction"]!.GetValue<string>());
        Assert.Equal(new BigInteger(1_000_000_000), (BigInteger)transaction.GetMaxFeePerGas());
        Assert.Equal(new BigInteger(100_000_000), (BigInteger)transaction.GetMaxPriorityFeePerGas());
    }

    [Fact]
    public async Task Given_TheNodeCountsPendingTransactions_When_ClientPays_Then_TheNonceIsThePendingCount()
    {
        var rpc = new ScriptedTokenRpcClient { Allowance = 0, PendingTransactionCount = 42 };

        var paid = await PayAsync(Declaration(), ApprovalOnlyRequirements(), RpcFor(rpc));

        var transaction = TransactionFactory.CreateTransaction(Info(paid)["signedTransaction"]!.GetValue<string>());
        Assert.Equal(new BigInteger(42), (BigInteger)transaction.GetNonce());
        Assert.Contains("eth_getTransactionCount", rpc.Methods);
    }

    [Fact]
    public async Task Given_ServerDeclaredTheExtension_When_ClientPays_Then_PayloadExtensionIsTheDeclarationMergedWithTheClientInfo()
    {
        var rpc = new ScriptedTokenRpcClient { Allowance = 0, PendingTransactionCount = PendingCount };

        var paid = await PayAsync(Declaration(), ApprovalOnlyRequirements(), RpcFor(rpc));

        var extension = ExtensionOf(paid);
        Assert.Equal(
            "The facilitator broadcasts a pre-signed ERC-20 approve() transaction to grant Permit2 allowance.",
            extension["info"]!["description"]!.GetValue<string>());
        Assert.NotNull(extension["info"]!["signedTransaction"]);
        Assert.Equal(6, extension["schema"]!["required"]!.AsArray().Count);
    }

    [Fact]
    public async Task Given_ServerDidNotDeclareTheExtension_When_ClientPays_Then_NoApprovalIsAttached()
    {
        var rpc = new ScriptedTokenRpcClient { Allowance = 0, PendingTransactionCount = PendingCount };

        var paid = await PayAsync(null, ApprovalOnlyRequirements(), RpcFor(rpc));

        Assert.Null(paid.Extensions);
        Assert.Empty(rpc.Methods);
    }

    [Fact]
    public async Task Given_NoRpcConfigured_When_ClientPays_Then_NoApprovalIsAttached()
    {
        var paid = await PayAsync(Declaration(), ApprovalOnlyRequirements(), null);

        Assert.Null(Info(paid)["signedTransaction"]);
    }

    [Fact]
    public async Task Given_RpcConfiguredForAnotherChain_When_ClientPays_Then_NoApprovalIsAttached()
    {
        var rpc = new ScriptedTokenRpcClient { Allowance = 0, PendingTransactionCount = PendingCount };

        var paid = await PayAsync(Declaration(), ApprovalOnlyRequirements(), new Dictionary<int, IClient> { [1] = rpc });

        Assert.Null(Info(paid)["signedTransaction"]);
        Assert.Empty(rpc.Methods);
    }

    [Fact]
    public async Task Given_Permit2AllowanceCoversTheAmount_When_ClientPays_Then_NoApprovalIsAttached()
    {
        var rpc = new ScriptedTokenRpcClient { Allowance = BigInteger.Parse(Amount), PendingTransactionCount = PendingCount };

        var paid = await PayAsync(Declaration(), ApprovalOnlyRequirements(), RpcFor(rpc));

        Assert.Null(Info(paid)["signedTransaction"]);
        Assert.DoesNotContain("eth_getTransactionCount", rpc.Methods);
    }

    [Fact]
    public async Task Given_TheAllowanceReadFails_When_ClientPays_Then_TheClientStillSignsTheApproval()
    {
        var rpc = new ScriptedTokenRpcClient { AllowanceReadFails = true, PendingTransactionCount = PendingCount };

        var paid = await PayAsync(Declaration(), ApprovalOnlyRequirements(), RpcFor(rpc));

        Assert.NotNull(Info(paid)["signedTransaction"]);
    }

    [Fact]
    public async Task Given_RequirementIsEip3009_When_ClientPays_Then_NoApprovalIsAttached()
    {
        var rpc = new ScriptedTokenRpcClient { Allowance = 0, PendingTransactionCount = PendingCount };
        var requirements = ApprovalOnlyRequirements();
        requirements.Extra = new ExactSchemeExtra { AssetTransferMethod = "eip3009", Name = "USDC", Version = "2" };

        var paid = await PayAsync(Declaration(), requirements, RpcFor(rpc));

        Assert.Null(Info(paid)["signedTransaction"]);
        Assert.Empty(rpc.Methods);
    }

    [Fact]
    public async Task Given_Eip2612IsApplicable_When_ClientPays_Then_OnlyTheEip2612InfoIsAttached()
    {
        var rpc = new ScriptedTokenRpcClient { Allowance = 0, Nonce = 3, PendingTransactionCount = PendingCount };
        var declaration = Declaration();
        declaration[X402ExtensionKeys.Eip2612GasSponsoring] = X402GasSponsoringExtensions.DeclareEip2612();

        var paid = await PayAsync(declaration, Permit2Requirements(withNameAndVersion: true), RpcFor(rpc));

        var extensions = JsonSerializer.SerializeToNode(paid.Extensions)!.AsObject();
        Assert.NotNull(extensions[X402ExtensionKeys.Eip2612GasSponsoring]!["info"]!["signature"]);
        Assert.Null(extensions[Key]!["info"]!["signedTransaction"]);
        Assert.DoesNotContain("eth_getTransactionCount", rpc.Methods);
    }

    [Fact]
    public async Task Given_BothDeclaredButTheTokenDomainIsUnknown_When_ClientPays_Then_TheApprovalIsAttached()
    {
        var rpc = new ScriptedTokenRpcClient { Allowance = 0, PendingTransactionCount = PendingCount };
        var declaration = Declaration();
        declaration[X402ExtensionKeys.Eip2612GasSponsoring] = X402GasSponsoringExtensions.DeclareEip2612();

        var paid = await PayAsync(declaration, ApprovalOnlyRequirements(), RpcFor(rpc));

        var extensions = JsonSerializer.SerializeToNode(paid.Extensions)!.AsObject();
        Assert.NotNull(extensions[Key]!["info"]!["signedTransaction"]);
        Assert.Null(extensions[X402ExtensionKeys.Eip2612GasSponsoring]!["info"]!["signature"]);
    }

    [Fact]
    [Trait("Rule", "S9")]
    public async Task Given_CapSponsoredApprovalToPaymentAmount_When_ClientPays_Then_TheApproveValueIsTheRequirementAmount()
    {
        var rpc = new ScriptedTokenRpcClient { Allowance = 0, PendingTransactionCount = PendingCount };

        var paid = await PayAsync(Declaration(), ApprovalOnlyRequirements(), RpcFor(rpc), capSponsoredApproval: true);

        var info = Info(paid);
        Assert.Equal(Amount, info["amount"]!.GetValue<string>());
        var transaction = TransactionFactory.CreateTransaction(info["signedTransaction"]!.GetValue<string>());
        Assert.Equal(Erc20ApprovalTransactions.ApproveData(X402Permit2Addresses.Permit2, BigInteger.Parse(Amount)),
            "0x" + Convert.ToHexString(transaction.GetData()!).ToLowerInvariant());
    }

    [Fact]
    [Trait("Rule", "S9")]
    public async Task Given_CapSponsoredApprovalToPaymentAmountLeftAtDefault_When_ClientPays_Then_TheApproveValueIsMaxUint256()
    {
        var rpc = new ScriptedTokenRpcClient { Allowance = 0, PendingTransactionCount = PendingCount };

        var paid = await PayAsync(Declaration(), ApprovalOnlyRequirements(), RpcFor(rpc));

        Assert.False(new X402HttpClientOptions().CapSponsoredApprovalToPaymentAmount);
        Assert.Equal(SignedApprovalSpec.MaxUint256.ToString(), Info(paid)["amount"]!.GetValue<string>());
    }

    private static Dictionary<int, IClient> RpcFor(IClient client) => new() { [ChainId] = client };

    private static Dictionary<string, object> Declaration() =>
        new() { [Key] = X402GasSponsoringExtensions.DeclareErc20Approval() };

    private static PaymentRequirements ApprovalOnlyRequirements() => Permit2Requirements(withNameAndVersion: false);

    private static PaymentRequirements Permit2Requirements(bool withNameAndVersion) => new()
    {
        Scheme = "exact",
        Network = Network,
        Amount = Amount,
        PayTo = PayTo,
        MaxTimeoutSeconds = 300,
        Asset = Token,
        Extra = withNameAndVersion
            ? new ExactSchemeExtra { AssetTransferMethod = "permit2", Name = "USDC", Version = "2" }
            : new ExactSchemeExtra { AssetTransferMethod = "permit2" }
    };

    private static JsonObject ExtensionOf(PaymentPayload paid) =>
        JsonSerializer.SerializeToNode(paid.Extensions)![Key]!.AsObject();

    private static JsonObject Info(PaymentPayload paid) => ExtensionOf(paid)["info"]!.AsObject();

    private static async Task<PaymentPayload> PayAsync(
        object? declaredExtensions,
        PaymentRequirements requirements,
        Dictionary<int, IClient>? rpcClients,
        bool capSponsoredApproval = false)
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
            RpcClientsByChainId = rpcClients,
            CapSponsoredApprovalToPaymentAmount = capSponsoredApproval
        });

        var response = await client.GetAsync("http://localhost/premium");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return paid!;
    }
}
