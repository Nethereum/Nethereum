using Nethereum.Model;
using System.Net;
using System.Numerics;
using System.Text.Json;
using Nethereum.DevChain;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Client;
using Nethereum.X402.IntegrationTests.Blockchain;
using Nethereum.X402.IntegrationTests.Client;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Nethereum.X402.Server;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Permit2;

[Collection("X402 DevChain E2E")]
public class X402SponsoredApprovalFundingBoundsTests : IClassFixture<X402DevChainFixture>, IAsyncLifetime
{
    private const int ChainId = X402DevChainFixture.ChainId;
    private const string FacilitatorKey = X402DevChainFixture.FacilitatorKey;
    private static readonly BigInteger ReferenceMaxFee = 1_000_000_000;
    private static readonly BigInteger ReferencePriority = 100_000_000;

    private readonly X402DevChainFixture _chain;
    private readonly EthECKey _fundedPayer = new(X402DevChainFixture.PayerKey.Substring(2));

    public X402SponsoredApprovalFundingBoundsTests(X402DevChainFixture chain)
    {
        _chain = chain;
    }

    public async Task InitializeAsync()
    {
        await X402Permit2DevChain.InstallPermit2StubsAsync(_chain.Node);
        await X402Permit2DevChain.ApprovePermit2Async(_chain.Node, _fundedPayer, ChainId, _chain.TokenAddress);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Trait("Rule", "X4")]
    public async Task Given_AnApproveDeclaringSixteenMillionGas_When_Verifying_Then_Erc20ApprovalTxGasLimitTooHigh()
    {
        var verify = await VerifyAsync(_chain.Node, _chain.Client, _chain.TokenAddress, ChainId, Reference() with { Gas = 16_000_000 });

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxGasLimitTooHigh, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "X4")]
    public async Task Given_TheReferenceGasLimitOfSeventyThousandBelowOnePointFiveTimesTheEstimate_When_Verifying_Then_TheGasLimitRuleAccepts()
    {
        var verify = await VerifyAsync(
            _chain.Node, new EstimateGasOverridingRpcClient(_chain.Client, 10_000), _chain.TokenAddress, ChainId, Reference());

        Assert.Equal(X402ErrorCodes.Permit2InsufficientBalance, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "X4")]
    public async Task Given_AGasLimitOneAboveOnePointFiveTimesTheEstimate_When_Verifying_Then_Erc20ApprovalTxGasLimitTooHigh()
    {
        var client = new EstimateGasOverridingRpcClient(_chain.Client, 100_000);

        var at = await VerifyAsync(_chain.Node, client, _chain.TokenAddress, ChainId, Reference() with { Gas = 150_000 });
        var over = await VerifyAsync(_chain.Node, client, _chain.TokenAddress, ChainId, Reference() with { Gas = 150_001 });

        Assert.Equal(X402ErrorCodes.Permit2InsufficientBalance, at.InvalidReason);
        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxGasLimitTooHigh, over.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "X4")]
    public async Task Given_APriorityFeeOfTwiceTheNodeSuggestionPlusOne_When_Verifying_Then_Erc20ApprovalTxPriorityFeeTooHigh()
    {
        var suggestion = await SuggestedPriorityFeeAsync(_chain.Node);
        var maxFee = 3 * await LatestBaseFeeAsync(_chain.Node) + 2 * suggestion;

        var at = await VerifyAsync(_chain.Node, _chain.Client, _chain.TokenAddress, ChainId,
            Reference() with { MaxFeePerGas = maxFee, MaxPriorityFeePerGas = 2 * suggestion });
        var over = await VerifyAsync(_chain.Node, _chain.Client, _chain.TokenAddress, ChainId,
            Reference() with { MaxFeePerGas = maxFee, MaxPriorityFeePerGas = 2 * suggestion + 1 });

        Assert.Equal(X402ErrorCodes.Permit2InsufficientBalance, at.InvalidReason);
        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxPriorityFeeTooHigh, over.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "X4")]
    public async Task Given_ANodeSuggestingLessThanFiftyMillionWei_When_APriorityOfOneHundredMillionWeiIsSigned_Then_ItIsAcceptedAndOneMoreIsRejected()
    {
        using var small = await StartChainAsync(31341, suggestedPriorityFee: 10_000_000);

        var at = await VerifyAsync(small.Node, small.Node.CreateWeb3().Client, small.Token, 31341,
            new SignedApprovalSpec(31341, small.Token, 0, ReferenceMaxFee, 70_000, MaxPriorityFeePerGas: ReferencePriority));
        var over = await VerifyAsync(small.Node, small.Node.CreateWeb3().Client, small.Token, 31341,
            new SignedApprovalSpec(31341, small.Token, 0, ReferenceMaxFee, 70_000, MaxPriorityFeePerGas: ReferencePriority + 1));

        Assert.Equal(X402ErrorCodes.Permit2InsufficientBalance, at.InvalidReason);
        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxPriorityFeeTooHigh, over.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "X4")]
    public async Task Given_AMaxFeeOfThreeTimesTheBaseFeePlusPriorityPlusOne_When_Verifying_Then_Erc20ApprovalTxMaxFeeTooHigh()
    {
        using var small = await StartChainAsync(31342);
        var baseFee = await LatestBaseFeeAsync(small.Node);
        var ceiling = 3 * baseFee + ReferencePriority;
        Assert.True(ceiling > ReferenceMaxFee);

        var at = await VerifyAsync(small.Node, small.Node.CreateWeb3().Client, small.Token, 31342,
            new SignedApprovalSpec(31342, small.Token, 0, ceiling, 70_000, MaxPriorityFeePerGas: ReferencePriority));
        var over = await VerifyAsync(small.Node, small.Node.CreateWeb3().Client, small.Token, 31342,
            new SignedApprovalSpec(31342, small.Token, 0, ceiling + 1, 70_000, MaxPriorityFeePerGas: ReferencePriority));

        Assert.Equal(X402ErrorCodes.Permit2InsufficientBalance, at.InvalidReason);
        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxMaxFeeTooHigh, over.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "X4")]
    public async Task Given_AMaxFeeOfOneGweiWhenThreeTimesTheBaseFeeIsLower_When_Verifying_Then_TheOneGweiFloorIsAccepted()
    {
        using var small = await StartChainAsync(31343, baseFee: 100_000_000);
        var baseFee = await LatestBaseFeeAsync(small.Node);
        Assert.True(3 * baseFee + ReferencePriority < ReferenceMaxFee);

        var at = await VerifyAsync(small.Node, small.Node.CreateWeb3().Client, small.Token, 31343,
            new SignedApprovalSpec(31343, small.Token, 0, ReferenceMaxFee, 70_000, MaxPriorityFeePerGas: ReferencePriority));
        var over = await VerifyAsync(small.Node, small.Node.CreateWeb3().Client, small.Token, 31343,
            new SignedApprovalSpec(31343, small.Token, 0, ReferenceMaxFee + 1, 70_000, MaxPriorityFeePerGas: ReferencePriority));

        Assert.Equal(X402ErrorCodes.Permit2InsufficientBalance, at.InvalidReason);
        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxMaxFeeTooHigh, over.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "X4")]
    public async Task Given_AReferenceStyleApprovalWithTheFallbackFees_When_Verifying_Then_NoApprovalBoundRejectsIt()
    {
        var verify = await VerifyAsync(_chain.Node, _chain.Client, _chain.TokenAddress, ChainId, Reference());

        Assert.Equal(X402ErrorCodes.Permit2InsufficientBalance, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "X4")]
    public async Task Given_TheClientsOwnSponsoredApproval_When_Verifying_Then_ItPassesEveryBound()
    {
        var payerKey = EthECKey.GenerateKey();
        await X402Permit2DevChain.GiveTokensAsync(
            _chain.Node, X402DevChainFixture.PayerKey, ChainId, _chain.TokenAddress, payerKey.GetPublicAddress(), 2_000_000);
        var paid = await PayAsync(payerKey, _chain.Client, _chain.TokenAddress);
        var service = new X402ExactPermit2Service(FacilitatorKey, new Dictionary<int, IClient> { [ChainId] = _chain.Client },
            new Erc20ApprovalGasSponsoringOptions { Enabled = true, MaxSponsoredGasCost = BigInteger.Pow(10, 24) });

        var verify = await service.VerifyPaymentAsync(paid, paid.Accepted);

        Assert.True(verify.IsValid, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "Y1")]
    public async Task Given_ANodeSuggestingAZeroPriorityFee_When_TheClientSignsTheApproval_Then_ItPassesTheFacilitatorsPriorityFloor()
    {
        using var small = await StartChainAsync(31350, suggestedPriorityFee: 0, mintToPayer: true);

        var (paid, verify) = await PayAndVerifyOnAsync(small, 31350);

        Assert.True(verify.IsValid, verify.InvalidReason);
        Assert.Equal(BigInteger.One, SignedPriorityFee(paid));
    }

    [Fact]
    [Trait("Rule", "Y1")]
    public async Task Given_ANodeSuggestingMoreThanTheCeiling_When_TheClientSignsTheApproval_Then_ItSignsExactlyTheCeilingAndPassesVerify()
    {
        using var small = await StartChainAsync(31351, suggestedPriorityFee: 2_000_000_000, mintToPayer: true);

        var (paid, verify) = await PayAndVerifyOnAsync(small, 31351);

        Assert.True(verify.IsValid, verify.InvalidReason);
        Assert.Equal(ReferencePriority, SignedPriorityFee(paid));
    }

    private static BigInteger SignedPriorityFee(PaymentPayload paid)
    {
        var info = JsonSerializer.SerializeToNode(paid.Extensions)![X402ExtensionKeys.Erc20ApprovalGasSponsoring]!["info"]!;
        var transaction = TransactionFactory.CreateTransaction(info["signedTransaction"]!.GetValue<string>());
        return (BigInteger)transaction.GetMaxPriorityFeePerGas();
    }

    private static async Task<(PaymentPayload Paid, VerificationResponse Verify)> PayAndVerifyOnAsync(SmallChain small, int chainId)
    {
        await X402Permit2DevChain.InstallPermit2StubsAsync(small.Node);
        var payerKey = EthECKey.GenerateKey();
        await X402Permit2DevChain.GiveTokensAsync(
            small.Node, X402DevChainFixture.PayerKey, chainId, small.Token, payerKey.GetPublicAddress(), 2_000_000);
        var client = small.Node.CreateWeb3().Client;
        var paid = await PayAsync(payerKey, client, small.Token, chainId);
        var service = new X402ExactPermit2Service(FacilitatorKey, new Dictionary<int, IClient> { [chainId] = client },
            new Erc20ApprovalGasSponsoringOptions { Enabled = true, MaxSponsoredGasCost = BigInteger.Pow(10, 24) });
        return (paid, await service.VerifyPaymentAsync(paid, paid.Accepted));
    }

    private SignedApprovalSpec Reference() =>
        new(ChainId, _chain.TokenAddress, 0, ReferenceMaxFee, 70_000, MaxPriorityFeePerGas: ReferencePriority);

    private static async Task<VerificationResponse> VerifyAsync(
        DevChainNode node, IClient client, string token, int chainId, SignedApprovalSpec spec)
    {
        var payer = EthECKey.GenerateKey();
        var info = Erc20ApprovalTransactions.Info(payer.GetPublicAddress(), spec, Erc20ApprovalTransactions.Sign(payer, spec));
        var payload = X402Permit2DevChain.Payload(payer, chainId, token, Erc20ApprovalTransactions.Extension(info));
        var service = new X402ExactPermit2Service(FacilitatorKey, new Dictionary<int, IClient> { [chainId] = client },
            new Erc20ApprovalGasSponsoringOptions { Enabled = true, MaxSponsoredGasCost = BigInteger.Pow(10, 24) });
        return await service.VerifyPaymentAsync(payload, payload.Accepted);
    }

    private static async Task<BigInteger> LatestBaseFeeAsync(DevChainNode node)
    {
        var latest = await node.CreateWeb3().Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(BlockParameter.CreateLatest());
        return latest.BaseFeePerGas.Value;
    }

    private static async Task<BigInteger> SuggestedPriorityFeeAsync(DevChainNode node) =>
        (await new Nethereum.RPC.Eth.EthMaxPriorityFeePerGas(node.CreateWeb3().Client).SendRequestAsync()).Value;

    private static Task<PaymentPayload> PayAsync(EthECKey payerKey, IClient rpc, string token) =>
        PayAsync(payerKey, rpc, token, ChainId);

    private static async Task<PaymentPayload> PayAsync(EthECKey payerKey, IClient rpc, string token, int chainId)
    {
        PaymentPayload? paid = null;
        var requirements = X402Permit2DevChain.Requirements(chainId, token);
        var count = 0;
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            count++;
            if (count == 1)
            {
                var required = new PaymentRequired
                {
                    Accepts = new List<PaymentRequirements> { requirements },
                    Extensions = new Dictionary<string, object>
                    {
                        [X402ExtensionKeys.Erc20ApprovalGasSponsoring] = X402GasSponsoringExtensions.DeclareErc20Approval()
                    }
                };
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PaymentRequired)
                {
                    Content = new StringContent(JsonSerializer.Serialize(required))
                });
            }

            paid = X402HttpClient.DecodePaymentHeader(request.Headers.GetValues("PAYMENT-SIGNATURE").First());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        });
        var client = new X402HttpClient(new HttpClient(handler), payerKey.GetPrivateKey(), new X402HttpClientOptions
        {
            PreferredNetwork = requirements.Network,
            PreferredScheme = "exact",
            MaxAmount = "10000000",
            RpcClientsByChainId = new Dictionary<int, IClient> { [chainId] = rpc }
        });

        var response = await client.GetAsync("http://localhost/premium");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return paid!;
    }

    private sealed class SmallChain : IDisposable
    {
        public SmallChain(DevChainNode node, string token)
        {
            Node = node;
            Token = token;
        }

        public DevChainNode Node { get; }

        public string Token { get; }

        public void Dispose() => Node.Dispose();
    }

    private static async Task<SmallChain> StartChainAsync(int chainId, BigInteger? suggestedPriorityFee = null, BigInteger? baseFee = null, bool mintToPayer = false)
    {
        var config = new DevChainConfig
        {
            ChainId = chainId,
            BaseFee = baseFee ?? 1_000_000_000,
            BlockGasLimit = 30_000_000,
            AutoMine = true
        };
        if (suggestedPriorityFee.HasValue)
            config.SuggestedPriorityFee = suggestedPriorityFee.Value;
        var node = DevChainNode.CreateInMemory(config);
        await node.StartAsync(
            new[] { X402DevChainFixture.FacilitatorAddress, X402DevChainFixture.PayerAddress },
            Nethereum.Web3.Web3.Convert.ToWei(10000));

        var deployer = new Nethereum.Web3.Accounts.Account(X402DevChainFixture.PayerKey, chainId);
        var usdc = new USDCDeploymentHelper((Nethereum.Web3.Web3)node.CreateWeb3(deployer), deployer);
        var token = await usdc.DeployAsync(X402DevChainFixture.TokenName, "USDC", 6, X402DevChainFixture.TokenVersion);
        if (mintToPayer)
            await usdc.MintAsync(X402DevChainFixture.PayerAddress, BigInteger.Parse("1000000000000"));
        return new SmallChain(node, token);
    }
}
