using System.Numerics;
using Nethereum.DevChain;
using Nethereum.JsonRpc.Client;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;

namespace Nethereum.X402.IntegrationTests.Blockchain;

[Collection("X402 DevChain E2E")]
public class X402SendLockBoundDevChainTests : IClassFixture<X402DevChainFixture>
{
    private static readonly BigInteger FarFuture = new(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600);

    private readonly X402DevChainFixture _chain;

    public X402SendLockBoundDevChainTests(X402DevChainFixture chain)
    {
        _chain = chain;
    }

    [Fact]
    [Trait("Rule", "W8")]
    public async Task Given_FourSettlementsQueuedOnTheSendLockWithASlowNonceRead_When_TheyAllSettle_Then_EveryPaidSettlementIsReportedSuccess()
    {
        var rpc = new InterceptingRpcClient(_chain.Client, async method =>
        {
            if (method == "eth_getTransactionCount")
                await Task.Delay(1000);
            if (method == "eth_getTransactionReceipt")
                await Task.Delay(50);
        });
        var service = new X402TransferWithAuthorisation3009Service(X402DevChainFixture.FacilitatorKey, Clients(rpc));
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        requirements.MaxTimeoutSeconds = 4;
        var payloads = new List<PaymentPayload>();
        for (var i = 0; i < 4; i++)
            payloads.Add(await X402DevChainPayments.SignedTransfer(_chain, requirements, validBefore: FarFuture));
        var before = await PayerBalanceAsync();

        var settles = await Task.WhenAll(payloads.Select(payload => service.SettlePaymentAsync(payload, requirements)));

        var after = await PayerBalanceAsync();
        Assert.All(settles, settle => Assert.True(settle.Success, settle.ErrorReason));
        Assert.Equal(before - after, 4 * BigInteger.Parse(requirements.Amount));
    }

    [Fact]
    [Trait("Rule", "W8")]
    public async Task Given_ASettlementWaitingOnTheSendLockLongerThanItsBound_When_TheLockIsNotAcquired_Then_ItIsUnexpectedSettleErrorWithNothingBroadcast()
    {
        var nonceReads = 0;
        var rpc = new InterceptingRpcClient(_chain.Client, async method =>
        {
            if (method == "eth_getTransactionCount" && Interlocked.Increment(ref nonceReads) == 1)
                await Task.Delay(3000);
        });
        var service = new X402TransferWithAuthorisation3009Service(X402DevChainFixture.FacilitatorKey, Clients(rpc));
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        requirements.MaxTimeoutSeconds = 1;
        var holder = await X402DevChainPayments.SignedTransfer(_chain, requirements, validBefore: FarFuture);
        var waiter = await X402DevChainPayments.SignedTransfer(_chain, requirements, validBefore: FarFuture);
        var before = await PayerBalanceAsync();

        var holding = service.SettlePaymentAsync(holder, requirements);
        await Task.Delay(300);
        var waiting = await service.SettlePaymentAsync(waiter, requirements);
        await holding;

        var after = await PayerBalanceAsync();
        Assert.False(waiting.Success);
        Assert.Equal(X402ErrorCodes.UnexpectedSettleError, waiting.ErrorReason);
        Assert.True(string.IsNullOrEmpty(waiting.Transaction));
        Assert.Equal(before - after, BigInteger.Parse(requirements.Amount));
    }

    private Task<BigInteger> PayerBalanceAsync() =>
        _chain.Node.CreateWeb3().Eth.ERC20.GetContractService(_chain.TokenAddress).BalanceOfQueryAsync(X402DevChainFixture.PayerAddress);

    private static Dictionary<int, IClient> Clients(IClient client) => new() { [X402DevChainFixture.ChainId] = client };
}
