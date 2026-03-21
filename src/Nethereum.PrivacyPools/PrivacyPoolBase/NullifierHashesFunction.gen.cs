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
    public partial class NullifierHashesFunction : NullifierHashesFunctionBase { }

    [Function("nullifierHashes", "bool")]
    public class NullifierHashesFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "_nullifierHash", 1)]
        public virtual BigInteger NullifierHash { get; set; }
    }
}
