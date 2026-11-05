using System.Numerics;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.X402.Signers;

public class CancelAuthorisationBuilder : Eip3009AuthorisationBuilderBase
{
    public TypedData<Domain> GetTypedDataForCancellation(
        string tokenName,
        string tokenVersion,
        BigInteger chainId,
        string verifyingContract)
        => BuildTypedData(
            typeof(CancelAuthorization),
            nameof(CancelAuthorization),
            tokenName,
            tokenVersion,
            chainId,
            verifyingContract);

    [Struct("CancelAuthorization")]
    public class CancelAuthorization
    {
        [Parameter("address", "authorizer", 1)]
        public string Authorizer { get; set; } = null!;

        [Parameter("bytes32", "nonce", 2)]
        public byte[] Nonce { get; set; } = null!;
    }
}
