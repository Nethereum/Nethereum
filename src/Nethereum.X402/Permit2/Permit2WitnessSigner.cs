using System.Numerics;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using Nethereum.Util;

namespace Nethereum.X402.Permit2;

public class Permit2WitnessSigner
{
    public TypedData<DomainWithNameChainIdAndVerifyingContract> GetTypedData(BigInteger chainId, string permit2Address)
    {
        return new TypedData<DomainWithNameChainIdAndVerifyingContract>
        {
            Domain = new DomainWithNameChainIdAndVerifyingContract
            {
                Name = "Permit2",
                ChainId = chainId,
                VerifyingContract = permit2Address
            },
            Types = MemberDescriptionFactory.GetTypesMemberDescription(
                typeof(DomainWithNameChainIdAndVerifyingContract),
                typeof(PermitWitnessTransferFrom),
                typeof(TokenPermissions),
                typeof(Witness)),
            PrimaryType = nameof(PermitWitnessTransferFrom)
        };
    }

    public string Sign(PermitWitnessTransferFrom message, BigInteger chainId, string permit2Address, EthECKey key)
    {
        var typedData = GetTypedData(chainId, permit2Address);
        return new Eip712TypedDataSigner().SignTypedDataV4(message, typedData, key);
    }

    public string RecoverSigner(PermitWitnessTransferFrom message, BigInteger chainId, string permit2Address, string signature)
    {
        var typedData = GetTypedData(chainId, permit2Address);
        return new Eip712TypedDataSigner().RecoverFromSignatureV4(message, typedData, signature);
    }

    /// <summary>
    /// Computes the EIP-712 digest (keccak of the encoded typed data) a payer signs for the permit2
    /// witness message. This is the hash an ERC-1271 contract wallet validates.
    /// </summary>
    public byte[] ComputeDigest(PermitWitnessTransferFrom message, BigInteger chainId, string permit2Address)
    {
        var typedData = GetTypedData(chainId, permit2Address);
        return Sha3Keccack.Current.CalculateHash(new Eip712TypedDataSigner().EncodeTypedData(message, typedData));
    }
}
