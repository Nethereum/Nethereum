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
    public partial class RootsFunction : RootsFunctionBase { }

    [Function("roots", "uint256")]
    public class RootsFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "_index", 1)]
        public virtual BigInteger Index { get; set; }
    }
}
