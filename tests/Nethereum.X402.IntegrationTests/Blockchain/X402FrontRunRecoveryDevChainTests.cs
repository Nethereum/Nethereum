using System.Numerics;
using Nethereum.Contracts.EIP3009.EIP3009.ContractDefinition;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Blockchain;

[Collection("X402 DevChain E2E")]
public class X402FrontRunRecoveryDevChainTests : IClassFixture<X402DevChainFixture>, IAsyncLifetime
{
    private const int ChainId = X402DevChainFixture.ChainId;
    private const string FacilitatorKey = X402DevChainFixture.FacilitatorKey;

    private static readonly byte[] Permit2BitmapSetByAValueCall = string.Concat(
        "34", "15", "600c", "57", "6001", "34", "14", "6000", "55", "5b", "6000", "54", "6000", "03", "6000", "52", "6020", "6000", "f3")
        .HexToByteArray();

    private static readonly byte[] ProxyRevertsOnceFlagged = string.Concat(
        "34", "15", "600d", "57", "6001", "34", "14", "6000", "55", "00", "5b", "6000", "54", "6015", "57", "00", "5b", "6000", "6000", "fd")
        .HexToByteArray();

    private readonly X402DevChainFixture _chain;
    private readonly EthECKey _payer = new(X402DevChainFixture.PayerKey.Substring(2));

    public X402FrontRunRecoveryDevChainTests(X402DevChainFixture chain)
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
    [Trait("Rule", "V14")]
    public async Task Given_AThirdPartySubmitsThePayersAuthorizationFirst_When_TheFacilitatorSettleReverts_Then_ItReportsSuccessWithTheThirdPartysHash()
    {
        TransactionReceipt? thirdParty = null;
        PaymentPayload payload = null!;
        var rpc = FrontRunAtNonceRead(async () => thirdParty = await ThirdPartySettleAsync(payload));
        var service = new X402TransferWithAuthorisation3009Service(FacilitatorKey, Clients(rpc));
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);
        var facilitatorBefore = await FacilitatorTransactionCountAsync();

        var settle = await service.SettlePaymentAsync(payload, requirements);

