using System.Numerics;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.Contracts;
using Nethereum.Contracts.EIP3009.EIP3009.ContractDefinition;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Facilitator;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Nethereum.X402.Signers;

namespace Nethereum.X402.IntegrationTests.Blockchain;

[Collection("X402 DevChain E2E")]
public class X402FrontRunBindingDevChainTests : IClassFixture<X402DevChainFixture>, IAsyncLifetime
{
    private const int ChainId = X402DevChainFixture.ChainId;
    private const string FacilitatorKey = X402DevChainFixture.FacilitatorKey;
    private const string OtherRecipient = "0x000000000000000000000000000000000000dEaD";

    private static readonly byte[] Permit2BitmapSetByAValueCall = string.Concat(
        "34", "15", "600c", "57", "6001", "34", "14", "6000", "55", "5b", "6000", "54", "6000", "03", "6000", "52", "6020", "6000", "f3")
        .HexToByteArray();

    private static readonly byte[] ProxyRevertsOnceFlagged = string.Concat(
        "34", "15", "600d", "57", "6001", "34", "14", "6000", "55", "00", "5b", "6000", "54", "6015", "57", "00", "5b", "6000", "6000", "fd")
        .HexToByteArray();

    private readonly X402DevChainFixture _chain;
    private readonly EthECKey _payer = new(X402DevChainFixture.PayerKey.Substring(2));

    public X402FrontRunBindingDevChainTests(X402DevChainFixture chain)
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
    [Trait("Rule", "W1")]
    public async Task Given_ABatchSettlesADifferentAuthorizationOfTheSameNonceAndSeparatelyTransfersTheAmount_When_TheFacilitatorSettleReverts_Then_NoRecoveryHappens()
    {
        await _chain.Node.SetCodeAsync(SettlementContracts.BatcherAddress, SettlementContracts.Batcher);
        await PayerWeb3().Eth.ERC20.GetContractService(_chain.TokenAddress)
            .ApproveRequestAndWaitForReceiptAsync(SettlementContracts.BatcherAddress, BigInteger.Pow(2, 100));
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);
        var nonce = ((ExactSchemePayload)payload.Payload).Authorization.Nonce;
        var otherAuthorization = await SignedForOtherRecipientAsync(requirements, nonce);
        var batch = new SettlementContracts.BatchFunction
        {
            Target = _chain.TokenAddress,
            First = otherAuthorization.GetCallData(),
            Second = new SettlementContracts.TransferFromFunction
            {
                From = X402DevChainFixture.PayerAddress,
                To = X402DevChainFixture.RecipientAddress,
                Value = BigInteger.Parse(requirements.Amount)
            }.GetCallData()
        };
        var rpc = FrontRunAtNonceRead(async () =>
            await PayerWeb3().Eth.GetContractHandler(SettlementContracts.BatcherAddress).SendRequestAndWaitForReceiptAsync(batch));
        var service = new X402TransferWithAuthorisation3009Service(FacilitatorKey, Clients(rpc));

        var settle = await service.SettlePaymentAsync(payload, requirements);

