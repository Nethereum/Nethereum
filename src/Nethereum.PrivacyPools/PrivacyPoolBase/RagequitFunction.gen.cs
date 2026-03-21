using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.PrivacyPools.PrivacyPoolBase;

namespace Nethereum.PrivacyPools.PrivacyPoolBase
{
    public partial class RagequitFunction : RagequitFunctionBase { }

    [Function("ragequit")]
    public class RagequitFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "_proof", 1)]
        public virtual RagequitProof Proof { get; set; }
    }
}
