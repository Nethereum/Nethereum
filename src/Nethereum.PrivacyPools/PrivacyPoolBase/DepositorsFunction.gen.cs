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
    public partial class DepositorsFunction : DepositorsFunctionBase { }

    [Function("depositors", "address")]
    public class DepositorsFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "_label", 1)]
        public virtual BigInteger Label { get; set; }
    }
}
