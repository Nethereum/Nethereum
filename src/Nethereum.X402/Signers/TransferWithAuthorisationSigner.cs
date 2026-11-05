using System;
using Nethereum.Documentation;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.ABI.EIP712;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.X402.Models;

namespace Nethereum.X402.Signers;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "TransferWithAuthorisationSigner - sign, recover and digest EIP-3009 authorizations")]
public class TransferWithAuthorisationSigner
{
    private readonly Eip712TypedDataSigner _typedDataSigner;

    public TransferWithAuthorisationSigner()
    {
        _typedDataSigner = new Eip712TypedDataSigner();
    }

    public Task<EthECDSASignature> SignWithPrivateKeyAsync(
        Authorization authorization,
        string tokenName,
        string tokenVersion,
        BigInteger chainId,
        string verifyingContract,
        string privateKey)
    {
        var typedData = new TransferWithAuthorisationBuilder()
            .GetTypedDataForAuthorization(tokenName, tokenVersion, chainId, verifyingContract);
        return Task.FromResult(SignWithPrivateKey(BuildTransferMessage(authorization), typedData, privateKey));
    }

    public Task<EthECDSASignature> SignWithWeb3Async(
        Authorization authorization,
        string tokenName,
        string tokenVersion,
        BigInteger chainId,
        string verifyingContract,
        IWeb3 web3,
        string signerAddress)
    {
        var typedData = new TransferWithAuthorisationBuilder()
            .GetTypedDataForAuthorization(tokenName, tokenVersion, chainId, verifyingContract);
        return SignWithWeb3(BuildTransferMessage(authorization), typedData, web3, signerAddress);
    }

    public string RecoverAddress(
        Authorization authorization,
        string tokenName,
        string tokenVersion,
        BigInteger chainId,
        string verifyingContract,
        EthECDSASignature signature)
    {
        var typedData = new TransferWithAuthorisationBuilder()
            .GetTypedDataForAuthorization(tokenName, tokenVersion, chainId, verifyingContract);
        return Recover(BuildTransferMessage(authorization), typedData, signature);
    }


    public Task<EthECDSASignature> SignReceiveWithPrivateKeyAsync(
        Authorization authorization,
        string tokenName,
        string tokenVersion,
        BigInteger chainId,
        string verifyingContract,
        string privateKey)
    {
        var typedData = new ReceiveWithAuthorisationBuilder()
            .GetTypedDataForAuthorization(tokenName, tokenVersion, chainId, verifyingContract);
        return Task.FromResult(SignWithPrivateKey(BuildReceiveMessage(authorization), typedData, privateKey));
    }

    public Task<EthECDSASignature> SignReceiveWithWeb3Async(
        Authorization authorization,
        string tokenName,
        string tokenVersion,
        BigInteger chainId,
        string verifyingContract,
        IWeb3 web3,
        string signerAddress)
    {
        var typedData = new ReceiveWithAuthorisationBuilder()
            .GetTypedDataForAuthorization(tokenName, tokenVersion, chainId, verifyingContract);
        return SignWithWeb3(BuildReceiveMessage(authorization), typedData, web3, signerAddress);
    }

    public string RecoverReceiveAddress(
        Authorization authorization,
        string tokenName,
        string tokenVersion,
        BigInteger chainId,
        string verifyingContract,
        EthECDSASignature signature)
    {
        var typedData = new ReceiveWithAuthorisationBuilder()
            .GetTypedDataForAuthorization(tokenName, tokenVersion, chainId, verifyingContract);
        return Recover(BuildReceiveMessage(authorization), typedData, signature);
    }

    public byte[] ComputeTransferDigest(
        Authorization authorization,
        string tokenName,
        string tokenVersion,
        BigInteger chainId,
        string verifyingContract)
    {
        var typedData = new TransferWithAuthorisationBuilder()
            .GetTypedDataForAuthorization(tokenName, tokenVersion, chainId, verifyingContract);
        return Sha3Keccack.Current.CalculateHash(_typedDataSigner.EncodeTypedData(BuildTransferMessage(authorization), typedData));
    }

