using System.Diagnostics;
using System.Numerics;
using Nethereum.DevChain;
using Nethereum.JsonRpc.Client;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Facilitator;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Blockchain;

[Collection("X402 DevChain E2E")]
public class X402SettlementRobustnessDevChainTests : IClassFixture<X402DevChainFixture>, IAsyncLifetime
{
    private const int ChainId = X402DevChainFixture.ChainId;
    private const string FacilitatorKey = X402DevChainFixture.FacilitatorKey;
    private static readonly BigInteger GoodMaxFee = 1_000_000_000;

    private readonly X402DevChainFixture _chain;
    private readonly EthECKey _payer = new(X402DevChainFixture.PayerKey.Substring(2));

    public X402SettlementRobustnessDevChainTests(X402DevChainFixture chain)
    {
        _chain = chain;
    }

    public async Task InitializeAsync()
    {
        await X402Permit2DevChain.InstallPermit2StubsAsync(_chain.Node);
        await X402Permit2DevChain.ApprovePermit2Async(_chain.Node, _payer, ChainId, _chain.TokenAddress);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Trait("Rule", "V7")]
    public async Task Given_AnEip3009ServiceAndAPermit2ServiceBuiltForTheSameAccount_When_TheySettleConcurrentlyOnOneChain_Then_TheTransactionsHaveDistinctConsecutiveNonces()
    {
        var rpc = new InterceptingRpcClient(_chain.Client, method =>
            method == "eth_getTransactionCount" ? Task.Delay(400) : Task.CompletedTask);
        var eip3009 = new X402TransferWithAuthorisation3009Service(FacilitatorKey, Clients(rpc));
        var permit2 = new X402ExactPermit2Service(FacilitatorKey, Clients(rpc));
        var requirements = X402DevChainPayments.Requirements(_chain, "100000", X402DevChainFixture.RecipientAddress);
        var transfer = await X402DevChainPayments.SignedTransfer(_chain, requirements);
        var permit2Payload = X402Permit2DevChain.Payload(_payer, ChainId, _chain.TokenAddress);
        var before = await FacilitatorTransactionCountAsync();

        var settles = await Task.WhenAll(
            eip3009.SettlePaymentAsync(transfer, requirements),
            permit2.SettlePaymentAsync(permit2Payload, permit2Payload.Accepted));

        Assert.True(settles[0].Success, settles[0].ErrorReason);
        Assert.False(string.IsNullOrEmpty(settles[1].Transaction), settles[1].ErrorReason);
        var nonces = new List<BigInteger>();
        foreach (var settle in settles)
        {
            var transaction = await _chain.Node.CreateWeb3().Eth.Transactions.GetTransactionByHash.SendRequestAsync(settle.Transaction);
            nonces.Add(transaction.Nonce.Value);
        }

        nonces.Sort();
        Assert.Equal(new[] { before, before + 1 }, nonces);
    }

    [Fact]
    [Trait("Rule", "V13")]
    public async Task Given_AnEnormousMaxTimeoutAfterTheBroadcast_When_SettlingAnEip3009Payment_Then_SettlementPendingCarriesTheHashInsteadOfAFailure()
    {
        var service = new X402TransferWithAuthorisation3009Service(
            FacilitatorKey, Clients(new InterceptingRpcClient(_chain.Client, ReceiptNeverReturns)),
            new X402FacilitatorOptions { MaxReceiptWaitSeconds = 2 });
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);
        requirements.MaxTimeoutSeconds = int.MaxValue;
        var elapsed = Stopwatch.StartNew();