        Assert.NotNull(thirdParty);
        Assert.True(settle.Success, settle.ErrorReason);
        Assert.Equal(thirdParty!.TransactionHash, settle.Transaction);
        Assert.Equal(X402DevChainFixture.PayerAddress, settle.Payer);
        Assert.Equal(facilitatorBefore, await FacilitatorTransactionCountAsync());
    }

    [Fact]
    [Trait("Rule", "V14")]
    public async Task Given_ARevertThatIsNotAFrontRun_When_TheFacilitatorSettleReverts_Then_TheFailureStandsAndNoSuccessIsReported()
    {
        var rpc = FrontRunAtNonceRead(() => X402DevChainPayments.SetBlocklistedAsync(_chain, X402DevChainFixture.PayerAddress, true));
        var service = new X402TransferWithAuthorisation3009Service(FacilitatorKey, Clients(rpc));
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);
        try
        {
            var settle = await service.SettlePaymentAsync(payload, requirements);

            Assert.False(settle.Success);
            Assert.Equal(X402ErrorCodes.UnexpectedSettleError, settle.ErrorReason);
        }
        finally
        {
            await X402DevChainPayments.SetBlocklistedAsync(_chain, X402DevChainFixture.PayerAddress, false);
        }
    }

    [Fact]
    [Trait("Rule", "V14")]
    public async Task Given_ThePayerPaidPayToWithAPlainTransferAndCancelledThePermit2Nonce_When_TheFacilitatorSettleReverts_Then_TheFailureStands()
    {
        await _chain.Node.SetCodeAsync(X402Permit2Addresses.Permit2, Permit2BitmapSetByAValueCall);
        await _chain.Node.SetCodeAsync(X402Permit2Addresses.ExactPermit2Proxy, ProxyRevertsOnceFlagged);
        await SendValueAsync(X402Permit2Addresses.ExactPermit2Proxy, 2);
        await SendValueAsync(X402Permit2Addresses.Permit2, 2);
        try
        {
            TransactionReceipt? transfer = null;
            var rpc = FrontRunAtNonceRead(async () =>
            {
                var payer = _chain.Node.CreateWeb3(new Account(X402DevChainFixture.PayerKey, ChainId));
                await SendValueAsync(X402Permit2Addresses.ExactPermit2Proxy, 1);
                await SendValueAsync(X402Permit2Addresses.Permit2, 1);
                transfer = await payer.Eth.ERC20.GetContractService(_chain.TokenAddress)
                    .TransferRequestAndWaitForReceiptAsync(X402DevChainFixture.RecipientAddress, BigInteger.Parse(X402Permit2DevChain.Amount));
            });
            var service = new X402ExactPermit2Service(FacilitatorKey, Clients(rpc));
            var payload = X402Permit2DevChain.Payload(_payer, ChainId, _chain.TokenAddress);

            var settle = await service.SettlePaymentAsync(payload, payload.Accepted);

            Assert.NotNull(transfer);
            Assert.False(settle.Success);
            Assert.Equal(X402ErrorCodes.InvalidTransactionState, settle.ErrorReason);
            Assert.NotEqual(transfer!.TransactionHash, settle.Transaction);
        }
        finally
        {
            await X402Permit2DevChain.InstallPermit2StubsAsync(_chain.Node);
        }
    }

    [Fact]
    [Trait("Rule", "V14")]
    public async Task Given_ThePermit2NonceBitIsSetButNoMatchingTransferExists_When_TheFacilitatorSettleReverts_Then_TheFailureStands()
    {
        await _chain.Node.SetCodeAsync(X402Permit2Addresses.Permit2, Permit2BitmapSetByAValueCall);
        await _chain.Node.SetCodeAsync(X402Permit2Addresses.ExactPermit2Proxy, ProxyRevertsOnceFlagged);
        await SendValueAsync(X402Permit2Addresses.ExactPermit2Proxy, 2);
        await SendValueAsync(X402Permit2Addresses.Permit2, 2);
        try
        {
            var rpc = FrontRunAtNonceRead(async () =>
            {
                await SendValueAsync(X402Permit2Addresses.ExactPermit2Proxy, 1);
                await SendValueAsync(X402Permit2Addresses.Permit2, 1);
            });
            var service = new X402ExactPermit2Service(FacilitatorKey, Clients(rpc));
            var payload = X402Permit2DevChain.Payload(_payer, ChainId, _chain.TokenAddress);

            var settle = await service.SettlePaymentAsync(payload, payload.Accepted);

            Assert.False(settle.Success);
            Assert.Equal(X402ErrorCodes.InvalidTransactionState, settle.ErrorReason);
            Assert.False(string.IsNullOrEmpty(settle.Transaction));
        }
        finally
        {
            await X402Permit2DevChain.InstallPermit2StubsAsync(_chain.Node);
        }
    }

    private async Task SendValueAsync(string to, int wei)
    {
        var payer = _chain.Node.CreateWeb3(new Account(X402DevChainFixture.PayerKey, ChainId));
        await payer.Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(new TransactionInput
        {
            From = X402DevChainFixture.PayerAddress,
            To = to,
            Value = new Nethereum.Hex.HexTypes.HexBigInteger(wei),
            Gas = new Nethereum.Hex.HexTypes.HexBigInteger(100_000)
        });
    }

    private InterceptingRpcClient FrontRunAtNonceRead(Func<Task> frontRun)
    {
        var fired = 0;
        return new InterceptingRpcClient(_chain.Client, async method =>
        {
            if (method == "eth_getTransactionCount" && Interlocked.Increment(ref fired) == 1)
                await frontRun();
        });
    }

    private async Task<TransactionReceipt> ThirdPartySettleAsync(PaymentPayload payload)
    {
        var exact = (ExactSchemePayload)payload.Payload;
        var authorization = exact.Authorization;
        var signature = EthECDSASignatureFactory.ExtractECDSASignature(exact.Signature);
        var function = new TransferWithAuthorization1Function
        {
            AuthorisationFrom = authorization.From,
            AuthorisationTo = authorization.To,
            Value = BigInteger.Parse(authorization.Value),
            ValidAfter = BigInteger.Parse(authorization.ValidAfter),
            ValidBefore = BigInteger.Parse(authorization.ValidBefore),
            AuthorisationNonce = authorization.Nonce.HexToByteArray(),
            V = signature.V[0],
            R = signature.R.PadTo32Bytes(),
            S = signature.S.PadTo32Bytes()
        };
        var payer = _chain.Node.CreateWeb3(new Account(X402DevChainFixture.PayerKey, ChainId));
        return await payer.Eth.GetContractHandler(_chain.TokenAddress).SendRequestAndWaitForReceiptAsync(function);
    }

    private Task<BigInteger> FacilitatorTransactionCountAsync() =>
        X402Permit2DevChain.TransactionCountAsync(_chain.Node, X402DevChainFixture.FacilitatorAddress);

    private static Dictionary<int, IClient> Clients(IClient client) => new() { [ChainId] = client };
}