    public byte[] ComputeReceiveDigest(
        Authorization authorization,
        string tokenName,
        string tokenVersion,
        BigInteger chainId,
        string verifyingContract)
    {
        var typedData = new ReceiveWithAuthorisationBuilder()
            .GetTypedDataForAuthorization(tokenName, tokenVersion, chainId, verifyingContract);
        return Sha3Keccack.Current.CalculateHash(_typedDataSigner.EncodeTypedData(BuildReceiveMessage(authorization), typedData));
    }

    [NethereumDocExample(DocSection.DeFi, "x402-payments", "SignCancelWithPrivateKeyAsync - the authorizer signs a cancellation")]
    public Task<EthECDSASignature> SignCancelWithPrivateKeyAsync(
        string authorizer,
        byte[] nonce,
        string tokenName,
        string tokenVersion,
        BigInteger chainId,
        string verifyingContract,
        string privateKey)
    {
        var typedData = new CancelAuthorisationBuilder()
            .GetTypedDataForCancellation(tokenName, tokenVersion, chainId, verifyingContract);
        return Task.FromResult(SignWithPrivateKey(BuildCancelMessage(authorizer, nonce), typedData, privateKey));
    }

    public Task<EthECDSASignature> SignCancelWithWeb3Async(
        string authorizer,
        byte[] nonce,
        string tokenName,
        string tokenVersion,
        BigInteger chainId,
        string verifyingContract,
        IWeb3 web3,
        string signerAddress)
    {
        var typedData = new CancelAuthorisationBuilder()
            .GetTypedDataForCancellation(tokenName, tokenVersion, chainId, verifyingContract);
        return SignWithWeb3(BuildCancelMessage(authorizer, nonce), typedData, web3, signerAddress);
    }

    private static CancelAuthorisationBuilder.CancelAuthorization BuildCancelMessage(string authorizer, byte[] nonce) =>
        new()
        {
            Authorizer = authorizer,
            Nonce = nonce
        };

    private static TransferWithAuthorisationBuilder.TransferWithAuthorization BuildTransferMessage(Authorization authorization) =>
        new()
        {
            From = authorization.From,
            To = authorization.To,
            Value = X402Number.Parse(authorization.Value),
            ValidAfter = X402Number.Parse(authorization.ValidAfter),
            ValidBefore = X402Number.Parse(authorization.ValidBefore),
            Nonce = authorization.Nonce.HexToByteArray()
        };

    private static ReceiveWithAuthorisationBuilder.ReceiveWithAuthorization BuildReceiveMessage(Authorization authorization) =>
        new()
        {
            From = authorization.From,
            To = authorization.To,
            Value = X402Number.Parse(authorization.Value),
            ValidAfter = X402Number.Parse(authorization.ValidAfter),
            ValidBefore = X402Number.Parse(authorization.ValidBefore),
            Nonce = authorization.Nonce.HexToByteArray()
        };

    private EthECDSASignature SignWithPrivateKey<TMessage>(TMessage message, TypedData<Domain> typedData, string privateKey)
    {
        var key = new EthECKey(privateKey.EnsureHexPrefix().Substring(2));
        var signatureHex = _typedDataSigner.SignTypedDataV4(message, typedData, key);
        return EthECDSASignatureFactory.ExtractECDSASignature(signatureHex);
    }

    private async Task<EthECDSASignature> SignWithWeb3<TMessage>(TMessage message, TypedData<Domain> typedData, IWeb3 web3, string signerAddress)
    {
        var accountAddress = web3.TransactionManager?.Account?.Address;
        if (!string.IsNullOrEmpty(accountAddress) && !accountAddress.IsTheSameAddress(signerAddress))
            throw new ArgumentException(
                $"The web3 account {accountAddress} is not the signer address {signerAddress}.",
                nameof(signerAddress));

        typedData.SetMessage(message);
        var signatureHex = await web3.Eth.AccountSigning.SignTypedDataV4.SendRequestAsync(typedData.ToJson());
        return EthECDSASignatureFactory.ExtractECDSASignature(signatureHex);
    }

    private string Recover<TMessage>(TMessage message, TypedData<Domain> typedData, EthECDSASignature signature)
    {
        return _typedDataSigner.RecoverFromSignatureV4(message, typedData, signature.CreateStringSignature());
    }
}