        var settle = await service.SettlePaymentAsync(payload, requirements).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(X402ErrorCodes.SettlementPending, settle.ErrorReason);
        Assert.False(string.IsNullOrEmpty(settle.Transaction));
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(30), elapsed.Elapsed.ToString());
        Assert.NotNull(await _chain.Node.CreateWeb3().Eth.Transactions.GetTransactionByHash.SendRequestAsync(settle.Transaction));
    }

    [Fact]
    [Trait("Rule", "V13")]
    public async Task Given_AnEnormousMaxTimeoutAfterTheBroadcast_When_SettlingAPermit2Payment_Then_SettlementPendingCarriesTheHashInsteadOfAFailure()
    {
        var service = new X402ExactPermit2Service(
            FacilitatorKey, Clients(new InterceptingRpcClient(_chain.Client, ReceiptNeverReturns)),
            (Erc20ApprovalGasSponsoringOptions?)null, new X402FacilitatorOptions { MaxReceiptWaitSeconds = 2 });
        var payload = X402Permit2DevChain.Payload(_payer, ChainId, _chain.TokenAddress, maxTimeoutSeconds: int.MaxValue);
        var elapsed = Stopwatch.StartNew();

        var settle = await service.SettlePaymentAsync(payload, payload.Accepted).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(X402ErrorCodes.SettlementPending, settle.ErrorReason);
        Assert.False(string.IsNullOrEmpty(settle.Transaction));
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(30), elapsed.Elapsed.ToString());
    }

    [Fact]
    [Trait("Rule", "V13")]
    public async Task Given_AnEnormousMaxTimeoutAfterTheApprovalWasBroadcast_When_SettlingASponsoredApproval_Then_SettlementPendingCarriesTheApprovalHash()
    {
        var payer = EthECKey.GenerateKey();
        await X402Permit2DevChain.GiveTokensAsync(
            _chain.Node, X402DevChainFixture.PayerKey, ChainId, _chain.TokenAddress, payer.GetPublicAddress(), 2_000_000);
        var facilitator = _chain.Node.CreateWeb3(new Account(FacilitatorKey, ChainId));
        await facilitator.Eth.GetEtherTransferService().TransferEtherAndWaitForReceiptAsync(payer.GetPublicAddress(), 1m);
        var service = new X402ExactPermit2Service(
            FacilitatorKey, Clients(new InterceptingRpcClient(_chain.Client, ReceiptNeverReturns)),
            new Erc20ApprovalGasSponsoringOptions { Enabled = true, MaxSponsoredGasCost = BigInteger.Parse("10000000000000000") },
            new X402FacilitatorOptions { MaxReceiptWaitSeconds = 2 });
        var spec = new SignedApprovalSpec(ChainId, _chain.TokenAddress, 0, GoodMaxFee);
        var info = Erc20ApprovalTransactions.Info(payer.GetPublicAddress(), spec, Erc20ApprovalTransactions.Sign(payer, spec));
        var payload = X402Permit2DevChain.Payload(
            payer, ChainId, _chain.TokenAddress, Erc20ApprovalTransactions.Extension(info), maxTimeoutSeconds: int.MaxValue);

        var settle = await service.SettlePaymentAsync(payload, payload.Accepted).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(X402ErrorCodes.SettlementPending, settle.ErrorReason);
        var approval = await _chain.Node.CreateWeb3().Eth.Transactions.GetTransactionByHash.SendRequestAsync(settle.Transaction);
        Assert.Equal(payer.GetPublicAddress().ToLowerInvariant(), approval.From.ToLowerInvariant());
        Assert.Equal(_chain.TokenAddress.ToLowerInvariant(), approval.To.ToLowerInvariant());
    }

    [Fact]
    [Trait("Rule", "V16")]
    public async Task Given_TwoRequiredConfirmationsAndNoFurtherBlock_When_Settling_Then_SettlementPendingCarriesTheHash()
    {
        var service = new X402TransferWithAuthorisation3009Service(
            FacilitatorKey, Clients(_chain.Client), new X402FacilitatorOptions { RequiredConfirmations = 2 });
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);
        requirements.MaxTimeoutSeconds = 2;
        var elapsed = Stopwatch.StartNew();

        var settle = await service.SettlePaymentAsync(payload, requirements).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(X402ErrorCodes.SettlementPending, settle.ErrorReason);
        Assert.False(string.IsNullOrEmpty(settle.Transaction));
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(30), elapsed.Elapsed.ToString());
    }

    [Fact]
    [Trait("Rule", "V16")]
    public async Task Given_TwoRequiredConfirmationsAndABlockMinedAfterTheReceipt_When_Settling_Then_TheSettlementSucceeds()
    {
        var mined = 0;
        var rpc = new InterceptingRpcClient(_chain.Client, async method =>
        {
            if (method == "eth_blockNumber" && Interlocked.Increment(ref mined) == 1)
                await _chain.Node.MineBlockAsync();
        });
        var service = new X402TransferWithAuthorisation3009Service(
            FacilitatorKey, Clients(rpc), new X402FacilitatorOptions { RequiredConfirmations = 2 });
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);

        var settle = await service.SettlePaymentAsync(payload, requirements).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.True(settle.Success, settle.ErrorReason);
        Assert.True(mined >= 1);
    }

    [Fact]
    [Trait("Rule", "V16")]
    public async Task Given_TwoRequiredConfirmationsOnAPermit2Settlement_When_ABlockIsMinedAfterTheReceipt_Then_TheSettleIsNotPending()
    {
        var mined = 0;
        var rpc = new InterceptingRpcClient(_chain.Client, async method =>
        {
            if (method == "eth_blockNumber" && Interlocked.Increment(ref mined) == 2)
                await _chain.Node.MineBlockAsync();
        });
        var service = new X402ExactPermit2Service(
            FacilitatorKey, Clients(rpc), (Erc20ApprovalGasSponsoringOptions?)null, new X402FacilitatorOptions { RequiredConfirmations = 2 });
        var payload = X402Permit2DevChain.Payload(_payer, ChainId, _chain.TokenAddress);

        var settle = await service.SettlePaymentAsync(payload, payload.Accepted).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.NotEqual(X402ErrorCodes.SettlementPending, settle.ErrorReason);
        Assert.False(string.IsNullOrEmpty(settle.Transaction));
    }

    [Fact]
    [Trait("Rule", "V16")]
    public async Task Given_TheDefaultOfOneConfirmation_When_Settling_Then_NoBlockNumberIsPolled()
    {
        var rpc = new InterceptingRpcClient(_chain.Client);
        var service = new X402TransferWithAuthorisation3009Service(FacilitatorKey, Clients(rpc));
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);

        var settle = await service.SettlePaymentAsync(payload, requirements);

        Assert.True(settle.Success, settle.ErrorReason);
        Assert.DoesNotContain("eth_blockNumber", rpc.Methods);
    }

    private static Task ReceiptNeverReturns(string method) =>
        method == InterceptingRpcClient.ReceiptMethod ? InterceptingRpcClient.Never : Task.CompletedTask;

    private Task<BigInteger> FacilitatorTransactionCountAsync() =>
        X402Permit2DevChain.TransactionCountAsync(_chain.Node, X402DevChainFixture.FacilitatorAddress);

    private static Dictionary<int, IClient> Clients(IClient client) => new() { [ChainId] = client };
}
