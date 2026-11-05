using Nethereum.Documentation;
using System.Numerics;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.EIP712.EIP2612;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;

namespace Nethereum.X402.Permit2;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "Eip2612PermitSigner - sign and recover an EIP-2612 permit")]
public class Eip2612PermitSigner
{
    public TypedData<Domain> GetTypedData(string tokenName, string tokenVersion, BigInteger chainId, string tokenAddress)
    {
        var typedData = EIP2612TypeFactory.GetTypedDefinition();
        typedData.Domain = new Domain
        {
            Name = tokenName,
            Version = tokenVersion,
            ChainId = chainId,
            VerifyingContract = tokenAddress
        };
        return typedData;
    }

    public string Sign(Permit permit, string tokenName, string tokenVersion, BigInteger chainId, string tokenAddress, EthECKey key) =>
        new Eip712TypedDataSigner().SignTypedDataV4(permit, GetTypedData(tokenName, tokenVersion, chainId, tokenAddress), key);

    public string RecoverSigner(Permit permit, string tokenName, string tokenVersion, BigInteger chainId, string tokenAddress, string signature) =>
        new Eip712TypedDataSigner().RecoverFromSignatureV4(permit, GetTypedData(tokenName, tokenVersion, chainId, tokenAddress), signature);
}
