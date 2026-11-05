using Nethereum.JsonRpc.Client;
using Nethereum.Signer;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Facilitator;
using Nethereum.X402.IntegrationTests.Helpers;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Blockchain;

[Collection("X402 DevChain E2E")]
public class X402FacilitatorClusterRecoveryDevChainTests : IClassFixture<X402DevChainFixture>
{
    private const string SecondFacilitatorKey = "0x5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdb9ee6a";

    private readonly X402DevChainFixture _chain;

    public X402FacilitatorClusterRecoveryDevChainTests(X402DevChainFixture chain)
    {
        _chain = chain;
    }

    [Fact(Timeout = 120000)]
    [Trait("Rule", "X2")]
    public async Task Given_TwoFacilitatorKeysListingBothAddresses_When_BothSettleOnePayment_Then_OnlyOneReportsSuccess()
    {
        var addresses = new[]
        {
            X402DevChainFixture.FacilitatorAddress,
            new EthECKey(SecondFacilitatorKey.Substring(2)).GetPublicAddress()
        };
        var req = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, req);

        var bVerified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var aDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var nonceReads = 0;
        var rpcB = new InterceptingRpcClient(_chain.Client, async method =>
        {
            if (method == "eth_getTransactionCount" && Interlocked.Increment(ref nonceReads) == 1)
            {
                bVerified.TrySetResult();
                await aDone.Task;
            }
        });

        var optionsA = new X402FacilitatorOptions();
        var optionsB = new X402FacilitatorOptions();
        foreach (var address in addresses)
        {
            optionsA.FacilitatorAddresses.Add(address);
            optionsB.FacilitatorAddresses.Add(address);
        }

        var a = new X402TransferWithAuthorisation3009Service(
            X402DevChainFixture.FacilitatorKey, new Dictionary<int, IClient> { [X402DevChainFixture.ChainId] = _chain.Client }, optionsA);
        var b = new X402TransferWithAuthorisation3009Service(
            SecondFacilitatorKey, new Dictionary<int, IClient> { [X402DevChainFixture.ChainId] = rpcB }, optionsB);

        var settledByB = b.SettlePaymentAsync(payload, req);
        await bVerified.Task;
        var resultA = await a.SettlePaymentAsync(payload, req);
        aDone.SetResult();
        var resultB = await settledByB;

        Assert.True(resultA.Success);
        Assert.False(resultB.Success);
    }
}