        Assert.False(settle.Success);
        Assert.Equal(X402ErrorCodes.UnexpectedSettleError, settle.ErrorReason);
    }

    [Fact]
    [Trait("Rule", "W1")]
    public async Task Given_APlainTransferInTheSameBlockRangeAsTwoPermit2Claims_When_BothFacilitatorSettlesRevert_Then_NeitherClaimIsRecovered()
    {
        await _chain.Node.SetCodeAsync(X402Permit2Addresses.Permit2, Permit2BitmapSetByAValueCall);
        await _chain.Node.SetCodeAsync(X402Permit2Addresses.ExactPermit2Proxy, ProxyRevertsOnceFlagged);
        await SendValueAsync(X402Permit2Addresses.ExactPermit2Proxy, 2);
        await SendValueAsync(X402Permit2Addresses.Permit2, 2);
        try
        {
            var estimates = 0;
            var nonceReads = 0;
            var rpc = new InterceptingRpcClient(_chain.Client, async method =>
            {
                if (method == "eth_estimateGas")
                    Interlocked.Increment(ref estimates);

                if (method == "eth_getTransactionCount" && Interlocked.Increment(ref nonceReads) == 1)
                {
                    for (var i = 0; i < 100 && Volatile.Read(ref estimates) < 2; i++)
                        await Task.Delay(50);

                    await SendValueAsync(X402Permit2Addresses.ExactPermit2Proxy, 1);
                    await SendValueAsync(X402Permit2Addresses.Permit2, 1);
                    await PayerWeb3().Eth.ERC20.GetContractService(_chain.TokenAddress)
                        .TransferRequestAndWaitForReceiptAsync(X402DevChainFixture.RecipientAddress, BigInteger.Parse(X402Permit2DevChain.Amount));
                }
            });
            var service = new X402ExactPermit2Service(FacilitatorKey, Clients(rpc));
            var first = X402Permit2DevChain.Payload(_payer, ChainId, _chain.TokenAddress);
            var second = X402Permit2DevChain.Payload(_payer, ChainId, _chain.TokenAddress);

            var settles = await Task.WhenAll(
                service.SettlePaymentAsync(first, first.Accepted),
                service.SettlePaymentAsync(second, second.Accepted));

            Assert.Equal(0, settles.Count(settle => settle.Success));
        }
        finally
        {
            await X402Permit2DevChain.InstallPermit2StubsAsync(_chain.Node);
        }
    }

    [Fact]
    [Trait("Rule", "W1")]
    public async Task Given_AThirdPartyCallsTheProxyWithThisPermit2Authorization_When_TheFacilitatorSettleReverts_Then_ItReportsSuccessWithTheThirdPartysHash()
    {
        await ArmSettlingProxyAsync();
        try
        {
            TransactionReceipt? thirdParty = null;
            PaymentPayload payload = null!;
            var rpc = FrontRunAtNonceRead(async () => thirdParty = await ProxySettlesAsync(payload, nonceOffset: 0));
            var service = new X402ExactPermit2Service(FacilitatorKey, Clients(rpc));
            payload = X402Permit2DevChain.Payload(_payer, ChainId, _chain.TokenAddress);

            var settle = await service.SettlePaymentAsync(payload, payload.Accepted);

            Assert.True(settle.Success, settle.ErrorReason);
            Assert.NotNull(thirdParty);
            Assert.Equal(thirdParty!.TransactionHash, settle.Transaction);
        }
        finally
        {
            await DisarmAsync();
        }
    }

    [Fact]
    [Trait("Rule", "W1")]
    public async Task Given_AThirdPartyCallsTheProxyWithAnotherPermit2NonceButTheSameTransfer_When_TheFacilitatorSettleReverts_Then_NoRecoveryHappens()
    {
        await ArmSettlingProxyAsync();
        try
        {
            PaymentPayload payload = null!;
            var rpc = FrontRunAtNonceRead(async () => await ProxySettlesAsync(payload, nonceOffset: 1));
            var service = new X402ExactPermit2Service(FacilitatorKey, Clients(rpc));
            payload = X402Permit2DevChain.Payload(_payer, ChainId, _chain.TokenAddress);

            var settle = await service.SettlePaymentAsync(payload, payload.Accepted);

            Assert.False(settle.Success);
            Assert.Equal(X402ErrorCodes.InvalidTransactionState, settle.ErrorReason);
        }
        finally
        {
            await DisarmAsync();
        }
    }

    [Fact]
    [Trait("Rule", "W6")]
    public async Task Given_AThirdPartySettledTheEip3009PaymentAndConfirmationsAreRequired_When_TheHeadIsTooShortWithinTheBound_Then_ItIsSettlementPendingWithThatHash()
    {
        var options = new X402FacilitatorOptions { RequiredConfirmations = 12, MaxReceiptWaitSeconds = 3 };
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        PaymentPayload payload = null!;
        TransactionReceipt? thirdParty = null;
        var rpc = FrontRunAtNonceRead(async () => thirdParty = await ThirdPartySettleAsync(payload));
        var service = new X402TransferWithAuthorisation3009Service(FacilitatorKey, Clients(rpc), options);
        payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);

        var settle = await service.SettlePaymentAsync(payload, requirements);

        Assert.False(settle.Success);
        Assert.Equal(X402ErrorCodes.SettlementPending, settle.ErrorReason);
        Assert.Equal(thirdParty!.TransactionHash, settle.Transaction);
    }

    [Fact]
    [Trait("Rule", "W6")]
    public async Task Given_AThirdPartySettledTheEip3009PaymentAndConfirmationsAreRequired_When_TheHeadReachesThemWithinTheBound_Then_ItReportsSuccessWithTheThirdPartysHash()
    {
        var options = new X402FacilitatorOptions { RequiredConfirmations = 3, MaxReceiptWaitSeconds = 20 };
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        PaymentPayload payload = null!;
        TransactionReceipt? thirdParty = null;
        Task mining = Task.CompletedTask;
        var rpc = FrontRunAtNonceRead(async () =>
        {
            thirdParty = await ThirdPartySettleAsync(payload);
            mining = Task.Run(async () =>
            {
                await Task.Delay(600);
                await SendValueAsync(X402DevChainFixture.RecipientAddress, 1);
                await SendValueAsync(X402DevChainFixture.RecipientAddress, 1);
            });
        });
        var service = new X402TransferWithAuthorisation3009Service(FacilitatorKey, Clients(rpc), options);
        payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);

        var settle = await service.SettlePaymentAsync(payload, requirements);
        await mining;

        Assert.True(settle.Success, settle.ErrorReason);
        Assert.Equal(thirdParty!.TransactionHash, settle.Transaction);
    }

    [Fact]
    [Trait("Rule", "W6")]
    public async Task Given_AThirdPartySettledThePermit2PaymentAndConfirmationsAreRequired_When_TheHeadIsTooShortWithinTheBound_Then_ItIsSettlementPendingWithThatHash()
    {
        await ArmSettlingProxyAsync();
        try
        {
            var options = new X402FacilitatorOptions { RequiredConfirmations = 12, MaxReceiptWaitSeconds = 3 };
            TransactionReceipt? thirdParty = null;
            PaymentPayload payload = null!;
            var rpc = FrontRunAtFirstEstimate(async () => thirdParty = await ProxySettlesAsync(payload, nonceOffset: 0));
            var service = new X402ExactPermit2Service(FacilitatorKey, Clients(rpc), null, options);
            payload = X402Permit2DevChain.Payload(_payer, ChainId, _chain.TokenAddress);

            var settle = await service.SettlePaymentAsync(payload, payload.Accepted);

            Assert.False(settle.Success);
            Assert.Equal(X402ErrorCodes.SettlementPending, settle.ErrorReason);
            Assert.Equal(thirdParty!.TransactionHash, settle.Transaction);
        }
        finally
        {
            await DisarmAsync();
        }
    }

    private async Task ArmSettlingProxyAsync()
    {
        await _chain.Node.SetCodeAsync(X402Permit2Addresses.Permit2, Permit2BitmapSetByAValueCall);
        await _chain.Node.SetCodeAsync(X402Permit2Addresses.ExactPermit2Proxy, SettlementContracts.SettlingProxy);
        await SendValueAsync(X402Permit2Addresses.Permit2, 2);
        await PayerWeb3().Eth.ERC20.GetContractService(_chain.TokenAddress)
            .ApproveRequestAndWaitForReceiptAsync(X402Permit2Addresses.ExactPermit2Proxy, BigInteger.Pow(2, 100));
        await SetFailNextAsync(false);
    }

    private async Task DisarmAsync()
    {
        await SetFailNextAsync(false);
        await X402Permit2DevChain.InstallPermit2StubsAsync(_chain.Node);
    }

    private Task<TransactionReceipt> SetFailNextAsync(bool value) =>
        PayerWeb3().Eth.GetContractHandler(X402Permit2Addresses.ExactPermit2Proxy)
            .SendRequestAndWaitForReceiptAsync(new SettlementContracts.SetFailNextFunction { Value = value });

    private async Task<TransactionReceipt> ProxySettlesAsync(PaymentPayload payload, int nonceOffset)
    {
        var permit2 = (Permit2SchemePayload)payload.Payload;
        var settle = permit2.Permit2Authorization.ToSettleFunction(permit2.Signature);
        settle.Permit.Nonce += nonceOffset;
        var receipt = await PayerWeb3().Eth.GetContractHandler(X402Permit2Addresses.ExactPermit2Proxy)
            .SendRequestAndWaitForReceiptAsync(settle);
        await SendValueAsync(X402Permit2Addresses.Permit2, 1);
        await SetFailNextAsync(true);
        return receipt;
    }

    private async Task<TransferWithAuthorization1Function> SignedForOtherRecipientAsync(PaymentRequirements requirements, string nonce)
    {
        var other = X402DevChainPayments.Requirements(_chain, "1", OtherRecipient);
        other.Extra = requirements.Extra;
        var authorization = new TransferWithAuthorisationBuilder().BuildFromPaymentRequirements(other, X402DevChainFixture.PayerAddress);
        authorization.Nonce = nonce;
        var signature = await new TransferWithAuthorisationSigner().SignWithPrivateKeyAsync(
            authorization, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion,
            ChainId, _chain.TokenAddress, X402DevChainFixture.PayerKey);
        return new TransferWithAuthorization1Function
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
        return await PayerWeb3().Eth.GetContractHandler(_chain.TokenAddress).SendRequestAndWaitForReceiptAsync(function);
    }

    private Nethereum.Web3.Web3 PayerWeb3() =>
        (Nethereum.Web3.Web3)_chain.Node.CreateWeb3(new Account(X402DevChainFixture.PayerKey, ChainId));

    private async Task SendValueAsync(string to, int wei)
    {
        await PayerWeb3().Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(new TransactionInput
        {
            From = X402DevChainFixture.PayerAddress,
            To = to,
            Value = new HexBigInteger(wei),
            Gas = new HexBigInteger(100_000)
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

    private InterceptingRpcClient FrontRunAtFirstEstimate(Func<Task> frontRun)
    {
        var fired = 0;
        return new InterceptingRpcClient(_chain.Client, async method =>
        {
            if (method == "eth_estimateGas" && Interlocked.Increment(ref fired) == 1)
                await frontRun();
        });
    }

    private static Dictionary<int, IClient> Clients(IClient client) => new() { [ChainId] = client };
}
