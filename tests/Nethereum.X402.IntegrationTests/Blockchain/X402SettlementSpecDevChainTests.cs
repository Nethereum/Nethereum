using System.Linq;
using System.Numerics;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.DevChain;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Blockchain;

[Collection("X402 DevChain E2E")]
public class X402SettlementSpecDevChainTests : IClassFixture<X402DevChainFixture>
{
    private const string BlocklistedPayTo = "0x90F79bf6EB2c4f870365E785982E1f101E93b906";
    private const int BlockTimeBufferSeconds = 6;

    private readonly X402DevChainFixture _chain;

    public X402SettlementSpecDevChainTests(X402DevChainFixture chain)
    {
        _chain = chain;
    }

    [Fact]
    public async Task Given_ValidTransferPayment_When_Settled_Then_ReceiptHasExactlyOneTransferOfTheAmountFromPayerToPayTo()
    {
        var service = TransferService(_chain.Client);
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);

        var settle = await service.SettlePaymentAsync(payload, requirements);

        Assert.True(settle.Success, settle.ErrorReason);
        var receipt = await Web3().Eth.Transactions.GetTransactionReceipt.SendRequestAsync(settle.Transaction);
        var payer = X402DevChainFixture.PayerAddress;
        var payTo = X402DevChainFixture.RecipientAddress;
        var transfer = Assert.Single(receipt.DecodeAllEvents<TransferEventDTO>()
            .Where(decoded => decoded.Log.Address.IsTheSameAddress(_chain.TokenAddress)));
        Assert.True(transfer.Event.From.IsTheSameAddress(payer));
        Assert.True(transfer.Event.To.IsTheSameAddress(payTo));
        Assert.Equal(new BigInteger(1000000), transfer.Event.Value);
    }

    [Fact]
    public async Task Given_TokenThatMovesFundsWithoutATransferEvent_When_Settled_Then_TransferEventMismatchCarriesTheTransactionHash()
    {
        await X402DevChainPayments.SetSuppressTransferEventAsync(_chain, true);
        try
        {
            var service = TransferService(_chain.Client);
            var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
            var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);

            var settle = await service.SettlePaymentAsync(payload, requirements);

            Assert.False(settle.Success);
            Assert.Equal(X402ErrorCodes.TransferEventMismatch, settle.ErrorReason);
            Assert.StartsWith("0x", settle.Transaction);
            Assert.Equal(requirements.Network, settle.Network);
            Assert.Equal(X402DevChainFixture.PayerAddress, settle.Payer);
        }
        finally
        {
            await X402DevChainPayments.SetSuppressTransferEventAsync(_chain, false);
        }
    }

    [Fact]
    public async Task Given_ValidReceivePayment_When_Settled_Then_TokensMoveToThePayeeAndTheTransferEventIsFound()
    {
        var service = ReceiveService(_chain.Client);
        var requirements = X402DevChainPayments.Requirements(_chain, "250000", X402DevChainFixture.FacilitatorAddress);
        var payload = await X402DevChainPayments.SignedReceive(_chain, requirements);

        var verify = await service.VerifyPaymentAsync(payload, requirements);
        Assert.True(verify.IsValid, verify.InvalidReason);

        var settle = await service.SettlePaymentAsync(payload, requirements);
        Assert.True(settle.Success, settle.ErrorReason);
        Assert.StartsWith("0x", settle.Transaction);
    }

    [Fact]
    public async Task Given_TransferSignatureWithALeadingZeroByteInROrS_When_VerifiedAndSettled_Then_ItIsAcceptedAndTheTokensMove()
    {
        var service = TransferService(_chain.Client);
        var requirements = X402DevChainPayments.Requirements(_chain, "1000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedWithShortSignatureComponent(_chain, requirements, receive: false);
        var before = await BalanceAsync(X402DevChainFixture.RecipientAddress);

        var verify = await service.VerifyPaymentAsync(payload, requirements);
        Assert.True(verify.IsValid, verify.InvalidReason);

        var settle = await service.SettlePaymentAsync(payload, requirements);
        Assert.True(settle.Success, settle.ErrorReason);
        Assert.Equal(before + 1000, await BalanceAsync(X402DevChainFixture.RecipientAddress));
    }

    [Fact]
    public async Task Given_ReceiveSignatureWithALeadingZeroByteInROrS_When_VerifiedAndSettled_Then_ItIsAcceptedAndTheTokensMove()
    {
        var service = ReceiveService(_chain.Client);
        var requirements = X402DevChainPayments.Requirements(_chain, "1000", X402DevChainFixture.FacilitatorAddress);
        var payload = await X402DevChainPayments.SignedWithShortSignatureComponent(_chain, requirements, receive: true);
        var before = await BalanceAsync(X402DevChainFixture.FacilitatorAddress);

        var verify = await service.VerifyPaymentAsync(payload, requirements);
        Assert.True(verify.IsValid, verify.InvalidReason);

        var settle = await service.SettlePaymentAsync(payload, requirements);
        Assert.True(settle.Success, settle.ErrorReason);
        Assert.Equal(before + 1000, await BalanceAsync(X402DevChainFixture.FacilitatorAddress));
    }

    [Fact]
    public async Task Given_AuthorizationValidBeforeWithinTheBlockTimeBuffer_When_Verifying_Then_InvalidValidBefore()
    {
        var service = TransferService(_chain.Client);
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var blockTimestamp = await LatestBlockTimestampAsync();
        var payload = await X402DevChainPayments.SignedTransfer(
            _chain, requirements,
            validAfter: blockTimestamp - 600,
            validBefore: blockTimestamp + BlockTimeBufferSeconds);

        var verify = await service.VerifyPaymentAsync(payload, requirements);

        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.InvalidValidBefore, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_PayToTheTokenBlocklists_When_Verifying_Then_SimulationFailedAlthoughEveryStaticCheckPasses()
    {
        var service = TransferService(_chain.Client);
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", BlocklistedPayTo);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);
        await X402DevChainPayments.SetBlocklistedAsync(_chain, BlocklistedPayTo, true);
        var balanceBefore = await BalanceAsync(X402DevChainFixture.PayerAddress);

        var verify = await service.VerifyPaymentAsync(payload, requirements);
        var settle = await service.SettlePaymentAsync(payload, requirements);

        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.TransactionSimulationFailed, verify.InvalidReason);
        Assert.Equal(X402DevChainFixture.PayerAddress, verify.Payer);
        Assert.False(settle.Success);
        Assert.Equal(X402ErrorCodes.TransactionSimulationFailed, settle.ErrorReason);
        Assert.Equal(string.Empty, settle.Transaction);
        Assert.Equal(balanceBefore, await BalanceAsync(X402DevChainFixture.PayerAddress));
    }

    [Fact]
    public async Task Given_ReceiptCannotBeObtained_When_Settled_Then_SettlementPendingCarriesTheBroadcastHashAndTheTokenMoved()
    {
        var service = TransferService(new ReceiptUnavailableClient(_chain.Client));
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);
        var recipientBefore = await BalanceAsync(X402DevChainFixture.RecipientAddress);

        var settle = await service.SettlePaymentAsync(payload, requirements);

        Assert.False(settle.Success);
        Assert.Equal(X402ErrorCodes.SettlementPending, settle.ErrorReason);
        Assert.False(string.IsNullOrEmpty(settle.Transaction));
        Assert.Equal(requirements.Network, settle.Network);
        Assert.Equal(X402DevChainFixture.PayerAddress, settle.Payer);

        var transaction = await Web3().Eth.Transactions.GetTransactionByHash.SendRequestAsync(settle.Transaction);
        Assert.NotNull(transaction);
        var receipt = await Web3().Eth.Transactions.GetTransactionReceipt.SendRequestAsync(settle.Transaction);
        Assert.Equal(1, (int)receipt.Status.Value);
        Assert.Equal(recipientBefore + 1000000, await BalanceAsync(X402DevChainFixture.RecipientAddress));
    }

    [Fact]
    public async Task Given_ReceiptCannotBeObtained_When_ReceiveSettled_Then_SettlementPendingCarriesTheBroadcastHash()
    {
        var service = ReceiveService(new ReceiptUnavailableClient(_chain.Client));
        var requirements = X402DevChainPayments.Requirements(_chain, "250000", X402DevChainFixture.FacilitatorAddress);
        var payload = await X402DevChainPayments.SignedReceive(_chain, requirements);

        var settle = await service.SettlePaymentAsync(payload, requirements);

        Assert.Equal(X402ErrorCodes.SettlementPending, settle.ErrorReason);
        var receipt = await Web3().Eth.Transactions.GetTransactionReceipt.SendRequestAsync(settle.Transaction);
        Assert.Equal(1, (int)receipt.Status.Value);
    }

    [Fact]
    public async Task Given_SettleFailsBeforeBroadcast_When_Settled_Then_TransactionIsEmptyNotNull()
    {
        var service = TransferService(_chain.Client);
        var requirements = X402DevChainPayments.Requirements(_chain, "100000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);

        var settle = await service.SettlePaymentAsync(payload, requirements);

        Assert.False(settle.Success);
        Assert.Equal(X402ErrorCodes.InsufficientFunds, settle.ErrorReason);
        Assert.Equal(string.Empty, settle.Transaction);
    }

    private X402TransferWithAuthorisation3009Service TransferService(IClient client) =>
        new(X402DevChainFixture.FacilitatorKey, new Dictionary<int, IClient> { [X402DevChainFixture.ChainId] = client });

    private X402ReceiveWithAuthorisation3009Service ReceiveService(IClient client) =>
        new(X402DevChainFixture.FacilitatorKey, new Dictionary<int, IClient> { [X402DevChainFixture.ChainId] = client });

    private Nethereum.Web3.IWeb3 Web3() => _chain.Node.CreateWeb3();

    private async Task<BigInteger> BalanceAsync(string address) =>
        await Web3().Eth.ERC20.GetContractService(_chain.TokenAddress).BalanceOfQueryAsync(address);

    private async Task<BigInteger> LatestBlockTimestampAsync()
    {
        var block = await Web3().Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(BlockParameter.CreateLatest());
        return block.Timestamp.Value;
    }
}
