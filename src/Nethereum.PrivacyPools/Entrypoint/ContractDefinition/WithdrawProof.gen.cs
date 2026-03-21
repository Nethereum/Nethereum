using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.PrivacyPools.Entrypoint.ContractDefinition
{
    public partial class WithdrawProof : WithdrawProofBase { }

    public class WithdrawProofBase 
    {
        [Parameter("uint256[2]", "pA", 1)]
        public virtual List<BigInteger> PA { get; set; }
        [Parameter("uint256[2][2]", "pB", 2)]
        public virtual List<List<BigInteger>> PB { get; set; }
        [Parameter("uint256[2]", "pC", 3)]
        public virtual List<BigInteger> PC { get; set; }
        [Parameter("uint256[8]", "pubSignals", 4)]
        public virtual List<BigInteger> PubSignals { get; set; }
    }
}
