using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.PrivacyPools.PrivacyPoolBase
{
    public partial class RagequitProof : RagequitProofBase { }

    public class RagequitProofBase 
    {
        [Parameter("uint256[2]", "pA", 1)]
        public virtual List<BigInteger> PA { get; set; }
        [Parameter("uint256[2][2]", "pB", 2)]
        public virtual List<List<BigInteger>> PB { get; set; }
        [Parameter("uint256[2]", "pC", 3)]
        public virtual List<BigInteger> PC { get; set; }
        [Parameter("uint256[4]", "pubSignals", 4)]
        public virtual List<BigInteger> PubSignals { get; set; }
    }
}
