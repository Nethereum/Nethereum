using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.AccountAbstraction.EntryPoint.ContractDefinition
{
    public partial class MemoryUserOp : MemoryUserOpBase { }

    public class MemoryUserOpBase 
    {
        [Parameter("address", "sender", 1)]
        public virtual string Sender { get; set; }
        [Parameter("uint256", "nonce", 2)]
        public virtual BigInteger Nonce { get; set; }
        [Parameter("uint256", "verificationGasLimit", 3)]
        public virtual BigInteger VerificationGasLimit { get; set; }
        [Parameter("uint256", "callGasLimit", 4)]
        public virtual BigInteger CallGasLimit { get; set; }
        [Parameter("uint256", "paymasterVerificationGasLimit", 5)]
        public virtual BigInteger PaymasterVerificationGasLimit { get; set; }
        [Parameter("uint256", "paymasterPostOpGasLimit", 6)]
        public virtual BigInteger PaymasterPostOpGasLimit { get; set; }
        [Parameter("uint256", "preVerificationGas", 7)]
        public virtual BigInteger PreVerificationGas { get; set; }
        [Parameter("address", "paymaster", 8)]
        public virtual string Paymaster { get; set; }
        [Parameter("uint256", "maxFeePerGas", 9)]
        public virtual BigInteger MaxFeePerGas { get; set; }
        [Parameter("uint256", "maxPriorityFeePerGas", 10)]
        public virtual BigInteger MaxPriorityFeePerGas { get; set; }
    }
}
