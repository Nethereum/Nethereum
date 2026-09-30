using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.ABI.EIP712;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.X402.Models;

namespace Nethereum.X402.Signers;

/// <summary>
/// Signs and recovers EIP-3009 authorizations for both the TransferWithAuthorization and
/// ReceiveWithAuthorization variants. The two variants differ only in the EIP-712 message type
/// (which fixes the type hash); the private-key/Web3 signing and recovery mechanics are shared by
/// the generic helpers below.
/// </summary>
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

    // ReceiveWithAuthorization methods

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

    /// <summary>
    /// Computes the EIP-712 digest (keccak of the encoded typed data) that a payer signs for a
    /// TransferWithAuthorization. This is the hash an ERC-1271 contract wallet validates.
    /// </summary>
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

    /// <summary>
    /// Computes the EIP-712 digest that a payer signs for a ReceiveWithAuthorization. This is the
    /// hash an ERC-1271 contract wallet validates.
    /// </summary>
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

    private static TransferWithAuthorisationBuilder.TransferWithAuthorization BuildTransferMessage(Authorization authorization) =>
        new()
        {
            From = authorization.From,
            To = authorization.To,
            Value = BigInteger.Parse(authorization.Value),
            ValidAfter = BigInteger.Parse(authorization.ValidAfter),
            ValidBefore = BigInteger.Parse(authorization.ValidBefore),
            Nonce = authorization.Nonce.HexToByteArray()
        };

    private static ReceiveWithAuthorisationBuilder.ReceiveWithAuthorization BuildReceiveMessage(Authorization authorization) =>
        new()
        {
            From = authorization.From,
            To = authorization.To,
            Value = BigInteger.Parse(authorization.Value),
            ValidAfter = BigInteger.Parse(authorization.ValidAfter),
            ValidBefore = BigInteger.Parse(authorization.ValidBefore),
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
        var typedDataWithMessage = new
        {
            types = typedData.Types,
            primaryType = typedData.PrimaryType,
            domain = typedData.Domain,
            message
        };
        var typedDataJson = JsonSerializer.Serialize(typedDataWithMessage);
        var signatureHex = await web3.Eth.AccountSigning.SignTypedDataV4.SendRequestAsync(signerAddress, typedDataJson);
        return EthECDSASignatureFactory.ExtractECDSASignature(signatureHex);
    }

    private string Recover<TMessage>(TMessage message, TypedData<Domain> typedData, EthECDSASignature signature)
    {
        var signatureBytes = new byte[signature.R.Length + signature.S.Length + signature.V.Length];
        signature.R.CopyTo(signatureBytes, 0);
        signature.S.CopyTo(signatureBytes, signature.R.Length);
        signature.V.CopyTo(signatureBytes, signature.R.Length + signature.S.Length);
        var signatureHex = signatureBytes.ToHex(true);

        return _typedDataSigner.RecoverFromSignatureV4(message, typedData, signatureHex);
    }
}
