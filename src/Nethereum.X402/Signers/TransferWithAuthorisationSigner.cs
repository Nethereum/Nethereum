using System;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using Nethereum.Web3;
using Nethereum.X402.Models;

namespace Nethereum.X402.Signers;

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
        var builder = new TransferWithAuthorisationBuilder();
        var typedData = builder.GetTypedDataForAuthorization(
            tokenName,
            tokenVersion,
            chainId,
            verifyingContract
        );

        var message = new TransferWithAuthorisationBuilder.TransferWithAuthorization
        {
            From = authorization.From,
            To = authorization.To,
            Value = BigInteger.Parse(authorization.Value),
            ValidAfter = BigInteger.Parse(authorization.ValidAfter),
            ValidBefore = BigInteger.Parse(authorization.ValidBefore),
            Nonce = authorization.Nonce.HexToByteArray()
        };

        var key = new EthECKey(privateKey.EnsureHexPrefix().Substring(2));
        var signatureHex = _typedDataSigner.SignTypedDataV4(message, typedData, key);
        var signature = EthECDSASignatureFactory.ExtractECDSASignature(signatureHex);

        return Task.FromResult(signature);
    }

    public async Task<EthECDSASignature> SignWithWeb3Async(
        Authorization authorization,
        string tokenName,
        string tokenVersion,
        BigInteger chainId,
        string verifyingContract,
        IWeb3 web3,
        string signerAddress)
    {
        var builder = new TransferWithAuthorisationBuilder();
        var typedData = builder.GetTypedDataForAuthorization(
            tokenName,
            tokenVersion,
            chainId,
            verifyingContract
        );

        var message = new TransferWithAuthorisationBuilder.TransferWithAuthorization
        {
            From = authorization.From,
            To = authorization.To,
            Value = BigInteger.Parse(authorization.Value),
            ValidAfter = BigInteger.Parse(authorization.ValidAfter),
            ValidBefore = BigInteger.Parse(authorization.ValidBefore),
            Nonce = authorization.Nonce.HexToByteArray()
        };

        var typedDataWithMessage = new
        {
            types = typedData.Types,
            primaryType = typedData.PrimaryType,
            domain = typedData.Domain,
            message = message
        };
        var typedDataJson = JsonSerializer.Serialize(typedDataWithMessage);
        var signatureHex = await web3.Eth.AccountSigning.SignTypedDataV4.SendRequestAsync(
            signerAddress,
            typedDataJson
        );
        var signature = EthECDSASignatureFactory.ExtractECDSASignature(signatureHex);

        return signature;
    }

    public string RecoverAddress(
        Authorization authorization,
        string tokenName,
        string tokenVersion,
        BigInteger chainId,
        string verifyingContract,
        EthECDSASignature signature)
    {
        var builder = new TransferWithAuthorisationBuilder();
        var typedData = builder.GetTypedDataForAuthorization(
            tokenName,
            tokenVersion,
            chainId,
            verifyingContract
        );

        var message = new TransferWithAuthorisationBuilder.TransferWithAuthorization
        {
            From = authorization.From,
            To = authorization.To,
            Value = BigInteger.Parse(authorization.Value),
            ValidAfter = BigInteger.Parse(authorization.ValidAfter),
            ValidBefore = BigInteger.Parse(authorization.ValidBefore),
            Nonce = authorization.Nonce.HexToByteArray()
        };

        var signatureBytes = new byte[signature.R.Length + signature.S.Length + signature.V.Length];
        signature.R.CopyTo(signatureBytes, 0);
        signature.S.CopyTo(signatureBytes, signature.R.Length);
        signature.V.CopyTo(signatureBytes, signature.R.Length + signature.S.Length);
        var signatureHex = signatureBytes.ToHex(true);

        var recoveredAddress = _typedDataSigner.RecoverFromSignatureV4(message, typedData, signatureHex);
        return recoveredAddress;
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
        var builder = new ReceiveWithAuthorisationBuilder();
        var typedData = builder.GetTypedDataForAuthorization(
            tokenName,
            tokenVersion,
            chainId,
            verifyingContract
        );

        var message = new ReceiveWithAuthorisationBuilder.ReceiveWithAuthorization
        {
            From = authorization.From,
            To = authorization.To,
            Value = BigInteger.Parse(authorization.Value),
            ValidAfter = BigInteger.Parse(authorization.ValidAfter),
            ValidBefore = BigInteger.Parse(authorization.ValidBefore),
            Nonce = authorization.Nonce.HexToByteArray()
        };

        var key = new EthECKey(privateKey.EnsureHexPrefix().Substring(2));
        var signatureHex = _typedDataSigner.SignTypedDataV4(message, typedData, key);
        var signature = EthECDSASignatureFactory.ExtractECDSASignature(signatureHex);

        return Task.FromResult(signature);
    }

    public async Task<EthECDSASignature> SignReceiveWithWeb3Async(
        Authorization authorization,
        string tokenName,
        string tokenVersion,
        BigInteger chainId,
        string verifyingContract,
        IWeb3 web3,
        string signerAddress)
    {
        var builder = new ReceiveWithAuthorisationBuilder();
        var typedData = builder.GetTypedDataForAuthorization(
            tokenName,
            tokenVersion,
            chainId,
            verifyingContract
        );

        var message = new ReceiveWithAuthorisationBuilder.ReceiveWithAuthorization
        {
            From = authorization.From,
            To = authorization.To,
            Value = BigInteger.Parse(authorization.Value),
            ValidAfter = BigInteger.Parse(authorization.ValidAfter),
            ValidBefore = BigInteger.Parse(authorization.ValidBefore),
            Nonce = authorization.Nonce.HexToByteArray()
        };

        var typedDataWithMessage = new
        {
            types = typedData.Types,
            primaryType = typedData.PrimaryType,
            domain = typedData.Domain,
            message = message
        };
        var typedDataJson = JsonSerializer.Serialize(typedDataWithMessage);
        var signatureHex = await web3.Eth.AccountSigning.SignTypedDataV4.SendRequestAsync(
            signerAddress,
            typedDataJson
        );
        var signature = EthECDSASignatureFactory.ExtractECDSASignature(signatureHex);

        return signature;
    }

    public string RecoverReceiveAddress(
        Authorization authorization,
        string tokenName,
        string tokenVersion,
        BigInteger chainId,
        string verifyingContract,
        EthECDSASignature signature)
    {
        var builder = new ReceiveWithAuthorisationBuilder();
        var typedData = builder.GetTypedDataForAuthorization(
            tokenName,
            tokenVersion,
            chainId,
            verifyingContract
        );

        var message = new ReceiveWithAuthorisationBuilder.ReceiveWithAuthorization
        {
            From = authorization.From,
            To = authorization.To,
            Value = BigInteger.Parse(authorization.Value),
            ValidAfter = BigInteger.Parse(authorization.ValidAfter),
            ValidBefore = BigInteger.Parse(authorization.ValidBefore),
            Nonce = authorization.Nonce.HexToByteArray()
        };

        var signatureBytes = new byte[signature.R.Length + signature.S.Length + signature.V.Length];
        signature.R.CopyTo(signatureBytes, 0);
        signature.S.CopyTo(signatureBytes, signature.R.Length);
        signature.V.CopyTo(signatureBytes, signature.R.Length + signature.S.Length);
        var signatureHex = signatureBytes.ToHex(true);

        var recoveredAddress = _typedDataSigner.RecoverFromSignatureV4(message, typedData, signatureHex);
        return recoveredAddress;
    }
}
