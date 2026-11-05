using System.Numerics;
using Nethereum.DevChain;
using Nethereum.JsonRpc.Client;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Permit2;

[Collection("X402 DevChain E2E")]
public class X402SponsoredApprovalRound2Tests : IClassFixture<X402DevChainFixture>, IAsyncLifetime
{
    private const int ChainId = X402DevChainFixture.ChainId;
    private const string FacilitatorKey = X402DevChainFixture.FacilitatorKey;
    private static readonly BigInteger GoodMaxFee = 1_000_000_000;
    private static readonly BigInteger CurveOrder = BigInteger.Parse(
        "0FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364141",
        System.Globalization.NumberStyles.HexNumber);

    private readonly X402DevChainFixture _chain;
    private readonly EthECKey _fundedPayer = new(X402DevChainFixture.PayerKey.Substring(2));

    public X402SponsoredApprovalRound2Tests(X402DevChainFixture chain)
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
    [Trait("Rule", "V4")]
    public async Task Given_AnApprovalSignedWithAHighS_When_Verifying_Then_Erc20ApprovalTxInvalidSignature()
    {
        var payer = EthECKey.GenerateKey();
        var spec = Spec();

        var verify = await VerifySignedAsync(payer, spec, SignWithHighS(payer, spec));

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxInvalidSignature, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "V4")]
    public async Task Given_TheSameApprovalSignedWithALowS_When_Verifying_Then_TheSignatureRuleAccepts()
    {
        var payer = EthECKey.GenerateKey();
        var spec = Spec();

        var verify = await VerifySignedAsync(payer, spec, Erc20ApprovalTransactions.Sign(payer, spec));

        Assert.Equal(X402ErrorCodes.Permit2InsufficientBalance, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "V4")]
    public async Task Given_AnApprovalGasLimitAboveTheSixteenMillionCap_When_Verifying_Then_Erc20ApprovalTxGasLimitTooHigh()
    {
        var payer = EthECKey.GenerateKey();
        var spec = Spec() with { Gas = 16_777_217 };

        var verify = await VerifyApprovalAsync(payer, spec, new EstimateGasOverridingRpcClient(_chain.Client, 16_777_216));

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxGasLimitTooHigh, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "V4")]
    public async Task Given_AnApprovalGasLimitExactlyAtTheSixteenMillionCap_When_Verifying_Then_TheGasLimitRuleAccepts()
    {
        var payer = EthECKey.GenerateKey();
        var spec = Spec() with { Gas = 16_777_216 };

        var verify = await VerifyApprovalAsync(payer, spec, new EstimateGasOverridingRpcClient(_chain.Client, 16_777_216));

        Assert.Equal(X402ErrorCodes.Permit2InsufficientBalance, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "V4")]
    public async Task Given_AChainWhoseBlockGasLimitIsBelowTheCap_When_AnApprovalExceedsTheBlockGasLimit_Then_Erc20ApprovalTxGasLimitTooHigh()
    {
        const int smallChainId = 31339;
        const long blockGasLimit = 6_000_000;
        using var small = await StartChainAsync(smallChainId, blockGasLimit);
        var payer = EthECKey.GenerateKey();

        var over = await VerifyOnAsync(small, payer, new SignedApprovalSpec(smallChainId, small.Token, 0, GoodMaxFee, blockGasLimit + 1), blockGasLimit);
        var at = await VerifyOnAsync(small, payer, new SignedApprovalSpec(smallChainId, small.Token, 0, GoodMaxFee, blockGasLimit), blockGasLimit);

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxGasLimitTooHigh, over.InvalidReason);
        Assert.Equal(X402ErrorCodes.Permit2InsufficientBalance, at.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "V4")]
    public async Task Given_AMaxFeeEqualToTheBaseFeeButBelowOnePointOneTwoFiveTimesIt_When_Verifying_Then_Erc20ApprovalTxMaxFeeBelowBaseFee()
    {
        var baseFee = await LatestBaseFeeAsync();
        var payer = EthECKey.GenerateKey();

        var verify = await VerifyApprovalAsync(payer, Spec() with { MaxFeePerGas = baseFee });

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxMaxFeeBelowBaseFee, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "V4")]
    public async Task Given_AMaxFeeOfExactlyTheCeilingOfOnePointOneTwoFiveTimesTheBaseFee_When_Verifying_Then_TheFeeRuleAccepts()
    {
        var baseFee = await LatestBaseFeeAsync();
        var payer = EthECKey.GenerateKey();

        var verify = await VerifyApprovalAsync(payer, Spec() with { MaxFeePerGas = (baseFee * 9 + 7) / 8 });

        Assert.Equal(X402ErrorCodes.Permit2InsufficientBalance, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "V4")]
    public async Task Given_AZeroMaxPriorityFee_When_Verifying_Then_Erc20ApprovalTxPriorityFeeTooLow()
    {
        var payer = EthECKey.GenerateKey();

        var verify = await VerifyApprovalAsync(payer, Spec() with { MaxPriorityFeePerGas = 0 });

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxPriorityFeeTooLow, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "V4")]
    public async Task Given_AOneWeiMaxPriorityFee_When_Verifying_Then_ThePriorityFeeRuleAccepts()
    {
        var payer = EthECKey.GenerateKey();

        var verify = await VerifyApprovalAsync(payer, Spec() with { MaxPriorityFeePerGas = 1 });

        Assert.Equal(X402ErrorCodes.Permit2InsufficientBalance, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "V5")]
    public async Task Given_AnRpcFailureInTheNonceCheckAfterTheApprovalWasLocked_When_TheSameApprovalIsSettledAgain_Then_ItIsNotRejectedAsInFlight()
    {
        var payer = await PayerWithTokensAsync();
        var failed = 0;
        var rpc = new InterceptingRpcClient(_chain.Client, method =>
        {
            if (method == "eth_getTransactionCount" && Interlocked.Increment(ref failed) == 1)
                throw new RpcClientUnknownException("rpc unreachable", new HttpRequestException("rpc unreachable"));
            return Task.CompletedTask;
        });
        var service = SponsoringService(rpc);
        var payload = SponsoredPayload(payer, nonce: 0);

        var first = await service.SettlePaymentAsync(payload, payload.Accepted);
        var second = await service.SettlePaymentAsync(payload, payload.Accepted);

        Assert.Equal(X402ErrorCodes.UnexpectedVerifyError, first.ErrorReason);
        Assert.NotEqual(X402ErrorCodes.Erc20ApprovalTxInFlight, second.ErrorReason);
        Assert.Equal(X402ErrorCodes.TransferEventMismatch, second.ErrorReason);
    }

    [Fact]
    [Trait("Rule", "V5")]
    public async Task Given_AnApprovalRejectedByTheNonceCheck_When_TheSameApprovalIsSettledAgain_Then_ItIsRejectedAgainForTheSameReasonAndNeverAsInFlight()
    {
        var payer = await PayerWithTokensAsync();
        var service = SponsoringService(_chain.Client);
        var payload = SponsoredPayload(payer, nonce: 5);

        var first = await service.SettlePaymentAsync(payload, payload.Accepted);
        var second = await service.SettlePaymentAsync(payload, payload.Accepted);

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxNonceMismatch, first.ErrorReason);
        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxNonceMismatch, second.ErrorReason);
    }

    [Fact]
    [Trait("Rule", "V6")]
    public async Task Given_TheSamePayerSpelledWithAndWithoutThe0xPrefix_When_TheirSettlesRunConcurrently_Then_ExactlyOneFundsAndTheOtherIsInFlight()
    {
        var payer = await PayerWithTokensAsync();
        var broadcastReached = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var gated = 0;
        var rpc = new InterceptingRpcClient(_chain.Client, async method =>
        {
            if (method == InterceptingRpcClient.SendRawMethod && Interlocked.Increment(ref gated) == 1)
            {
                broadcastReached.SetResult();
                await release.Task;
            }
        });
        var service = SponsoringService(rpc);
        var prefixed = SponsoredPayload(payer, nonce: 0);
        var bare = WithPayerSpelledWithout0x(prefixed);
        var payerTransactionsBefore = await X402Permit2DevChain.TransactionCountAsync(_chain.Node, payer.GetPublicAddress());

        var winner = service.SettlePaymentAsync(prefixed, prefixed.Accepted);
        await broadcastReached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var loser = await service.SettlePaymentAsync(bare, bare.Accepted).WaitAsync(TimeSpan.FromSeconds(30));
        release.SetResult();
        var winning = await winner.WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxInFlight, loser.ErrorReason);
        Assert.Equal(X402ErrorCodes.TransferEventMismatch, winning.ErrorReason);
        Assert.Equal(payerTransactionsBefore + 1, await X402Permit2DevChain.TransactionCountAsync(_chain.Node, payer.GetPublicAddress()));
    }

    private SignedApprovalSpec Spec() => new(ChainId, _chain.TokenAddress, 0, GoodMaxFee);

    private static string SignWithHighS(EthECKey key, SignedApprovalSpec spec)
    {
        var transaction = new Transaction1559(
            spec.ChainId,
            spec.Nonce,
            spec.MaxFeePerGas / 10,
            spec.MaxFeePerGas,
            spec.EffectiveGas,
            spec.Token,
            spec.Value,
            Erc20ApprovalTransactions.ApproveData(spec.EffectiveSpender, spec.EffectiveAmount),
            new List<AccessListItem>());
        var signature = key.SignAndCalculateYParityV(transaction.RawHash);
        var s = new BigInteger(signature.S, isUnsigned: true, isBigEndian: true);
        var highS = (CurveOrder - s).ToByteArray(isUnsigned: true, isBigEndian: true);
        var flippedV = signature.V.Length == 0 || signature.V[0] == 0 ? new byte[] { 1 } : Array.Empty<byte>();
        transaction.SetSignature(new Signature { R = signature.R, S = highS, V = flippedV });
        return "0x" + transaction.GetRLPEncoded().ToHex();
    }

    private async Task<BigInteger> LatestBaseFeeAsync()
    {
        var latest = await _chain.Node.CreateWeb3().Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(BlockParameter.CreateLatest());
        return latest.BaseFeePerGas.Value;
    }

    private Task<VerificationResponse> VerifyApprovalAsync(EthECKey payer, SignedApprovalSpec spec, IClient? client = null) =>
        VerifySignedAsync(payer, spec, Erc20ApprovalTransactions.Sign(payer, spec), client);

    private async Task<VerificationResponse> VerifySignedAsync(
        EthECKey payer, SignedApprovalSpec spec, string signedTransaction, IClient? client = null)
    {
        var info = Erc20ApprovalTransactions.Info(payer.GetPublicAddress(), spec, signedTransaction);
        var payload = X402Permit2DevChain.Payload(payer, ChainId, _chain.TokenAddress, Erc20ApprovalTransactions.Extension(info));
        var service = new X402ExactPermit2Service(FacilitatorKey, new Dictionary<int, IClient> { [ChainId] = client ?? _chain.Client },
            new Erc20ApprovalGasSponsoringOptions { Enabled = true, MaxSponsoredGasCost = BigInteger.Pow(10, 24) });
        return await service.VerifyPaymentAsync(payload, payload.Accepted);
    }

    private static async Task<VerificationResponse> VerifyOnAsync(
        SmallChain chain, EthECKey payer, SignedApprovalSpec spec, BigInteger estimate)
    {
        var info = Erc20ApprovalTransactions.Info(payer.GetPublicAddress(), spec, Erc20ApprovalTransactions.Sign(payer, spec));
        var payload = X402Permit2DevChain.Payload(payer, spec.ChainId, chain.Token, Erc20ApprovalTransactions.Extension(info));
        var service = new X402ExactPermit2Service(FacilitatorKey, new Dictionary<int, IClient> { [spec.ChainId] = new EstimateGasOverridingRpcClient(chain.Node.CreateWeb3().Client, estimate) },
            new Erc20ApprovalGasSponsoringOptions { Enabled = true, MaxSponsoredGasCost = BigInteger.Pow(10, 24) });
        return await service.VerifyPaymentAsync(payload, payload.Accepted);
    }

    private PaymentPayload SponsoredPayload(EthECKey payer, BigInteger nonce)
    {
        var spec = new SignedApprovalSpec(ChainId, _chain.TokenAddress, nonce, GoodMaxFee);
        var info = Erc20ApprovalTransactions.Info(payer.GetPublicAddress(), spec, Erc20ApprovalTransactions.Sign(payer, spec));
        return X402Permit2DevChain.Payload(payer, ChainId, _chain.TokenAddress, Erc20ApprovalTransactions.Extension(info));
    }

    private static PaymentPayload WithPayerSpelledWithout0x(PaymentPayload payment)
    {
        var original = (Permit2SchemePayload)payment.Payload;
        var auth = original.Permit2Authorization;
        return new PaymentPayload
        {
            X402Version = payment.X402Version,
            Accepted = payment.Accepted,
            Extensions = payment.Extensions,
            Payload = new Permit2SchemePayload
            {
                Signature = original.Signature,
                Permit2Authorization = new Permit2Authorization
                {
                    From = auth.From.Substring(2),
                    Permitted = auth.Permitted,
                    Spender = auth.Spender,
                    Nonce = auth.Nonce,
                    Deadline = auth.Deadline,
                    Witness = auth.Witness
                }
            }
        };
    }

    private async Task<EthECKey> PayerWithTokensAsync()
    {
        var payer = EthECKey.GenerateKey();
        await X402Permit2DevChain.GiveTokensAsync(
            _chain.Node, X402DevChainFixture.PayerKey, ChainId, _chain.TokenAddress, payer.GetPublicAddress(), 2_000_000);
        return payer;
    }

    private X402ExactPermit2Service SponsoringService(IClient client) =>
        new(FacilitatorKey, new Dictionary<int, IClient> { [ChainId] = client },
            new Erc20ApprovalGasSponsoringOptions { Enabled = true, MaxSponsoredGasCost = BigInteger.Parse("10000000000000000") });

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

    private static async Task<SmallChain> StartChainAsync(int chainId, long blockGasLimit)
    {
        var node = DevChainNode.CreateInMemory(new DevChainConfig
        {
            ChainId = chainId,
            BaseFee = 1_000_000_000,
            BlockGasLimit = blockGasLimit,
            AutoMine = true
        });
        await node.StartAsync(
            new[] { X402DevChainFixture.FacilitatorAddress, X402DevChainFixture.PayerAddress },
            Nethereum.Web3.Web3.Convert.ToWei(10000));

        var deployer = new Nethereum.Web3.Accounts.Account(X402DevChainFixture.PayerKey, chainId);
        var usdc = new USDCDeploymentHelper((Nethereum.Web3.Web3)node.CreateWeb3(deployer), deployer);
        var token = await usdc.DeployAsync(X402DevChainFixture.TokenName, "USDC", 6, X402DevChainFixture.TokenVersion);
        return new SmallChain(node, token);
    }
}
