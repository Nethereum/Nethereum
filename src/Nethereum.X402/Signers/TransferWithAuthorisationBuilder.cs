using System.Numerics;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.X402.Signers;

public class TransferWithAuthorisationBuilder : Eip3009AuthorisationBuilderBase
{
    public TypedData<Domain> GetTypedDataForAuthorization(
        string tokenName,
        string tokenVersion,
        BigInteger chainId,
        string verifyingContract)
        => BuildTypedData(
            typeof(TransferWithAuthorization),
            nameof(TransferWithAuthorization),
            tokenName,
            tokenVersion,
            chainId,
            verifyingContract);

    [Struct("TransferWithAuthorization")]
    public class TransferWithAuthorization
    {
        [Parameter("address", "from", 1)]
        public string From { get; set; } = null!;

        [Parameter("address", "to", 2)]
        public string To { get; set; } = null!;

        [Parameter("uint256", "value", 3)]
        public BigInteger Value { get; set; }

        [Parameter("uint256", "validAfter", 4)]
        public BigInteger ValidAfter { get; set; }

        [Parameter("uint256", "validBefore", 5)]
        public BigInteger ValidBefore { get; set; }

        [Parameter("bytes32", "nonce", 6)]
        public byte[] Nonce { get; set; } = null!;
    }
}
