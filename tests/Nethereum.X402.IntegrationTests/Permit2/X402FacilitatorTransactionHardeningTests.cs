using System.Diagnostics;
using System.Numerics;
using Nethereum.DevChain;
using Nethereum.JsonRpc.Client;
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
public class X402FacilitatorTransactionHardeningTests : IClassFixture<X402DevChainFixture>, IAsyncLifetime
{
    private const int ChainId = X402DevChainFixture.ChainId;
    private const int SecondChainId = 31338;
    private const string FacilitatorKey = X402DevChainFixture.FacilitatorKey;
    private static readonly BigInteger GoodMaxFee = 1_000_000_000;
    private static readonly BigInteger ApprovalGasCost = 70_000 * GoodMaxFee;
    private static readonly BigInteger MaxSponsoredGasCost = BigInteger.Parse("10000000000000000");

    private readonly X402DevChainFixture _chain;
    private readonly EthECKey _fundedPayer = new(X402DevChainFixture.PayerKey.Substring(2));

    public X402FacilitatorTransactionHardeningTests(X402DevChainFixture chain)
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
    [Trait("Rule", "S1")]
    public async Task Given_TheProxySettleReceiptNeverReturns_When_Settling_Then_SettlementPendingCarriesTheBroadcastHashWithinTheBound()
    {
        var rpc = new InterceptingRpcClient(_chain.Client, ReceiptNeverReturns);
        var service = ServiceFor(rpc);
        var payload = X402Permit2DevChain.Payload(_fundedPayer, ChainId, _chain.TokenAddress, maxTimeoutSeconds: 2);
        var elapsed = Stopwatch.StartNew();

        var settle = await service.SettlePaymentAsync(payload, payload.Accepted).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(X402ErrorCodes.SettlementPending, settle.ErrorReason);
        Assert.False(string.IsNullOrEmpty(settle.Transaction));
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(30), elapsed.Elapsed.ToString());
        var broadcast = await _chain.Node.CreateWeb3().Eth.Transactions.GetTransactionByHash.SendRequestAsync(settle.Transaction);
        Assert.NotNull(broadcast);
        Assert.Equal(X402Permit2Addresses.ExactPermit2Proxy.ToLowerInvariant(), broadcast.To.ToLowerInvariant());
    }

    [Fact]
    [Trait("Rule", "S1")]
    public async Task Given_TheFundingReceiptNeverReturns_When_SettlingTheSponsoredApproval_Then_SettlementPendingCarriesTheFundingHashAndNothingFurtherIsSent()
    {
        var payer = EthECKey.GenerateKey();
        var rpc = new InterceptingRpcClient(_chain.Client, ReceiptNeverReturns);
        var settled = false;
        var request = SponsoredRequest(rpc, payer, nonce: 0, maxTimeoutSeconds: 1, _ => { settled = true; });

        var result = await new Erc20ApprovalSponsoredSettlement().SettleAsync(request).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(X402ErrorCodes.SettlementPending, result.ErrorReason);
        var funding = await _chain.Node.CreateWeb3().Eth.Transactions.GetTransactionByHash.SendRequestAsync(result.Transaction);
        Assert.Equal(payer.GetPublicAddress().ToLowerInvariant(), funding.To.ToLowerInvariant());
        Assert.False(settled);
        Assert.Equal(BigInteger.Zero, await X402Permit2DevChain.TransactionCountAsync(_chain.Node, payer.GetPublicAddress()));
    }

    [Fact]
    [Trait("Rule", "S1")]
    public async Task Given_TheApprovalReceiptNeverReturns_When_SettlingTheSponsoredApproval_Then_SettlementPendingCarriesTheApprovalHashAndTheProxyIsNotCalled()
    {
        var rpc = new InterceptingRpcClient(_chain.Client, ReceiptNeverReturns);
        var nonce = await X402Permit2DevChain.TransactionCountAsync(_chain.Node, _fundedPayer.GetPublicAddress());
        var settled = false;
        var request = SponsoredRequest(rpc, _fundedPayer, nonce, maxTimeoutSeconds: 1, _ => { settled = true; });

        var result = await new Erc20ApprovalSponsoredSettlement().SettleAsync(request).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(X402ErrorCodes.SettlementPending, result.ErrorReason);
        var approval = await _chain.Node.CreateWeb3().Eth.Transactions.GetTransactionByHash.SendRequestAsync(result.Transaction);
        Assert.Equal(_fundedPayer.GetPublicAddress().ToLowerInvariant(), approval.From.ToLowerInvariant());
        Assert.Equal(_chain.TokenAddress.ToLowerInvariant(), approval.To.ToLowerInvariant());
        Assert.False(settled);
    }

    [Fact]
    [Trait("Rule", "S1")]
    public async Task Given_TheCallerCancelsAfterTheFundingWasBroadcast_When_SettlingTheSponsoredApproval_Then_TheApprovalAndTheSettleRunToCompletion()
    {
        var payer = EthECKey.GenerateKey();
        using var cancellation = new CancellationTokenSource();
        var rpc = new InterceptingRpcClient(_chain.Client, method =>
        {
            if (method == InterceptingRpcClient.ReceiptMethod)
                cancellation.Cancel();
            return Task.CompletedTask;
        });
        var settleToken = new CancellationToken(true);
        var request = SponsoredRequest(rpc, payer, nonce: 0, maxTimeoutSeconds: 30, token => settleToken = token);

        var result = await new Erc20ApprovalSponsoredSettlement().SettleAsync(request, cancellation.Token);

        Assert.True(cancellation.IsCancellationRequested);
        Assert.True(result.Success, result.ErrorReason);
        Assert.False(settleToken.CanBeCanceled);
        Assert.Equal(BigInteger.One, await X402Permit2DevChain.TransactionCountAsync(_chain.Node, payer.GetPublicAddress()));
        var allowance = await _chain.Node.CreateWeb3().Eth.ERC20.GetContractService(_chain.TokenAddress)
            .AllowanceQueryAsync(payer.GetPublicAddress(), X402Permit2Addresses.Permit2);
        Assert.Equal(SignedApprovalSpec.MaxUint256, allowance);
    }

    [Fact]
    [Trait("Rule", "S1")]
    public async Task Given_TheCallerAlreadyCancelled_When_SettlingTheSponsoredApproval_Then_NothingIsFunded()
    {
        var payer = EthECKey.GenerateKey();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var facilitatorBefore = await X402Permit2DevChain.TransactionCountAsync(_chain.Node, X402DevChainFixture.FacilitatorAddress);
        var request = SponsoredRequest(_chain.Client, payer, nonce: 0, maxTimeoutSeconds: 30, _ => { });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new Erc20ApprovalSponsoredSettlement().SettleAsync(request, cancellation.Token));

        Assert.Equal(facilitatorBefore, await X402Permit2DevChain.TransactionCountAsync(_chain.Node, X402DevChainFixture.FacilitatorAddress));
    }

    [Fact]
    [Trait("Rule", "S6")]
    public async Task Given_TwoConcurrentSettlesOnOneChain_When_Sending_Then_TheTransactionsHaveDistinctConsecutiveNonces()
    {
        var service = ServiceFor(_chain.Client);
        var before = await X402Permit2DevChain.TransactionCountAsync(_chain.Node, X402DevChainFixture.FacilitatorAddress);
        var first = X402Permit2DevChain.Payload(_fundedPayer, ChainId, _chain.TokenAddress);
        var second = X402Permit2DevChain.Payload(_fundedPayer, ChainId, _chain.TokenAddress);

        var settles = await Task.WhenAll(
            service.SettlePaymentAsync(first, first.Accepted),
            service.SettlePaymentAsync(second, second.Accepted));

        var nonces = new List<BigInteger>();
        foreach (var settle in settles)
        {
            Assert.False(string.IsNullOrEmpty(settle.Transaction), settle.ErrorReason);
            var transaction = await _chain.Node.CreateWeb3().Eth.Transactions.GetTransactionByHash.SendRequestAsync(settle.Transaction);
            nonces.Add(transaction.Nonce.Value);
        }

        nonces.Sort();
        Assert.Equal(new[] { before, before + 1 }, nonces);
    }

    [Fact]
    [Trait("Rule", "S6")]
    public async Task Given_ConcurrentSettlesOnTwoChains_When_Sending_Then_EachChainUsesItsOwnNonceCounter()
    {
        using var secondChain = await StartSecondChainAsync();
        var service = new X402ExactPermit2Service(FacilitatorKey, new Dictionary<int, IClient>
        {
            [ChainId] = _chain.Client,
            [SecondChainId] = secondChain.Node.CreateWeb3().Client
        });
        var firstBefore = await X402Permit2DevChain.TransactionCountAsync(_chain.Node, X402DevChainFixture.FacilitatorAddress);
        var secondBefore = await X402Permit2DevChain.TransactionCountAsync(secondChain.Node, X402DevChainFixture.FacilitatorAddress);
        Assert.NotEqual(firstBefore, secondBefore);
        var onFirst = X402Permit2DevChain.Payload(_fundedPayer, ChainId, _chain.TokenAddress);
        var onSecond = X402Permit2DevChain.Payload(_fundedPayer, SecondChainId, secondChain.Token);

        var settles = await Task.WhenAll(
            service.SettlePaymentAsync(onFirst, onFirst.Accepted),
            service.SettlePaymentAsync(onSecond, onSecond.Accepted));

        Assert.All(settles, settle => Assert.False(string.IsNullOrEmpty(settle.Transaction), settle.ErrorReason));
        var firstTransaction = await _chain.Node.CreateWeb3().Eth.Transactions.GetTransactionByHash.SendRequestAsync(settles[0].Transaction);
        var secondTransaction = await secondChain.Node.CreateWeb3().Eth.Transactions.GetTransactionByHash.SendRequestAsync(settles[1].Transaction);
        Assert.Equal(firstBefore, firstTransaction.Nonce.Value);
        Assert.Equal(secondBefore, secondTransaction.Nonce.Value);
    }

    [Fact]
    [Trait("Rule", "S2")]
    public async Task Given_TwoConcurrentSettlesPresentingTheSameSignedApproval_When_Settling_Then_ExactlyOneFundsAndTheOtherIsInFlight()
    {
        var payer = EthECKey.GenerateKey();
        await X402Permit2DevChain.GiveTokensAsync(
            _chain.Node, X402DevChainFixture.PayerKey, ChainId, _chain.TokenAddress, payer.GetPublicAddress(), 2_000_000);
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
        var service = SponsoringServiceFor(rpc);
        var spec = new SignedApprovalSpec(ChainId, _chain.TokenAddress, 0, GoodMaxFee);
        var signed = Erc20ApprovalTransactions.Sign(payer, spec);
        var payload = X402Permit2DevChain.Payload(
            payer, ChainId, _chain.TokenAddress,
            Erc20ApprovalTransactions.Extension(Erc20ApprovalTransactions.Info(payer.GetPublicAddress(), spec, signed)));
        var facilitatorBefore = await X402Permit2DevChain.TransactionCountAsync(_chain.Node, X402DevChainFixture.FacilitatorAddress);

        var winner = service.SettlePaymentAsync(payload, payload.Accepted);
        await broadcastReached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var loser = await service.SettlePaymentAsync(payload, payload.Accepted).WaitAsync(TimeSpan.FromSeconds(30));
        release.SetResult();
        var winning = await winner.WaitAsync(TimeSpan.FromSeconds(60));

        Assert.False(loser.Success);
        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxInFlight, loser.ErrorReason);
        Assert.Equal(X402ErrorCodes.TransferEventMismatch, winning.ErrorReason);
        Assert.Equal(facilitatorBefore + 2, await X402Permit2DevChain.TransactionCountAsync(_chain.Node, X402DevChainFixture.FacilitatorAddress));
        Assert.Equal(BigInteger.One, await X402Permit2DevChain.TransactionCountAsync(_chain.Node, payer.GetPublicAddress()));
    }

    [Fact]
    [Trait("Rule", "S2")]
    public async Task Given_ASettleWhoseVerificationFailsAfterTheLockWasTaken_When_SettlingTheSameApprovalAgain_Then_TheLockWasReleased()
    {
        var payer = EthECKey.GenerateKey();
        var service = SponsoringServiceFor(_chain.Client);
        var spec = new SignedApprovalSpec(ChainId, _chain.TokenAddress, 0, GoodMaxFee);
        var info = Erc20ApprovalTransactions.Info(payer.GetPublicAddress(), spec, Erc20ApprovalTransactions.Sign(payer, spec));
        var payload = X402Permit2DevChain.Payload(payer, ChainId, _chain.TokenAddress, Erc20ApprovalTransactions.Extension(info));

        var first = await service.SettlePaymentAsync(payload, payload.Accepted);
        var second = await service.SettlePaymentAsync(payload, payload.Accepted);

        Assert.Equal(X402ErrorCodes.Permit2InsufficientBalance, first.ErrorReason);
        Assert.Equal(X402ErrorCodes.Permit2InsufficientBalance, second.ErrorReason);
    }

    private static Task ReceiptNeverReturns(string method) =>
        method == InterceptingRpcClient.ReceiptMethod ? InterceptingRpcClient.Never : Task.CompletedTask;

    private X402ExactPermit2Service ServiceFor(IClient client) =>
        new(FacilitatorKey, new Dictionary<int, IClient> { [ChainId] = client });

    private X402ExactPermit2Service SponsoringServiceFor(IClient client) =>
        new(FacilitatorKey, new Dictionary<int, IClient> { [ChainId] = client },
            new Erc20ApprovalGasSponsoringOptions { Enabled = true, MaxSponsoredGasCost = MaxSponsoredGasCost });

    private Erc20ApprovalSponsoredSettlementRequest SponsoredRequest(
        IClient client,
        EthECKey payer,
        BigInteger nonce,
        int maxTimeoutSeconds,
        Action<CancellationToken> onSettle)
    {
        var facilitator = new Nethereum.Web3.Web3(new Account(FacilitatorKey, ChainId), client);
        var spec = new SignedApprovalSpec(ChainId, _chain.TokenAddress, nonce, GoodMaxFee);
        return new Erc20ApprovalSponsoredSettlementRequest(
            facilitator,
            Caip2.FormatEip155(ChainId),
            payer.GetPublicAddress(),
            Erc20ApprovalTransactions.Sign(payer, spec),
            ApprovalGasCost,
            token =>
            {
                onSettle(token);
                return Task.FromResult(new SettlementResponse { Success = true, Transaction = "0x01" });
            })
        {
            MaxTimeoutSeconds = maxTimeoutSeconds
        };
    }

    private sealed class SecondChain : IDisposable
    {
        public SecondChain(DevChainNode node, string token)
        {
            Node = node;
            Token = token;
        }

        public DevChainNode Node { get; }

        public string Token { get; }

        public void Dispose() => Node.Dispose();
    }

    private async Task<SecondChain> StartSecondChainAsync()
    {
        var node = DevChainNode.CreateInMemory(new DevChainConfig
        {
            ChainId = SecondChainId,
            BaseFee = 1_000_000_000,
            BlockGasLimit = 30_000_000,
            AutoMine = true
        });
        await node.StartAsync(
            new[] { X402DevChainFixture.FacilitatorAddress, X402DevChainFixture.PayerAddress },
            Nethereum.Web3.Web3.Convert.ToWei(10000));

        var deployer = new Account(X402DevChainFixture.PayerKey, SecondChainId);
        var usdc = new USDCDeploymentHelper((Nethereum.Web3.Web3)node.CreateWeb3(deployer), deployer);
        var token = await usdc.DeployAsync(X402DevChainFixture.TokenName, "USDC", 6, X402DevChainFixture.TokenVersion);
        await usdc.MintAsync(X402DevChainFixture.PayerAddress, BigInteger.Parse(X402DevChainFixture.PayerInitialBalance));
        await X402Permit2DevChain.InstallPermit2StubsAsync(node);
        await X402Permit2DevChain.ApprovePermit2Async(node, _fundedPayer, SecondChainId, token);
        return new SecondChain(node, token);
    }
}
