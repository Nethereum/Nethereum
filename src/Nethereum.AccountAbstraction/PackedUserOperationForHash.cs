using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;


namespace Nethereum.AccountAbstraction
{

    [Struct("PackedUserOperation")]
    public class PackedUserOperationForHash
    {

        [Parameter("address", "sender", 1)]
        public string Sender { get; set; }

        [Parameter("uint256", "nonce", 2)]
        public BigInteger Nonce { get; set; }

        [Parameter("bytes", "initCode", 3)]
        public byte[] InitCode { get; set; }

        [Parameter("bytes", "callData", 4)]
        public byte[] CallData { get; set; }

        [Parameter("bytes32", "accountGasLimits", 5)]
        public byte[] AccountGasLimits { get; set; }

        [Parameter("uint256", "preVerificationGas", 6)]
        public BigInteger PreVerificationGas { get; set; }

        [Parameter("bytes32", "gasFees", 7)]
        public byte[] GasFees { get; set; }

        [Parameter("bytes", "paymasterAndData", 8)]
        public byte[] PaymasterAndData { get; set; }
    }
}
