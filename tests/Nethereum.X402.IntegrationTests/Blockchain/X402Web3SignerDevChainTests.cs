using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.DevChain;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Signers;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Blockchain;

[Collection("X402 DevChain E2E")]
public class X402Web3SignerDevChainTests : IClassFixture<X402DevChainFixture>
{
    private readonly X402DevChainFixture _chain;
    private readonly TransferWithAuthorisationSigner _signer = new();

    public X402Web3SignerDevChainTests(X402DevChainFixture chain)
    {
        _chain = chain;
    }

    private static PaymentPayload PaymentOf(PaymentRequirements requirements, Authorization authorization, EthECDSASignature signature) => new()
    {
        X402Version = 2,
        Accepted = requirements,
        Payload = new ExactSchemePayload { Authorization = authorization, Signature = EthECDSASignature.CreateStringSignature(signature) }
    };

    private static string Network => Caip2.FormatEip155(X402DevChainFixture.ChainId);

    private Nethereum.Web3.IWeb3 PayerWeb3() =>
        _chain.Node.CreateWeb3(new Account(X402DevChainFixture.PayerKey, X402DevChainFixture.ChainId));

    private X402TransferWithAuthorisation3009Service TransferService() => new(
        X402DevChainFixture.FacilitatorKey,
        new Dictionary<int, IClient> { [X402DevChainFixture.ChainId] = _chain.Client });

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "Sign an EIP-3009 authorization with a Web3 account", Order = 16)]
    public async Task SignWithWeb3Async_ReturnsTheSignatureOfThePrivateKeyOverloadAndTheFacilitatorAcceptsIt()
    {
        var requirements = X402DevChainPayments.Requirements(_chain, "10000", X402DevChainFixture.RecipientAddress);
        var builder = new TransferWithAuthorisationBuilder();
        var signer = new TransferWithAuthorisationSigner();
        var usdcAddress = _chain.TokenAddress;
        var chainId = X402DevChainFixture.ChainId;
        var signerAddress = X402DevChainFixture.PayerAddress;
        var web3 = PayerWeb3();

        var authorization = builder.BuildFromPaymentRequirements(requirements, signerAddress);

        var signature = await signer.SignWithWeb3Async(
            authorization, "USDC", "2", chainId, usdcAddress, web3, signerAddress);

        var expected = await signer.SignWithPrivateKeyAsync(
            authorization, "USDC", "2", chainId, usdcAddress, X402DevChainFixture.PayerKey);
        Assert.Equal(EthECDSASignature.CreateStringSignature(expected), EthECDSASignature.CreateStringSignature(signature));
        Assert.Equal(signerAddress, signer.RecoverAddress(authorization, "USDC", "2", chainId, usdcAddress, signature), ignoreCase: true);
        var payment = PaymentOf(requirements, authorization, signature);
        var verification = await TransferService().VerifyPaymentAsync(payment, requirements);
        Assert.True(verification.IsValid, verification.InvalidReason);
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "Sign an EIP-3009 authorization with a Web3 built from a private-key account", Order = 81)]
    public async Task SignWithWeb3Async_WithAWeb3BuiltFromAPrivateKeyAccount_ReturnsTheSignatureOfThePrivateKeyOverload()
    {
        var requirements = X402DevChainPayments.Requirements(_chain, "10000", X402DevChainFixture.RecipientAddress);
        var signer = new TransferWithAuthorisationSigner();
        var usdcAddress = _chain.TokenAddress;
        var chainId = X402DevChainFixture.ChainId;
        var payerPrivateKey = X402DevChainFixture.PayerKey;
        var signerAddress = X402DevChainFixture.PayerAddress;
        var authorization = new TransferWithAuthorisationBuilder().BuildFromPaymentRequirements(requirements, signerAddress);

        var web3 = new Nethereum.Web3.Web3(new Account(payerPrivateKey, chainId));

        var signature = await signer.SignWithWeb3Async(
            authorization, "USDC", "2", chainId, usdcAddress, web3, signerAddress);

        var expected = await signer.SignWithPrivateKeyAsync(
            authorization, "USDC", "2", chainId, usdcAddress, payerPrivateKey);
        Assert.Equal(EthECDSASignature.CreateStringSignature(expected), EthECDSASignature.CreateStringSignature(signature));
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "Sign a ReceiveWithAuthorization with a Web3 account", Order = 17)]
    public async Task SignReceiveWithWeb3Async_ReturnsTheSignatureOfThePrivateKeyOverloadAndTheReceiveServiceAcceptsIt()
    {
        var requirements = X402DevChainPayments.Requirements(_chain, "10000", X402DevChainFixture.FacilitatorAddress);
        var builder = new ReceiveWithAuthorisationBuilder();
        var signer = new TransferWithAuthorisationSigner();
        var usdcAddress = _chain.TokenAddress;
        var chainId = X402DevChainFixture.ChainId;
        var signerAddress = X402DevChainFixture.PayerAddress;
        var web3 = PayerWeb3();
        var receiveService = new X402ReceiveWithAuthorisation3009Service(
            X402DevChainFixture.FacilitatorKey,
            new Dictionary<int, IClient> { [X402DevChainFixture.ChainId] = _chain.Client });

        var authorization = builder.BuildFromPaymentRequirements(requirements, signerAddress);

        var signature = await signer.SignReceiveWithWeb3Async(
            authorization, "USDC", "2", chainId, usdcAddress, web3, signerAddress);

        var expected = await signer.SignReceiveWithPrivateKeyAsync(
            authorization, "USDC", "2", chainId, usdcAddress, X402DevChainFixture.PayerKey);
        Assert.Equal(EthECDSASignature.CreateStringSignature(expected), EthECDSASignature.CreateStringSignature(signature));
        Assert.Equal(signerAddress, signer.RecoverReceiveAddress(authorization, "USDC", "2", chainId, usdcAddress, signature), ignoreCase: true);
        var payment = PaymentOf(requirements, authorization, signature);
        var verification = await receiveService.VerifyPaymentAsync(payment, requirements);
        Assert.True(verification.IsValid, verification.InvalidReason);
    }

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "Sign a cancellation with a Web3 account", Order = 18)]
    public async Task SignCancelWithWeb3Async_ProducesASignatureThatCancelsAnUnsettledAuthorization()
    {
        var service = TransferService();
        var requirements = X402DevChainPayments.Requirements(_chain, "10000", X402DevChainFixture.RecipientAddress);
        var payment = await X402DevChainPayments.SignedTransfer(_chain, requirements);
        var nonce = ((ExactSchemePayload)payment.Payload).Authorization.Nonce.HexToByteArray();
        var signer = new TransferWithAuthorisationSigner();
        var usdcAddress = _chain.TokenAddress;
        var chainId = X402DevChainFixture.ChainId;
        var payerAddress = X402DevChainFixture.PayerAddress;
        var web3 = PayerWeb3();

        var cancelSignature = EthECDSASignature.CreateStringSignature(
            await signer.SignCancelWithWeb3Async(
                payerAddress, nonce, "USDC", "2", chainId, usdcAddress, web3, payerAddress));

        var expected = EthECDSASignature.CreateStringSignature(
            await signer.SignCancelWithPrivateKeyAsync(
                payerAddress, nonce, "USDC", "2", chainId, usdcAddress, X402DevChainFixture.PayerKey));
        Assert.Equal(expected, cancelSignature);
        var cancel = await service.CancelAuthorizationAsync(payerAddress, nonce, cancelSignature, Network, usdcAddress);
        Assert.True(cancel.Success, cancel.ErrorReason);
        var verification = await service.VerifyPaymentAsync(payment, requirements);
        Assert.False(verification.IsValid);
    }

    [Fact]
    public async Task Given_AWeb3WhoseAccountIsNotTheSignerAddress_When_SigningWithWeb3_Then_ItThrowsBeforeSigning()
    {
        var requirements = X402DevChainPayments.Requirements(_chain, "10000", X402DevChainFixture.RecipientAddress);
        var authorization = new TransferWithAuthorisationBuilder().BuildFromPaymentRequirements(requirements, X402DevChainFixture.PayerAddress);
        var web3 = PayerWeb3();

        var transfer = await Assert.ThrowsAsync<ArgumentException>(() => _signer.SignWithWeb3Async(
            authorization, "USDC", "2", X402DevChainFixture.ChainId, _chain.TokenAddress, web3, X402DevChainFixture.RecipientAddress));
        await Assert.ThrowsAsync<ArgumentException>(() => _signer.SignReceiveWithWeb3Async(
            authorization, "USDC", "2", X402DevChainFixture.ChainId, _chain.TokenAddress, web3, X402DevChainFixture.RecipientAddress));
        await Assert.ThrowsAsync<ArgumentException>(() => _signer.SignCancelWithWeb3Async(
            X402DevChainFixture.PayerAddress, new byte[32], "USDC", "2", X402DevChainFixture.ChainId, _chain.TokenAddress,
            web3, X402DevChainFixture.RecipientAddress));
        Assert.Equal("signerAddress", transfer.ParamName);
    }
}
