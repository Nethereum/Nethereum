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
    public partial class DepositFunction : DepositFunctionBase { }

    [Function("deposit", "uint256")]
    public class DepositFunctionBase : FunctionMessage
    {
        [Parameter("address", "_depositor", 1)]
        public virtual string Depositor { get; set; }
        [Parameter("uint256", "_value", 2)]
        public virtual BigInteger Value { get; set; }
        [Parameter("uint256", "_precommitmentHash", 3)]
        public virtual BigInteger PrecommitmentHash { get; set; }
    }
}
