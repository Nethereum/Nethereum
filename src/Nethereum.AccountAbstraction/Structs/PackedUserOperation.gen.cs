using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.AccountAbstraction.Structs
{
    public partial class PackedUserOperation : PackedUserOperationBase { }

    public class PackedUserOperationBase 
    {
        [Parameter("address", "sender", 1)]
        public virtual string Sender { get; set; }
        [Parameter("uint256", "nonce", 2)]
        public virtual BigInteger Nonce { get; set; }
        [Parameter("bytes", "initCode", 3)]
        public virtual byte[] InitCode { get; set; }
        [Parameter("bytes", "callData", 4)]
        public virtual byte[] CallData { get; set; }
        [Parameter("bytes32", "accountGasLimits", 5)]
        public virtual byte[] AccountGasLimits { get; set; }
        [Parameter("uint256", "preVerificationGas", 6)]
        public virtual BigInteger PreVerificationGas { get; set; }
        [Parameter("bytes32", "gasFees", 7)]
        public virtual byte[] GasFees { get; set; }
        [Parameter("bytes", "paymasterAndData", 8)]
        public virtual byte[] PaymasterAndData { get; set; }
        [Parameter("bytes", "signature", 9)]
        public virtual byte[] Signature { get; set; }
    }
}