using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.DevChain;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.Signer;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Signers;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Blockchain;

[Collection("X402 DevChain E2E")]
public class X402CancelAuthorizationDevChainTests : IClassFixture<X402DevChainFixture>
{
    private readonly X402DevChainFixture _chain;
    private readonly X402TransferWithAuthorisation3009Service _service;
    private readonly TransferWithAuthorisationSigner _signer = new();

    public X402CancelAuthorizationDevChainTests(X402DevChainFixture chain)
    {
        _chain = chain;
        _service = new X402TransferWithAuthorisation3009Service(
            X402DevChainFixture.FacilitatorKey,
            new Dictionary<int, IClient> { [X402DevChainFixture.ChainId] = chain.Client });
    }

    private static string Network => Caip2.FormatEip155(X402DevChainFixture.ChainId);

    private async Task<string> SignCancel(byte[] nonce, string privateKey) =>
        EthECDSASignature.CreateStringSignature(await _signer.SignCancelWithPrivateKeyAsync(
            X402DevChainFixture.PayerAddress, nonce, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion,
            X402DevChainFixture.ChainId, _chain.TokenAddress, privateKey));

    private async Task<(PaymentPayload Payload, PaymentRequirements Requirements, byte[] Nonce)> UnsettledAuthorization()
    {
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);
        var nonce = ((ExactSchemePayload)payload.Payload).Authorization.Nonce.HexToByteArray();
        return (payload, requirements, nonce);
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "Cancel an unsettled EIP-3009 authorization", Order = 14)]
    public async Task Given_ASignedUnsettledAuthorization_When_TheAuthorizerSignsACancellation_Then_ItIsCancelledAndCannotBeSettled()
    {
        var (payload, requirements, nonce) = await UnsettledAuthorization();
        var cancelSignature = await SignCancel(nonce, X402DevChainFixture.PayerKey);

        var cancel = await _service.CancelAuthorizationAsync(
            X402DevChainFixture.PayerAddress, nonce, cancelSignature, Network, _chain.TokenAddress);

        Assert.True(cancel.Success, cancel.ErrorReason);
        Assert.StartsWith("0x", cancel.Transaction);
        var token = new Nethereum.Contracts.EIP3009.EIP3009.Eip3009Service(_chain.Node.CreateWeb3(), _chain.TokenAddress);
        Assert.True(await token.AuthorizationStateQueryAsync(X402DevChainFixture.PayerAddress, nonce));
        var verify = await _service.VerifyPaymentAsync(payload, requirements);
        Assert.False(verify.IsValid);
        var settle = await _service.SettlePaymentAsync(payload, requirements);
        Assert.False(settle.Success);
    }

    [Fact]
    public async Task Given_ACancellationSignedByAnotherKey_When_Cancelling_Then_ItFails()
    {
        var (_, _, nonce) = await UnsettledAuthorization();
        var cancelSignature = await SignCancel(nonce, X402DevChainFixture.FacilitatorKey);

        var cancel = await _service.CancelAuthorizationAsync(
            X402DevChainFixture.PayerAddress, nonce, cancelSignature, Network, _chain.TokenAddress);

        Assert.False(cancel.Success);
        Assert.Equal(X402ErrorCodes.InvalidTransactionState, cancel.ErrorReason);
        var token = new Nethereum.Contracts.EIP3009.EIP3009.Eip3009Service(_chain.Node.CreateWeb3(), _chain.TokenAddress);
        Assert.False(await token.AuthorizationStateQueryAsync(X402DevChainFixture.PayerAddress, nonce));
    }

    [Fact]
    public async Task Given_TheZeroSignatureOverload_When_Cancelling_Then_ItFails()
    {
        var (_, _, nonce) = await UnsettledAuthorization();

#pragma warning disable CS0618
        var cancel = await _service.CancelAuthorizationAsync(
            X402DevChainFixture.PayerAddress, nonce, Network, _chain.TokenAddress);
#pragma warning restore CS0618

        Assert.False(cancel.Success);
        var token = new Nethereum.Contracts.EIP3009.EIP3009.Eip3009Service(_chain.Node.CreateWeb3(), _chain.TokenAddress);
        Assert.False(await token.AuthorizationStateQueryAsync(X402DevChainFixture.PayerAddress, nonce));
    }
}
