using System.Diagnostics;
using System.Numerics;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.X402.Signers;
using Nethereum.JsonRpc.Client;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Blockchain;

[Collection("X402 DevChain E2E")]
public class X402Eip3009FacilitatorTransactionTests : IClassFixture<X402DevChainFixture>
{
    private readonly X402DevChainFixture _chain;

    public X402Eip3009FacilitatorTransactionTests(X402DevChainFixture chain)
    {
        _chain = chain;
    }

    [Fact]
    [Trait("Rule", "S1")]
    public async Task Given_TheTransferWithAuthorizationReceiptNeverReturns_When_Settling_Then_SettlementPendingWithinTheBound()
    {
        var service = new X402TransferWithAuthorisation3009Service(
            X402DevChainFixture.FacilitatorKey, Clients(new InterceptingRpcClient(_chain.Client, ReceiptNeverReturns)));
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);
        requirements.MaxTimeoutSeconds = 2;
        var elapsed = Stopwatch.StartNew();

        var settle = await service.SettlePaymentAsync(payload, requirements).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(X402ErrorCodes.SettlementPending, settle.ErrorReason);
        Assert.False(string.IsNullOrEmpty(settle.Transaction));
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(30), elapsed.Elapsed.ToString());
        Assert.NotNull(await _chain.Node.CreateWeb3().Eth.Transactions.GetTransactionByHash.SendRequestAsync(settle.Transaction));
    }

    [Fact]
    [Trait("Rule", "S1")]
    public async Task Given_TheReceiveWithAuthorizationReceiptNeverReturns_When_Settling_Then_SettlementPendingWithinTheBound()
    {
        var service = new X402ReceiveWithAuthorisation3009Service(
            X402DevChainFixture.FacilitatorKey, Clients(new InterceptingRpcClient(_chain.Client, ReceiptNeverReturns)));
        var requirements = X402DevChainPayments.Requirements(_chain, "250000", X402DevChainFixture.FacilitatorAddress);
        var payload = await X402DevChainPayments.SignedReceive(_chain, requirements);
        requirements.MaxTimeoutSeconds = 2;
        var elapsed = Stopwatch.StartNew();

        var settle = await service.SettlePaymentAsync(payload, requirements).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(X402ErrorCodes.SettlementPending, settle.ErrorReason);
        Assert.False(string.IsNullOrEmpty(settle.Transaction));
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(30), elapsed.Elapsed.ToString());
        Assert.NotNull(await _chain.Node.CreateWeb3().Eth.Transactions.GetTransactionByHash.SendRequestAsync(settle.Transaction));
    }

    [Fact]
    [Trait("Rule", "S6")]
    public async Task Given_TwoConcurrentTransferSettlesOnOneChain_When_Sending_Then_TheTransactionsHaveDistinctConsecutiveNonces()
    {
        var service = new X402TransferWithAuthorisation3009Service(X402DevChainFixture.FacilitatorKey, Clients(_chain.Client));
        var requirements = X402DevChainPayments.Requirements(_chain, "100000", X402DevChainFixture.RecipientAddress);
        var first = await X402DevChainPayments.SignedTransfer(_chain, requirements);
        var second = await X402DevChainPayments.SignedTransfer(_chain, requirements);
        var before = await X402Permit2DevChain.TransactionCountAsync(_chain.Node, X402DevChainFixture.FacilitatorAddress);

        var settles = await Task.WhenAll(
            service.SettlePaymentAsync(first, requirements),
            service.SettlePaymentAsync(second, requirements));

        Assert.All(settles, settle => Assert.True(settle.Success, settle.ErrorReason));
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
    [Trait("Rule", "S1")]
    public async Task Given_TheCancellationReceiptNeverReturnsAndTheCallerTokenExpires_When_Cancelling_Then_TheCallReturnsInsteadOfWaitingForever()
    {
        var service = new X402TransferWithAuthorisation3009Service(
            X402DevChainFixture.FacilitatorKey, Clients(new InterceptingRpcClient(_chain.Client, ReceiptNeverReturns)));
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);
        var nonce = ((ExactSchemePayload)payload.Payload!).Authorization.Nonce.HexToByteArray();
        var signature = EthECDSASignature.CreateStringSignature(await new TransferWithAuthorisationSigner().SignCancelWithPrivateKeyAsync(
            X402DevChainFixture.PayerAddress, nonce, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion,
            X402DevChainFixture.ChainId, _chain.TokenAddress, X402DevChainFixture.PayerKey));
        using var expiry = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var cancel = await service.CancelAuthorizationAsync(
            X402DevChainFixture.PayerAddress, nonce, signature, Caip2.FormatEip155(X402DevChainFixture.ChainId),
            _chain.TokenAddress, expiry.Token).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.False(cancel.Success);
        Assert.Equal(X402ErrorCodes.CancellationError, cancel.ErrorReason);
        var token = new Nethereum.Contracts.EIP3009.EIP3009.Eip3009Service(_chain.Node.CreateWeb3(), _chain.TokenAddress);
        Assert.True(await token.AuthorizationStateQueryAsync(X402DevChainFixture.PayerAddress, nonce));
    }

    private static Task ReceiptNeverReturns(string method) =>
        method == InterceptingRpcClient.ReceiptMethod ? InterceptingRpcClient.Never : Task.CompletedTask;

    private static Dictionary<int, IClient> Clients(IClient client) => new() { [X402DevChainFixture.ChainId] = client };
}
